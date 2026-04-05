// ============================================================================
// PlayerController.cs — 玩家控制器（状态机宿主 + 核心游戏逻辑）
// ============================================================================
//
// 每帧数据流：
//   Update()
//     1. 更新辅助计时器（跳跃缓冲 / 郊狼时间）
//     2. stateMachine.LogicTick(deltaTime) → 状态计算水平速度 + 检测转移
//     3. ApplyGravity(deltaTime) → 全局重力累加
//     4. ClampFallSpeed() → 限制最大下落速度
//     5. ExecuteMovement(deltaTime) → Velocity 送入物理控制器
//
// 重力在状态机外面：避免每个状态重复写重力逻辑。
// 跳跃状态只负责设置 Velocity.y = JumpVelocity，之后重力接管。
// ============================================================================

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
        [SerializeField] private PlayerMovementData moveData;

        [Header("=== Spine 动画名称映射 ===")]
        [SerializeField] private string idleAnimName = "idle";
        [SerializeField] private string runAnimName = "run";
        [SerializeField] private string jumpAnimName = "jump";
        [SerializeField] private string fallAnimName = "fall";

        // ══════════════════════════════════════════════
        //  强类型状态机
        // ══════════════════════════════════════════════

        private StateMachine<PlayerController> _stateMachine;

        // ══════════════════════════════════════════════
        //  公开引用
        // ══════════════════════════════════════════════

        public PlayerMovementData MoveData => moveData;
        public IInputProvider Input => inputProvider;
        public PlayerPhysicsController Physics => physicsController;
        public StateMachine<PlayerController> StateMachine => _stateMachine;
        public SpineAnimator Animator { get; private set; }

        // ── 动画名称 ──
        public string IdleAnimName => idleAnimName;
        public string RunAnimName => runAnimName;
        public string JumpAnimName => jumpAnimName;
        public string FallAnimName => fallAnimName;

        // ══════════════════════════════════════════════
        //  跳跃缓冲
        // ══════════════════════════════════════════════

        public float JumpBufferTimer { get; set; }
        public bool HasJumpBuffer => JumpBufferTimer > 0f;

        // ══════════════════════════════════════════════
        //  郊狼时间
        // ══════════════════════════════════════════════

        public float CoyoteTimer { get; set; }
        public bool InCoyoteTime => CoyoteTimer > 0f;

        // ══════════════════════════════════════════════
        //  状态辅助
        // ══════════════════════════════════════════════

        public bool HasJumpCut { get; set; }
        public bool WasGroundedLastFrame { get; private set; }

        [System.NonSerialized]
        public float HorizontalSmoothVelocity;

        // ══════════════════════════════════════════════
        //  依赖收集
        // ══════════════════════════════════════════════

        protected override void GatherDependencies()
        {
            physicsController = GetComponent<PlayerPhysicsController>();

            var localProvider = GetComponent<InputSystemProvider>();
            if (localProvider != null)
            {
                inputProvider = localProvider;
            }
            else
            {
                var sceneProvider = FindObjectOfType<InputSystemProvider>();
                if (sceneProvider != null)
                    inputProvider = sceneProvider;
            }

            Animator = GetComponent<SpineAnimator>();
            if (Animator == null)
                Animator = GetComponentInChildren<SpineAnimator>();
            spineBridge = Animator;

            if (physicsController == null)
                Debug.LogError("[PlayerController] PlayerPhysicsController not found!", this);
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
            if (physicsController == null) return;

            // 初始化地面检测
            physicsController.ForceGroundCheck();

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
            if (inputProvider == null || moveData == null || physicsController == null)
                return;

            // ── Step 1: 辅助计时器 ──
            if (Input.JumpPressed)
                JumpBufferTimer = moveData.jumpBufferTime;
            else
                JumpBufferTimer -= deltaTime;

            if (IsGrounded)
                CoyoteTimer = moveData.coyoteTime;
            else
                CoyoteTimer -= deltaTime;

            // ── Step 2: 状态机 Tick ──
            _stateMachine.LogicTick(deltaTime);

            // ── Step 3: 重力 ──
            ApplyGravity(deltaTime);

            // ── Step 4: 限速 ──
            ClampFallSpeed();

            // ── Step 5: 执行移动 ──
            ExecuteMovement(deltaTime);

            // ── Step 6: 记录着地状态 ──
            WasGroundedLastFrame = IsGrounded;
        }

        // ══════════════════════════════════════════════
        //  重力
        // ══════════════════════════════════════════════

        private void ApplyGravity(float deltaTime)
        {
            if (IsGrounded && Velocity.y <= 0f)
            {
                // 着地时给微小向下速度，确保下一帧物理检测能触碰地面
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
        //  执行移动
        // ══════════════════════════════════════════════

        private void ExecuteMovement(float deltaTime)
        {
            // 将 Velocity 传给物理控制器，获取碰撞修正后的速度
            Velocity = physicsController.Move(Velocity, deltaTime);
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

            float newX = Mathf.SmoothDamp(
                Velocity.x, targetSpeed,
                ref HorizontalSmoothVelocity, smoothTime);

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
