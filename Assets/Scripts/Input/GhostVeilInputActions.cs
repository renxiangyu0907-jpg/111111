// ============================================================================
// GhostVeilInputActions.cs — Input System Generated C# Class 的手写等价物
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  本文件是 Unity Input System 的 .inputactions 资产所生成 C# 类的          │
// │  手写等价实现。                                                           │
// │                                                                          │
// │  在真实项目中：                                                           │
// │    1. 创建 Assets/Settings/GhostVeilInputActions.inputactions             │
// │    2. 在 Inspector 中勾选 "Generate C# Class"                            │
// │    3. Unity 会自动生成与本文件等价的代码                                   │
// │                                                                          │
// │  为什么要手写？                                                           │
// │    · 纯代码架构下无需依赖 .inputactions 资产即可编译                       │
// │    · 提供精确的 Action Map / Action / Binding 定义参考                    │
// │    · 方便 CI / 无 Unity Editor 环境下的编译验证                           │
// │                                                                          │
// │  Action Map 设计：                                                       │
// │                                                                          │
// │    [Gameplay]           — 常规游戏操控                                    │
// │      Move    : Value<Vector2>  — 左摇杆 / WASD                           │
// │      Jump    : Button          — Space / A(Gamepad)                      │
// │      Attack  : Button          — J / X(Gamepad)                          │
// │      Dash    : Button          — Shift / RB(Gamepad)                     │
// │      Interact: Button          — E / Y(Gamepad)                          │
// │      Pause   : Button          — Escape / Start(Gamepad)                 │
// │                                                                          │
// │    [UI]                 — UI 导航（暂留空，后续扩展）                      │
// └──────────────────────────────────────────────────────────────────────────┘
//
// 注意：本文件模拟 InputActionAsset 的运行时行为。
// 当你在 Unity 编辑器中正式创建 .inputactions 并生成代码后，
// 可用生成的类 1:1 替换本文件，InputSystemProvider 的对接代码无需改动。
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostVeil.Input
{
    /// <summary>
    /// 等价于 Unity Input System 自动生成的 C# Wrapper 类。
    /// 封装 InputActionAsset，暴露强类型的 Action Map 与 Action 访问器。
    /// 实现 IDisposable 以确保 InputActionAsset 的正确释放。
    /// </summary>
    public class GhostVeilInputActions : IDisposable
    {
        // ══════════════════════════════════════════════
        //  底层 Asset
        // ══════════════════════════════════════════════

        private readonly InputActionAsset _asset;

        public InputActionAsset asset => _asset;

        // ══════════════════════════════════════════════
        //  Gameplay Action Map
        // ══════════════════════════════════════════════

        private readonly GameplayActions _gameplay;
        public GameplayActions Gameplay => _gameplay;

        // ══════════════════════════════════════════════
        //  构造：通过代码构建完整的 InputActionAsset
        // ══════════════════════════════════════════════

        public GhostVeilInputActions()
        {
            _asset = InputActionAsset.FromJson(ActionMapJson);
            _gameplay = new GameplayActions(_asset);
        }

        // ══════════════════════════════════════════════
        //  启用 / 禁用（全局快捷方法）
        // ══════════════════════════════════════════════

        public void Enable()  => _asset.Enable();
        public void Disable() => _asset.Disable();

        public void Dispose()
        {
            _asset?.Disable();
            UnityEngine.Object.Destroy(_asset);
        }

        // ══════════════════════════════════════════════
        //  Gameplay Action Map 包装类
        // ══════════════════════════════════════════════

        /// <summary>
        /// Gameplay Action Map 的强类型访问器。
        /// 每个 InputAction 以只读属性暴露，使用者无需手写魔法字符串。
        /// 实现 IGameplayActions 回调接口以获取事件驱动通知。
        /// </summary>
        public sealed class GameplayActions
        {
            private readonly InputActionMap _map;

            // ── 各 Action 缓存 ──────────────────────────
            public InputAction Move     { get; }
            public InputAction Jump     { get; }
            public InputAction Attack   { get; }
            public InputAction Dash     { get; }
            public InputAction Interact { get; }
            public InputAction Pause    { get; }

            internal GameplayActions(InputActionAsset asset)
            {
                _map     = asset.FindActionMap("Gameplay", throwIfNotFound: true);
                Move     = _map.FindAction("Move",     throwIfNotFound: true);
                Jump     = _map.FindAction("Jump",     throwIfNotFound: true);
                Attack   = _map.FindAction("Attack",   throwIfNotFound: true);
                Dash     = _map.FindAction("Dash",     throwIfNotFound: true);
                Interact = _map.FindAction("Interact", throwIfNotFound: true);
                Pause    = _map.FindAction("Pause",    throwIfNotFound: true);
            }

            public void Enable()  => _map.Enable();
            public void Disable() => _map.Disable();

            public bool enabled => _map.enabled;

            /// <summary>
            /// 设置回调接口（可选的事件驱动模式）。
            /// 调用后 IGameplayActions 的方法将在对应 Action 触发时被调用。
            /// </summary>
            public void SetCallbacks(IGameplayActions callbacks)
            {
                // 先清除旧回调（防止重复注册）
                Move.started     -= callbacks.OnMove;
                Move.performed   -= callbacks.OnMove;
                Move.canceled    -= callbacks.OnMove;
                Jump.started     -= callbacks.OnJump;
                Jump.performed   -= callbacks.OnJump;
                Jump.canceled    -= callbacks.OnJump;
                Attack.started   -= callbacks.OnAttack;
                Attack.performed -= callbacks.OnAttack;
                Attack.canceled  -= callbacks.OnAttack;
                Dash.started     -= callbacks.OnDash;
                Dash.performed   -= callbacks.OnDash;
                Dash.canceled    -= callbacks.OnDash;
                Interact.started     -= callbacks.OnInteract;
                Interact.performed   -= callbacks.OnInteract;
                Interact.canceled    -= callbacks.OnInteract;
                Pause.started    -= callbacks.OnPause;
                Pause.performed  -= callbacks.OnPause;
                Pause.canceled   -= callbacks.OnPause;

                // 注册新回调
                Move.started     += callbacks.OnMove;
                Move.performed   += callbacks.OnMove;
                Move.canceled    += callbacks.OnMove;
                Jump.started     += callbacks.OnJump;
                Jump.performed   += callbacks.OnJump;
                Jump.canceled    += callbacks.OnJump;
                Attack.started   += callbacks.OnAttack;
                Attack.performed += callbacks.OnAttack;
                Attack.canceled  += callbacks.OnAttack;
                Dash.started     += callbacks.OnDash;
                Dash.performed   += callbacks.OnDash;
                Dash.canceled    += callbacks.OnDash;
                Interact.started     += callbacks.OnInteract;
                Interact.performed   += callbacks.OnInteract;
                Interact.canceled    += callbacks.OnInteract;
                Pause.started    += callbacks.OnPause;
                Pause.performed  += callbacks.OnPause;
                Pause.canceled   += callbacks.OnPause;
            }
        }

        // ══════════════════════════════════════════════
        //  Gameplay 回调接口
        // ══════════════════════════════════════════════

        /// <summary>
        /// Gameplay Action Map 的回调接口。
        /// InputSystemProvider 实现此接口以接收所有 Action 事件。
        /// </summary>
        public interface IGameplayActions
        {
            void OnMove(InputAction.CallbackContext context);
            void OnJump(InputAction.CallbackContext context);
            void OnAttack(InputAction.CallbackContext context);
            void OnDash(InputAction.CallbackContext context);
            void OnInteract(InputAction.CallbackContext context);
            void OnPause(InputAction.CallbackContext context);
        }

        // ══════════════════════════════════════════════
        //  InputActionAsset JSON 定义
        // ══════════════════════════════════════════════
        //
        //  这段 JSON 等价于在 Unity Editor 中配置的 .inputactions 资产。
        //  使用 InputActionAsset.FromJson() 在运行时构建。
        //
        //  Binding 说明：
        //    · Move 使用 2DVector Composite（WASD + 左摇杆）
        //    · 按钮类 Action 同时绑定键盘键 + 手柄按钮
        //    · 所有 Binding 的 group 标记了 "Keyboard" 或 "Gamepad"，
        //      后续可通过 Control Scheme 做设备切换

        private const string ActionMapJson = @"
{
    ""name"": ""GhostVeilInputActions"",
    ""maps"": [
        {
            ""name"": ""Gameplay"",
            ""id"": ""a1b2c3d4-0001-0001-0001-000000000001"",
            ""actions"": [
                {
                    ""name"": ""Move"",
                    ""type"": ""Value"",
                    ""id"": ""a1b2c3d4-0002-0001-0001-000000000001"",
                    ""expectedControlType"": ""Vector2"",
                    ""interactions"": """",
                    ""processors"": ""StickDeadzone(min=0.125,max=0.925)""
                },
                {
                    ""name"": ""Jump"",
                    ""type"": ""Button"",
                    ""id"": ""a1b2c3d4-0002-0002-0001-000000000001"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                },
                {
                    ""name"": ""Attack"",
                    ""type"": ""Button"",
                    ""id"": ""a1b2c3d4-0002-0003-0001-000000000001"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                },
                {
                    ""name"": ""Dash"",
                    ""type"": ""Button"",
                    ""id"": ""a1b2c3d4-0002-0004-0001-000000000001"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                },
                {
                    ""name"": ""Interact"",
                    ""type"": ""Button"",
                    ""id"": ""a1b2c3d4-0002-0005-0001-000000000001"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                },
                {
                    ""name"": ""Pause"",
                    ""type"": ""Button"",
                    ""id"": ""a1b2c3d4-0002-0006-0001-000000000001"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""WASD"",
                    ""id"": ""b1-0001"",
                    ""path"": """",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false,
                    ""compositeType"": ""2DVector""
                },
                {
                    ""name"": ""up"",
                    ""id"": ""b1-0002"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""b1-0003"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""b1-0004"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""b1-0005"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""Arrow Keys"",
                    ""id"": ""b1-0006"",
                    ""path"": """",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false,
                    ""compositeType"": ""2DVector""
                },
                {
                    ""name"": ""up"",
                    ""id"": ""b1-0007"",
                    ""path"": ""<Keyboard>/upArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""b1-0008"",
                    ""path"": ""<Keyboard>/downArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""b1-0009"",
                    ""path"": ""<Keyboard>/leftArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""b1-0010"",
                    ""path"": ""<Keyboard>/rightArrow"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""b1-0011"",
                    ""path"": ""<Gamepad>/leftStick"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b2-0001"",
                    ""path"": ""<Keyboard>/space"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Jump"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b2-0002"",
                    ""path"": ""<Gamepad>/buttonSouth"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Jump"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b3-0001"",
                    ""path"": ""<Keyboard>/j"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Attack"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b3-0002"",
                    ""path"": ""<Gamepad>/buttonWest"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Attack"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b4-0001"",
                    ""path"": ""<Keyboard>/leftShift"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Dash"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b4-0002"",
                    ""path"": ""<Gamepad>/rightShoulder"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Dash"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b5-0001"",
                    ""path"": ""<Keyboard>/e"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Interact"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b5-0002"",
                    ""path"": ""<Gamepad>/buttonNorth"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Interact"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b6-0001"",
                    ""path"": ""<Keyboard>/escape"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard"",
                    ""action"": ""Pause"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b6-0002"",
                    ""path"": ""<Gamepad>/start"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Pause"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": [
        {
            ""name"": ""Keyboard"",
            ""bindingGroup"": ""Keyboard"",
            ""devices"": [
                { ""devicePath"": ""<Keyboard>"", ""isOptional"": false },
                { ""devicePath"": ""<Mouse>"",    ""isOptional"": true  }
            ]
        },
        {
            ""name"": ""Gamepad"",
            ""bindingGroup"": ""Gamepad"",
            ""devices"": [
                { ""devicePath"": ""<Gamepad>"", ""isOptional"": false }
            ]
        }
    ]
}";
    }
}
