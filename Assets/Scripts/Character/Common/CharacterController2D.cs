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
    /// <summary>
    /// 所有可控角色（Player / NPC / Boss）的抽象基类。
    /// 
    /// 设计要点：
    /// 1. 组合优于继承 —— 持有接口引用而非硬编码具体组件。
    /// 2. 状态机由子类在 RegisterStates() 中注册具体状态。
    /// 3. Update / FixedUpdate 的调用节奏由此基类统一管理，
    ///    子类不应再写 Update —— 通过 OnLogicUpdate / OnPhysicsUpdate 钩子扩展。
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class CharacterController2D : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  可序列化配置（Inspector 可调）
        // ══════════════════════════════════════════════

        [Header("=== Character Identity ===")]
        [SerializeField] protected string characterID;

        // ══════════════════════════════════════════════
        //  运行时组件引用（通过接口解耦）
        // ══════════════════════════════════════════════

        /// <summary>射线物理控制器</summary>
        protected IRaycastController raycastController;

        /// <summary>Spine 动画桥接</summary>
        protected ISpineBridge spineBridge;

        /// <summary>输入源（Player 为真实输入，NPC/Boss 可为 AI 输入）</summary>
        protected IInputProvider inputProvider;

        // ══════════════════════════════════════════════
        //  状态机
        // ══════════════════════════════════════════════

        // 子类通过泛型参数指定自身类型（CRTP 模式）
        // 例: PlayerController 中为 StateMachine<PlayerController>
        // 此处用 object 做占位；子类应自行声明强类型的状态机字段

        // ══════════════════════════════════════════════
        //  运行时状态（公开只读供状态读取）
        // ══════════════════════════════════════════════

        /// <summary>当前速度（由状态计算，每帧更新）</summary>
        public Vector2 Velocity { get; set; }

        /// <summary>当前朝向</summary>
        public FacingDirection Facing { get; set; } = FacingDirection.Right;

        /// <summary>角色生命周期状态</summary>
        public CharacterLifeState LifeState { get; set; } = CharacterLifeState.Alive;

        /// <summary>是否站在地面</summary>
        public bool IsGrounded => raycastController != null && raycastController.Collisions.Below;

        /// <summary>是否贴墙</summary>
        public bool IsTouchingWall =>
            raycastController != null &&
            (raycastController.Collisions.Left || raycastController.Collisions.Right);

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        protected virtual void Awake()
        {
            // 子类在 override Awake 中：
            // 1. 获取 / 注入接口引用
            // 2. 调用 InitializeStateMachine()
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

        /// <summary>
        /// 收集依赖：从 GetComponent / ServiceLocator / DI 容器获取接口引用。
        /// 在 Awake 中自动调用。
        /// </summary>
        protected abstract void GatherDependencies();

        /// <summary>
        /// 向状态机注册所有状态实例。在 Start 中自动调用。
        /// </summary>
        protected abstract void RegisterStates();

        // ══════════════════════════════════════════════
        //  子类可选重写的钩子
        // ══════════════════════════════════════════════

        /// <summary>Start 完成后的额外初始化</summary>
        protected virtual void OnStart() { }

        /// <summary>每帧逻辑更新（状态机 Tick 之外的额外逻辑）</summary>
        protected virtual void OnLogicUpdate(float deltaTime) { }

        /// <summary>每物理帧更新</summary>
        protected virtual void OnPhysicsUpdate(float fixedDeltaTime) { }

        // ══════════════════════════════════════════════
        //  公共方法
        // ══════════════════════════════════════════════

        /// <summary>
        /// 翻转角色朝向（翻转 localScale.x）。
        /// Spine 骨骼的 flipX 由 SpineBridge 内部处理。
        /// </summary>
        public void SetFacing(FacingDirection direction)
        {
            if (Facing == direction) return;
            Facing = direction;

            // 注意：如果挂了 SpineAnimator 且 controlFlip = true，
            // 翻转由 Skeleton.ScaleX 处理（见 SpineAnimator.SetFaceDirection）。
            // 此处不再翻转 transform.localScale，避免与 Spine 翻转双重叠加。
            // 如果没有 Spine（纯 SpriteRenderer），取消下面注释即可恢复 localScale 翻转：
            // var scale = transform.localScale;
            // scale.x = Mathf.Abs(scale.x) * (int)direction;
            // transform.localScale = scale;
        }

        /// <summary>
        /// 执行实际位移（将 Velocity 应用到射线控制器）。
        /// 通常在 FixedUpdate / PhysicsUpdate 结尾调用。
        /// </summary>
        public Vector2 ApplyMovement(float deltaTime)
        {
            if (raycastController == null) return Vector2.zero;

            Vector2 displacement = Velocity * deltaTime;
            return raycastController.Move(displacement);
        }
    }
}
