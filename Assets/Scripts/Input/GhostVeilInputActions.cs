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
            ""id"": ""ecb4fd46-6ce0-4b10-a271-d3e071509f93"",
            ""actions"": [
                {
                    ""name"": ""Move"",
                    ""type"": ""Value"",
                    ""id"": ""956b1d8e-f4b8-473c-9c73-1ecc5a2676dc"",
                    ""expectedControlType"": ""Vector2"",
                    ""interactions"": """",
                    ""processors"": """"
                },
                {
                    ""name"": ""Jump"",
                    ""type"": ""Button"",
                    ""id"": ""3bf9c3ed-d542-4268-b17c-619c72f0d6c3"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                },
                {
                    ""name"": ""Attack"",
                    ""type"": ""Button"",
                    ""id"": ""c0a15d9f-c088-4630-8e2b-5202424a8c2b"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                },
                {
                    ""name"": ""Dash"",
                    ""type"": ""Button"",
                    ""id"": ""8984144a-e44f-4521-925e-e005cfb92716"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                },
                {
                    ""name"": ""Interact"",
                    ""type"": ""Button"",
                    ""id"": ""cc40fdef-014e-497e-8176-efa956a086eb"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                },
                {
                    ""name"": ""Pause"",
                    ""type"": ""Button"",
                    ""id"": ""3a9d43b3-5579-4c09-8bf2-364c94799199"",
                    ""expectedControlType"": ""Button"",
                    ""interactions"": """",
                    ""processors"": """"
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""WASD"",
                    ""id"": ""92704ffd-de80-4e0c-b72a-2afd0b94b155"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""f83b962d-027e-4894-9448-47f8325dd385"",
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
                    ""id"": ""1130e884-7822-4f63-b789-b9c0c974edf6"",
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
                    ""id"": ""c851143a-a72c-43a6-b237-97e452801cd3"",
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
                    ""id"": ""faf0e8d9-ef9d-4844-ac0b-f974b23c7977"",
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
                    ""id"": ""78286c7c-364f-43bb-88ca-9ea880348a06"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""5ce66235-cae2-47cb-a3e2-4d0c4342e099"",
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
                    ""id"": ""8d1397e1-fc1a-44a3-a0a2-7099e1c98067"",
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
                    ""id"": ""b304f40e-fadd-4735-bd77-aec29daa2993"",
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
                    ""id"": ""a46f5c2b-5c4a-411c-946b-c7db94c4dfdb"",
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
                    ""id"": ""72ae853f-3d1f-48d5-9a25-846dcb11af14"",
                    ""path"": ""<Gamepad>/leftStick"",
                    ""interactions"": """",
                    ""processors"": ""StickDeadzone(min=0.125,max=0.925)"",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""6359d46f-51a4-47f5-8a38-cc076ec5c94a"",
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
                    ""id"": ""cec415f7-005a-4051-bcb5-0febee575897"",
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
                    ""id"": ""34db6431-4693-47e1-a55b-d1f49b9984ff"",
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
                    ""id"": ""2515aa4d-a8e4-4b3b-81e7-61b140b28733"",
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
                    ""id"": ""8b9e2ff1-a131-4763-a4c6-4f28f099c2e6"",
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
                    ""id"": ""9a98d221-7eae-4664-bd62-894101f52f06"",
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
                    ""id"": ""4c13ccb9-aca2-4bf7-bfdc-f03dcbcb7350"",
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
                    ""id"": ""67296100-9be0-4a6b-aa05-399c59382a73"",
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
                    ""id"": ""4ade9c29-2ec6-406a-b250-2ac12cb25364"",
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
                    ""id"": ""c037378b-750c-4d6b-8de7-0dda7d2c0408"",
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
