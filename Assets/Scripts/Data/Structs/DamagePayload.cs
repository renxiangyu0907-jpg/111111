// ============================================================================
// DamagePayload.cs — 伤害数据包
// ============================================================================
using UnityEngine;

namespace GhostVeil.Data
{
    /// <summary>
    /// 不可变伤害数据包，从攻击源传递到 IDamageable。
    /// 使用 readonly struct 保证线程安全 + 零 GC。
    /// </summary>
    public readonly struct DamagePayload
    {
        /// <summary>基础伤害值</summary>
        public readonly float Amount;

        /// <summary>伤害类型</summary>
        public readonly DamageType Type;

        /// <summary>击退方向（归一化）</summary>
        public readonly Vector2 KnockbackDirection;

        /// <summary>击退力度</summary>
        public readonly float KnockbackForce;

        /// <summary>受击反应等级</summary>
        public readonly HitReaction Reaction;

        /// <summary>攻击来源 Transform（用于方向判定、仇恨溯源）</summary>
        public readonly Transform Source;

        public DamagePayload(
            float amount,
            DamageType type,
            Vector2 knockbackDirection,
            float knockbackForce,
            HitReaction reaction,
            Transform source)
        {
            Amount             = amount;
            Type               = type;
            KnockbackDirection = knockbackDirection.normalized;
            KnockbackForce     = knockbackForce;
            Reaction           = reaction;
            Source              = source;
        }
    }
}
