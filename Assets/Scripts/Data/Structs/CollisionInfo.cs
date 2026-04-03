// ============================================================================
// CollisionInfo.cs — 射线碰撞检测结果数据
// ============================================================================
using UnityEngine;

namespace GhostVeil.Data
{
    /// <summary>
    /// 每帧射线检测的聚合结果。
    /// 由 RaycastController 填充，状态机和移动逻辑读取。
    /// 使用 struct 避免 GC，按值传递。
    /// </summary>
    public struct CollisionInfo
    {
        // ── 四向碰撞标记 ──────────────────────────────
        public bool Above, Below, Left, Right;

        // ── 坡道数据 ────────────────────────────────
        /// <summary>当前站立面的法线（用于坡道判定）</summary>
        public Vector2 GroundNormal;

        /// <summary>当前站立面的坡度角（度）</summary>
        public float SlopeAngle;

        /// <summary>上一帧的坡度角（用于坡道过渡平滑）</summary>
        public float PreviousSlopeAngle;

        /// <summary>是否正在爬升坡道</summary>
        public bool ClimbingSlope;

        /// <summary>是否正在下降坡道</summary>
        public bool DescendingSlope;

        // ── 碰撞体引用 ──────────────────────────────
        /// <summary>脚下的 Collider 引用（用于移动平台 / 材质查询）</summary>
        public Collider2D GroundCollider;

        /// <summary>触墙的 Collider 引用（用于墙壁滑行判定）</summary>
        public Collider2D WallCollider;

        // ── 单向平台 ────────────────────────────────
        /// <summary>
        /// 当前帧是否正在执行"下落穿透"单向平台。
        /// 为 true 时垂直检测忽略 oneWayPlatform 层。
        /// 由外部在 Move 之前置位，在穿透完成后（不再与平台重叠）自动清除。
        /// </summary>
        public bool FallingThroughPlatform;

        // ── 移动方向记录（辅助坡道过渡） ────────────────
        /// <summary>本帧原始移动方向符号（-1 / 0 / 1），水平</summary>
        public int FaceDir;

        // ── 按位聚合的碰撞方向 ──────────────────────
        public CollisionSide Sides
        {
            get
            {
                var s = CollisionSide.None;
                if (Below) s |= CollisionSide.Bottom;
                if (Above) s |= CollisionSide.Top;
                if (Left)  s |= CollisionSide.Left;
                if (Right) s |= CollisionSide.Right;
                return s;
            }
        }

        public void Reset()
        {
            PreviousSlopeAngle = SlopeAngle;
            Above = Below = Left = Right = false;
            ClimbingSlope = DescendingSlope = false;
            SlopeAngle = 0f;
            GroundNormal = Vector2.up;
            GroundCollider = null;
            WallCollider = null;
            // 注意：FallingThroughPlatform 不在 Reset 中清除，
            // 它由外部逻辑（计时器/重叠检测）控制生命周期。
        }
    }
}
