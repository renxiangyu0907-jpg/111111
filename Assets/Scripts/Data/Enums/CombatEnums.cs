// ============================================================================
// CombatEnums.cs — 战斗系统枚举
// ============================================================================
namespace GhostVeil.Data
{
    /// <summary>
    /// 伤害类型标签；用于抗性计算与特效分派
    /// </summary>
    public enum DamageType
    {
        Physical,
        Fire,
        Psychic,    // 心理恐惧型伤害（与叙事主题契合）
        Environmental
    }

    /// <summary>
    /// 受击反应等级，决定是否打断动作
    /// </summary>
    public enum HitReaction
    {
        None,       // 霸体 / 无硬直
        Light,      // 轻微后退
        Heavy,      // 大硬直 + 弹飞
        Launch      // 击飞 / 特殊处决
    }
}
