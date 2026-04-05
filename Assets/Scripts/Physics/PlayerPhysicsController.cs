// ============================================================================
// PlayerPhysicsController.cs — 基于 Rigidbody2D 的玩家物理控制器
// ============================================================================
// 替代原来的射线碰撞系统，使用 Unity 内置物理引擎。
//
// 核心设计：
//   · Rigidbody2D (Kinematic) + BoxCollider2D 作为角色碰撞体
//   · 使用 Physics2D.BoxCast 检测碰撞，手动计算最终位置
//   · 使用 transform.position 直接设置位置（避免 MovePosition 的延迟问题）
//   · 不使用 OnCollision / OnTrigger 回调，全部通过主动查询
//
// 重要：
//   · 水平和垂直移动合并为一次位置更新
//   · BoxCast 使用碰撞体 bounds 减去 skinWidth 避免贴墙卡住
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

        [Header("=== Detection Distances ===")]
        [Tooltip("地面检测向下延伸距离")]
        [SerializeField] private float groundCheckDistance = 0.05f;

        [Tooltip("墙壁检测水平延伸距离")]
        [SerializeField] private float wallCheckDistance = 0.05f;

        [Tooltip("碰撞体内缩距离（防止贴墙/贴地卡住）")]
        [SerializeField] private float skinWidth = 0.015f;

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

            // Kinematic 模式：不受力/重力影响
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.useFullKinematicContacts = false;
            _rb.interpolation = RigidbodyInterpolation2D.None;

            // BoxCollider2D 不设为 Trigger —— 我们不依赖 Unity 碰撞响应，
            // 但也不需要它产生碰撞响应（Kinematic 模式下不会产生物理力）
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
        /// 
        /// 关键设计：水平和垂直碰撞检测分开做，
        /// 但最终位置在一次 transform.position 赋值中完成，
        /// 避免 MovePosition 的延迟覆盖问题。
        /// </summary>
        /// <param name="velocity">当前速度</param>
        /// <param name="deltaTime">时间步长</param>
        /// <returns>碰撞修正后的速度</returns>
        public Vector2 Move(Vector2 velocity, float deltaTime)
        {
            // 计算本帧位移
            float moveX = velocity.x * deltaTime;
            float moveY = velocity.y * deltaTime;

            // 记录朝向
            if (moveX > 0.001f) FaceDir = 1;
            else if (moveX < -0.001f) FaceDir = -1;

            // 当前位置（从 transform 读取，确保是最新的）
            Vector2 currentPos = transform.position;

            // ── 水平碰撞检测 ──
            HitLeft = false;
            HitRight = false;
            WallCollider = null;

            if (Mathf.Abs(moveX) > 0.0001f)
            {
                float dirX = Mathf.Sign(moveX);
                float distX = Mathf.Abs(moveX);

                // 使用当前碰撞体的 bounds（考虑 offset 和 size）
                Vector2 castSize = GetCastSize(horizontal: true);
                Vector2 castOrigin = currentPos + _collider.offset;

                RaycastHit2D hit = Physics2D.BoxCast(
                    castOrigin, castSize, 0f,
                    new Vector2(dirX, 0f),
                    distX + skinWidth,
                    groundMask);

                if (hit.collider != null)
                {
                    // hit.distance == 0 表示起点已在碰撞体内部，不能再往该方向移动
                    float maxAllowed = (hit.distance <= 0f)
                        ? 0f
                        : Mathf.Max(0f, hit.distance - skinWidth);

                    if (maxAllowed < distX)
                    {
                        distX = maxAllowed;
                        velocity.x = 0f;

                        if (dirX < 0f) HitLeft = true;
                        else HitRight = true;
                        WallCollider = hit.collider;
                    }
                }

                moveX = distX * dirX;
            }

            // 更新临时位置（水平移动后）
            currentPos.x += moveX;

            // ── 垂直碰撞检测 ──
            HitCeiling = false;

            if (Mathf.Abs(moveY) > 0.0001f)
            {
                float dirY = Mathf.Sign(moveY);
                float distY = Mathf.Abs(moveY);

                // 构建碰撞掩码（向下时包含单向平台）
                LayerMask mask = groundMask;
                if (dirY < 0f && !FallingThroughPlatform)
                    mask |= oneWayPlatformMask;

                // 使用更新后的水平位置来做垂直检测
                Vector2 castSize = GetCastSize(horizontal: false);
                Vector2 castOrigin = currentPos + _collider.offset;

                RaycastHit2D hit = Physics2D.BoxCast(
                    castOrigin, castSize, 0f,
                    new Vector2(0f, dirY),
                    distY + skinWidth,
                    mask);

                if (hit.collider != null)
                {
                    // hit.distance == 0 表示起点已在碰撞体内部（紧贴地面），不能继续移动
                    float maxAllowed = (hit.distance <= 0f)
                        ? 0f
                        : Mathf.Max(0f, hit.distance - skinWidth);

                    if (maxAllowed < distY)
                    {
                        distY = maxAllowed;
                        velocity.y = 0f;

                        if (dirY > 0f) HitCeiling = true;
                    }
                }

                moveY = distY * dirY;
            }

            // 更新临时位置（垂直移动后）
            currentPos.y += moveY;

            // ── 一次性设置最终位置 ──
            transform.position = new Vector3(currentPos.x, currentPos.y, transform.position.z);

            // ── 独立地面检测 ──
            UpdateGroundCheck();

            return velocity;
        }

        // ══════════════════════════════════════════════
        //  碰撞检测辅助
        // ══════════════════════════════════════════════

        /// <summary>
        /// 获取 BoxCast 使用的尺寸（比碰撞体略小，避免边角误判）。
        /// horizontal=true 时水平方向保持完整，垂直方向缩小；
        /// horizontal=false 时反之。
        /// </summary>
        private Vector2 GetCastSize(bool horizontal)
        {
            Vector2 size = _collider.size * (Vector2)transform.lossyScale;
            float shrink = skinWidth * 2f;

            if (horizontal)
            {
                // 水平检测时垂直方向缩小，避免地面/天花板边角误判
                size.y = Mathf.Max(size.y - shrink, 0.01f);
            }
            else
            {
                // 垂直检测时水平方向缩小，避免墙壁边角误判
                size.x = Mathf.Max(size.x - shrink, 0.01f);
            }

            return size;
        }

        // ══════════════════════════════════════════════
        //  地面检测
        // ══════════════════════════════════════════════

        /// <summary>
        /// 独立的地面检测，在移动之后执行。
        /// 从碰撞体底部向下做短距离 BoxCast。
        /// </summary>
        private void UpdateGroundCheck()
        {
            IsGrounded = false;
            GroundCollider = null;

            LayerMask mask = groundMask;
            if (!FallingThroughPlatform)
                mask |= oneWayPlatformMask;

            // 从碰撞体底部中心开始检测
            Vector2 scale = (Vector2)transform.lossyScale;
            Vector2 colSize = _collider.size * scale;
            Vector2 colCenter = (Vector2)transform.position + _collider.offset * scale;
            Vector2 origin = new Vector2(colCenter.x, colCenter.y - colSize.y * 0.5f + 0.01f);
            Vector2 boxSize = new Vector2(colSize.x - skinWidth * 2f, 0.02f);

            RaycastHit2D hit = Physics2D.BoxCast(
                origin, boxSize, 0f,
                Vector2.down, groundCheckDistance,
                mask);

            if (hit.collider != null)
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

            Vector2 scale = (Vector2)transform.lossyScale;
            Vector2 colSize = col.size * scale;
            Vector2 colCenter = (Vector2)transform.position + col.offset * scale;

            // 地面检测范围
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Vector2 groundOrigin = new Vector2(colCenter.x, colCenter.y - colSize.y * 0.5f);
            Gizmos.DrawWireCube(
                groundOrigin + Vector2.down * groundCheckDistance * 0.5f,
                new Vector3(colSize.x - skinWidth * 2f, groundCheckDistance, 0f));
        }
#endif
    }
}
