// ============================================================================
// INarrativeController.cs — 叙事系统控制器接口
// ============================================================================
using System;
using GhostVeil.Data;

namespace GhostVeil.Narrative
{
    /// <summary>
    /// 叙事 / 剧情系统的顶层控制器接口。
    /// 管理"控制权"的申请 / 释放，是角色控制与叙事演出之间的仲裁者。
    /// </summary>
    public interface INarrativeController
    {
        /// <summary>当前叙事控制权级别</summary>
        NarrativeAuthorityLevel CurrentAuthority { get; }

        /// <summary>
        /// 请求接管控制权。
        /// 若请求的级别 >= 当前级别则立即生效并返回 true。
        /// 叙事演出、对话系统、QTE 等通过此方法夺取玩家控制权。
        /// </summary>
        bool RequestAuthority(NarrativeAuthorityLevel level, string sequenceID);

        /// <summary>释放控制权，恢复到 None 或更低级别</summary>
        void ReleaseAuthority(string sequenceID);

        /// <summary>控制权变更时触发</summary>
        event Action<NarrativeAuthorityLevel /*old*/, NarrativeAuthorityLevel /*new*/> OnAuthorityChanged;

        /// <summary>当前是否允许玩家移动输入</summary>
        bool IsPlayerMovementAllowed { get; }

        /// <summary>当前是否允许玩家战斗输入</summary>
        bool IsPlayerCombatAllowed { get; }
    }
}
