// ============================================================================
// DialogueRunner.cs — 对话运行器（解析 DialogueData，驱动逐字打印）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  数据流：                                                                │
// │                                                                          │
// │  触发对话（NPC / 交互 / 过场）                                          │
// │    │                                                                     │
// │    ▼                                                                     │
// │  DialogueRunner.StartDialogue(dialogueData)                             │
// │    ├─ NarrativeController.RequestAuthority(Dialogue)  ← 锁定输入       │
// │    ├─ 逐行推送 DialogueLine                                              │
// │    │    ├─ OnLinePresented 事件 → DialogueBoxUI 逐字打印               │
// │    │    └─ 等待玩家按 E 确认（或自动翻页）                               │
// │    └─ 全部行读完                                                         │
// │         ├─ NarrativeController.ReleaseAuthority()     ← 解锁输入       │
// │         └─ OnDialogueCompleted 事件                                     │
// └──────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections;
using UnityEngine;
using GhostVeil.Data;
using GhostVeil.Data.ScriptableObjects;
using GhostVeil.Core.Event;
using GhostVeil.Narrative.Controller;

namespace GhostVeil.Narrative.Dialogue
{
    public class DialogueRunner : MonoBehaviour, IDialogueRunner
    {
        // ══════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════

        [Header("=== 默认设置 ===")]
        [Tooltip("默认逐字打印速度（字/秒）")]
        [SerializeField] private float defaultTypeSpeed = 30f;

        [Header("=== 引用（留空自动查找） ===")]
        [SerializeField] private NarrativeController narrativeController;

        // ══════════════════════════════════════════════
        //  IDialogueRunner 实现
        // ══════════════════════════════════════════════

        public bool IsRunning { get; private set; }

        public event Action OnDialogueCompleted;
        public event Action<string, string> OnLinePresented;
        public event Action<string[]> OnChoicesPresented;

        // ══════════════════════════════════════════════
        //  扩展事件（供 UI 逐字打印用）
        // ══════════════════════════════════════════════

        /// <summary>逐字打印中，每次推送当前已显示的部分文本</summary>
        public event Action<string /*speaker*/, string /*partialText*/, string /*portraitKey*/> OnTypewriterUpdate;

        /// <summary>单行打字完成</summary>
        public event Action OnLineComplete;

        /// <summary>对话框应该隐藏</summary>
        public event Action OnDialogueBoxHide;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private DialogueData _currentData;
        private int _currentLineIndex;
        private bool _waitingForAdvance;
        private bool _typewriterFinished;
        private bool _skipTypewriter;
        private Coroutine _typewriterCoroutine;
        private Coroutine _dialogueCoroutine;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            if (narrativeController == null)
                narrativeController = FindObjectOfType<NarrativeController>();
        }

        // ══════════════════════════════════════════════
        //  公共方法
        // ══════════════════════════════════════════════

        /// <summary>
        /// 用 DialogueData 资产启动对话。
        /// </summary>
        public void StartDialogue(DialogueData data)
        {
            if (data == null || data.lines == null || data.lines.Length == 0)
            {
                Debug.LogWarning("[DialogueRunner] DialogueData 为空或无对话行。");
                return;
            }

            if (IsRunning)
            {
                Debug.LogWarning("[DialogueRunner] 对话已在运行中，忽略新请求。");
                return;
            }

            _currentData = data;
            _currentLineIndex = 0;
            IsRunning = true;

            // 请求控制权
            narrativeController?.RequestAuthority(data.authorityLevel, data.dialogueID);

            // 启动对话协程
            _dialogueCoroutine = StartCoroutine(RunDialogueCoroutine());
        }

        /// <summary>IDialogueRunner 接口实现（通过字符串 ID 查找）</summary>
        public void StartDialogue(string dialogueID)
        {
            // 从 Resources 加载（简单实现，后续可改为 Addressables）
            var data = Resources.Load<DialogueData>($"Dialogues/{dialogueID}");
            if (data != null)
                StartDialogue(data);
            else
                Debug.LogError($"[DialogueRunner] 找不到对话资源：Resources/Dialogues/{dialogueID}");
        }

        /// <summary>推进到下一句（玩家按 E 时调用）</summary>
        public void Advance()
        {
            if (!IsRunning) return;

            if (!_typewriterFinished)
            {
                // 打字未完成 → 跳过打字，直接显示完整文本
                _skipTypewriter = true;
            }
            else if (_waitingForAdvance)
            {
                // 打字已完成，等待翻页 → 翻到下一句
                _waitingForAdvance = false;
            }
        }

        public void SelectChoice(int index) { /* 后续扩展分支对话 */ }

        public void Abort()
        {
            if (!IsRunning) return;

            if (_typewriterCoroutine != null)
                StopCoroutine(_typewriterCoroutine);
            if (_dialogueCoroutine != null)
                StopCoroutine(_dialogueCoroutine);

            EndDialogue();
        }

        // ══════════════════════════════════════════════
        //  对话协程
        // ══════════════════════════════════════════════

        private IEnumerator RunDialogueCoroutine()
        {
            while (_currentLineIndex < _currentData.lines.Length)
            {
                var line = _currentData.lines[_currentLineIndex];

                // ── 逐字打印本行 ────────────────────────
                _typewriterFinished = false;
                _skipTypewriter = false;
                _waitingForAdvance = false;

                float speed = line.typeSpeed > 0 ? line.typeSpeed : defaultTypeSpeed;
                _typewriterCoroutine = StartCoroutine(TypewriterCoroutine(line, speed));

                // 等待打字完成
                yield return _typewriterCoroutine;

                // 通知完整行已显示
                OnLinePresented?.Invoke(line.speakerName, line.content);
                OnLineComplete?.Invoke();

                // ── 等待翻页 ────────────────────────────
                if (line.autoAdvance)
                {
                    yield return new WaitForSeconds(line.autoAdvanceDelay);
                }
                else
                {
                    _waitingForAdvance = true;
                    while (_waitingForAdvance)
                        yield return null;
                }

                _currentLineIndex++;
            }

            // ── 对话结束 ────────────────────────────────
            EndDialogue();
        }

        private IEnumerator TypewriterCoroutine(DialogueLine line, float charsPerSecond)
        {
            string fullText = line.content;
            float interval = 1f / Mathf.Max(charsPerSecond, 1f);
            int charIndex = 0;

            while (charIndex < fullText.Length)
            {
                if (_skipTypewriter)
                {
                    // 跳过 → 直接显示完整文本
                    OnTypewriterUpdate?.Invoke(line.speakerName, fullText, line.portraitKey);
                    break;
                }

                charIndex++;
                string partial = fullText.Substring(0, charIndex);
                OnTypewriterUpdate?.Invoke(line.speakerName, partial, line.portraitKey);

                yield return new WaitForSeconds(interval);
            }

            // 确保最终显示完整文本
            OnTypewriterUpdate?.Invoke(line.speakerName, fullText, line.portraitKey);
            _typewriterFinished = true;
        }

        private void EndDialogue()
        {
            IsRunning = false;

            // 释放控制权
            if (_currentData != null)
                narrativeController?.ReleaseAuthority(_currentData.dialogueID);

            // 通知 UI 隐藏
            OnDialogueBoxHide?.Invoke();
            OnDialogueCompleted?.Invoke();

            _currentData = null;

            Debug.Log("[DialogueRunner] 对话结束。");
        }
    }
}
