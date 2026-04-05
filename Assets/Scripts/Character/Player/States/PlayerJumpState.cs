// PlayerJumpState.cs — 跳跃上升状态
using GhostVeil.Core.StateMachine;
using GhostVeil.Data;

namespace GhostVeil.Character.Player
{
    public class PlayerJumpState : BaseState<PlayerController>
    {
        public override int StateID => BuiltInStateID.Jump;

        public override void Enter(PlayerController ctx)
        {
            ctx.CoyoteTimer = 0f;
            ctx.Animator?.PlayAnim(ctx.JumpAnimName, false);
        }

        public override void LogicUpdate(PlayerController ctx, float deltaTime)
        {
            float targetSpeed = ctx.Input.HorizontalInput * ctx.MoveData.maxRunSpeed;
            ctx.ApplyHorizontalSmoothing(targetSpeed);
            ctx.UpdateFacing();

            if (ctx.Input.JumpReleased && ctx.Velocity.y > 0f)
            {
                ctx.ExecuteJumpCut();
            }
        }

        public override int CheckTransitions(PlayerController ctx)
        {
            if (ctx.Velocity.y <= 0f)
                return BuiltInStateID.Fall;

            if (ctx.Physics != null && ctx.Physics.HitCeiling)
                return BuiltInStateID.Fall;

            return StateID;
        }
    }
}
