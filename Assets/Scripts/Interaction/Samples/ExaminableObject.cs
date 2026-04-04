// ============================================================================
// ExaminableObject.cs — 可调查物品（病历本、病床、墙上的画等）
// ============================================================================
//
//  用法：
//    1. 在场景中创建一个 Sprite（如病床、病历本）
//    2. 添加 BoxCollider2D（确保大小包裹住 Sprite）
//    3. 添加 ExaminableObject 脚本
//    4. 在 Inspector 中填写 promptText 和 examineText
//    5. 设置该物体的 Layer 为 Interactable（或确保 InteractionDetector 的 Layer 匹配）
//
//  支持两种模式：
//    A. 简单模式 —— 直接发布 DialogueLineEvent（默认）
//    B. DialogueData 模式 —— 使用 DialogueRunner 逐字打印
//

using UnityEngine;
using GhostVeil.Core.Event;
using GhostVeil.Data.ScriptableObjects;
using GhostVeil.Narrative.Dialogue;

namespace GhostVeil.Interaction.Samples
{
    /// <summary>
    /// 可调查物品 —— 按 E 显示调查文本。
    /// 支持简单文本和 DialogueData 两种模式。
    /// </summary>
    public class ExaminableObject : AbstractInteractable
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 调查内容（简单模式） ===")]
        [Tooltip("调查时显示的文本内容")]
        [SerializeField, TextArea(3, 10)]
        private string examineText = "这里什么都没有...";

        [Header("=== 调查内容（高级模式，优先使用） ===")]
        [Tooltip("对话数据资产（可选）。如果赋值，将使用 DialogueRunner 逐字打印")]
        [SerializeField] private DialogueData examineDialogueData;

        [Header("=== 设置 ===")]
        [Tooltip("调查后是否锁定（只能调查一次）")]
        [SerializeField] private bool lockAfterExamine = false;

        // ══════════════════════════════════════════════
        //  运行时
        // ══════════════════════════════════════════════

        private DialogueRunner _runner;
        private bool _examining;

        private void Start()
        {
            _runner = FindObjectOfType<DialogueRunner>();
        }

        // ── 交互逻辑 ────────────────────────────────
        protected override void OnInteract(GameObject instigator)
        {
            // 如果正在调查中 → 推进对话
            if (_examining && _runner != null && _runner.IsRunning)
            {
                _runner.Advance();
                return;
            }

            // ── 模式 B：使用 DialogueData + DialogueRunner ──
            if (examineDialogueData != null && _runner != null)
            {
                _examining = true;
                _runner.OnDialogueCompleted += OnExamineComplete;
                _runner.StartDialogue(examineDialogueData);
                return;
            }

            // ── 模式 A：简单事件 ────────────────────────
            Debug.Log($"[调查] {gameObject.name}: {examineText}");

            GameEvent.Publish(new DialogueLineEvent
            {
                SpeakerName = "",
                Content = examineText,
                PortraitKey = ""
            });

            if (lockAfterExamine)
                Lock();
        }

        private void OnExamineComplete()
        {
            _examining = false;
            if (_runner != null)
                _runner.OnDialogueCompleted -= OnExamineComplete;

            if (lockAfterExamine)
                Lock();
        }

        private void OnDestroy()
        {
            if (_runner != null)
                _runner.OnDialogueCompleted -= OnExamineComplete;
        }
    }
}
