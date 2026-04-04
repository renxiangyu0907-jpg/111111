// ============================================================================
// AbstractRaycastController.cs — 射线碰撞控制器抽象基类
// ============================================================================
using UnityEngine;
using GhostVeil.Data;

namespace GhostVeil.Physics
{
    /// <summary>
    /// 纯射线 2D 碰撞控制器的抽象骨架。
    /// 
    /// 核心思想（Sebastian Lague 式 Raycast Controller）：
    /// - 角色挂载 BoxCollider2D 仅用作射线原点参考框，不参与物理引擎碰撞。
    /// - 每帧在 BoxCollider2D 四边发射若干射线，检测碰撞并修正位移。
    /// - 完全掌控角色移动，杜绝 Rigidbody2D 带来的不可控抖动。
    /// 
    /// 管线顺序（严格不可打乱）：
    ///   1. UpdateRaycastOrigins
    ///   2. Reset CollisionInfo（保留 FallingThroughPlatform）
    ///   3. 记录原始 velocity 快照
    ///   4. DescendSlope（仅 movement.y &lt; 0 时）
    ///   5. HorizontalCollisions（若 movement.x != 0）
    ///   6. VerticalCollisions（若 movement.y != 0）
    ///   7. Translate
    ///   8. 强制 grounded（移动平台场景）
    /// 
    /// 子类职责：
    /// - 实现 HorizontalCollisions / VerticalCollisions 具体射线逻辑。
    /// - 实现 ClimbSlope / DescendSlope 坡道处理。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public abstract class AbstractRaycastController : MonoBehaviour, IRaycastController
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== Raycast Settings ===")]
        [SerializeField] protected LayerMask collisionMask;

        [Tooltip("单向平台层（可从下方穿越、可按下+跳跃下落）")]
        [SerializeField] protected LayerMask oneWayPlatformMask;

        [Tooltip("水平方向射线条数")]
        [SerializeField, Range(2, 20)] protected int horizontalRayCount = 4;

        [Tooltip("垂直方向射线条数")]
        [SerializeField, Range(2, 20)] protected int verticalRayCount = 4;

        [Tooltip("射线收缩内边距（Skin Width，防止浮点误差导致射线从墙内发射）")]
        [SerializeField] protected float skinWidth = 0.015f;

        [Tooltip("可攀爬的最大坡度角（度）")]
        [SerializeField] protected float maxSlopeAngle = 55f;

        // ══════════════════════════════════════════════
        //  运行时数据
        // ══════════════════════════════════════════════

        protected CollisionInfo _collisions;
        protected BoxCollider2D _boxCollider;
        protected RaycastOrigins _raycastOrigins;

        protected float _horizontalRaySpacing;
        protected float _verticalRaySpacing;

        /// <summary>
        /// 进入 Move 时的原始期望速度快照。
        /// ClimbSlope / DescendSlope 需要用原始 X 分量来计算坡道上的 Y 分量，
        /// 不能使用被碰撞修正过的 movement。
        /// </summary>
        protected Vector2 _velocityOld;

        // ══════════════════════════════════════════════
        //  IRaycastController 实现
        // ══════════════════════════════════════════════

        public ref CollisionInfo Collisions => ref _collisions;

        public LayerMask CollisionMask
        {
            get => collisionMask;
            set => collisionMask = value;
        }

        /// <summary>运行时访问单向平台掩码</summary>
        public LayerMask OneWayPlatformMask
        {
            get => oneWayPlatformMask;
            set => oneWayPlatformMask = value;
        }

        // ══════════════════════════════════════════════
        //  射线原点结构
        // ══════════════════════════════════════════════

        protected struct RaycastOrigins
        {
            public Vector2 TopLeft, TopRight;
            public Vector2 BottomLeft, BottomRight;
        }

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        protected virtual void Awake()
        {
            _boxCollider = GetComponent<BoxCollider2D>();
        }

        protected virtual void Start()
        {
            CalculateRaySpacing();
        }

        // ══════════════════════════════════════════════
        //  IRaycastController.Move — 核心管线
        // ══════════════════════════════════════════════

        public Vector2 Move(Vector2 desiredMovement, bool standingOnPlatform = false)
        {
            // ── Step 1: 刷新射线原点 ────────────────────
            UpdateRaycastOrigins();

            // ── Step 2: 重置碰撞信息（保留 FallingThroughPlatform） ──
            _collisions.Reset();

            // ── Step 3: 快照原始速度 ────────────────────
            _velocityOld = desiredMovement;

            // ── Step 4: 记录水平朝向 ────────────────────
            if (desiredMovement.x != 0)
                _collisions.FaceDir = (int)Mathf.Sign(desiredMovement.x);

            Vector2 movement = desiredMovement;

            // ── Step 5: 下坡检测（仅当向下移动时） ────────
            if (movement.y < 0)
                DescendSlope(ref movement);

            // ── Step 6: 水平碰撞检测 ────────────────────
            if (movement.x != 0)
                HorizontalCollisions(ref movement);

            // ── Step 7: 垂直碰撞检测 ────────────────────
            // 始终执行垂直碰撞检测，不再跳过 movement.y == 0 的情况。
            // 原因：Move() 开头 Reset() 会清掉 Below 标记，如果跳过垂直检测，
            // IsGrounded 会在 Below=false 的状态下被读取，导致状态机误判为
            // 空中 → fall↔idle 抖动。VerticalCollisions 内部已处理 movement.y==0
            // 的零速度地面探测（向下发射 skinWidth*2 的短射线），不会产生副作用。
            VerticalCollisions(ref movement);

            // ── Step 8: 应用最终位移 ────────────────────
            transform.Translate(movement);

            // ── Step 9: 移动平台强制着地 ────────────────
            if (standingOnPlatform)
                _collisions.Below = true;

            return movement;
        }

        // ══════════════════════════════════════════════
        //  射线原点 / 间距计算
        // ══════════════════════════════════════════════

        public void UpdateRaycastOrigins()
        {
            Bounds bounds = _boxCollider.bounds;
            bounds.Expand(skinWidth * -2f);

            _raycastOrigins.BottomLeft  = new Vector2(bounds.min.x, bounds.min.y);
            _raycastOrigins.BottomRight = new Vector2(bounds.max.x, bounds.min.y);
            _raycastOrigins.TopLeft     = new Vector2(bounds.min.x, bounds.max.y);
            _raycastOrigins.TopRight    = new Vector2(bounds.max.x, bounds.max.y);
        }

        protected void CalculateRaySpacing()
        {
            Bounds bounds = _boxCollider.bounds;
            bounds.Expand(skinWidth * -2f);

            horizontalRayCount = Mathf.Clamp(horizontalRayCount, 2, int.MaxValue);
            verticalRayCount   = Mathf.Clamp(verticalRayCount,   2, int.MaxValue);

            _horizontalRaySpacing = bounds.size.y / (horizontalRayCount - 1);
            _verticalRaySpacing   = bounds.size.x / (verticalRayCount   - 1);
        }

        // ══════════════════════════════════════════════
        //  子类必须实现的碰撞检测方法
        // ══════════════════════════════════════════════

        protected abstract void HorizontalCollisions(ref Vector2 movement);
        protected abstract void VerticalCollisions(ref Vector2 movement);
        protected abstract void ClimbSlope(ref Vector2 movement, float slopeAngle, Vector2 slopeNormal);
        protected abstract void DescendSlope(ref Vector2 movement);
    }
}
