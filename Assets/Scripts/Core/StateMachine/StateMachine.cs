// ============================================================================
// StateMachine.cs — 通用有限状态机驱动器
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace GhostVeil.Core.StateMachine
{
    /// <summary>
    /// 泛型有限状态机。
    /// - 不继承 MonoBehaviour，由宿主在 Update / FixedUpdate 中驱动。
    /// - 职责单一：维护状态字典、执行状态生命周期、处理转移。
    /// - 支持状态历史记录（用于"返回上一状态"类需求）。
    /// </summary>
    public class StateMachine<TContext> where TContext : class
    {
        // ── 内部数据 ──────────────────────────────────
        private readonly Dictionary<int, IState<TContext>> _states = new();
        private readonly TContext _context;

        private IState<TContext> _currentState;
        private IState<TContext> _previousState;
        private bool _isTransitioning;

        // ── 公共只读访问 ────────────────────────────────
        public IState<TContext> CurrentState  => _currentState;
        public IState<TContext> PreviousState => _previousState;
        public int CurrentStateID => _currentState?.StateID ?? -1;

        // ── 事件（可选订阅，用于 UI / 音频 / 日志） ────────
        public event System.Action<int /*oldID*/, int /*newID*/> OnStateChanged;

        // ── 构造 ──────────────────────────────────────
        public StateMachine(TContext context)
        {
            _context = context;
        }

        // ── 注册 ──────────────────────────────────────
        /// <summary>注册一个状态。重复 ID 会覆盖（方便热重载）。</summary>
        public void RegisterState(IState<TContext> state)
        {
            _states[state.StateID] = state;
        }

        // ── 初始化 ─────────────────────────────────────
        /// <summary>设置初始状态，必须在第一次 Tick 之前调用。</summary>
        public void Initialize(int startStateID)
        {
            if (!_states.TryGetValue(startStateID, out var state))
            {
                Debug.LogError($"[StateMachine] Start state {startStateID} not registered.");
                return;
            }
            _currentState = state;
            _currentState.Enter(_context);
        }

        // ── 每帧驱动 ──────────────────────────────────
        public void LogicTick(float deltaTime)
        {
            if (_currentState == null || _isTransitioning) return;

            _currentState.LogicUpdate(_context, deltaTime);

            int nextID = _currentState.CheckTransitions(_context);
            if (nextID != _currentState.StateID)
            {
                TransitionTo(nextID);
            }
        }

        public void PhysicsTick(float fixedDeltaTime)
        {
            if (_currentState == null || _isTransitioning) return;
            _currentState.PhysicsUpdate(_context, fixedDeltaTime);
        }

        // ── 强制切换（外部命令，如叙事系统直接指令切入 Cutscene） ──
        public void ForceTransition(int targetStateID)
        {
            TransitionTo(targetStateID);
        }

        // ── 内部转移 ──────────────────────────────────
        private void TransitionTo(int targetStateID)
        {
            if (!_states.TryGetValue(targetStateID, out var nextState))
            {
                Debug.LogWarning($"[StateMachine] Target state {targetStateID} not registered. Ignored.");
                return;
            }

            _isTransitioning = true;

            int oldID = _currentState.StateID;
            _currentState.Exit(_context);
            _previousState = _currentState;
            _currentState = nextState;
            _currentState.Enter(_context);

            _isTransitioning = false;

            OnStateChanged?.Invoke(oldID, targetStateID);
        }
    }
}
