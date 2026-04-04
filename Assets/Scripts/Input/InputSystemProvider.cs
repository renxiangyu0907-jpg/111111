// ============================================================================
// InputSystemProvider.cs — 基于 New Input System Generated Class 的输入实现
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  数据流：                                                                │
// │                                                                          │
// │  Input System Runtime                                                    │
// │    │  (硬件事件 → InputAction callback)                                  │
// │    ▼                                                                     │
// │  IGameplayActions 回调方法                                               │
// │    │  OnMove / OnJump / OnAttack / ...                                   │
// │    │  在回调中：                                                          │
// │    │    · 更新持续量（_rawMoveVector）                                    │
// │    │    · 置位帧标记（_jumpPressedFlag = true）                           │
// │    │    · 触发事件（OnJumpPressed?.Invoke()）                             │
// │    ▼                                                                     │
// │  MonoBehaviour.Update() — LateUpdate 之前                                │
// │    │  (什么都不做 — 标记在回调中已经置位)                                  │
// │    ▼                                                                     │
// │  状态机 LogicUpdate 读取                                                 │
// │    │  provider.JumpPressed  → 读到 true（本帧刚按下）                    │
// │    │  provider.MoveVector   → 读到当前摇杆值                             │
// │    ▼                                                                     │
// │  MonoBehaviour.LateUpdate()                                              │
// │    │  清零所有帧标记（_jumpPressedFlag = false ...）                      │
// │    │  这样下一帧如果没有新回调，标记就是 false                             │
// │    ▼                                                                     │
// │  下一帧...                                                               │
// │                                                                          │
// │  InputLocked = true 时：                                                 │
// │    · 所有属性返回零值 / false                                             │
// │    · 回调中不触发外部事件、不置位帧标记                                    │
// │    · 但 Input System 回调仍在运行（保持内部状态同步）                      │
// └──────────────────────────────────────────────────────────────────────────┘

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostVeil.Input
{
    /// <summary>
    /// IInputProvider 的正式实现 —— 对接 New Input System 的 Generated C# Class。
    /// 
    /// 挂载方式：挂到场景中一个持久 GameObject 上（如 "InputManager"），
    ///           或由 PlayerController 在 Awake 中 AddComponent。
    /// 
    /// 职责边界：
    ///   ✔ 捕获原始输入并暴露为属性 + 事件
    ///   ✔ 帧标记的自动置位与清零
    ///   ✔ InputLocked 全局锁
    ///   ✘ 不做跳跃缓冲 / 土狼时间 / 连击判定 — 那些是 Controller 的事
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // 确保在 PlayerController 之前初始化
    public class InputSystemProvider : MonoBehaviour, IInputProvider,
                                       GhostVeilInputActions.IGameplayActions
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 下方向阈值 ===")]
        [Tooltip("垂直轴低于此值视为'按住下'（用于下+跳穿透单向平台）")]
        [SerializeField, Range(-1f, 0f)]
        private float _downThreshold = -0.5f;

        // ══════════════════════════════════════════════
        //  内部状态
        // ══════════════════════════════════════════════

        // ── Generated Class 实例 ────────────────────
        private GhostVeilInputActions _actions;

        // ── 持续量（每帧持续有效，由 performed/canceled 更新） ──
        private Vector2 _rawMoveVector;
        private bool _jumpHeldRaw;
        private bool _attackHeldRaw;

        // ── 帧标记（仅触发帧为 true，LateUpdate 清零） ──
        //    用 "Flag" 后缀的私有字段存储原始标记，
        //    公开属性在 InputLocked 时返回 false。
        private bool _jumpPressedFlag;
        private bool _jumpReleasedFlag;
        private bool _attackPressedFlag;
        private bool _dashPressedFlag;
        private bool _interactPressedFlag;

        // ── 输入锁 ─────────────────────────────────
        private bool _inputLocked;

        // ══════════════════════════════════════════════
        //  IInputProvider — 轴 / 向量
        // ══════════════════════════════════════════════

        public float HorizontalInput => _inputLocked ? 0f : _rawMoveVector.x;
        public float VerticalInput   => _inputLocked ? 0f : _rawMoveVector.y;
        public Vector2 MoveVector    => _inputLocked ? Vector2.zero : _rawMoveVector;

        // ══════════════════════════════════════════════
        //  IInputProvider — 帧标记
        // ══════════════════════════════════════════════

        public bool JumpPressed     => !_inputLocked && _jumpPressedFlag;
        public bool JumpReleased    => !_inputLocked && _jumpReleasedFlag;
        public bool JumpHeld        => !_inputLocked && _jumpHeldRaw;
        public bool AttackPressed   => !_inputLocked && _attackPressedFlag;
        public bool AttackHeld      => !_inputLocked && _attackHeldRaw;
        public bool DashPressed     => !_inputLocked && _dashPressedFlag;
        public bool InteractPressed => !_inputLocked && _interactPressedFlag;
        public bool DownHeld        => !_inputLocked && (_rawMoveVector.y < _downThreshold);

        // ══════════════════════════════════════════════
        //  IInputProvider — 事件
        // ══════════════════════════════════════════════

        public event Action OnJumpPressed;
        public event Action OnJumpReleased;
        public event Action OnAttackPressed;
        public event Action OnDashPressed;
        public event Action OnInteractPressed;
        public event Action OnPausePressed;

        // ══════════════════════════════════════════════
        //  IInputProvider — 输入锁
        // ══════════════════════════════════════════════

        public bool InputLocked
        {
            get => _inputLocked;
            set
            {
                _inputLocked = value;

                // 锁定瞬间立即清空所有残留标记，
                // 防止锁定前一帧的 Pressed 在下一帧被状态机读到。
                if (value)
                {
                    ClearAllFlags();
                }
            }
        }

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            // 创建 Generated Class 实例并注册回调
            _actions = new GhostVeilInputActions();
            _actions.Gameplay.SetCallbacks(this);
        }

        private void OnEnable()
        {
            _actions?.Gameplay.Enable();
        }

        private void OnDisable()
        {
            _actions?.Gameplay.Disable();
        }

        private void OnDestroy()
        {
            // 释放 InputActionAsset，防止泄漏
            _actions?.Dispose();
            _actions = null;
        }

        /// <summary>
        /// LateUpdate 在所有 Update / 状态机 LogicUpdate 之后执行。
        /// 此时状态机已经读取过本帧标记，可以安全清零。
        /// 
        /// 为什么不用 Update？
        ///   Input System 的回调可能在 Update 之前或之间触发，
        ///   如果在 Update 里清零，可能会在状态机读取之前就把标记清掉。
        ///   LateUpdate 保证了：
        ///     回调置位 → Update(状态机读取) → LateUpdate(清零)
        /// </summary>
        private void LateUpdate()
        {
            ClearAllFlags();
        }

        // ══════════════════════════════════════════════
        //  IGameplayActions 回调实现
        // ══════════════════════════════════════════════
        //
        //  New Input System 回调的三个阶段：
        //    · started   — 按键刚开始（手指接触，尚未达到阈值）
        //    · performed — 按键确认触发（达到阈值 / 满足 Interaction 条件）
        //    · canceled  — 按键释放
        //
        //  对于 Button 类 Action：
        //    performed = 按下瞬间
        //    canceled  = 松开瞬间
        //
        //  对于 Value<Vector2> 类 Action（Move）：
        //    performed = 值发生变化且不为零
        //    canceled  = 值回到零

        // ── Move ────────────────────────────────────
        public void OnMove(InputAction.CallbackContext context)
        {
            // 无论是否锁定都更新内部原始值，
            // 这样解锁后可以立即拿到当前真实输入，
            // 而不是残留锁定前的旧值。
            _rawMoveVector = context.ReadValue<Vector2>();
        }

        // ── Jump ────────────────────────────────────
        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _jumpHeldRaw = true;

                // 锁定时不置位帧标记、不触发事件
                if (_inputLocked) return;

                _jumpPressedFlag = true;
                OnJumpPressed?.Invoke();
            }
            else if (context.canceled)
            {
                _jumpHeldRaw = false;

                if (_inputLocked) return;

                _jumpReleasedFlag = true;
                OnJumpReleased?.Invoke();
            }
        }

        // ── Attack ──────────────────────────────────
        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _attackHeldRaw = true;

                if (_inputLocked) return;

                _attackPressedFlag = true;
                OnAttackPressed?.Invoke();
            }
            else if (context.canceled)
            {
                _attackHeldRaw = false;
            }
        }

        // ── Dash ────────────────────────────────────
        public void OnDash(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            if (_inputLocked) return;

            _dashPressedFlag = true;
            OnDashPressed?.Invoke();
        }

        // ── Interact ────────────────────────────────
        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            if (_inputLocked) return;

            _interactPressedFlag = true;
            OnInteractPressed?.Invoke();
        }

        // ── Pause ───────────────────────────────────
        public void OnPause(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            // Pause 是特殊的：即使 InputLocked 也触发。
            // 理由：玩家在过场动画中也应该能按 Pause 打开菜单。
            // 如果你的设计不需要此行为，加上 _inputLocked 检查即可。
            OnPausePressed?.Invoke();
        }

        // ══════════════════════════════════════════════
        //  内部辅助
        // ══════════════════════════════════════════════

        /// <summary>清除所有帧脉冲标记（不清除持续量和 held 状态）</summary>
        private void ClearAllFlags()
        {
            _jumpPressedFlag    = false;
            _jumpReleasedFlag   = false;
            _attackPressedFlag  = false;
            _dashPressedFlag    = false;
            _interactPressedFlag = false;
        }
    }
}
