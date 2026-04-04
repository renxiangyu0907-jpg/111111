// ============================================================================
// PickupItem.cs — 可拾取道具
// ============================================================================
using UnityEngine;
using GhostVeil.Core.Event;

namespace GhostVeil.Interaction.Samples
{
    /// <summary>
    /// 可拾取道具 —— 按 E 后道具消失，触发拾取事件。
    /// 后续可对接背包系统。
    /// </summary>
    public class PickupItem : AbstractInteractable
    {
        [Header("=== 道具信息 ===")]
        [Tooltip("道具 ID（用于背包系统）")]
        [SerializeField] private string itemID = "unknown_item";

        [Tooltip("道具名称（用于 UI 显示）")]
        [SerializeField] private string itemName = "未知道具";

        [Tooltip("拾取后播放的提示文本")]
        [SerializeField] private string pickupMessage = "获得了 {0}";

        protected override void Awake()
        {
            base.Awake();
            interactionType = Data.InteractionType.PickUp;
            if (string.IsNullOrEmpty(promptText) || promptText == "Interact")
                promptText = "拾取";
        }

        protected override void OnInteract(GameObject instigator)
        {
            string msg = string.Format(pickupMessage, itemName);
            Debug.Log($"[拾取] {msg} (ID: {itemID})");

            // 发布拾取事件（后续背包系统监听）
            GameEvent.Publish(new ItemPickedUpEvent
            {
                ItemID = itemID,
                ItemName = itemName,
                PickedUpBy = instigator
            });

            // 拾取后销毁物体
            Lock();
            Destroy(gameObject, 0.1f);
        }
    }

    // ── 拾取事件结构体 ──────────────────────────────
    public struct ItemPickedUpEvent
    {
        public string ItemID;
        public string ItemName;
        public GameObject PickedUpBy;
    }
}
