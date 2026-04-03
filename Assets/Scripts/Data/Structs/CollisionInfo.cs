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
        public bool Above, Below, Left, Right;

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

        /// <summary>脚下的 Collider 引用（用于移动平台 / 材质查询）</summary>
        public Collider2D GroundCollider;

        /// <summary>触墙的 Collider 引用（用于墙壁滑行判定）</summary>
        public Collider2D WallCollider;

        /// <summary>按位聚合的碰撞方向</summary>
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
        }
    }
}
