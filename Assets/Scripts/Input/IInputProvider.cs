// ============================================================================
// IInputProvider.cs — 输入抽象层接口
// ============================================================================
using System;
using UnityEngine;

namespace GhostVeil.Input
{
    /// <summary>
    /// 输入源的抽象。
    /// 底层由 New Input System 的 PlayerInput / InputAction 实现，
    /// 但角色控制器只依赖此接口 —— 方便做回放、AI 接管、叙事强制输入。
    /// </summary>
    public interface IInputProvider
    {
        // ── 移动 ──────────────────────────────────────
        /// <summary>水平移动输入 [-1, 1]</summary>
        float HorizontalInput { get; }

        /// <summary>垂直输入（爬梯 / 菜单导航）[-1, 1]</summary>
        float VerticalInput { get; }

        /// <summary>归一化移动向量</summary>
        Vector2 MoveVector { get; }

        // ── 动作按键（事件驱动） ──────────────────────
        event Action OnJumpPressed;
        event Action OnJumpReleased;
        event Action OnAttackPressed;
        event Action OnDashPressed;
        event Action OnInteractPressed;

        // ── 特殊 ────────────────────────────────────
        event Action OnPausePressed;

        /// <summary>
        /// 全局输入锁：为 true 时所有输入归零 / 不触发事件。
        /// 叙事系统通过此属性在不销毁 InputProvider 的前提下冻结玩家输入。
        /// </summary>
        bool InputLocked { get; set; }
    }
}
