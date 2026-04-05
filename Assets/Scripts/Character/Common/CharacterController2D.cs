// ============================================================================
// CharacterController2D.cs — 角色控制器抽象基类
// ============================================================================
using UnityEngine;
using GhostVeil.Core.StateMachine;
using GhostVeil.Data;
using GhostVeil.Physics;
using GhostVeil.Animation;
using GhostVeil.Input;

namespace GhostVeil.Character.Common
{
    [DisallowMultipleComponent]
    public abstract class CharacterController2D : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  可序列化配置
        // ══════════════════════════════════════════════

        [Header("=== Character Identity ===")]
        [SerializeField] protected string characterID;

        // ══════════════════════════════════════════════
        //  运行时组件引用
        // ══════════════════════════════════════════════

        /// <summary>物理控制器</summary>
        protected PlayerPhysicsController physicsController;

        /// <summary>Spine 动画桥接</summary>
        protected ISpineBridge spineBridge;

        /// <summary>输入源</summary>
        protected IInputProvider inputProvider;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        /// <summary>当前速度</summary>
        public Vector2 Velocity { get; set; }

        /// <summary>当前朝向</summary>
        public FacingDirection Facing { get; set; } = FacingDirection.Right;

        /// <summary>角色生命周期状态</summary>
        public CharacterLifeState LifeState { get; set; } = CharacterLifeState.Alive;

        /// <summary>是否站在地面</summary>
        public bool IsGrounded => physicsController != null && physicsController.IsGrounded;

        /// <summary>是否贴墙</summary>
        public bool IsTouchingWall =>
            physicsController != null &&
            (physicsController.HitLeft || physicsController.HitRight);

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        protected virtual void Awake()
        {
            GatherDependencies();
        }

        protected virtual void Start()
        {
            RegisterStates();
            OnStart();
        }

        protected virtual void Update()
        {
            if (LifeState == CharacterLifeState.Dead) return;
            OnLogicUpdate(Time.deltaTime);
        }

        protected virtual void FixedUpdate()
        {
            if (LifeState == CharacterLifeState.Dead) return;
            OnPhysicsUpdate(Time.fixedDeltaTime);
        }

        // ══════════════════════════════════════════════
        //  子类必须实现
        // ══════════════════════════════════════════════

        protected abstract void GatherDependencies();
        protected abstract void RegisterStates();

        // ══════════════════════════════════════════════
        //  子类可选重写
        // ══════════════════════════════════════════════

        protected virtual void OnStart() { }
        protected virtual void OnLogicUpdate(float deltaTime) { }
        protected virtual void OnPhysicsUpdate(float fixedDeltaTime) { }

        // ══════════════════════════════════════════════
        //  公共方法
        // ══════════════════════════════════════════════

        public void SetFacing(FacingDirection direction)
        {
            if (Facing == direction) return;
            Facing = direction;
        }
    }
}
