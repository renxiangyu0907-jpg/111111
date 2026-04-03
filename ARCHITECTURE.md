// ============================================================================
// ARCHITECTURE.md — GhostVeil 项目核心架构设计文档
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │                     GhostVeil Architecture Overview                     │
// │             2D Side-Scrolling Action + Narrative Puzzle Game            │
// └──────────────────────────────────────────────────────────────────────────┘
//
// ═══════════════════════════════════════════════════════════════════════════
//  一、系统总览与依赖关系图
// ═══════════════════════════════════════════════════════════════════════════
//
//  ┌─────────────────────────────────────────────────────────────────────┐
//  │                        SERVICE LOCATOR                              │
//  │  (各系统在 Awake 注册自身接口，消费者在 Start+ 通过接口获取)         │
//  └───────────────────────┬─────────────────────────────────────────────┘
//                          │
//    ┌─────────────────────┼─────────────────────┐
//    │                     │                     │
//    ▼                     ▼                     ▼
//  ┌──────────┐  ┌──────────────────┐  ┌────────────────┐
//  │  Input   │  │ NarrativeController│  │ CameraManager │
//  │ Provider │  │ (控制权仲裁)       │  │ (Cinemachine) │
//  └────┬─────┘  └────────┬─────────┘  └───────┬────────┘
//       │                 │                     │
//       │   ┌─────────────┴──────────┐          │
//       │   │  DialogueRunner        │          │
//       │   │  CutsceneDirector      │          │
//       │   └────────────────────────┘          │
//       │                                       │
//       ▼                                       │
//  ┌──────────────────────────────────┐         │
//  │     CharacterController2D        │◄────────┘
//  │  ┌────────────────────────────┐  │
//  │  │  StateMachine<TContext>    │  │
//  │  │  ┌──────┐ ┌──────┐ ┌───┐ │  │
//  │  │  │ Idle │ │ Run  │ │...│ │  │
//  │  │  └──────┘ └──────┘ └───┘ │  │
//  │  └────────────────────────────┘  │
//  │  ┌──────────────┐ ┌──────────┐   │
//  │  │ IRaycast     │ │ ISpine   │   │
//  │  │ Controller   │ │ Bridge   │   │
//  │  └──────────────┘ └──────────┘   │
//  └──────────────────────────────────┘
//       │                     │
//       ▼                     ▼
//  ┌──────────────┐   ┌──────────────┐
//  │ Pure Raycast │   │ Spine 4.3    │
//  │ Physics      │   │ Runtime      │
//  │ (no Rigidbody│   │ (Skin/IK/    │
//  │  2D)         │   │  Material)   │
//  └──────────────┘   └──────────────┘
//
//  横向通信全部走 GameEvent（事件总线）或 ServiceLocator（接口查询），
//  不存在系统间的直接引用。
//
//
// ═══════════════════════════════════════════════════════════════════════════
//  二、四大核心系统设计思路
// ═══════════════════════════════════════════════════════════════════════════
//
// ─── 1. 物理移动系统（Raycast Controller） ────────────────────────────────
//
//  ▸ 完全放弃 Rigidbody2D，使用 BoxCollider2D 四边发射射线的方式
//    手动检测碰撞并修正位移。
//  ▸ 好处：
//    · 100% 确定性移动，无浮点漂移 / 隧穿。
//    · 精确的坡道处理（角度阈值、上坡减速、下坡贴地）。
//    · 可完美支持单向平台、移动平台、传送门等。
//  ▸ 职责边界：
//    · AbstractRaycastController 只负责"给一个 desiredMovement，返回修正后的位移"。
//    · 重力、加速度、跳跃弧线等全部由状态机的各状态计算并写入 Velocity，
//      最终通过 CharacterController2D.ApplyMovement() 送入射线控制器。
//  ▸ 关键数据结构：CollisionInfo（struct，零 GC），每帧 Reset 后重新填充。
//
//
// ─── 2. 有限状态机（Generic FSM） ────────────────────────────────────────
//
//  ▸ 泛型设计 StateMachine<TContext>，TContext 即宿主类型。
//    · PlayerController 使用 StateMachine<PlayerController>
//    · EnemyController 使用 StateMachine<EnemyController>
//  ▸ 状态通过 IState<TContext> 接口定义生命周期：
//    Enter → LogicUpdate → PhysicsUpdate → CheckTransitions → Exit
//  ▸ 转移条件内聚到状态内部（CheckTransitions 返回目标 StateID），
//    消除外部 God-class 式的巨型 if-else / switch。
//  ▸ 支持 ForceTransition()，供叙事系统 / 外部命令强制打断当前状态
//    （如：正在跑步时被过场强制切入 CutsceneState）。
//  ▸ 状态 ID 使用 int 而非 enum，允许业务层通过 ScriptableObject 动态扩展
//    （BuiltInStateID 仅提供基础预定义 ID）。
//
//
// ─── 3. Spine 动画桥接（ISpineBridge） ───────────────────────────────────
//
//  ▸ 状态机 / 控制器 不直接调用 Spine.Unity API。
//    所有动画操作通过 ISpineBridge 接口完成。
//  ▸ 原因：
//    · Spine Runtime 的 API 版本迭代频繁，桥接层隔离变更影响。
//    · SkeletonAnimation 与 SkeletonGraphic 的 API 不同，
//      桥接层统一后上层无需关心具体组件类型。
//    · 方便做动画事件到游戏事件的转发（footstep → 音效、attack_hit → 伤害判定）。
//  ▸ 功能覆盖：
//    · 多轨道播放 / 排队 / 清空
//    · Skin 组合换装（Spine 4.x combineSkins）
//    · IK 约束动态设置（枪口瞄准、头部注视）
//    · 材质属性驱动（URP 2D 自发光、受击闪白、溶解效果）
//
//
// ─── 4. 叙事控制权交接（Narrative Authority） ─────────────────────────────
//
//  ▸ 这是本项目最关键的架构决策。
//    一款叙事驱动游戏最棘手的问题：
//    "当过场动画需要控制角色走到某个位置时，输入系统该怎么办？"
//
//  ▸ 解决方案 —— NarrativeAuthorityLevel（分级控制权）：
//
//    Level 0: None       → 玩家完全控制。
//    Level 10: Ambient   → 环境叙事（旁白、BGM），不抢输入。
//    Level 20: Dialogue  → 对话中，锁移动但保留 UI 输入（选项选择）。
//    Level 30: Cutscene  → 过场演出，角色+相机完全被脚本驱动。
//    Level 40: Scripted  → QTE / 过场战斗，仅开放有限输入窗口。
//
//  ▸ 流程：
//    1. 叙事系统调用 INarrativeController.RequestAuthority(level, sequenceID)
//    2. NarrativeController 检查优先级，若 >= 当前级别则接管成功
//    3. 广播 NarrativeAuthorityRequestEvent
//    4. PlayerController 监听事件 → 状态机 ForceTransition(CutsceneState)
//    5. IInputProvider.InputLocked = true（对话级别则只锁移动不锁 UI）
//    6. 叙事序列执行完毕 → ReleaseAuthority(sequenceID)
//    7. 广播 NarrativeAuthorityReleaseEvent
//    8. PlayerController 恢复到之前的状态
//    9. IInputProvider.InputLocked = false
//
//  ▸ 多叙事序列并发时，高优先级压制低优先级；
//    释放时只有最后一个释放者才会真正恢复到 None。
//
//
// ═══════════════════════════════════════════════════════════════════════════
//  三、脚本文件夹结构
// ═══════════════════════════════════════════════════════════════════════════
//
//  Assets/Scripts/
//  ├── Core/                          # 框架核心（不含业务逻辑）
//  │   ├── StateMachine/              # 泛型状态机
//  │   │   ├── IState.cs              # 状态接口
//  │   │   ├── BaseState.cs           # 状态空实现基类
//  │   │   └── StateMachine.cs        # 状态机驱动器
//  │   ├── Event/                     # 全局事件总线
//  │   │   ├── GameEvent.cs           # 发布/订阅核心
//  │   │   └── GameEvents.cs          # 预定义事件结构体
//  │   └── ServiceLocator/            # 服务定位器
//  │       └── ServiceLocator.cs
//  │
//  ├── Physics/                       # 纯射线物理
//  │   ├── IRaycastController.cs      # 接口
//  │   └── AbstractRaycastController.cs # 抽象基类
//  │   └── (后续) PlayerRaycastController.cs
//  │
//  ├── Character/                     # 角色系统
//  │   ├── Common/                    # 公共基类
//  │   │   └── CharacterController2D.cs
//  │   ├── Player/                    # 玩家具体实现
//  │   │   ├── (后续) PlayerController.cs
//  │   │   └── (后续) States/         # 玩家状态集
//  │   └── NPC/                       # NPC / 敌人
//  │       └── (后续) ...
//  │
//  ├── Animation/                     # 动画系统
//  │   └── Spine/
//  │       ├── ISpineBridge.cs        # 桥接接口
//  │       └── AbstractSpineBridge.cs # 桥接抽象基类
//  │       └── (后续) SpineSkeletonAnimBridge.cs
//  │
//  ├── Combat/                        # 战斗系统
//  │   ├── IDamageable.cs
//  │   └── IAttackSource.cs
//  │
//  ├── Interaction/                   # 交互系统
//  │   ├── IInteractable.cs
//  │   └── AbstractInteractable.cs
//  │
//  ├── Narrative/                     # 叙事系统
//  │   ├── INarrativeController.cs    # 控制权仲裁接口
//  │   ├── Dialogue/
//  │   │   └── IDialogueRunner.cs
//  │   ├── Cutscene/
//  │   │   └── ICutsceneDirector.cs
//  │   └── Timeline/                  # Unity Timeline 扩展（后续）
//  │
//  ├── Camera/                        # 相机系统
//  │   └── ICameraTarget.cs
//  │
//  ├── Input/                         # 输入抽象
//  │   └── IInputProvider.cs
//  │
//  ├── UI/                            # UI 系统（后续）
//  ├── Audio/                         # 音频管理（后续）
//  ├── Save/                          # 存档系统
//  │   └── ISaveable.cs
//  ├── Level/                         # 关卡 / 场景管理（后续）
//  │
//  ├── Data/                          # 纯数据定义（零 MonoBehaviour）
//  │   ├── Enums/
//  │   │   ├── CharacterEnums.cs
//  │   │   ├── CombatEnums.cs
//  │   │   ├── NarrativeEnums.cs
//  │   │   └── PhysicsEnums.cs
//  │   ├── Structs/
//  │   │   ├── CollisionInfo.cs
//  │   │   └── DamagePayload.cs
//  │   └── ScriptableObjects/         # SO 数据容器（后续）
//  │
//  └── Utility/                       # 工具类（后续）
//      └── (后续) Timer.cs, Extensions.cs ...
