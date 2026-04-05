// PlayerFallState.cs — 下落状态
using GhostVeil.Core.StateMachine;
using GhostVeil.Data;

namespace GhostVeil.Character.Player
{
    public class PlayerFallState : BaseState<PlayerController>
    {
        public override int StateID => BuiltInStateID.Fall;

        public override void Enter(PlayerController ctx)
        {
            ctx.Animator?.PlayAnim(ctx.FallAnimName, false);
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
                ctx.ExecuteJump();
                return BuiltInStateID.Jump;
            }

            if (ctx.IsGrounded)
            {
                if (UnityEngine.Mathf.Abs(ctx.Input.HorizontalInput) > 0.01f)
                    return BuiltInStateID.Run;
                else
                    return BuiltInStateID.Idle;
            }

            return StateID;
        }
    }
}
