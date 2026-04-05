// ============================================================================
// PlayerPhysicsController.cs — 基于 BoxCast 的玩家物理控制器
// ============================================================================
//
// 核心设计：
//   · Rigidbody2D (Kinematic) + BoxCollider2D 作为角色碰撞体
//   · 使用 Physics2D.BoxCast 检测碰撞，手动计算最终位置
//   · 使用 transform.position 直接设置位置
//   · 水平/垂直 BoxCast 各自使用内缩的检测盒，互不干扰
//
// 关键防坑点：
//   · 水平 BoxCast 的检测盒上下各内缩 skinWidth，避免蹭到地面/天花板
//   · 垂直 BoxCast 的检测盒左右各内缩 skinWidth，避免蹭到墙壁
//   · 着地判定用独立的薄盒子从底部向下探测
//   · hit.distance==0 时（起点已重叠）允许位移=0，不向该方向移动
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
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private LayerMask oneWayPlatformMask;

        [Header("=== Detection ===")]
        [SerializeField] private float groundCheckDistance = 0.05f;
        [SerializeField] private float skinWidth = 0.02f;

        [Header("=== One-Way Platform ===")]
        [SerializeField] private float fallThroughDuration = 0.15f;

        // ══════════════════════════════════════════════
        //  运行时
        // ══════════════════════════════════════════════

        private Rigidbody2D _rb;
        private BoxCollider2D _collider;

        public bool IsGrounded { get; private set; }
        public bool HitCeiling { get; private set; }
        public bool HitLeft { get; private set; }
        public bool HitRight { get; private set; }
        public bool FallingThroughPlatform { get; private set; }
        public Collider2D GroundCollider { get; private set; }
        public Collider2D WallCollider { get; private set; }
        public int FaceDir { get; set; } = 1;

        private float _fallThroughTimer;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<BoxCollider2D>();

            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.useFullKinematicContacts = false;
            _rb.interpolation = RigidbodyInterpolation2D.None;
            _collider.isTrigger = false;
        }

        private void Update()
        {
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
        //  核心移动
        // ══════════════════════════════════════════════

        public Vector2 Move(Vector2 velocity, float deltaTime)
        {
            float moveX = velocity.x * deltaTime;
            float moveY = velocity.y * deltaTime;

            if (moveX > 0.001f) FaceDir = 1;
            else if (moveX < -0.001f) FaceDir = -1;

            Vector2 currentPos = (Vector2)transform.position;

            // ── 缓存碰撞体世界尺寸 ──
            Vector2 scale = (Vector2)transform.lossyScale;
            Vector2 colWorldSize = _collider.size * scale;
            Vector2 colWorldOffset = _collider.offset * scale;
            Vector2 colCenter = currentPos + colWorldOffset;

            // ══════════════════════════════════════════════
            //  水平碰撞
            // ══════════════════════════════════════════════

            HitLeft = false;
            HitRight = false;
            WallCollider = null;

            if (Mathf.Abs(moveX) > 0.0001f)
            {
                float dirX = Mathf.Sign(moveX);
                float distX = Mathf.Abs(moveX);

                // ── 水平检测盒：上下各内缩 skinWidth，避免蹭到地面/天花板 ──
                // origin 从碰撞体中心沿移动方向偏移到边缘
                float halfW = colWorldSize.x * 0.5f;
                float castH = Mathf.Max(colWorldSize.y - skinWidth * 2f, 0.01f);
                Vector2 castSize = new Vector2(skinWidth, castH);
                Vector2 castOrigin = new Vector2(
                    colCenter.x + dirX * (halfW - skinWidth * 0.5f),
                    colCenter.y);

                RaycastHit2D hit = Physics2D.BoxCast(
                    castOrigin, castSize, 0f,
                    new Vector2(dirX, 0f),
                    distX + skinWidth,
                    groundMask);

                if (hit.collider != null)
                {
                    float maxAllowed = Mathf.Max(0f, hit.distance - skinWidth);
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

            currentPos.x += moveX;
            // 更新 colCenter（水平移动后）
            colCenter = currentPos + colWorldOffset;

            // ══════════════════════════════════════════════
            //  垂直碰撞
            // ══════════════════════════════════════════════

            HitCeiling = false;

            if (Mathf.Abs(moveY) > 0.0001f)
            {
                float dirY = Mathf.Sign(moveY);
                float distY = Mathf.Abs(moveY);

                LayerMask mask = groundMask;
                if (dirY < 0f && !FallingThroughPlatform)
                    mask |= oneWayPlatformMask;

                // ── 垂直检测盒：左右各内缩 skinWidth，避免蹭到墙壁 ──
                // origin 从碰撞体中心沿移动方向偏移到边缘
                float halfH = colWorldSize.y * 0.5f;
                float castW = Mathf.Max(colWorldSize.x - skinWidth * 2f, 0.01f);
                Vector2 castSize = new Vector2(castW, skinWidth);
                Vector2 castOrigin = new Vector2(
                    colCenter.x,
                    colCenter.y + dirY * (halfH - skinWidth * 0.5f));

                RaycastHit2D hit = Physics2D.BoxCast(
                    castOrigin, castSize, 0f,
                    new Vector2(0f, dirY),
                    distY + skinWidth,
                    mask);

                if (hit.collider != null)
                {
                    float maxAllowed = Mathf.Max(0f, hit.distance - skinWidth);
                    if (maxAllowed < distY)
                    {
                        distY = maxAllowed;
                        velocity.y = 0f;
                        if (dirY > 0f) HitCeiling = true;
                    }
                }

                moveY = distY * dirY;
            }

            currentPos.y += moveY;

            // ── 一次性写入最终位置 ──
            transform.position = new Vector3(currentPos.x, currentPos.y, transform.position.z);

            // ── 独立着地检测 ──
            UpdateGroundCheck();

            return velocity;
        }

        // ══════════════════════════════════════════════
        //  着地检测
        // ══════════════════════════════════════════════

        private void UpdateGroundCheck()
        {
            IsGrounded = false;
            GroundCollider = null;

            LayerMask mask = groundMask;
            if (!FallingThroughPlatform)
                mask |= oneWayPlatformMask;

            Vector2 scale = (Vector2)transform.lossyScale;
            Vector2 colWorldSize = _collider.size * scale;
            Vector2 colCenter = (Vector2)transform.position + _collider.offset * scale;

            // 从碰撞体正下方做一个薄盒向下探测
            float castW = Mathf.Max(colWorldSize.x - skinWidth * 2f, 0.01f);
            Vector2 boxSize = new Vector2(castW, skinWidth);
            Vector2 origin = new Vector2(colCenter.x, colCenter.y - colWorldSize.y * 0.5f + skinWidth * 0.5f);

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

        public void StartFallThroughPlatform()
        {
            FallingThroughPlatform = true;
            _fallThroughTimer = fallThroughDuration;
        }

        public void ForceGroundCheck()
        {
            UpdateGroundCheck();
        }

        // ══════════════════════════════════════════════
        //  Debug Gizmos
        // ══════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) return;

            Vector2 scale = (Vector2)transform.lossyScale;
            Vector2 sz = col.size * scale;
            Vector2 center = (Vector2)transform.position + col.offset * scale;

            // 着地检测区域
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            float castW = Mathf.Max(sz.x - skinWidth * 2f, 0.01f);
            Vector2 btm = new Vector2(center.x, center.y - sz.y * 0.5f);
            Gizmos.DrawWireCube(
                btm + Vector2.down * groundCheckDistance * 0.5f,
                new Vector3(castW, groundCheckDistance, 0f));

            // 水平检测区域（左右各一个薄条）
            Gizmos.color = Color.cyan;
            float castH = Mathf.Max(sz.y - skinWidth * 2f, 0.01f);
            // 右
            Gizmos.DrawWireCube(
                new Vector2(center.x + sz.x * 0.5f + skinWidth, center.y),
                new Vector3(skinWidth * 2f, castH, 0f));
            // 左
            Gizmos.DrawWireCube(
                new Vector2(center.x - sz.x * 0.5f - skinWidth, center.y),
                new Vector3(skinWidth * 2f, castH, 0f));
        }
#endif
    }
}
