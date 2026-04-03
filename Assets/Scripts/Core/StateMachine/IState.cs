// ============================================================================
// IState.cs — 有限状态机的核心状态契约
// ============================================================================
namespace GhostVeil.Core.StateMachine
{
    /// <summary>
    /// 泛型状态接口。
    /// TContext 为宿主类型（如 PlayerController / EnemyController），
    /// 状态通过 context 引用宿主的所有公共 API 而无需 GetComponent。
    /// </summary>
    public interface IState<TContext> where TContext : class
    {
        /// <summary>状态唯一 ID（对应 BuiltInStateID 或自定义 int）</summary>
        int StateID { get; }

        /// <summary>进入状态时调用一次</summary>
        void Enter(TContext context);

        /// <summary>每帧由状态机驱动（MonoBehaviour.Update 节奏）</summary>
        void LogicUpdate(TContext context, float deltaTime);

        /// <summary>固定帧率更新（用于物理相关计算，FixedUpdate 节奏）</summary>
        void PhysicsUpdate(TContext context, float fixedDeltaTime);

        /// <summary>退出状态时调用一次</summary>
        void Exit(TContext context);

        /// <summary>
        /// 每帧由状态机在 LogicUpdate 之后调用。
        /// 返回需要切换的目标状态 ID；返回自身 StateID 则不切换。
        /// 把转移条件内聚到状态自身，避免外部 God-class 式的 if-else。
        /// </summary>
        int CheckTransitions(TContext context);
    }
}
