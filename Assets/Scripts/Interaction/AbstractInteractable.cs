// ============================================================================
// AbstractInteractable.cs — 可交互对象抽象基类
// ============================================================================
using UnityEngine;
using GhostVeil.Data;
using GhostVeil.Core.Event;

namespace GhostVeil.Interaction
{
    /// <summary>
    /// 场景可交互对象的 MonoBehaviour 抽象基类。
    /// 提供 IInteractable 的默认骨架实现 + 通用提示事件发布。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]  // Trigger 检测范围
    public abstract class AbstractInteractable : MonoBehaviour, IInteractable
    {
        // ── Inspector 配置 ────────────────────────────
        [Header("=== Interaction Settings ===")]
        [SerializeField] protected InteractionType interactionType = InteractionType.Examine;
        [SerializeField] protected string promptText = "Interact";
        [SerializeField] protected bool startsEnabled = true;

        // ── 运行时 ────────────────────────────────────
        private bool _canInteract;

        // ── IInteractable 实现 ────────────────────────
        public InteractionType Type => interactionType;

        public virtual bool CanInteract
        {
            get => _canInteract;
            protected set => _canInteract = value;
        }

        public virtual string PromptText => promptText;

        // ── Unity 生命周期 ────────────────────────────
        protected virtual void Awake()
        {
            _canInteract = startsEnabled;
        }

        // ── 聚焦（提示显隐） ─────────────────────────
        public virtual void OnFocused(bool isFocused)
        {
            GameEvent.Publish(new InteractionPromptEvent
            {
                Interactable = gameObject,
                Type = interactionType,
                Show = isFocused && _canInteract
            });
        }

        // ── 交互入口（子类实现具体逻辑） ────────────────
        public void Interact(GameObject instigator)
        {
            if (!CanInteract) return;
            OnInteract(instigator);
        }

        /// <summary>子类实现具体交互逻辑</summary>
        protected abstract void OnInteract(GameObject instigator);

        // ── 便捷方法 ──────────────────────────────────
        /// <summary>锁定交互（谜题完成后 / 道具已拾取）</summary>
        public void Lock()
        {
            CanInteract = false;
            OnFocused(false);
        }

        /// <summary>解锁交互</summary>
        public void Unlock()
        {
            CanInteract = true;
        }
    }
}
