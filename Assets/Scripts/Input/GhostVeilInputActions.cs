// ============================================================================
// GhostVeilInputActions.cs — 通过 C# API 构建 InputActionAsset（无 JSON）
// ============================================================================
//
// 彻底抛弃 FromJson，改用 InputActionMap / InputAction / InputBinding 的
// 程序化 API 构建所有绑定，避免 JSON 解析器版本兼容问题。
//
// 按键映射：
//   [Gameplay]
//     Move     : Value<Vector2>  — WASD / 方向键 / 左摇杆
//     Jump     : Button          — Space / A(Gamepad)
//     Attack   : Button          — J / X(Gamepad)
//     Dash     : Button          — LeftShift / RB(Gamepad)
//     Interact : Button          — E / Y(Gamepad)
//     Pause    : Button          — Escape / Start(Gamepad)
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostVeil.Input
{
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
        //  构造：通过 C# API 构建（不使用 JSON）
        // ══════════════════════════════════════════════

        public GhostVeilInputActions()
        {
            _asset = ScriptableObject.CreateInstance<InputActionAsset>();
            _asset.name = "GhostVeilInputActions";

            // ── 创建 Gameplay Action Map ──────────────
            var gameplay = new InputActionMap("Gameplay");
            _asset.AddActionMap(gameplay);

            // ── Move: Value<Vector2> ──────────────────
            var move = gameplay.AddAction("Move", InputActionType.Value,
                expectedControlLayout: "Vector2");

            // WASD Composite
            move.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // Arrow Keys Composite
            move.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/upArrow")
                .With("Down",  "<Keyboard>/downArrow")
                .With("Left",  "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            // Gamepad Left Stick
            move.AddBinding("<Gamepad>/leftStick");

            // ── Jump: Button ──────────────────────────
            var jump = gameplay.AddAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            // ── Attack: Button ────────────────────────
            var attack = gameplay.AddAction("Attack", InputActionType.Button);
            attack.AddBinding("<Keyboard>/j");
            attack.AddBinding("<Gamepad>/buttonWest");

            // ── Dash: Button ──────────────────────────
            var dash = gameplay.AddAction("Dash", InputActionType.Button);
            dash.AddBinding("<Keyboard>/leftShift");
            dash.AddBinding("<Gamepad>/rightShoulder");

            // ── Interact: Button ──────────────────────
            var interact = gameplay.AddAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/buttonNorth");

            // ── Pause: Button ─────────────────────────
            var pause = gameplay.AddAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/escape");
            pause.AddBinding("<Gamepad>/start");

            // ── 初始化包装类 ──────────────────────────
            _gameplay = new GameplayActions(_asset);
        }

        // ══════════════════════════════════════════════
        //  启用 / 禁用
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

        public sealed class GameplayActions
        {
            private readonly InputActionMap _map;

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

            public void SetCallbacks(IGameplayActions callbacks)
            {
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

        public interface IGameplayActions
        {
            void OnMove(InputAction.CallbackContext context);
            void OnJump(InputAction.CallbackContext context);
            void OnAttack(InputAction.CallbackContext context);
            void OnDash(InputAction.CallbackContext context);
            void OnInteract(InputAction.CallbackContext context);
            void OnPause(InputAction.CallbackContext context);
        }
    }
}
