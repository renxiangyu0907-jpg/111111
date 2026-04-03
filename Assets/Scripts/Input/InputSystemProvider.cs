// ============================================================================
// InputSystemProvider.cs — 基于 New Input System Generated Class 的输入实现
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  数据流：                                                                │
// │                                                                          │
// │  New Input System (硬件层)                                               │
// │       │                                                                  │
// │       ▼                                                                  │
// │  GhostVeilInputActions (Generated C# Wrapper)                           │
// │       │  IGameplayActions 回调                                           │
// │       ▼                                                                  │
// │  InputSystemProvider (本类)                                              │
// │       │  ① 更新轴数据 (MoveVector)                                      │
// │       │  ② 设置帧标记 (JumpPressed = true)                              │
// │       │  ③ 触发事件   (OnJumpPressed?.Invoke)                           │
// │       │  ④ LateUpdate 清零帧标记                                        │
// │       ▼                                                                  │
// │  IInputProvider 接口                                                     │
// │       │                                                                  │
// │  ┌────┴────────────┐                                                     │
// │  │ 状态机          │  ← LogicUpdate 中读 bool 标记                       │
// │  │ (PlayerController)                                                    │
// │  └────┬────────────┘                                                     │
// │       │                                                                  │
// │  ┌────┴────────────┐                                                     │
// │  │ UI / 音效       │  ← 订阅 event 回调                                  │
// │  └─────────────────┘                                                     │
// │                                                                          │
// │  帧标记生命周期：                                                         │
// │                                                                          │
// │  ── frame N ──────────────────────── frame N+1 ──────                    │
// │  Input callback → JumpPressed=true   LateUpdate → JumpPressed=false     │
// │                   Event 触发          (清零，仅存活一帧)                  │
// │                   状态机可读 true                                         │
// │                                                                          │
// │  为什么用 LateUpdate 清零？                                               │
// │    · Update 中状态机的 LogicUpdate 已经执行完毕                           │
// │    · LateUpdate 在所有 Update 之后执行                                   │
// │    · 保证同一帧内所有消费者都能读到 true                                  │
// └──────────────────────────────────────────────────────────────────────────┘

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostVeil.Input
{
    /// <summary>
    /// IInputProvider 的生产环境实现。
    /// 
    /// 核心设计：
    ///   · 实现 IGameplayActions 回调接口，由 GhostVeilInputActions 驱动。
    ///   · 帧标记（JumpPressed 等）在回调中置 true，在 LateUpdate 中清零。
    ///   · InputLocked 为 true 时，所有输出归零、事件不触发。
    ///   · 此类只做"采集 + 转发"，不包含任何游戏逻辑
    ///     （跳跃缓冲、土狼时间等由 Controller / State 处理）。
    /// </summary>
    [DefaultExecutionOrder(-100)] // 确保在所有游戏逻辑之前执行
    public class InputSystemProvider : MonoBehaviour, IInputProvider, GhostVeilInputActions.IGameplayActions
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 下方向判定 ===")]
        [Tooltip("垂直轴低于此阈值时视为"按住下"（用于下+跳穿透单向平台）")]
        [SerializeField, Range(-1f, 0f)] private float _downThreshold = -0.5f;

        // ══════════════════════════════════════════════
        //  Input Actions 实例
        // ══════════════════════════════════════════════

        private GhostVeilInputActions _inputActions;

        // ══════════════════════════════════════════════
        //  内部帧标记存储
        // ══════════════════════════════════════════════
        //
        //  为什么用独立的 backing field？
        //  · 接口属性只暴露 get，内部需要写入
        //  · InputLocked 为 true 时 get 统一返回 false，
        //    但 backing field 仍然记录真实状态（解锁后立刻恢复）

        private Vector2 _moveVector;

        private bool _jumpPressed;
        private bool _jumpReleased;
        private bool _jumpHeld;

        private bool _attackPressed;
        private bool _dashPressed;
        private bool _interactPressed;
        private bool _pausePressed; // 内部暂存，通过事件对外

        // ══════════════════════════════════════════════
        //  IInputProvider — 轴 / 向量
        // ══════════════════════════════════════════════

        public float HorizontalInput => InputLocked ? 0f : _moveVector.x;
        public float VerticalInput   => InputLocked ? 0f : _moveVector.y;
        public Vector2 MoveVector    => InputLocked ? Vector2.zero : _moveVector;

        // ══════════════════════════════════════════════
        //  IInputProvider — 帧标记
        // ══════════════════════════════════════════════

        public bool JumpPressed     => !InputLocked && _jumpPressed;
        public bool JumpReleased    => !InputLocked && _jumpReleased;
        public bool JumpHeld        => !InputLocked && _jumpHeld;
        public bool AttackPressed   => !InputLocked && _attackPressed;
        public bool DashPressed     => !InputLocked && _dashPressed;
        public bool InteractPressed => !InputLocked && _interactPressed;

        public bool DownHeld        => !InputLocked && (_moveVector.y < _downThreshold);

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
        //  IInputProvider — 输入锁定
        // ══════════════════════════════════════════════

        private bool _inputLocked;

        public bool InputLocked
        {
            get => _inputLocked;
            set
            {
                if (_inputLocked == value) return;
                _inputLocked = value;

                // 锁定时立即清除所有残留帧标记和轴数据，
                // 防止"锁定前一帧按下的按钮"在锁定后仍被读取到
                if (_inputLocked)
                {
                    ClearAllFrameFlags();
                    _moveVector = Vector2.zero;
                    _jumpHeld = false;
                }
            }
        }

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            // 创建 InputActions 实例并注册回调
            _inputActions = new GhostVeilInputActions();
            _inputActions.Gameplay.SetCallbacks(this);
        }

        private void OnEnable()
        {
            // 启用 Gameplay Action Map
            _inputActions.Gameplay.Enable();
        }

        private void OnDisable()
        {
            // 禁用 Gameplay Action Map
            _inputActions.Gameplay.Disable();
        }

        private void OnDestroy()
        {
            // 释放 InputActionAsset 资源
            _inputActions?.Dispose();
            _inputActions = null;
        }

        /// <summary>
        /// LateUpdate —— 在所有 Update 执行完毕后清零帧标记。
        /// 这样可以保证：
        ///   · 同一帧内所有 MonoBehaviour.Update 都能读到 JumpPressed = true
        ///   · 下一帧的 Update 开始时，所有帧标记已被重置
        /// </summary>
        private void LateUpdate()
        {
            ClearAllFrameFlags();
        }

        // ══════════════════════════════════════════════
        //  IGameplayActions 回调实现
        // ══════════════════════════════════════════════
        //
        //  New Input System 的 CallbackContext 包含三个阶段：
        //    · started    — 按键开始按下（刚接触，尚未达到 press threshold）
        //    · performed  — 按键确认触发（超过阈值 / 完成 interaction）
        //    · canceled   — 按键释放
        //
        //  对于 Button 类型：
        //    performed = 按下   → 设置 Pressed 标记 + 触发 event
        //    canceled  = 松开   → 设置 Released 标记 + 清除 Held
        //
        //  对于 Value 类型（Move）：
        //    performed = 轴值变化 → 更新 _moveVector
        //    canceled  = 回到零位 → _moveVector = zero

        // ── Move ──────────────────────────────────────
        public void OnMove(InputAction.CallbackContext context)
        {
            // 直接读取 Vector2 值。
            // canceled 时 ReadValue 自动返回 zero，无需特殊处理。
            _moveVector = context.ReadValue<Vector2>();
        }

        // ── Jump ──────────────────────────────────────
        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _jumpPressed = true;
                _jumpHeld = true;

                if (!InputLocked)
                    OnJumpPressed?.Invoke();
            }
            else if (context.canceled)
            {
                _jumpReleased = true;
                _jumpHeld = false;

                if (!InputLocked)
                    OnJumpReleased?.Invoke();
            }
        }

        // ── Attack ────────────────────────────────────
        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _attackPressed = true;

                if (!InputLocked)
                    OnAttackPressed?.Invoke();
            }
        }

        // ── Dash ──────────────────────────────────────
        public void OnDash(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _dashPressed = true;

                if (!InputLocked)
                    OnDashPressed?.Invoke();
            }
        }

        // ── Interact ──────────────────────────────────
        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _interactPressed = true;

                if (!InputLocked)
                    OnInteractPressed?.Invoke();
            }
        }

        // ── Pause ─────────────────────────────────────
        public void OnPause(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _pausePressed = true;

                // Pause 比较特殊：即使 InputLocked 也应该能暂停
                // （玩家在过场中想暂停应该被允许）
                // 如果你的设计不允许，把这行移到 if(!InputLocked) 内
                OnPausePressed?.Invoke();
            }
        }

        // ══════════════════════════════════════════════
        //  内部辅助
        // ══════════════════════════════════════════════

        /// <summary>
        /// 清除所有脉冲型帧标记。
        /// 在 LateUpdate 和 InputLocked 置 true 时调用。
        /// 注意：不清除 _jumpHeld（持续量）和 _moveVector（持续量）。
        /// </summary>
        private void ClearAllFrameFlags()
        {
            _jumpPressed    = false;
            _jumpReleased   = false;
            _attackPressed  = false;
            _dashPressed    = false;
            _interactPressed = false;
            _pausePressed   = false;
        }

        // ══════════════════════════════════════════════
        //  公共辅助（供叙事系统 / 调试使用）
        // ══════════════════════════════════════════════

        /// <summary>
        /// 切换 Action Map。
        /// 例如进入 UI 菜单时切到 UI Map，退出后切回 Gameplay。
        /// </summary>
        public void SwitchToGameplay()
        {
            _inputActions.Gameplay.Enable();
            // 未来扩展：_inputActions.UI.Disable();
        }

        public void SwitchToUI()
        {
            _inputActions.Gameplay.Disable();
            // 未来扩展：_inputActions.UI.Enable();
        }

        /// <summary>
        /// 获取底层 InputActions 实例的只读引用。
        /// 用于需要直接访问 InputAction 的高级场景（如 Rebinding UI）。
        /// </summary>
        public GhostVeilInputActions RawActions => _inputActions;
    }
}
