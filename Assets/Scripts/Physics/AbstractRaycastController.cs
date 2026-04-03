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
    /// 子类职责：
    /// - 实现 HorizontalCollisions / VerticalCollisions 具体射线逻辑。
    /// - 实现坡道处理（ClimbSlope / DescendSlope）。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public abstract class AbstractRaycastController : MonoBehaviour, IRaycastController
    {
        // ── Inspector 配置 ────────────────────────────
        [Header("=== Raycast Settings ===")]
        [SerializeField] protected LayerMask collisionMask;

        [Tooltip("水平方向射线条数")]
        [SerializeField, Range(2, 20)] protected int horizontalRayCount = 4;

        [Tooltip("垂直方向射线条数")]
        [SerializeField, Range(2, 20)] protected int verticalRayCount = 4;

        [Tooltip("射线收缩内边距（防止射线从墙内发射）")]
        [SerializeField] protected float skinWidth = 0.015f;

        [Tooltip("可攀爬的最大坡度角")]
        [SerializeField] protected float maxSlopeAngle = 55f;

        // ── 运行时数据 ────────────────────────────────
        protected CollisionInfo _collisions;
        protected BoxCollider2D _boxCollider;
        protected RaycastOrigins _raycastOrigins;

        protected float _horizontalRaySpacing;
        protected float _verticalRaySpacing;

        // ── IRaycastController 实现 ───────────────────
        public ref CollisionInfo Collisions => ref _collisions;

        public LayerMask CollisionMask
        {
            get => collisionMask;
            set => collisionMask = value;
        }

        // ── 射线原点结构 ──────────────────────────────
        protected struct RaycastOrigins
        {
            public Vector2 TopLeft, TopRight;
            public Vector2 BottomLeft, BottomRight;
        }

        // ── Unity 生命周期 ────────────────────────────
        protected virtual void Awake()
        {
            _boxCollider = GetComponent<BoxCollider2D>();
        }

        protected virtual void Start()
        {
            CalculateRaySpacing();
        }

        // ── IRaycastController.Move ───────────────────
        public Vector2 Move(Vector2 desiredMovement, bool standingOnPlatform = false)
        {
            UpdateRaycastOrigins();
            _collisions.Reset();

            Vector2 movement = desiredMovement;

            // 下坡检测（仅当向下移动时）
            if (movement.y < 0)
                DescendSlope(ref movement);

            // 水平碰撞检测
            if (movement.x != 0)
                HorizontalCollisions(ref movement);

            // 垂直碰撞检测
            if (movement.y != 0)
                VerticalCollisions(ref movement);

            // 应用位移
            transform.Translate(movement);

            // 若站在平台上，强制标记 grounded（防止平台下降时角色浮空）
            if (standingOnPlatform)
                _collisions.Below = true;

            return movement;
        }

        // ── 射线原点计算 ──────────────────────────────
        public void UpdateRaycastOrigins()
        {
            Bounds bounds = _boxCollider.bounds;
            bounds.Expand(skinWidth * -2f);

            _raycastOrigins.BottomLeft  = new Vector2(bounds.min.x, bounds.min.y);
            _raycastOrigins.BottomRight = new Vector2(bounds.max.x, bounds.min.y);
            _raycastOrigins.TopLeft     = new Vector2(bounds.min.x, bounds.max.y);
            _raycastOrigins.TopRight    = new Vector2(bounds.max.x, bounds.max.y);
        }

        // ── 射线间距计算 ──────────────────────────────
        protected void CalculateRaySpacing()
        {
            Bounds bounds = _boxCollider.bounds;
            bounds.Expand(skinWidth * -2f);

            horizontalRayCount = Mathf.Clamp(horizontalRayCount, 2, int.MaxValue);
            verticalRayCount   = Mathf.Clamp(verticalRayCount,   2, int.MaxValue);

            _horizontalRaySpacing = bounds.size.y / (horizontalRayCount - 1);
            _verticalRaySpacing   = bounds.size.x / (verticalRayCount   - 1);
        }

        // ── 子类必须实现的碰撞检测细节 ──────────────────
        protected abstract void HorizontalCollisions(ref Vector2 movement);
        protected abstract void VerticalCollisions(ref Vector2 movement);
        protected abstract void ClimbSlope(ref Vector2 movement, float slopeAngle, Vector2 slopeNormal);
        protected abstract void DescendSlope(ref Vector2 movement);
    }
}
