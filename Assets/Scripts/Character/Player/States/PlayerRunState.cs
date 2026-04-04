// ============================================================================
// PlayerRunState.cs — 跑动状态
// ============================================================================
//
//  进入条件：角色着地 且 有水平输入
//  退出条件：
//    → Idle  ：水平输入归零
//    → Jump  ：满足起跳条件
//    → Fall  ：离开地面
//
//  逻辑：
//    · 水平速度平滑趋近 input.x * maxRunSpeed（加速阻尼）
//    · 翻转角色朝向
//    · 持续检测跳跃缓冲
//    · 处理"下+跳"穿透
//
using GhostVeil.Core.StateMachine;
using GhostVeil.Data;

namespace GhostVeil.Character.Player
{
    public class PlayerRunState : BaseState<PlayerController>
    {
        public override int StateID => BuiltInStateID.Run;

        // ── 进入 ──────────────────────────────────────
        public override void Enter(PlayerController ctx)
        {
            // 播放 Run 动画（循环）
            ctx.Animator?.PlayAnim("animation", true);
        }

        // ── 每帧逻辑 ──────────────────────────────────
        public override void LogicUpdate(PlayerController ctx, float deltaTime)
        {
            // 水平速度平滑趋近目标值
            float targetSpeed = ctx.Input.HorizontalInput * ctx.MoveData.maxRunSpeed;
            ctx.ApplyHorizontalSmoothing(targetSpeed);

            // 更新朝向
            ctx.UpdateFacing();
        }

        // ── 状态转移判定 ──────────────────────────────
        public override int CheckTransitions(PlayerController ctx)
        {
            // ── 优先级 1：跳跃 ──────────────────────────
            if (ctx.ShouldJump())
            {
                // "下+跳" → 穿透单向平台
                if (ctx.Input.DownHeld && ctx.RaycastCtrl != null)
                {
                    ctx.RaycastCtrl.StartFallThroughPlatform();
                    return BuiltInStateID.Fall;
                }

                ctx.ExecuteJump();
                return BuiltInStateID.Jump;
            }

            // ── 优先级 2：离开地面 ──────────────────────
            if (!ctx.IsGrounded)
                return BuiltInStateID.Fall;

            // ── 优先级 3：无水平输入 → 待机 ────────────
            if (UnityEngine.Mathf.Abs(ctx.Input.HorizontalInput) < 0.01f)
                return BuiltInStateID.Idle;

            return StateID;
        }
    }
}
