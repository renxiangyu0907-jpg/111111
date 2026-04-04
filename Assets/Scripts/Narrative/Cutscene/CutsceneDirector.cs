// ============================================================================
// CutsceneDirector.cs — 过场演出控制器（协程驱动，串联镜头 + 动画 + 对话）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  职责：                                                                  │
// │                                                                          │
// │  1. 解析 CutsceneData 中的步骤序列                                      │
// │  2. 按顺序执行每个 CutsceneStep：                                       │
// │     · Dialogue → 借用 DialogueRunner 逐字打印                          │
// │     · CameraMove → 调用 CameraController.FocusOn()                     │
// │     · PlayAnimation → 查找对象播放 Spine 动画                           │
// │     · Wait → 简单等待                                                   │
// │     · Fade → 屏幕淡入/淡出                                             │
// │     · CameraShake → 调用 CameraController.RequestShake()               │
// │     · TriggerEvent → 发布全局事件                                       │
// │     · Parallel → 并行执行子步骤                                         │
// │  3. 通过 NarrativeController 锁定输入                                   │
// │  4. 过场结束后自动释放控制权                                            │
// │                                                                          │
// │  挂载方式：                                                               │
// │    · 挂在场景中的 NarrativeSystem 空物体上                              │
// │    · 或由 InteractionSystemBootstrap 自动创建                           │
// └──────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GhostVeil.Data;
using GhostVeil.Data.ScriptableObjects;
using GhostVeil.Core.Event;
using GhostVeil.Narrative.Controller;
using GhostVeil.Narrative.Dialogue;
using GhostVeil.Camera;
using GhostVeil.Animation.Spine;

namespace GhostVeil.Narrative.Cutscene
{
    public class CutsceneDirector : MonoBehaviour, ICutsceneDirector
    {
        // ══════════════════════════════════════════════
        //  Inspector 引用
        // ══════════════════════════════════════════════

        [Header("=== 引用（留空自动查找） ===")]
        [SerializeField] private NarrativeController narrativeController;
        [SerializeField] private Component cameraControllerRef; // 用 Component 避免循环依赖

        // ══════════════════════════════════════════════
        //  ICutsceneDirector 实现
        // ══════════════════════════════════════════════

        public bool IsPlaying { get; private set; }

        public event Action<string> OnCutsceneCompleted;

        // ══════════════════════════════════════════════
        //  屏幕渐变事件（供 UI 订阅）
        // ══════════════════════════════════════════════

        /// <summary>淡入/淡出更新（alpha 0~1, color）</summary>
        public event Action<float /*alpha*/, Color /*color*/> OnFadeUpdate;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private CutsceneData _currentData;
        private string _currentCutsceneID;
        private Action _onCompleted;
        private Coroutine _cutsceneCoroutine;
        private bool _paused;
        private bool _skipRequested;

        // ── 组件缓存 ────────────────────────────────
        private CameraController _camera;
        private DialogueRunner _dialogueRunner;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            if (narrativeController == null)
                narrativeController = FindObjectOfType<NarrativeController>();
            if (cameraControllerRef != null)
                _camera = cameraControllerRef as CameraController;
            if (_camera == null)
                _camera = FindObjectOfType<CameraController>();

            _dialogueRunner = FindObjectOfType<DialogueRunner>();
        }

        // ══════════════════════════════════════════════
        //  公共方法
        // ══════════════════════════════════════════════

        /// <summary>
        /// 使用 CutsceneData 资产启动过场。
        /// </summary>
        public void PlayCutscene(CutsceneData data, Action onCompleted = null)
        {
            if (data == null || data.steps == null || data.steps.Length == 0)
            {
                Debug.LogWarning("[CutsceneDirector] CutsceneData 为空或无步骤。");
                onCompleted?.Invoke();
                return;
            }

            if (IsPlaying)
            {
                Debug.LogWarning("[CutsceneDirector] 过场已在播放中，忽略新请求。");
                return;
            }

            _currentData = data;
            _currentCutsceneID = data.cutsceneID;
            _onCompleted = onCompleted;
            _paused = false;
            _skipRequested = false;
            IsPlaying = true;

            // 请求控制权
            narrativeController?.RequestAuthority(data.authorityLevel, data.cutsceneID);

            // 启动过场协程
            _cutsceneCoroutine = StartCoroutine(RunCutsceneCoroutine(data));
        }

        /// <summary>ICutsceneDirector 接口 —— 通过 ID 启动过场</summary>
        public void Play(string cutsceneID, Action onCompleted = null)
        {
            var data = Resources.Load<CutsceneData>($"Cutscenes/{cutsceneID}");
            if (data != null)
                PlayCutscene(data, onCompleted);
            else
                Debug.LogError($"[CutsceneDirector] 找不到过场资源：Resources/Cutscenes/{cutsceneID}");
        }

