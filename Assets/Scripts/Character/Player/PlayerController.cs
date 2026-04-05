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

        /// <summary>移动参数数据</summary>
        public PlayerMovementData MoveData => moveData;

        /// <summary>输入源（状态通过此属性读取输入）</summary>
        public IInputProvider Input => inputProvider;

        /// <summary>射线控制器的强类型引用（供状态调用单向平台穿透等）</summary>
        public PlayerRaycastController RaycastCtrl { get; private set; }

        /// <summary>状态机只读访问（供 Debug UI 显示当前状态）</summary>
        public StateMachine<PlayerController> StateMachine => _stateMachine;

        /// <summary>
        /// Spine 动画控制器（供状态调用 PlayAnim / SetFaceDirection）。
        /// 可为 null —— 未挂 SpineAnimator 时角色逻辑照常运行，只是没有动画。
        /// </summary>
        public SpineAnimator Animator { get; private set; }

        // ── 动画名称公开属性（供状态类读取） ────────────
        /// <summary>Idle 动画名（Inspector 可配置）</summary>
        public string IdleAnimName => idleAnimName;
        /// <summary>Run 动画名</summary>
        public string RunAnimName => runAnimName;
        /// <summary>Jump 动画名</summary>
        public string JumpAnimName => jumpAnimName;
        /// <summary>Fall 动画名</summary>
        public string FallAnimName => fallAnimName;

        // ══════════════════════════════════════════════
        //  跳跃缓冲 (Jump Buffer)
        // ══════════════════════════════════════════════
        //
        //  原理：玩家在空中按下跳跃键时，记录一个倒计时。
        //  如果在倒计时归零之前角色着地了 → 立即起跳。
        //  这样就不会"吞掉"落地前几帧的按键。

        /// <summary>跳跃缓冲剩余时间（> 0 表示缓冲有效）</summary>
        public float JumpBufferTimer { get; set; }

        /// <summary>是否存在有效的跳跃缓冲请求</summary>
        public bool HasJumpBuffer => JumpBufferTimer > 0f;

        // ══════════════════════════════════════════════
        //  郊狼时间 (Coyote Time)
        // ══════════════════════════════════════════════
        //
        //  原理：角色走出平台边缘后，短暂时间内仍算"站在地面上"。
        //  给玩家一个反应窗口，让他们在视觉上已经离开平台后仍能起跳。

        /// <summary>郊狼时间剩余（> 0 表示仍可起跳）</summary>
        public float CoyoteTimer { get; set; }

        /// <summary>是否在郊狼时间窗口内</summary>
        public bool InCoyoteTime => CoyoteTimer > 0f;

        // ══════════════════════════════════════════════
        //  状态辅助标记
        // ══════════════════════════════════════════════

        /// <summary>
        /// 本次跳跃是否已经执行过"松手削减"（Jump Cut）。
        /// 防止一次跳跃中多次削减速度。
        /// </summary>
        public bool HasJumpCut { get; set; }

        /// <summary>上一帧是否站在地面（用于检测"刚离开地面"）</summary>
        public bool WasGroundedLastFrame { get; private set; }

        // ══════════════════════════════════════════════
        //  水平速度平滑计算的中间变量
        // ══════════════════════════════════════════════

        /// <summary>
        /// 水平速度平滑计算的中间变量。
        /// SmoothDamp 内部使用的参考速度（不要手动修改）
        /// </summary>
        [System.NonSerialized]
        public float HorizontalSmoothVelocity;

        // ══════════════════════════════════════════════
        //  着地宽容期 (Ground Grace)
        // ══════════════════════════════════════════════
        //
        //  在不平地形上 IsGrounded 可能每帧闪烁，导致状态机
        //  在 Idle/Run <-> Fall 之间快速切换，表现为抖动。
        //  Ground Grace 在角色刚刚失去地面接触后提供一个很短的
        //  宽容期（远小于 coyoteTime），期间仍视为着地。

        private float _groundGraceTimer;

        /// <summary>调试用：宽容期剩余时间</summary>
        public float GroundGraceRemaining => _groundGraceTimer;

        /// <summary>
        /// 着地宽容期时长（秒）。
        /// 拼接地面（多个 BoxCollider2D，Edge Radius=0）的接缝处，
        /// 射线可能在 1~3 帧内全部落入间隙导致 Below=false。
        /// 100ms 宽容期可覆盖约 6 帧（60fps），足够消除接缝闪烁，
        /// 同时仍远小于 coyoteTime，不会影响走出平台后的下落感觉。
        /// </summary>
        private const float GroundGraceDuration = 0.10f; // 100ms 宽容期

        /// <summary>
        /// 是否视为站在地面（含宽容期）。
        /// 状态机和跳跃判定应使用此属性而非 base.IsGrounded。
        /// </summary>
        public new bool IsGrounded
        {
            get
            {
                if (base.IsGrounded) return true;
                return _groundGraceTimer > 0f;
            }
        }

        // ══════════════════════════════════════════════
        //  CharacterController2D 抽象方法实现
        // ══════════════════════════════════════════════

        protected override void GatherDependencies()
        {
            // ── 1. 射线控制器（必须在同一 GameObject 上） ──────
            RaycastCtrl = GetComponent<PlayerRaycastController>();
            raycastController = RaycastCtrl;

            // ── 2. 输入源 ──────────────────────────────────────
            //    优先从同物体找具体类型（接口 GetComponent 在某些 Unity 版本不可靠）
            var localProvider = GetComponent<InputSystemProvider>();
            if (localProvider != null)
            {
                inputProvider = localProvider;
            }
            else
            {
                // 不在同一物体上 → 全场景搜索
                var sceneProvider = FindObjectOfType<InputSystemProvider>();
                if (sceneProvider != null)
                {
                    inputProvider = sceneProvider;
                }
            }

            // ── 3. Spine 动画（可选，找不到不报错） ────────────
            Animator = GetComponent<SpineAnimator>();
            if (Animator == null)
                Animator = GetComponentInChildren<SpineAnimator>();
            // 同时填充基类的接口引用，保持一致性
            spineBridge = Animator;

            // ── 安全检查（Error 级别，不会被 strip 掉） ──────────
            if (RaycastCtrl == null)
                Debug.LogError("[PlayerController] 找不到 PlayerRaycastController！请确认已挂在同一 GameObject 上。", this);
            if (inputProvider == null)
                Debug.LogError("[PlayerController] 找不到 IInputProvider！请确认 InputSystemProvider 已挂在 Player 或场景中的某个 GameObject 上。", this);
            if (moveData == null)
                Debug.LogError("[PlayerController] PlayerMovementData 未赋值！请在 Inspector 中拖入 MoveData 资产。", this);
        }

        protected override void RegisterStates()
        {
            _stateMachine = new StateMachine<PlayerController>(this);

            // 注册四个基础状态
            _stateMachine.RegisterState(new PlayerIdleState());
            _stateMachine.RegisterState(new PlayerRunState());
            _stateMachine.RegisterState(new PlayerJumpState());
            _stateMachine.RegisterState(new PlayerFallState());

            // 以 Idle 为初始状态
            _stateMachine.Initialize(BuiltInStateID.Idle);
        }

        /// <summary>
        /// Start 完成后的额外初始化。
        /// 执行一次原地地面检测（不位移），避免第一帧因 Collisions.Below 未设置
        /// 而从 Idle 误切到 Fall（表现为开场瞬间抖一下）。
        /// </summary>
        protected override void OnStart()
        {
            if (raycastController == null) return;

            // 用一次零位移 Move 来初始化碰撞状态。
            // movement = (0,0) → VerticalCollisions 走纯探测路径（只设标记不位移）。
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
            // ── 安全守卫：任何核心依赖缺失时跳过整个逻辑帧 ──
            if (inputProvider == null || moveData == null || raycastController == null)
                return;

            // ── Step 0: 更新着地宽容期计时器 ────────────
            if (base.IsGrounded)
            {
                _groundGraceTimer = GroundGraceDuration;
            }
            else
            {
                _groundGraceTimer -= deltaTime;
            }

            // ── Step 1: 更新辅助计时器 ──────────────────

            // 跳跃缓冲：每帧衰减，按下跳跃键时重置
            if (Input.JumpPressed)
                JumpBufferTimer = moveData.jumpBufferTime;
            else
                JumpBufferTimer -= deltaTime;

            // 郊狼时间：站在地面上时持续重置，离开地面后开始衰减
            if (IsGrounded)
                CoyoteTimer = moveData.coyoteTime;
            else
                CoyoteTimer -= deltaTime;

            // ── Step 2: 状态机 Tick ────────────────────
            _stateMachine.LogicTick(deltaTime);

            // ── Step 3: 全局重力累加 ────────────────────
            ApplyGravity(deltaTime);

            // ── Step 4: 限制最大下落速度 ────────────────
            ClampFallSpeed();

            // ── Step 5: 执行物理位移 ────────────────────
            ExecuteMovement(deltaTime);

            // ── Step 6: 记录本帧着地状态（供下一帧判断） ──
            WasGroundedLastFrame = IsGrounded;
        }

        // ══════════════════════════════════════════════
        //  重力
        // ══════════════════════════════════════════════

        /// <summary>
        /// 全局重力累加。
        /// · 上升期：使用基础重力（让跳跃弧线更"飘"）
        /// · 下落期：使用加强重力（让落地更"脆"、更有重量感）
        /// · 着地时：Velocity.y 保持微小负值（确保射线持续检测地面）
        /// </summary>
        private void ApplyGravity(float deltaTime)
        {
            if (IsGrounded && Velocity.y <= 0f)
            {
                // 着地：给一个很小的向下速度以维持地面射线检测。
                // -0.5f 足以让 VerticalCollisions 运行（movement.y != 0），
                // 但不会大到让角色在一帧内产生可感知的下坠。
                // 之前的 -2f 值过大，在帧率波动时可能导致 grounded 闪烁。
                Velocity = new Vector2(Velocity.x, -0.5f);
                return;
            }

            // 选择重力：上升用基础重力，下落用加强重力
            float gravity = (Velocity.y > 0f) ? moveData.Gravity : moveData.FallGravity;
            Velocity = new Vector2(Velocity.x, Velocity.y + gravity * deltaTime);
        }

        /// <summary>限制最大下落速度</summary>
        private void ClampFallSpeed()
        {
            if (Velocity.y < moveData.maxFallSpeed)
                Velocity = new Vector2(Velocity.x, moveData.maxFallSpeed);
        }

        // ══════════════════════════════════════════════
        //  物理位移执行
        // ══════════════════════════════════════════════

        /// <summary>
        /// 将 Velocity 转换为位移并送入射线控制器。
        /// 射线控制器碰撞修正后，回写 Velocity（撞墙/撞地/撞头时清零对应分量）。
        /// </summary>
        private void ExecuteMovement(float deltaTime)
        {
            Vector2 displacement = Velocity * deltaTime;
            Vector2 actualMovement = raycastController.Move(displacement);

            // ── 碰撞后速度修正 ──────────────────────────
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

        /// <summary>
        /// 平滑水平移动计算。
        /// 根据当前是否在地面选择不同的加速/减速参数。
        /// 
        /// 使用 SmoothDamp 而非线性插值的原因：
        ///   · SmoothDamp 天然带有"起步快、到位缓"的曲线，手感更自然
        ///   · smoothTime 参数直觉上就是"达到目标的大致时间"，策划友好
        ///   · 自带速度钳制，不会出现过冲（overshoot）
        /// </summary>
        /// <param name="targetSpeed">目标水平速度（input.x * maxSpeed）</param>
        public void ApplyHorizontalSmoothing(float targetSpeed)
        {
            // 选择平滑时间：有输入 = 加速，无输入 = 减速
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

        /// <summary>
        /// 执行跳跃：设置垂直速度 + 消耗缓冲 + 清除郊狼时间。
        /// 由状态（Idle / Run / Fall）在满足起跳条件时调用。
        /// </summary>
        public void ExecuteJump()
        {
            Velocity = new Vector2(Velocity.x, moveData.JumpVelocity);
            JumpBufferTimer = 0f;  // 消耗掉跳跃缓冲
            CoyoteTimer = 0f;      // 消耗掉郊狼时间
            HasJumpCut = false;    // 重置松手削减标记
        }

        /// <summary>
        /// 跳跃削减（Jump Cut）：松开跳跃键时削减上升速度。
        /// 只在上升阶段且未被削减过时生效。
        /// </summary>
        public void ExecuteJumpCut()
        {
            if (HasJumpCut) return;
            if (Velocity.y <= 0f) return;

            Velocity = new Vector2(Velocity.x, Velocity.y * moveData.jumpCutMultiplier);
            HasJumpCut = true;
        }

        /// <summary>
        /// 根据水平输入翻转角色朝向。
        /// 同时同步 Spine 骨骼的 ScaleX 翻转。
        /// </summary>
        public void UpdateFacing()
        {
            float h = Input.HorizontalInput;
            if (h > 0.01f) SetFacing(FacingDirection.Right);
            else if (h < -0.01f) SetFacing(FacingDirection.Left);

            // 同步 Spine 骨骼翻转
            Animator?.SetFaceDirection(Facing);
        }

        /// <summary>
        /// 判断当前是否可以起跳（综合郊狼时间 + 着地状态）。
        /// </summary>
        public bool CanJump()
        {
            return IsGrounded || InCoyoteTime;
        }

        /// <summary>
        /// 判断是否应该触发跳跃（有缓冲请求 且 可以起跳）。
        /// </summary>
        public bool ShouldJump()
        {
            return HasJumpBuffer && CanJump();
        }
    }
}
