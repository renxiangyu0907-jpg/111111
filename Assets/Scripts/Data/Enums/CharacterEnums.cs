// ============================================================================
// CharacterEnums.cs — 角色系统通用枚举
// ============================================================================
namespace GhostVeil.Data
{
    /// <summary>
    /// 角色朝向（纯 2D 横版只需左右）
    /// </summary>
    public enum FacingDirection
    {
        Right = 1,
        Left  = -1
    }

    /// <summary>
    /// 角色生命周期阶段，驱动全局逻辑（如 UI 灰显、存档锁定）
    /// </summary>
    public enum CharacterLifeState
    {
        Alive,
        Dying,      // 播放死亡动画中，尚未回收
        Dead,
        Intangible  // 叙事演出 / 灵魂状态，不参与碰撞但可移动
    }

    /// <summary>
    /// 可扩展的状态 ID；用整型而非 enum 允许后期 ScriptableObject 驱动
    /// 这里只列出最基本的内置 ID，业务层可自行追加
    /// </summary>
    public static class BuiltInStateID
    {
        public const int Idle       = 0;
        public const int Run        = 1;
        public const int Jump       = 2;
        public const int Fall       = 3;
        public const int WallSlide  = 4;
        public const int Dash       = 5;
        public const int Attack     = 10;
        public const int Hurt       = 20;
        public const int Death      = 21;
        public const int Interact   = 30;
        public const int Cutscene   = 100;  // 被叙事系统接管
    }
}