        public void Pause()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
        }

        public void Skip()
        {
            if (!IsPlaying) return;
            if (_currentData != null && !_currentData.allowSkip) return;

            _skipRequested = true;

            // 如果有正在运行的对话，也中断它
            if (_dialogueRunner != null && _dialogueRunner.IsRunning)
                _dialogueRunner.Abort();
        }

        // ══════════════════════════════════════════════
        //  核心过场协程
        // ══════════════════════════════════════════════

        private IEnumerator RunCutsceneCoroutine(CutsceneData data)
        {
            Debug.Log($"[CutsceneDirector] 过场开始：{data.cutsceneID} ({data.title})");

            foreach (var step in data.steps)
            {
                if (_skipRequested) break;

                // 暂停支持
                while (_paused)
                    yield return null;

                yield return ExecuteStep(step);
            }

            EndCutscene();
        }

        /// <summary>
        /// 执行单个过场步骤。
        /// </summary>
        private IEnumerator ExecuteStep(CutsceneStep step)
        {
            switch (step.stepType)
            {
                case CutsceneStepType.Dialogue:
                    yield return ExecuteDialogueStep(step);
                    break;

                case CutsceneStepType.CameraMove:
                    yield return ExecuteCameraMoveStep(step);
                    break;

                case CutsceneStepType.PlayAnimation:
                    yield return ExecuteAnimationStep(step);
                    break;

                case CutsceneStepType.Wait:
                    yield return new WaitForSeconds(step.waitDuration);
                    break;

                case CutsceneStepType.Fade:
                    yield return ExecuteFadeStep(step);
                    break;

                case CutsceneStepType.CameraShake:
                    yield return ExecuteShakeStep(step);
                    break;

                case CutsceneStepType.TriggerEvent:
                    ExecuteEventStep(step);
                    break;

                case CutsceneStepType.Parallel:
                    yield return ExecuteParallelSteps(step);
                    break;

                default:
                    Debug.LogWarning($"[CutsceneDirector] 未知步骤类型：{step.stepType}");
                    break;
            }
        }

        // ══════════════════════════════════════════════
        //  步骤执行器
        // ══════════════════════════════════════════════

        /// <summary>
        /// 对话步骤 —— 使用 DialogueRunner 逐字打印。
        /// </summary>
        private IEnumerator ExecuteDialogueStep(CutsceneStep step)
        {
            // 构建临时 DialogueLine 并直接通过事件驱动 UI
            if (_dialogueRunner != null)
            {
                // 创建临时 DialogueData
                var tempData = ScriptableObject.CreateInstance<DialogueData>();
                tempData.dialogueID = $"{_currentCutsceneID}_line";
                // 过场对话不需要 DialogueRunner 再锁一次输入（已经被 CutsceneDirector 锁了）
                tempData.authorityLevel = NarrativeAuthorityLevel.None;
                tempData.lines = new DialogueLine[]
                {
                    new DialogueLine
                    {
                        speakerName = step.speakerName,
                        content = step.content,
                        portraitKey = step.portraitKey,
                        typeSpeed = step.typeSpeed,
                        autoAdvance = step.autoAdvance,
                        autoAdvanceDelay = step.autoAdvanceDelay
                    }
                };

                _dialogueRunner.StartDialogue(tempData);

                // 等待对话完成
                while (_dialogueRunner.IsRunning && !_skipRequested)
                    yield return null;

                DestroyImmediate(tempData);
            }
            else
            {
                // 降级：直接发布事件
                GameEvent.Publish(new DialogueLineEvent
                {
                    SpeakerName = step.speakerName,
                    Content = step.content,
                    PortraitKey = step.portraitKey
                });
                yield return new WaitForSeconds(2f);
            }
        }

        /// <summary>
        /// 镜头移动步骤 —— 平滑推向目标位置/对象。
        /// </summary>
        private IEnumerator ExecuteCameraMoveStep(CutsceneStep step)
        {
            if (_camera == null)
            {
                Debug.LogWarning("[CutsceneDirector] 未找到 CameraController，跳过镜头步骤。");
                yield break;
            }

            Vector3 targetPos;

            // 优先使用 Tag 查找目标
            if (!string.IsNullOrEmpty(step.cameraTargetTag))
            {
                var target = GameObject.FindGameObjectWithTag(step.cameraTargetTag);
                if (target != null)
                    targetPos = target.transform.position;
                else
                {
                    Debug.LogWarning($"[CutsceneDirector] 找不到 Tag \"{step.cameraTargetTag}\" 的对象");
                    yield break;
                }
            }
            else
            {
                targetPos = step.cameraTargetPosition;
            }

            _camera.FocusOn(targetPos, step.cameraTransitionTime);

            // 等待镜头移动完成
            yield return new WaitForSeconds(step.cameraTransitionTime);
        }

        /// <summary>
        /// 动画步骤 —— 在指定对象上播放 Spine 动画。
        /// </summary>
        private IEnumerator ExecuteAnimationStep(CutsceneStep step)
        {
            if (string.IsNullOrEmpty(step.animationTargetTag))
            {
                Debug.LogWarning("[CutsceneDirector] 动画步骤缺少目标 Tag。");
                yield break;
            }

            var target = GameObject.FindGameObjectWithTag(step.animationTargetTag);
            if (target == null)
            {
                Debug.LogWarning($"[CutsceneDirector] 找不到 Tag \"{step.animationTargetTag}\" 的对象");
                yield break;
            }

            var animator = target.GetComponent<SpineAnimator>();
            if (animator == null)
                animator = target.GetComponentInChildren<SpineAnimator>();

            if (animator != null)
            {
                animator.PlayAnim(step.animationName, step.animationLoop);

                if (step.waitForAnimation && !step.animationLoop)
                {
                    // 等待动画完成
                    yield return new WaitForSeconds(0.1f); // 等一帧让动画开始
                    while (!animator.IsAnimationComplete(0) && !_skipRequested)
                        yield return null;
                }
            }
            else
            {
                Debug.LogWarning($"[CutsceneDirector] 对象 \"{target.name}\" 上没有 SpineAnimator");
            }
        }

        /// <summary>
        /// 屏幕淡入/淡出步骤。
        /// </summary>
        private IEnumerator ExecuteFadeStep(CutsceneStep step)
        {
            float elapsed = 0f;
            while (elapsed < step.fadeDuration && !_skipRequested)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / step.fadeDuration);

                // fadeIn: alpha 从 1 → 0（从黑到透明）
                // fadeOut: alpha 从 0 → 1（从透明到黑）
                float alpha = step.fadeIn ? (1f - t) : t;

                OnFadeUpdate?.Invoke(alpha, step.fadeColor);

                yield return null;
            }

            // 确保最终状态
            float finalAlpha = step.fadeIn ? 0f : 1f;
            OnFadeUpdate?.Invoke(finalAlpha, step.fadeColor);
        }

        /// <summary>
        /// 相机震动步骤。
        /// </summary>
        private IEnumerator ExecuteShakeStep(CutsceneStep step)
        {
            if (_camera != null)
            {
                _camera.RequestShake(step.shakeIntensity, step.shakeDuration);
            }
            yield return new WaitForSeconds(step.shakeDuration);
        }

        /// <summary>
        /// 事件触发步骤。
        /// </summary>
        private void ExecuteEventStep(CutsceneStep step)
        {
            if (!string.IsNullOrEmpty(step.eventID))
            {
                Debug.Log($"[CutsceneDirector] 触发事件：{step.eventID}");
                // 通过全局事件总线发布（后续可扩展为具体事件结构体）
                GameEvent.Publish(new CutsceneEventTriggered
                {
                    EventID = step.eventID,
                    CutsceneID = _currentCutsceneID
                });
            }
        }

        /// <summary>
        /// 并行步骤 —— 同时执行多个子步骤，等待全部完成。
        /// </summary>
        private IEnumerator ExecuteParallelSteps(CutsceneStep step)
        {
            if (step.parallelSteps == null || step.parallelSteps.Length == 0)
                yield break;

            var coroutines = new List<Coroutine>();
            foreach (var subStep in step.parallelSteps)
            {
                coroutines.Add(StartCoroutine(ExecuteStep(subStep)));
            }

            // 等待所有并行步骤完成
            foreach (var co in coroutines)
            {
                yield return co;
            }
        }

        // ══════════════════════════════════════════════
        //  过场结束
        // ══════════════════════════════════════════════

        private void EndCutscene()
        {
            IsPlaying = false;
            _skipRequested = false;

            // 恢复相机
            _camera?.ResetToDefault();

            // 清除淡入/淡出
            OnFadeUpdate?.Invoke(0f, Color.black);

            // 释放控制权
            if (_currentData != null)
                narrativeController?.ReleaseAuthority(_currentData.cutsceneID);

            // 触发完成事件
            string id = _currentCutsceneID;
            _onCompleted?.Invoke();
            OnCutsceneCompleted?.Invoke(id);

            Debug.Log($"[CutsceneDirector] 过场结束：{id}");

            _currentData = null;
            _currentCutsceneID = null;
            _onCompleted = null;
        }
    }

    // ══════════════════════════════════════════════════
    //  过场事件结构体（可移到 GameEvents.cs）
    // ══════════════════════════════════════════════════

    /// <summary>过场中触发的自定义事件</summary>
    public struct CutsceneEventTriggered
    {
        public string EventID;
        public string CutsceneID;
    }
}
