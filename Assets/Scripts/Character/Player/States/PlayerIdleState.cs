// PlayerIdleState.cs — 待机状态
using GhostVeil.Core.StateMachine;
using GhostVeil.Data;

namespace GhostVeil.Character.Player
{
    public class PlayerIdleState : BaseState<PlayerController>
    {
        public override int StateID => BuiltInStateID.Idle;

        public override void Enter(PlayerController ctx)
        {
            ctx.Animator?.PlayAnim(ctx.IdleAnimName, true);
        }

        public override void LogicUpdate(PlayerController ctx, float deltaTime)
        {
            float targetSpeed = ctx.Input.HorizontalInput * ctx.MoveData.maxRunSpeed;
            ctx.ApplyHorizontalSmoothing(targetSpeed);
            ctx.UpdateFacing();
        }

        public override int CheckTransitions(PlayerController ctx)
        {
            if (ctx.ShouldJump())
            {
                if (ctx.Input.DownHeld && ctx.Physics != null)
                {
                    ctx.Physics.StartFallThroughPlatform();
                    return BuiltInStateID.Fall;
                }

                ctx.ExecuteJump();
                return BuiltInStateID.Jump;
            }

            if (!ctx.IsGrounded)
                return BuiltInStateID.Fall;

            if (UnityEngine.Mathf.Abs(ctx.Input.HorizontalInput) > 0.01f)
                return BuiltInStateID.Run;

            return StateID;
        }
    }
}
