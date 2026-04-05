// ============================================================================
// PlayerController.cs — 玩家控制器（状态机宿主 + 核心游戏逻辑）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  每帧数据流：                                                            │
// │                                                                          │
// │  Update()                                                                │
// │    ├─ 1. 更新辅助计时器（跳跃缓冲 / 郊狼时间）                           │
// │    ├─ 2. stateMachine.LogicTick(deltaTime)                               │
// │    │      ├─ currentState.LogicUpdate()     ← 状态内计算水平速度         │
// │    │      └─ currentState.CheckTransitions()← 状态自行决定是否切换       │
// │    ├─ 3. ApplyGravity(deltaTime)            ← 全局统一累加重力           │
// │    ├─ 4. ClampFallSpeed()                   ← 限制最大下落速度           │
// │    └─ 5. ExecuteMovement(deltaTime)          ← Velocity → Raycast.Move  │
// │           └─ 碰撞修正后更新 Velocity.y（撞地/撞头清零）                  │
// │                                                                          │
// │  为什么重力在状态机外面？                                                 │
// │    · 重力是全局规则，每个状态都需要（Idle 站在坡上也要重力贴地）          │
// │    · 放在外面避免 4 个状态各写一遍 gravity 逻辑                          │
// │    · 跳跃状态只负责设置 Velocity.y = JumpVelocity，之后重力接管           │
// │                                                                          │
// │  GroundGrace 已移除：                                                     │
// │    · 原来的 GroundGrace 通过 100ms 宽容期掩盖 Below 闪烁，               │
// │      但它会在跳跃首帧仍然返回 IsGrounded=true，干扰跳跃判定。             │
// │    · 现在 Below 由 GroundCheck 独立设置（与碰撞修正分离），               │
// │      不再有接缝漏检导致的闪烁，因此不再需要 GroundGrace。                 │
// │    · Coyote Time（0.12s）仍在状态机层面提供走出平台后的跳跃宽限。         │
// └──────────────────────────────────────────────────────────────────────────┘

using UnityEngine;
using GhostVeil.Character.Common;
using GhostVeil.Core.StateMachine;
using GhostVeil.Data;
using GhostVeil.Data.ScriptableObjects;
using GhostVeil.Physics;
using GhostVeil.Input;
using GhostVeil.Animation.Spine;

namespace GhostVeil.Character.Player
{
    public class PlayerController : CharacterController2D
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== Player Settings ===")]
        [Tooltip("移动参数数据（ScriptableObject）")]
        [SerializeField] private PlayerMovementData moveData;

        [Header("=== Spine 动画名称映射 ===")]
        [Tooltip("Idle 状态对应的 Spine 动画名（必须与 Spine 编辑器中一致）")]
        [SerializeField] private string idleAnimName = "idle";

        [Tooltip("Run 状态对应的 Spine 动画名")]
        [SerializeField] private string runAnimName = "run";

        [Tooltip("Jump 状态对应的 Spine 动画名")]
        [SerializeField] private string jumpAnimName = "jump";

        [Tooltip("Fall 状态对应的 Spine 动画名")]
        [SerializeField] private string fallAnimName = "fall";

        // ══════════════════════════════════════════════
        //  强类型状态机
        // ══════════════════════════════════════════════

        private StateMachine<PlayerController> _stateMachine;

        // ══════════════════════════════════════════════
        //  公开引用（供状态读取，只读语义）
        // ══════════════════════════════════════════════

        public PlayerMovementData MoveData => moveData;
        public IInputProvider Input => inputProvider;
        public PlayerRaycastController RaycastCtrl { get; private set; }
        public StateMachine<PlayerController> StateMachine => _stateMachine;

        public SpineAnimator Animator { get; private set; }

        // ── 动画名称公开属性 ────────────
        public string IdleAnimName => idleAnimName;
        public string RunAnimName => runAnimName;
        public string JumpAnimName => jumpAnimName;
        public string FallAnimName => fallAnimName;

        // ══════════════════════════════════════════════
        //  跳跃缓冲 (Jump Buffer)
        // ══════════════════════════════════════════════

