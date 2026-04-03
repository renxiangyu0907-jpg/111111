// ============================================================================
// IDamageable.cs — 可受伤接口
// ============================================================================
using GhostVeil.Data;

namespace GhostVeil.Combat
{
    /// <summary>
    /// 任何可受伤实体（玩家、敌人、可破坏物）实现此接口。
    /// 攻击源只需拿到 IDamageable 引用即可施加伤害，解耦类型依赖。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>当前是否可以受伤（无敌帧 / 叙事保护时返回 false）</summary>
        bool CanBeDamaged { get; }

        /// <summary>接收伤害并返回实际造成的伤害值（被抗性 / 护盾削减后的）</summary>
        float TakeDamage(DamagePayload payload);
    }
}
