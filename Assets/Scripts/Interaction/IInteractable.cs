// ============================================================================
// IInteractable.cs — 可交互对象接口
// ============================================================================
using UnityEngine;
using GhostVeil.Data;

namespace GhostVeil.Interaction
{
    /// <summary>
    /// 场景中所有可交互对象的统一契约。
    /// 玩家的交互检测器只面向此接口，不依赖具体类型。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>交互类别（检查、拾取、对话、机关等）</summary>
        InteractionType Type { get; }

        /// <summary>当前是否可以被交互（可能被剧情 / 谜题锁定）</summary>
        bool CanInteract { get; }

        /// <summary>交互提示文本（"按 E 调查" / "按 E 对话"）</summary>
        string PromptText { get; }

        /// <summary>交互执行入口，由玩家交互组件调用</summary>
        /// <param name="instigator">发起交互的角色</param>
        void Interact(GameObject instigator);

        /// <summary>
        /// 当玩家进入 / 离开交互范围时调用。
        /// 用于 UI 提示的显隐控制。
        /// </summary>
        void OnFocused(bool isFocused);
    }
}