        public float JumpBufferTimer { get; set; }
        public bool HasJumpBuffer => JumpBufferTimer > 0f;

        // ══════════════════════════════════════════════
        //  郊狼时间 (Coyote Time)
        // ══════════════════════════════════════════════
        //
        //  角色走出平台边缘后，短暂时间内仍算"可以跳跃"。
        //  这是纯状态机层面的宽限，不影响物理层的 Below 判定。

        public float CoyoteTimer { get; set; }
        public bool InCoyoteTime => CoyoteTimer > 0f;

        // ══════════════════════════════════════════════
        //  状态辅助标记
        // ══════════════════════════════════════════════

        public bool HasJumpCut { get; set; }
        public bool WasGroundedLastFrame { get; private set; }

        // ══════════════════════════════════════════════
        //  水平速度平滑计算的中间变量
        // ══════════════════════════════════════════════

        [System.NonSerialized]
        public float HorizontalSmoothVelocity;

        // ══════════════════════════════════════════════
        //  调试信息（供 DebugOverlay 读取）
        // ══════════════════════════════════════════════

        /// <summary>调试用：始终返回 0（GroundGrace 已移除）</summary>
        public float GroundGraceRemaining => 0f;

        // ══════════════════════════════════════════════
        //  IsGrounded —— 直接使用 base.IsGrounded
        // ══════════════════════════════════════════════
        //
        //  GroundGrace 已移除。IsGrounded 现在直接读取物理层的
        //  Collisions.Below，不再有宽容期掩盖。
        //  Coyote Time 在 CanJump() 中提供走出平台后的跳跃宽限。

        // 注意：不再有 `new bool IsGrounded` 覆盖。
        // base.IsGrounded 从 CharacterController2D 继承，
        // 直接返回 raycastController.Collisions.Below。

        // ══════════════════════════════════════════════
        //  CharacterController2D 抽象方法实现
        // ══════════════════════════════════════════════

        protected override void GatherDependencies()
        {
            RaycastCtrl = GetComponent<PlayerRaycastController>();
            raycastController = RaycastCtrl;

            var localProvider = GetComponent<InputSystemProvider>();
            if (localProvider != null)
            {
                inputProvider = localProvider;
            }
            else
            {
                var sceneProvider = FindObjectOfType<InputSystemProvider>();
                if (sceneProvider != null)
                {
                    inputProvider = sceneProvider;
                }
            }

            Animator = GetComponent<SpineAnimator>();
            if (Animator == null)
                Animator = GetComponentInChildren<SpineAnimator>();
            spineBridge = Animator;

            if (RaycastCtrl == null)
                Debug.LogError("[PlayerController] PlayerRaycastController not found!", this);
            if (inputProvider == null)
                Debug.LogError("[PlayerController] IInputProvider not found!", this);
            if (moveData == null)
                Debug.LogError("[PlayerController] PlayerMovementData not assigned!", this);
        }

        protected override void RegisterStates()
        {
            _stateMachine = new StateMachine<PlayerController>(this);

            _stateMachine.RegisterState(new PlayerIdleState());
            _stateMachine.RegisterState(new PlayerRunState());
            _stateMachine.RegisterState(new PlayerJumpState());
            _stateMachine.RegisterState(new PlayerFallState());

            _stateMachine.Initialize(BuiltInStateID.Idle);
        }

        protected override void OnStart()
        {
            if (raycastController == null) return;

            // 零位移 Move 初始化碰撞状态
            raycastController.Move(Vector2.zero);

            if (IsGrounded)
            {
                WasGroundedLastFrame = true;
                Velocity = Vector2.zero;
            }
        }

        // ══════════════════════════════════════════════
        //  主循环
        // ══════════════════════════════════════════════

