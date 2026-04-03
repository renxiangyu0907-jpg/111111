// ============================================================================
// IAttackSource.cs — 攻击来源接口
// ============================================================================
using GhostVeil.Data;

namespace GhostVeil.Combat
{
    /// <summary>
    /// 标记一个攻击来源（武器 hitbox、子弹、陷阱等）。
    /// 碰撞检测逻辑通过此接口获取伤害数据包。
    /// </summary>
    public interface IAttackSource
    {
        /// <summary>构建当前帧的伤害数据包</summary>
        DamagePayload BuildDamagePayload();

        /// <summary>攻击命中后的回调（用于击中停顿、特效等）</summary>
        void OnHitConfirmed(IDamageable target, float actualDamage);
    }
}
