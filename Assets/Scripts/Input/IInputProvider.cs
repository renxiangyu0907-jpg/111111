// ============================================================================
// IInputProvider.cs — 输入抽象层接口（v2 — 增加帧轮询标记）
// ============================================================================
using System;
using UnityEngine;

namespace GhostVeil.Input
{
    /// <summary>
    /// 输入源的统一抽象。
    /// 
    /// 设计原则：
    ///   1. 双通道暴露 —— 同时提供「事件回调」与「帧轮询布尔标记」。
    ///      · 事件回调：适合 UI / 音效等"触发即忘"的消费者。
    ///      · 布尔标记：适合状态机在 LogicUpdate 里主动查询。
    ///   2. 帧标记语义 —— JumpPressed 等布尔值的含义是
    ///      "本帧刚刚按下"（即 Input System 的 performed 回调触发帧），
    ///      由实现类在每帧末尾自动清零。
    ///   3. 职责单一 —— 此接口只暴露原始输入数据，
    ///      不做跳跃缓冲、浮空土狼时间等游戏逻辑。
    ///   4. 可替换 —— 实现类可以是真实输入、AI 输入、回放输入或网络同步输入。
    /// </summary>
    public interface IInputProvider
    {
        // ═══════════════════════════════════════════════
        //  轴 / 向量（持续量，每帧读取）
        // ═══════════════════════════════════════════════

        /// <summary>水平移动输入 [-1, 1]</summary>
        float HorizontalInput { get; }

        /// <summary>垂直输入（爬梯 / 菜单导航 / 下蹲判定）[-1, 1]</summary>
        float VerticalInput { get; }

        /// <summary>归一化二维移动向量</summary>
        Vector2 MoveVector { get; }

        // ═══════════════════════════════════════════════
        //  帧标记（脉冲型布尔，仅触发帧为 true，下帧自动清零）
        // ═══════════════════════════════════════════════

        /// <summary>本帧是否刚刚按下跳跃键（用于触发跳跃）</summary>
        bool JumpPressed { get; }

        /// <summary>本帧是否刚刚松开跳跃键（用于小跳/中断上升）</summary>
        bool JumpReleased { get; }

        /// <summary>跳跃键是否正在被持续按住（用于长按浮空等判定）</summary>
        bool JumpHeld { get; }

        /// <summary>本帧是否刚刚按下攻击键</summary>
        bool AttackPressed { get; }

        /// <summary>攻击键是否正在被持续按住（用于长按射击）</summary>
        bool AttackHeld { get; }

        /// <summary>本帧是否刚刚按下冲刺键</summary>
        bool DashPressed { get; }

        /// <summary>本帧是否刚刚按下交互键</summary>
        bool InteractPressed { get; }

        /// <summary>
        /// 垂直轴是否正在向下按住（用于"下+跳"穿透单向平台）。
        /// 阈值判定在实现类内部完成（如 VerticalInput &lt; -0.5f）。
        /// </summary>
        bool DownHeld { get; }

        // ═══════════════════════════════════════════════
        //  事件回调（触发型，适合 UI / 音效等即发即忘的订阅者）
        // ═══════════════════════════════════════════════

        event Action OnJumpPressed;
        event Action OnJumpReleased;
        event Action OnAttackPressed;
        event Action OnDashPressed;
        event Action OnInteractPressed;
        event Action OnPausePressed;

        // ═══════════════════════════════════════════════
        //  输入锁定（叙事系统控制权交接）
        // ═══════════════════════════════════════════════

        /// <summary>
        /// 全局输入锁。
        /// 为 true 时：
        ///   · 所有轴归零
        ///   · 所有帧标记为 false
        ///   · 所有事件不触发
        /// 叙事系统 / 过场动画通过此属性冻结玩家输入，
        /// 无需销毁或禁用 InputProvider 组件。
        /// </summary>
        bool InputLocked { get; set; }
    }
}
