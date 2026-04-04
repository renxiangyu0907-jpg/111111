// ============================================================================
// PlayerIdleState.cs — 待机状态
// ============================================================================
//
//  进入条件：角色着地 且 无水平输入
//  退出条件：
//    → Run   ：有水平输入
//    → Jump  ：满足起跳条件（含跳跃缓冲）
//    → Fall  ：离开地面（如被推下平台、移动平台下降等）
//
//  逻辑：
//    · 水平速度向 0 平滑衰减（减速阻尼）
//    · 持续检测跳跃缓冲
//    · 处理"下+跳"单向平台穿透
//
using GhostVeil.Core.StateMachine;
using GhostVeil.Data;

namespace GhostVeil.Character.Player
{
    public class PlayerIdleState : BaseState<PlayerController>
    {
        public override int StateID => BuiltInStateID.Idle;

        // ── 进入 ──────────────────────────────────────
        public override void Enter(PlayerController ctx)
        {
            // 刚着地时可能还残留微小水平速度，由 LogicUpdate 的平滑减速自然归零
            // 播放 Idle 动画（循环）—— Animator 为 null 时安全跳过
            ctx.Animator?.PlayAnim(ctx.IdleAnimName, true);
        }

        // ── 每帧逻辑 ──────────────────────────────────
        public override void LogicUpdate(PlayerController ctx, float deltaTime)
        {
            // 水平速度平滑衰减到 0（无输入时 targetSpeed = 0）
            float targetSpeed = ctx.Input.HorizontalInput * ctx.MoveData.maxRunSpeed;
            ctx.ApplyHorizontalSmoothing(targetSpeed);

            // 更新朝向（即使在 Idle 状态，按方向键也应该转身）
            ctx.UpdateFacing();
        }

        // ── 状态转移判定 ──────────────────────────────
        public override int CheckTransitions(PlayerController ctx)
        {
            // ── 优先级 1：跳跃（含跳跃缓冲） ────────────
            if (ctx.ShouldJump())
            {
                // 检查"下+跳" → 单向平台穿透
                if (ctx.Input.DownHeld && ctx.RaycastCtrl != null)
                {
                    ctx.RaycastCtrl.StartFallThroughPlatform();
                    return BuiltInStateID.Fall; // 直接进入下落，不起跳
                }

                ctx.ExecuteJump();
                return BuiltInStateID.Jump;
            }

            // ── 优先级 2：离开地面（被外力推落等） ───────
            if (!ctx.IsGrounded)
                return BuiltInStateID.Fall;

            // ── 优先级 3：有水平输入 → 跑动 ────────────
            if (UnityEngine.Mathf.Abs(ctx.Input.HorizontalInput) > 0.01f)
                return BuiltInStateID.Run;

            // ── 保持当前状态 ────────────────────────────
            return StateID;
        }
    }
}
