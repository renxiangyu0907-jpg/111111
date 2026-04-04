// ============================================================================
// ExaminableObject.cs — 可调查物品（病历本、病床、墙上的画等）
// ============================================================================
//
//  用法：
//    1. 在场景中创建一个 Sprite（如病床、病历本）
//    2. 添加 BoxCollider2D（确保 isTrigger = false，大小包裹住 Sprite）
//    3. 添加 ExaminableObject 脚本
//    4. 在 Inspector 中填写 promptText 和 examineText
//    5. 设置该物体的 Layer 为 Interactable（或确保 InteractionDetector 的 Layer 匹配）
//
using UnityEngine;
using GhostVeil.Core.Event;

namespace GhostVeil.Interaction.Samples
{
    /// <summary>
    /// 可调查物品 —— 最简单的可交互对象。
    /// 按 E 后在 Console 输出调查文本（后续对接 UI 对话框）。
    /// </summary>
    public class ExaminableObject : AbstractInteractable
    {
        [Header("=== 调查内容 ===")]
        [Tooltip("调查时显示的文本内容")]
        [SerializeField, TextArea(3, 10)]
        private string examineText = "这里什么都没有...";

        [Tooltip("调查后是否锁定（只能调查一次）")]
        [SerializeField] private bool lockAfterExamine = false;

        // ── 交互逻辑 ────────────────────────────────
        protected override void OnInteract(GameObject instigator)
        {
            Debug.Log($"[调查] {gameObject.name}: {examineText}");

            // 发布对话行事件（UI 层监听用于显示文本框）
            GameEvent.Publish(new DialogueLineEvent
            {
                SpeakerName = "",
                Content = examineText,
                PortraitKey = ""
            });

            if (lockAfterExamine)
            {
                Lock();
            }
        }
    }
}
