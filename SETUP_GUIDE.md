# GhostVeil 框架接入手册 — 从零到角色跑起来

本文档面向拿到这套代码后，在 Unity 编辑器中实际搭建场景并让角色动起来的完整步骤。

---

## 一、前置环境要求

| 项目 | 要求 |
|------|------|
| Unity 版本 | 2022.3 LTS 或更高 |
| 渲染管线 | URP (Universal Render Pipeline) |
| Input System | 必须安装 `com.unity.inputsystem` 包 |
| Spine (可选) | 当前阶段不需要，后续接入 |

### 1.1 安装 Input System 包

```
Unity 菜单 → Window → Package Manager → 搜索 "Input System" → Install
```

安装后 Unity 会弹窗提示切换 Active Input Handling：

```
Edit → Project Settings → Player → Other Settings →
  Active Input Handling → 选择 "Both" 或 "Input System Package (New)"
```

> 选 "Both" 最安全，兼容新旧两套系统。

---

## 二、导入脚本

将 `Assets/Scripts/` 整个文件夹拷贝到你的 Unity 项目的 `Assets/` 下。
等待编译通过（如果报错请确认 Input System 包已安装）。

最终结构应该是：
```
你的Unity项目/
  Assets/
    Scripts/
      Core/
      Physics/
      Character/
      Input/
      Data/
      ...（与仓库一致）
```

---

## 三、Layer 配置（关键！）

框架的射线检测完全依赖 Layer + LayerMask，必须先配好。

### 3.1 创建 Layer

```
Edit → Project Settings → Tags and Layers → Layers
```

| 序号 | Layer 名称 | 用途 |
|------|-----------|------|
| 8 | Ground | 实心地面、墙壁、坡道 |
| 9 | OneWayPlatform | 单向平台（可从下方穿越） |
| 10 | Player | 玩家角色（防止射线打到自己） |

> 序号不必严格一致，但名称要对。后面 Inspector 里选的是名称。

---

## 四、创建 ScriptableObject 数据资产

### 4.1 创建移动参数配置

```
Project 面板右键 → Create → GhostVeil → Player Movement Data
```

这会生成一个 `.asset` 文件。Inspector 中可以看到所有参数，使用默认值即可开始测试：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| Max Run Speed | 8 | 最大水平速度 |
| Ground Accel Time | 0.08 | 地面加速时长 |
| Ground Decel Time | 0.05 | 地面刹车时长 |
| Air Accel Time | 0.15 | 空中加速时长 |
| Air Decel Time | 0.25 | 空中惯性时长 |
| Jump Height | 3.5 | 跳跃最大高度 |
| Time To Jump Apex | 0.4 | 到达顶点时间 |
| Jump Cut Multiplier | 0.5 | 松手速度削减 |
| Jump Buffer Time | 0.1 | 跳跃缓冲窗口 |
| Coyote Time | 0.08 | 郊狼时间窗口 |
| Fall Gravity Multiplier | 1.5 | 下落重力倍率 |
| Max Fall Speed | -25 | 最大下落速度 |

---

## 五、搭建测试场景

### 5.1 创建地面

```
1. Hierarchy 右键 → 2D Object → Sprites → Square
2. 重命名为 "Ground"
3. Transform:
     Position: (0, -2, 0)
     Scale:    (20, 1, 0)
4. Add Component → BoxCollider2D（默认尺寸即可）
5. Inspector 右上角 Layer → 设为 "Ground"
```

### 5.2 创建一个坡道（可选）

```
1. 创建一个空 GameObject，命名 "Slope"
2. Add Component → Polygon Collider2D
3. 编辑顶点画成一个三角形斜面
4. Layer 设为 "Ground"
```

### 5.3 创建单向平台（可选）

```
1. Hierarchy 右键 → 2D Object → Sprites → Square
2. 重命名为 "OneWayPlatform"
3. Transform:
     Position: (5, 0, 0)
     Scale:    (4, 0.2, 0)
4. Add Component → BoxCollider2D
5. Layer 设为 "OneWayPlatform"
```

---

## 六、搭建玩家角色（核心步骤）

### 6.1 创建 Player GameObject

