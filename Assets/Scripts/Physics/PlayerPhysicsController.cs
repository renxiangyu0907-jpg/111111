// ============================================================================
// PlayerPhysicsController.cs — 基于 Rigidbody2D 的玩家物理控制器
// ============================================================================
// 替代原来的射线碰撞系统，使用 Unity 内置物理引擎。
//
// 核心设计：
//   · Rigidbody2D (Kinematic) + BoxCollider2D 作为角色碰撞体
//   · 使用 Rigidbody2D.MovePosition() 移动角色（Kinematic 模式不受力影响）
//   · 使用 Physics2D.BoxCast / OverlapBox 检测地面、墙壁、天花板
//   · 不使用 OnCollision / OnTrigger 回调，全部通过主动查询
//
// 优点：
//   · 利用 Unity 物理引擎处理碰撞穿透修正，不需要手写射线碰撞
//   · 代码量大幅减少，维护更简单
//   · 与 Tilemap CompositeCollider2D 天然兼容
// ============================================================================

using UnityEngine;

namespace GhostVeil.Physics
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerPhysicsController : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== Layer Masks ===")]
        [Tooltip("地面/墙壁碰撞层")]
        [SerializeField] private LayerMask groundMask;

        [Tooltip("单向平台层")]
        [SerializeField] private LayerMask oneWayPlatformMask;

        [Header("=== Ground Detection ===")]
        [Tooltip("地面检测射线向下延伸距离")]
        [SerializeField] private float groundCheckDistance = 0.05f;

        [Tooltip("墙壁检测射线水平延伸距离")]
        [SerializeField] private float wallCheckDistance = 0.05f;

        [Header("=== One-Way Platform ===")]
        [Tooltip("下落穿透单向平台后的冷却时间")]
        [SerializeField] private float fallThroughDuration = 0.15f;

        // ══════════════════════════════════════════════
        //  运行时组件引用
        // ══════════════════════════════════════════════

        private Rigidbody2D _rb;
        private BoxCollider2D _collider;

        // ══════════════════════════════════════════════
        //  公开状态（供 PlayerController / 状态机读取）
        // ══════════════════════════════════════════════

        /// <summary>是否站在地面上</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>是否碰到天花板</summary>
        public bool HitCeiling { get; private set; }

        /// <summary>是否碰到左边墙壁</summary>
        public bool HitLeft { get; private set; }

        /// <summary>是否碰到右边墙壁</summary>
        public bool HitRight { get; private set; }

        /// <summary>是否正在穿透单向平台</summary>
        public bool FallingThroughPlatform { get; private set; }

        /// <summary>脚下碰撞体</summary>
        public Collider2D GroundCollider { get; private set; }

        /// <summary>墙壁碰撞体</summary>
        public Collider2D WallCollider { get; private set; }

        /// <summary>朝向（-1 左 / 1 右）</summary>
        public int FaceDir { get; set; } = 1;

        // ── 单向平台穿透计时器 ──
        private float _fallThroughTimer;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<BoxCollider2D>();

            // Kinematic 模式：不受力/重力影响，但仍参与碰撞检测
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.useFullKinematicContacts = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 确保碰撞体不是 Trigger（需要物理碰撞阻挡）
            _collider.isTrigger = false;
        }

        private void Update()
        {
            // 单向平台穿透计时器
            if (_fallThroughTimer > 0f)
            {
                _fallThroughTimer -= Time.deltaTime;
                if (_fallThroughTimer <= 0f)
                {
                    FallingThroughPlatform = false;
                    _fallThroughTimer = 0f;
                }
            }
        }

        // ══════════════════════════════════════════════
        //  核心移动方法
        // ══════════════════════════════════════════════

        /// <summary>
        /// 移动角色并更新碰撞状态。
        /// 由 PlayerController 每帧调用。
        /// </summary>
        /// <param name="velocity">当前速度</param>
        /// <param name="deltaTime">时间步长</param>
        /// <returns>碰撞修正后的速度</returns>
        public Vector2 Move(Vector2 velocity, float deltaTime)
        {
            Vector2 displacement = velocity * deltaTime;

            // 记录朝向
            if (displacement.x != 0f)
                FaceDir = (int)Mathf.Sign(displacement.x);

            // ── 水平移动 + 碰撞 ──
            velocity = MoveHorizontal(velocity, displacement.x, deltaTime);

            // ── 垂直移动 + 碰撞 ──
            velocity = MoveVertical(velocity, velocity.y * deltaTime, deltaTime);

            // ── 地面检测（独立于移动） ──
            UpdateGroundCheck();

            return velocity;
        }

        // ══════════════════════════════════════════════
        //  水平移动
        // ══════════════════════════════════════════════

        private Vector2 MoveHorizontal(Vector2 velocity, float moveX, float deltaTime)
        {
            HitLeft = false;
            HitRight = false;
            WallCollider = null;

            if (Mathf.Abs(moveX) < 0.0001f)
                return velocity;

            float dirX = Mathf.Sign(moveX);
            float distance = Mathf.Abs(moveX);

            // BoxCast 检测墙壁
            Bounds bounds = _collider.bounds;
            Vector2 origin = bounds.center;
            Vector2 size = new Vector2(bounds.size.x, bounds.size.y - 0.02f); // 略微缩小避免地面边角误判

            RaycastHit2D hit = Physics2D.BoxCast(
                origin, size, 0f,
                Vector2.right * dirX, distance + wallCheckDistance,
                groundMask);

            if (hit && hit.distance > 0f)
            {
                // 修正距离：只走到碰撞点
                float allowedDist = Mathf.Max(0f, hit.distance - wallCheckDistance);
                if (allowedDist < distance)
                {
                    distance = allowedDist;
                    velocity.x = 0f;

                    if (dirX < 0) HitLeft = true;
                    else HitRight = true;
                    WallCollider = hit.collider;
                }
            }

            // 应用水平位移
            Vector2 pos = _rb.position;
            pos.x += distance * dirX;
            _rb.MovePosition(pos);

            return velocity;
        }

        // ══════════════════════════════════════════════
        //  垂直移动
        // ══════════════════════════════════════════════

        private Vector2 MoveVertical(Vector2 velocity, float moveY, float deltaTime)
        {
            HitCeiling = false;

            if (Mathf.Abs(moveY) < 0.0001f)
                return velocity;

            float dirY = Mathf.Sign(moveY);
            float distance = Mathf.Abs(moveY);

            // 构建碰撞掩码
            LayerMask mask = groundMask;
            if (dirY < 0 && !FallingThroughPlatform)
                mask |= oneWayPlatformMask;

            // BoxCast 检测地面/天花板
            Bounds bounds = _collider.bounds;
            Vector2 origin = bounds.center;
            Vector2 size = new Vector2(bounds.size.x - 0.02f, bounds.size.y); // 略微缩小避免墙角误判

            RaycastHit2D hit = Physics2D.BoxCast(
                origin, size, 0f,
                Vector2.up * dirY, distance + groundCheckDistance,
                mask);

            if (hit && hit.distance > 0f)
            {
                float allowedDist = Mathf.Max(0f, hit.distance - groundCheckDistance);
                if (allowedDist < distance)
                {
                    distance = allowedDist;
                    velocity.y = 0f;

                    if (dirY > 0) HitCeiling = true;
                }
            }

            // 应用垂直位移
            Vector2 pos = _rb.position;
            pos.y += distance * dirY;
            _rb.MovePosition(pos);

            return velocity;
        }

        // ══════════════════════════════════════════════
        //  地面检测
        // ══════════════════════════════════════════════

        /// <summary>
        /// 独立的地面检测，在移动之后执行。
        /// 使用小距离 BoxCast 向下探测。
        /// </summary>
        private void UpdateGroundCheck()
        {
            IsGrounded = false;
            GroundCollider = null;

            LayerMask mask = groundMask;
            if (!FallingThroughPlatform)
                mask |= oneWayPlatformMask;

            Bounds bounds = _collider.bounds;
            Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.01f);
            Vector2 size = new Vector2(bounds.size.x - 0.02f, 0.01f);

            RaycastHit2D hit = Physics2D.BoxCast(
                origin, size, 0f,
                Vector2.down, groundCheckDistance,
                mask);

            if (hit && hit.distance >= 0f)
            {
                IsGrounded = true;
                GroundCollider = hit.collider;
            }
        }

        // ══════════════════════════════════════════════
        //  公共 API
        // ══════════════════════════════════════════════

        /// <summary>开始穿透单向平台</summary>
        public void StartFallThroughPlatform()
        {
            FallingThroughPlatform = true;
            _fallThroughTimer = fallThroughDuration;
        }

        /// <summary>立即执行一次地面检测（用于初始化）</summary>
        public void ForceGroundCheck()
        {
            UpdateGroundCheck();
        }

        // ══════════════════════════════════════════════
        //  Debug
        // ══════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) return;

            Bounds bounds = col.bounds;

            // 地面检测范围
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Vector2 groundOrigin = new Vector2(bounds.center.x, bounds.min.y);
            Gizmos.DrawWireCube(
                groundOrigin + Vector2.down * groundCheckDistance * 0.5f,
                new Vector3(bounds.size.x - 0.02f, groundCheckDistance, 0f));
        }
#endif
    }
}
