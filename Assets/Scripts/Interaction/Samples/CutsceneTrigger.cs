// ============================================================================
// CutsceneTrigger.cs — 过场触发器（配合 AbstractInteractable 或 Trigger 使用）
// ============================================================================
//
//  用法 A：作为可交互物体 —— 按 E 触发过场
//    1. 给场景物体添加 BoxCollider2D
//    2. 添加 CutsceneTrigger 脚本
//    3. 在 Inspector 中拖入 CutsceneData
//
//  用法 B：作为自动触发区域 —— 玩家进入即触发
//    1. 给空物体添加 BoxCollider2D (isTrigger = true)
//    2. 添加 CutsceneTrigger 脚本
//    3. 勾选 autoTriggerOnEnter
//    4. 在 Inspector 中拖入 CutsceneData
//

using UnityEngine;
using GhostVeil.Data.ScriptableObjects;
using GhostVeil.Narrative.Cutscene;

namespace GhostVeil.Interaction.Samples
{
    /// <summary>
    /// 通用过场触发器 —— 支持交互触发和自动区域触发两种模式。
    /// </summary>
    public class CutsceneTrigger : AbstractInteractable
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 过场数据 ===")]
        [Tooltip("要播放的过场数据资产")]
        [SerializeField] private CutsceneData cutsceneData;

        [Header("=== 触发模式 ===")]
        [Tooltip("玩家进入触发区域时自动触发（无需按 E）")]
        [SerializeField] private bool autoTriggerOnEnter = false;

        [Tooltip("触发后是否锁定（一次性过场）")]
        [SerializeField] private bool oneTimeTrigger = true;

        // ══════════════════════════════════════════════
        //  运行时
        // ══════════════════════════════════════════════

        private CutsceneDirector _director;
        private bool _hasTriggered;

        protected override void Awake()
        {
            base.Awake();
            interactionType = Data.InteractionType.Mechanism;
            if (string.IsNullOrEmpty(promptText) || promptText == "Interact")
                promptText = autoTriggerOnEnter ? "" : "触发";
        }

        private void Start()
        {
            _director = FindObjectOfType<CutsceneDirector>();
        }

        // ── 交互触发（按 E） ────────────────────────
        protected override void OnInteract(GameObject instigator)
        {
            TriggerCutscene();
        }

        // ── 自动触发（进入区域） ────────────────────
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!autoTriggerOnEnter) return;
            if (_hasTriggered && oneTimeTrigger) return;

            // 检查是否为玩家
            if (other.CompareTag("Player") ||
                other.GetComponent<GhostVeil.Character.Player.PlayerController>() != null)
            {
                TriggerCutscene();
            }
        }

        // ── 触发过场 ────────────────────────────────
        private void TriggerCutscene()
        {
            if (_hasTriggered && oneTimeTrigger) return;
            if (_director == null)
            {
                _director = FindObjectOfType<CutsceneDirector>();
                if (_director == null)
                {
                    Debug.LogError("[CutsceneTrigger] 找不到 CutsceneDirector！");
                    return;
                }
            }

            if (cutsceneData == null)
            {
                Debug.LogWarning("[CutsceneTrigger] CutsceneData 未赋值！");
                return;
            }

            if (_director.IsPlaying) return;

            _hasTriggered = true;
            _director.PlayCutscene(cutsceneData, OnCutsceneFinished);

            Debug.Log($"[CutsceneTrigger] 触发过场：{cutsceneData.cutsceneID}");
        }

        private void OnCutsceneFinished()
        {
            if (oneTimeTrigger)
                Lock();
            else
                _hasTriggered = false; // 可重复触发
        }
    }
}