```
1. Hierarchy 右键 → 2D Object → Sprites → Square
   （临时用方块代替美术，后续替换为 Spine 骨骼）
2. 重命名为 "Player"
3. Transform:
     Position: (0, 1, 0)
     Scale:    (0.8, 1.2, 1)  ← 竖长方形，像个角色轮廓
4. Layer 设为 "Player"
```

### 6.2 配置 BoxCollider2D

```
Player 已经自带一个 BoxCollider2D（创建 Sprite 时附带的）。
确认以下设置：
  ☑ 不要勾选 Is Trigger（这是射线参考框，不是触发器）
  Size: (1, 1) 默认即可（跟 Sprite 匹配）
```

> **绝对不要** 添加 Rigidbody2D！整个框架的核心就是不用它。

### 6.3 挂载组件（按顺序）

选中 Player GameObject，在 Inspector 中依次 Add Component：

#### ① PlayerRaycastController

```
Add Component → 搜索 "PlayerRaycastController"

Inspector 配置：
  Collision Mask:        勾选 "Ground"
  One Way Platform Mask: 勾选 "OneWayPlatform"
  Horizontal Ray Count:  4（默认）
  Vertical Ray Count:    4（默认）
  Skin Width:            0.015（默认）
  Max Slope Angle:       55（默认）
  Fall Through Duration: 0.15（默认）
```

> **重要**：Collision Mask 不要勾选 "Player" 层，否则射线会打到自己。

#### ② InputSystemProvider

```
Add Component → 搜索 "InputSystemProvider"

Inspector 配置：
  Down Threshold: -0.5（默认）
```

#### ③ PlayerController

```
Add Component → 搜索 "PlayerController"

Inspector 配置：
  Move Data: 拖入第四步创建的 PlayerMovementData 资产
```

### 6.4 最终 Player 组件一览

```
Player (GameObject)
  ├── SpriteRenderer        ← 临时方块视觉
  ├── BoxCollider2D          ← 射线参考框（不是物理碰撞！）
  ├── PlayerRaycastController ← 射线碰撞引擎
  ├── InputSystemProvider    ← 输入捕获
  └── PlayerController       ← 状态机 + 移动逻辑
         └── Move Data: PlayerMovementData.asset
```

---

## 七、运行测试

### 7.1 按 Play

你应该看到：
- 方块站在地面上不掉下去 ✅
- 按 A/D 或 方向键左右移动，有平滑加速减速 ✅
- 按 Space 跳跃，有明显的抛物线弧度 ✅
- 长按 Space = 高跳，轻点 Space = 矮跳 ✅
- 走出平台边缘后极短时间内仍可跳跃（郊狼时间） ✅
- 快落地时按 Space，着地瞬间自动弹起（跳跃缓冲） ✅
- 在单向平台上按 S + Space 向下穿透 ✅

### 7.2 打开 Debug 射线可视化

```
Scene 窗口 → 确保 Gizmos 开关打开（工具栏右上角）
运行时在 Scene 视图中可以看到：
  红色线 = 水平射线（撞墙检测）
  绿色线 = 垂直射线（落地检测）
  橙色框 = skinWidth 内缩后的检测区域
  青色点 = 射线发射原点
```

### 7.3 运行时调参

```
选中 Project 面板中的 PlayerMovementData.asset
修改参数 → 立即生效（无需停止运行）

推荐先调这几个感受差异：
  · Jump Height:           2 → 5 观察跳跃高度
  · Time To Jump Apex:     0.2(脆) → 0.6(飘)
  · Fall Gravity Multiplier: 1(飘) → 3(砸)
  · Ground Accel Time:     0.01(瞬移) → 0.3(溜冰)
```

---

## 八、Debug：运行时查看状态机

在 PlayerController 的 Inspector 中，你可以看到以下运行时数据（展开脚本）：

| 字段 | 含义 |
|------|------|
| Velocity | 当前速度向量 |
| Facing | 当前朝向 (Right / Left) |
| Life State | 生命状态 |

如果你想在 Game 视图中显示当前状态名，可以创建一个简单的 Debug UI：

