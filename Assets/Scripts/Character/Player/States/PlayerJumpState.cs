// ============================================================================
// PlayerJumpState.cs — 跳跃上升状态
// ============================================================================
//
//  进入条件：ExecuteJump() 已被调用（Velocity.y = JumpVelocity）
//  退出条件：
//    → Fall  ：Velocity.y <= 0（到达顶点，开始下落）
//    → Fall  ：撞到天花板（Collisions.Above = true）
//
//  逻辑：
//    · 空中水平操控（使用 airAccelTime / airDecelTime）
//    · 可变跳跃高度：监听 JumpReleased → 削减上升速度
//    · 重力由 PlayerController 全局处理，此状态不管重力
//
//  关键细节：
//    · JumpCut（松手削减）只执行一次（HasJumpCut 标记）
//    · 撞头时 Velocity.y 已被 ExecuteMovement 清零 → 下帧自然转入 Fall
//
using GhostVeil.Core.StateMachine;
using GhostVeil.Data;

namespace GhostVeil.Character.Player
{
    public class PlayerJumpState : BaseState<PlayerController>
    {
        public override int StateID => BuiltInStateID.Jump;

        // ── 进入 ──────────────────────────────────────
        public override void Enter(PlayerController ctx)
        {
            // 进入跳跃状态时，ExecuteJump() 已经在转移前被调用了，
            // Velocity.y 已经是 JumpVelocity。
            // 清除郊狼时间，防止在空中二次利用
            ctx.CoyoteTimer = 0f;

            // 播放跳跃上升动画（不循环，播完保持最后一帧）
            ctx.Animator?.PlayAnim("jump", false);
        }

        // ── 每帧逻辑 ──────────────────────────────────
        public override void LogicUpdate(PlayerController ctx, float deltaTime)
        {
            // ── 空中水平操控 ────────────────────────────
            float targetSpeed = ctx.Input.HorizontalInput * ctx.MoveData.maxRunSpeed;
            ctx.ApplyHorizontalSmoothing(targetSpeed);
            ctx.UpdateFacing();

            // ── 可变跳跃高度：松手削减 ────────────────
            //
            //  玩家在上升阶段松开跳跃键 → 立即将上升速度乘以 jumpCutMultiplier
            //  效果：轻按 = 小跳，长按 = 大跳
            //
            //  为什么检查 JumpReleased 而不是 !JumpHeld？
            //    · JumpReleased 是脉冲（只在松开那一帧为 true），精确到帧
            //    · !JumpHeld 是持续状态，会在松手后的每一帧都为 true，
            //      如果用它来做削减会导致多次削减
            //    · HasJumpCut 是双重保险，但 JumpReleased 的脉冲性才是正确性保障
            //
            if (ctx.Input.JumpReleased && ctx.Velocity.y > 0f)
            {
                ctx.ExecuteJumpCut();
            }
        }

        // ── 状态转移判定 ──────────────────────────────
        public override int CheckTransitions(PlayerController ctx)
        {
            // ── 上升速度 <= 0 → 到达顶点，切入下落 ─────
            if (ctx.Velocity.y <= 0f)
                return BuiltInStateID.Fall;

            // ── 撞到天花板 → 直接下落 ──────────────────
            // （ExecuteMovement 已经把 Velocity.y 清零了，
            //   但在同一帧内 CheckTransitions 先于下一次 ApplyGravity，
            //   所以这里用 Collisions.Above 做显式判断更安全）
            if (ctx.RaycastCtrl != null && ctx.RaycastCtrl.Collisions.Above)
                return BuiltInStateID.Fall;

            return StateID;
        }
    }
}
