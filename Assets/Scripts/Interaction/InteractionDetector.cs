// ============================================================================
// InteractionDetector.cs — 玩家交互检测器
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  职责：                                                                  │
// │                                                                          │
// │  1. 在角色前方维护一个小型触发器区域，检测进入/离开的 IInteractable      │
// │  2. 自动追踪当前"最近且可交互"的目标（焦点目标）                        │
// │  3. 监听 InteractPressed 输入，调用焦点目标的 Interact()                │
// │  4. 当焦点目标变化时，通知旧目标 OnFocused(false)、新目标 OnFocused(true)│
// │     从而驱动 UI 提示的显隐                                               │
// │                                                                          │
// │  挂载方式：                                                               │
// │    · 挂在 Player 物体上（与 PlayerController 同物体）                    │
// │    · 需要一个子物体 "InteractionZone" 带 Collider2D (isTrigger=true)    │
// │    · 或者本脚本自动创建子物体和触发器                                    │
// │                                                                          │
// │  设计要点：                                                               │
// │    · 通过 IInputProvider.InteractPressed 驱动，不直接读 Input System     │
// │    · 支持多个可交互对象重叠时自动选最近的                                │
// │    · 角色翻转时触发器跟随前方（通过父子关系自动处理）                    │
// └──────────────────────────────────────────────────────────────────────────┘

using System.Collections.Generic;
using UnityEngine;
using GhostVeil.Input;
using GhostVeil.Core.Event;

namespace GhostVeil.Interaction
{
    public class InteractionDetector : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 检测设置 ===")]
        [Tooltip("检测范围的中心偏移（相对于角色）。X 正方向 = 角色前方")]
        [SerializeField] private Vector2 detectionOffset = new Vector2(0.5f, 0f);

        [Tooltip("检测范围尺寸")]
        [SerializeField] private Vector2 detectionSize = new Vector2(1.0f, 1.5f);

        [Tooltip("可交互对象所在的 Layer（如 Interactable）")]
        [SerializeField] private LayerMask interactableLayer = ~0; // 默认全部

        [Header("=== 输入 ===")]
        [Tooltip("输入源引用，留空则自动从自身或场景查找")]
        [SerializeField] private Component inputProviderRef;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private IInputProvider _input;
        private readonly List<IInteractable> _nearbyInteractables = new List<IInteractable>();
        private IInteractable _currentFocus;
        private Collider2D _detectionCollider;

        /// <summary>当前焦点目标（只读，供外部 UI 查询）</summary>
        public IInteractable CurrentFocus => _currentFocus;

        /// <summary>是否有可交互目标在范围内</summary>
        public bool HasTarget => _currentFocus != null && _currentFocus.CanInteract;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            // ── 获取输入源 ──────────────────────────────
            if (inputProviderRef != null)
                _input = inputProviderRef as IInputProvider;

            if (_input == null)
            {
                var provider = GetComponent<InputSystemProvider>();
                if (provider == null)
                    provider = FindObjectOfType<InputSystemProvider>();
                _input = provider;
            }

            if (_input == null)
                Debug.LogError("[InteractionDetector] 找不到 IInputProvider！", this);

            // ── 创建检测触发器子物体 ────────────────────
            SetupDetectionZone();
        }

        private void Update()
        {
            if (_input == null) return;

            // ── 每帧用 OverlapBox 检测（比 OnTriggerEnter 更可靠） ──
            UpdateDetection();

            // ── 选择最近的焦点目标 ──────────────────────
            UpdateFocus();

            // ── 检测交互输入 ────────────────────────────
            if (_input.InteractPressed && _currentFocus != null && _currentFocus.CanInteract)
            {
                _currentFocus.Interact(gameObject);
            }
        }

        // ══════════════════════════════════════════════
        //  检测逻辑
        // ══════════════════════════════════════════════

        /// <summary>
        /// 每帧用 Physics2D.OverlapBoxAll 扫描前方区域。
        /// 比 OnTriggerStay2D 更可控：不依赖 Rigidbody2D 的唤醒状态。
        /// </summary>
        private void UpdateDetection()
        {
            _nearbyInteractables.Clear();

            // 计算检测中心（考虑角色朝向）
            float faceSign = transform.localScale.x >= 0 ? 1f : -1f;
            Vector2 center = (Vector2)transform.position + 
                             new Vector2(detectionOffset.x * faceSign, detectionOffset.y);

            // OverlapBox 检测
            var hits = Physics2D.OverlapBoxAll(center, detectionSize, 0f, interactableLayer);

            foreach (var hit in hits)
            {
                // 跳过自身
                if (hit.gameObject == gameObject) continue;

                var interactable = hit.GetComponent<IInteractable>();
                if (interactable != null && interactable.CanInteract)
                {
                    _nearbyInteractables.Add(interactable);
                }
            }
        }

        /// <summary>
        /// 从附近的可交互对象中选择最近的作为焦点。
        /// 焦点变化时通知旧/新目标更新 UI 提示。
        /// </summary>
        private void UpdateFocus()
        {
            IInteractable nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var interactable in _nearbyInteractables)
            {
                // IInteractable 必须在 MonoBehaviour 上，安全转换获取位置
                var mb = interactable as MonoBehaviour;
                if (mb == null) continue;

                float dist = Vector2.Distance(transform.position, mb.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = interactable;
                }
            }

            // ── 焦点未变化 → 跳过 ──────────────────────
            if (nearest == _currentFocus) return;

            // ── 焦点变化 → 通知旧目标失焦、新目标聚焦 ──
            _currentFocus?.OnFocused(false);
            _currentFocus = nearest;
            _currentFocus?.OnFocused(true);
        }

        // ══════════════════════════════════════════════
        //  初始化辅助
        // ══════════════════════════════════════════════

        /// <summary>
        /// 设置检测区域（仅用于 Gizmo 可视化，实际检测用 OverlapBox）。
        /// </summary>
        private void SetupDetectionZone()
        {
            // OverlapBox 模式不需要实际的 Collider
            // 但如果需要可视化调试，可以添加一个 BoxCollider2D 作为参考
        }

        // ══════════════════════════════════════════════
        //  Debug 可视化
        // ══════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 绘制检测范围（编辑器中选中时可见）
            float faceSign = Application.isPlaying 
                ? (transform.localScale.x >= 0 ? 1f : -1f)
                : 1f;

            Vector2 center = (Vector2)transform.position + 
                             new Vector2(detectionOffset.x * faceSign, detectionOffset.y);

            Gizmos.color = HasTarget ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(center, detectionSize);

            // 标注焦点目标
            if (_currentFocus != null)
            {
                var mb = _currentFocus as MonoBehaviour;
                if (mb != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(transform.position, mb.transform.position);
                }
            }
        }
#endif
    }
}