```csharp
// DebugStateDisplay.cs — 挂到 Player 上，显示当前状态
using UnityEngine;
using GhostVeil.Character.Player;
using GhostVeil.Data;

public class DebugStateDisplay : MonoBehaviour
{
    private PlayerController _player;

    void Start() => _player = GetComponent<PlayerController>();

    void OnGUI()
    {
        if (_player == null || _player.StateMachine == null) return;

        int id = _player.StateMachine.CurrentStateID;
        string stateName = id switch
        {
            BuiltInStateID.Idle => "IDLE",
            BuiltInStateID.Run  => "RUN",
            BuiltInStateID.Jump => "JUMP",
            BuiltInStateID.Fall => "FALL",
            _ => $"STATE_{id}"
        };

        // 左上角显示
        GUI.Label(new Rect(10, 10, 300, 30),
            $"State: {stateName}  |  Vel: {_player.Velocity}");
        GUI.Label(new Rect(10, 35, 300, 30),
            $"Grounded: {_player.IsGrounded}  |  Coyote: {_player.CoyoteTimer:F3}  |  Buffer: {_player.JumpBufferTimer:F3}");
    }
}
```

---

## 九、常见问题排查

### Q: 角色穿过地面掉下去了
```
原因：Ground 的 Layer 没有被 PlayerRaycastController 的 Collision Mask 包含。
修复：选中 Player → PlayerRaycastController → Collision Mask → 勾选 "Ground"。
```

### Q: 角色卡在空中不动
```
原因：PlayerMovementData 没有拖入 PlayerController 的 Move Data 槽位。
修复：Inspector 中把 .asset 文件拖进去。
```

### Q: 按键没反应
```
原因 1：没安装 Input System 包。
原因 2：Project Settings → Player → Active Input Handling 没切换到 "Both" 或 "Input System Package"。
原因 3：InputSystemProvider 组件没挂到场景中的任何 GameObject 上。
```

### Q: 角色能移动但不能跳跃
```
原因：角色没有被检测为"站在地面上"（IsGrounded = false）。
排查：Scene 视图中看绿色垂直射线是否命中了地面 Collider。
     确认地面有 BoxCollider2D 且 Layer = Ground。
```

### Q: 单向平台从上面也穿过去了
```
原因：单向平台的 Layer 被加入了 Collision Mask（不应该）。
修复：单向平台的 Layer 只应该出现在 One Way Platform Mask 中，
     不要同时勾选到 Collision Mask 里。
```

### Q: 坡道上角色抖动
```
原因：坡面角度超过 Max Slope Angle（默认 55°）。
修复：增大 Max Slope Angle，或降低坡面倾斜度。
```

---

## 十、代码阅读地图

想读懂代码？按这个顺序：

```
第一层（数据定义，最简单）：
  Data/Enums/CharacterEnums.cs          ← BuiltInStateID 定义
  Data/ScriptableObjects/PlayerMovementData.cs  ← 所有参数

第二层（框架核心）：
  Core/StateMachine/IState.cs           ← 状态接口（5 个方法）
  Core/StateMachine/StateMachine.cs     ← 状态机驱动器

第三层（物理引擎）：
  Physics/IRaycastController.cs         ← 接口
  Physics/AbstractRaycastController.cs  ← Move 管线
  Physics/PlayerRaycastController.cs    ← 射线检测实现

第四层（输入）：
  Input/IInputProvider.cs               ← 输入接口
  Input/InputSystemProvider.cs          ← New Input System 实现

第五层（角色控制器，读到这里就全通了）：
  Character/Player/PlayerController.cs  ← 状态机宿主
  Character/Player/States/PlayerIdleState.cs
  Character/Player/States/PlayerRunState.cs
  Character/Player/States/PlayerJumpState.cs
  Character/Player/States/PlayerFallState.cs
```

---

## 十一、下一步扩展方向

| 方向 | 要做什么 |
|------|---------|
| **视觉** | 实现 SpineSkeletonAnimBridge，替换方块为 Spine 骨骼动画 |
| **战斗** | 新增 PlayerAttackState，实现 IDamageable / IAttackSource |
| **叙事** | 实现 NarrativeController，让过场动画能接管角色 |
| **关卡** | 添加移动平台、机关、区域切换 |
| **存档** | 实现 ISaveable，序列化角色位置/状态 |