        protected override void OnLogicUpdate(float deltaTime)
        {
            if (inputProvider == null || moveData == null || raycastController == null)
                return;

            // ── Step 1: 更新辅助计时器 ──

            // 跳跃缓冲
            if (Input.JumpPressed)
                JumpBufferTimer = moveData.jumpBufferTime;
            else
                JumpBufferTimer -= deltaTime;

            // 郊狼时间：站在地面时持续重置，离开地面后衰减
            if (IsGrounded)
                CoyoteTimer = moveData.coyoteTime;
            else
                CoyoteTimer -= deltaTime;

            // ── Step 2: 状态机 Tick ──
            _stateMachine.LogicTick(deltaTime);

            // ── Step 3: 全局重力累加 ──
            ApplyGravity(deltaTime);

            // ── Step 4: 限制最大下落速度 ──
            ClampFallSpeed();

            // ── Step 5: 执行物理位移 ──
            ExecuteMovement(deltaTime);

            // ── Step 6: 记录本帧着地状态 ──
            WasGroundedLastFrame = IsGrounded;
        }

        // ══════════════════════════════════════════════
        //  重力
        // ══════════════════════════════════════════════

        private void ApplyGravity(float deltaTime)
        {
            if (IsGrounded && Velocity.y <= 0f)
            {
                // 着地：微小向下速度维持地面射线检测。
                // -0.5f 让 VerticalCollisions 运行（movement.y != 0），
                // 但 GroundCheck 的独立探测确保 Below 正确。
                Velocity = new Vector2(Velocity.x, -0.5f);
                return;
            }

            float gravity = (Velocity.y > 0f) ? moveData.Gravity : moveData.FallGravity;
            Velocity = new Vector2(Velocity.x, Velocity.y + gravity * deltaTime);
        }

        private void ClampFallSpeed()
        {
            if (Velocity.y < moveData.maxFallSpeed)
                Velocity = new Vector2(Velocity.x, moveData.maxFallSpeed);
        }

        // ══════════════════════════════════════════════
        //  物理位移执行
        // ══════════════════════════════════════════════

        private void ExecuteMovement(float deltaTime)
        {
            Vector2 displacement = Velocity * deltaTime;
            Vector2 actualMovement = raycastController.Move(displacement);

            ref var col = ref raycastController.Collisions;

            // 撞到地面或天花板 → 垂直速度清零
            if (col.Above || col.Below)
                Velocity = new Vector2(Velocity.x, 0f);

            // 撞到左右墙壁 → 水平速度清零
            if (col.Left || col.Right)
                Velocity = new Vector2(0f, Velocity.y);
        }

        // ══════════════════════════════════════════════
        //  公共方法（供状态调用）
        // ══════════════════════════════════════════════

        public void ApplyHorizontalSmoothing(float targetSpeed)
        {
            bool isAccelerating = Mathf.Abs(targetSpeed) > 0.01f;
            float smoothTime;

            if (IsGrounded)
                smoothTime = isAccelerating ? moveData.groundAccelTime : moveData.groundDecelTime;
            else
                smoothTime = isAccelerating ? moveData.airAccelTime : moveData.airDecelTime;

            float currentX = Velocity.x;
            float newX = Mathf.SmoothDamp(
                currentX,
                targetSpeed,
                ref HorizontalSmoothVelocity,
                smoothTime
            );

            Velocity = new Vector2(newX, Velocity.y);
        }

        public void ExecuteJump()
        {
            Velocity = new Vector2(Velocity.x, moveData.JumpVelocity);
            JumpBufferTimer = 0f;
            CoyoteTimer = 0f;
            HasJumpCut = false;
        }

        public void ExecuteJumpCut()
        {
            if (HasJumpCut) return;
            if (Velocity.y <= 0f) return;

            Velocity = new Vector2(Velocity.x, Velocity.y * moveData.jumpCutMultiplier);
            HasJumpCut = true;
        }

        public void UpdateFacing()
        {
            float h = Input.HorizontalInput;
            if (h > 0.01f) SetFacing(FacingDirection.Right);
            else if (h < -0.01f) SetFacing(FacingDirection.Left);

            Animator?.SetFaceDirection(Facing);
        }

        /// <summary>
        /// 判断当前是否可以起跳（着地 或 在郊狼时间窗口内）。
        /// Coyote Time 提供走出平台后 0.08~0.12 秒的跳跃宽限。
        /// </summary>
        public bool CanJump()
        {
            return IsGrounded || InCoyoteTime;
        }

        public bool ShouldJump()
        {
            return HasJumpBuffer && CanJump();
        }
    }
}
