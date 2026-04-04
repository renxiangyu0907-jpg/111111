// ============================================================================
// PlayerFallState.cs — 下落状态
// ============================================================================
//
//  进入条件：
//    · 从 JumpState 到达顶点（Velocity.y <= 0）
//    · 从 Idle / Run 走出平台边缘（!IsGrounded）
//    · 从 Idle / Run 执行"下+跳"穿透单向平台
//
//  退出条件：
//    → Idle  ：着地 且 无水平输入
//    → Run   ：着地 且 有水平输入
//    → Jump  ：在郊狼时间窗口内 或 有跳跃缓冲 且 满足起跳条件
//
//  逻辑：
//    · 空中水平操控
//    · 郊狼时间起跳（从走出边缘进入 Fall 的情况）
//    · 跳跃缓冲检测（快落地时按的跳跃键）
//    · 重力 + 最大下落速度由 PlayerController 全局处理
//
//  郊狼时间详解：
//    · 只有从地面状态"自然掉落"进入 Fall 时，CoyoteTimer 才有残余值
//    · 从 JumpState 转入 Fall 时，CoyoteTimer 已在 JumpState.Enter 中清零
//    · 这确保了：跳跃后不会二段跳，但走出边缘后可以补跳
//
using GhostVeil.Core.StateMachine;
using GhostVeil.Data;

namespace GhostVeil.Character.Player
{
    public class PlayerFallState : BaseState<PlayerController>
    {
        public override int StateID => BuiltInStateID.Fall;

        // ── 进入 ──────────────────────────────────────
        public override void Enter(PlayerController ctx)
        {
            // 如果是从地面状态自然掉落进来的（不是跳跃），
            // CoyoteTimer 仍有残余值，可以在窗口内起跳。
            // 如果是从 JumpState 转入的，CoyoteTimer 已经是 0 了。

            // 播放下落动画（不循环，播完保持最后一帧）
            ctx.Animator?.PlayAnim("animation", false);
        }

        // ── 每帧逻辑 ──────────────────────────────────
        public override void LogicUpdate(PlayerController ctx, float deltaTime)
        {
            // ── 空中水平操控 ────────────────────────────
            float targetSpeed = ctx.Input.HorizontalInput * ctx.MoveData.maxRunSpeed;
            ctx.ApplyHorizontalSmoothing(targetSpeed);
            ctx.UpdateFacing();

            // ── 跳跃缓冲记录 ────────────────────────────
            // 跳跃缓冲的计时器更新在 PlayerController.OnLogicUpdate 中统一处理，
            // 这里不需要额外操作。状态只需在 CheckTransitions 中检查即可。
        }

        // ── 状态转移判定 ──────────────────────────────
        public override int CheckTransitions(PlayerController ctx)
        {
            // ── 优先级 1：郊狼时间 / 跳跃缓冲起跳 ───────
            //
            //  场景 A（郊狼时间）：
            //    玩家走出平台 → 进入 Fall → CoyoteTimer > 0 → 按跳跃 → 起跳
            //
            //  场景 B（跳跃缓冲）：
            //    玩家在空中按了跳跃（JumpBufferTimer > 0）→ 着地 → 立即起跳
            //    这个情况下 ShouldJump() = HasJumpBuffer && (IsGrounded || InCoyoteTime)
            //    着地帧 IsGrounded 为 true → 满足
            //
            if (ctx.ShouldJump())
            {
                ctx.ExecuteJump();
                return BuiltInStateID.Jump;
            }

            // ── 优先级 2：着地 ──────────────────────────
            if (ctx.IsGrounded)
            {
                // 着地瞬间检查：是否有水平输入？
                if (UnityEngine.Mathf.Abs(ctx.Input.HorizontalInput) > 0.01f)
                    return BuiltInStateID.Run;
                else
                    return BuiltInStateID.Idle;
            }

            return StateID;
        }
    }
}
