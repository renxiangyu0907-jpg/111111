// ============================================================================
// BaseState.cs — 状态的默认空实现基类（便于只重写需要的方法）
// ============================================================================
namespace GhostVeil.Core.StateMachine
{
    /// <summary>
    /// 为 IState 提供空默认实现的抽象基类。
    /// 具体状态继承此类后只需重写关心的生命周期方法，
    /// 避免每个状态都手写五个空方法。
    /// </summary>
    public abstract class BaseState<TContext> : IState<TContext> where TContext : class
    {
        public abstract int StateID { get; }

        public virtual void Enter(TContext context) { }
        public virtual void LogicUpdate(TContext context, float deltaTime) { }
        public virtual void PhysicsUpdate(TContext context, float fixedDeltaTime) { }
        public virtual void Exit(TContext context) { }

        /// <summary>默认：保持当前状态（不切换）</summary>
        public virtual int CheckTransitions(TContext context) => StateID;
    }
}
