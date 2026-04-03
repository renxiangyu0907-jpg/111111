// ============================================================================
// PhysicsEnums.cs — 物理 / 射线控制器枚举
// ============================================================================
namespace GhostVeil.Data
{
    /// <summary>
    /// 碰撞检测面（射线组）
    /// </summary>
    [System.Flags]
    public enum CollisionSide
    {
        None   = 0,
        Bottom = 1 << 0,
        Top    = 1 << 1,
        Left   = 1 << 2,
        Right  = 1 << 3
    }
}
