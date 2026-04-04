// ============================================================================
// NarrativeController.cs — 叙事系统控制器（控制权仲裁 + 输入锁定）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  核心职责：                                                              │
// │                                                                          │
// │  1. 管理"谁在控制角色"—— 玩家 / 对话 / 过场 / QTE                     │
// │  2. 请求控制权时：锁定 InputSystemProvider、强制角色进入 Idle             │
// │  3. 释放控制权时：解锁输入、恢复玩家操控                                │
// │  4. 发布 NarrativeAuthorityRequestEvent / ReleaseEvent 供全局监听        │
// │                                                                          │
// │  使用方式：                                                               │
// │    var ctrl = FindObjectOfType<NarrativeController>();                   │
// │    ctrl.RequestAuthority(NarrativeAuthorityLevel.Dialogue, "npc_01");   │
// │    // ... 对话结束后 ...                                                  │
// │    ctrl.ReleaseAuthority("npc_01");                                     │
// └──────────────────────────────────────────────────────────────────────────┘

using System;
using UnityEngine;
using GhostVeil.Data;
using GhostVeil.Core.Event;
using GhostVeil.Input;
using GhostVeil.Character.Player;

namespace GhostVeil.Narrative.Controller
{
    public class NarrativeController : MonoBehaviour, INarrativeController
    {
        // ══════════════════════════════════════════════
        //  Inspector
        // ══════════════════════════════════════════════

        [Header("=== 引用（留空自动查找） ===")]
        [SerializeField] private Component inputProviderRef;
        [SerializeField] private PlayerController playerControllerRef;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private IInputProvider _input;
        private PlayerController _player;
        private NarrativeAuthorityLevel _currentAuthority = NarrativeAuthorityLevel.None;
        private string _currentSequenceID;

        // ══════════════════════════════════════════════
        //  INarrativeController 实现
        // ══════════════════════════════════════════════

        public NarrativeAuthorityLevel CurrentAuthority => _currentAuthority;

        public bool IsPlayerMovementAllowed =>
            _currentAuthority <= NarrativeAuthorityLevel.Ambient;

        public bool IsPlayerCombatAllowed =>
            _currentAuthority <= NarrativeAuthorityLevel.Ambient;

        public event Action<NarrativeAuthorityLevel, NarrativeAuthorityLevel> OnAuthorityChanged;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            // 查找输入源
            if (inputProviderRef != null)
                _input = inputProviderRef as IInputProvider;
            if (_input == null)
            {
                var provider = FindObjectOfType<InputSystemProvider>();
                _input = provider;
            }

            // 查找 PlayerController
            _player = playerControllerRef;
            if (_player == null)
                _player = FindObjectOfType<PlayerController>();
        }

        // ══════════════════════════════════════════════
        //  控制权管理
        // ══════════════════════════════════════════════

        /// <summary>
        /// 请求接管控制权。
        /// 
        /// 规则：
        ///   · 请求级别 >= 当前级别 → 生效，返回 true
        ///   · 请求级别 <  当前级别 → 拒绝，返回 false
        ///   · Dialogue 级别：锁定移动，但 UI 输入（确认/选择）仍可用
        ///   · Cutscene 级别：完全锁定所有输入
        /// </summary>
        public bool RequestAuthority(NarrativeAuthorityLevel level, string sequenceID)
        {
            if (level < _currentAuthority)
            {
                Debug.LogWarning($"[NarrativeController] 控制权请求被拒绝：" +
                    $"请求 {level}，当前 {_currentAuthority}（{_currentSequenceID}）");
                return false;
            }

            var oldLevel = _currentAuthority;
            _currentAuthority = level;
            _currentSequenceID = sequenceID;

            // ── 锁定输入 ────────────────────────────────
            ApplyAuthorityEffects(level);

            // ── 发布事件 ────────────────────────────────
            GameEvent.Publish(new NarrativeAuthorityRequestEvent
            {
                RequestedLevel = level,
                SequenceID = sequenceID
            });

            OnAuthorityChanged?.Invoke(oldLevel, level);

            Debug.Log($"[NarrativeController] 控制权已接管：{level}（{sequenceID}）");
            return true;
        }

        /// <summary>
        /// 释放控制权，恢复玩家操控。
        /// 只有持有当前 sequenceID 的调用者才能释放。
        /// </summary>
        public void ReleaseAuthority(string sequenceID)
        {
            if (_currentSequenceID != sequenceID && !string.IsNullOrEmpty(_currentSequenceID))
            {
                Debug.LogWarning($"[NarrativeController] 释放控制权失败：" +
                    $"当前持有者为 {_currentSequenceID}，请求释放者为 {sequenceID}");
                return;
            }

            var oldLevel = _currentAuthority;
            _currentAuthority = NarrativeAuthorityLevel.None;
            _currentSequenceID = null;

            // ── 解锁输入 ────────────────────────────────
            if (_input != null)
                _input.InputLocked = false;

            // ── 发布事件 ────────────────────────────────
            GameEvent.Publish(new NarrativeAuthorityReleaseEvent
            {
                ReleasedLevel = oldLevel,
                SequenceID = sequenceID
            });

            OnAuthorityChanged?.Invoke(oldLevel, NarrativeAuthorityLevel.None);

            Debug.Log($"[NarrativeController] 控制权已释放：{oldLevel}（{sequenceID}）");
        }

        // ══════════════════════════════════════════════
        //  内部逻辑
        // ══════════════════════════════════════════════

        /// <summary>
        /// 根据控制权级别应用效果。
        /// </summary>
        private void ApplyAuthorityEffects(NarrativeAuthorityLevel level)
        {
            switch (level)
            {
                case NarrativeAuthorityLevel.None:
                    if (_input != null) _input.InputLocked = false;
                    break;

                case NarrativeAuthorityLevel.Ambient:
                    // 环境叙事不锁输入
                    break;

                case NarrativeAuthorityLevel.Dialogue:
                    // 对话：锁定移动输入，强制 Idle
                    if (_input != null) _input.InputLocked = true;
                    ForcePlayerIdle();
                    break;

                case NarrativeAuthorityLevel.Cutscene:
                case NarrativeAuthorityLevel.Scripted:
                    // 过场/脚本：完全锁定
                    if (_input != null) _input.InputLocked = true;
                    ForcePlayerIdle();
                    break;
            }
        }

        /// <summary>
        /// 强制角色进入 Idle 状态并清零速度。
        /// </summary>
        private void ForcePlayerIdle()
        {
            if (_player == null) return;

            // 清零速度，防止角色惯性滑动
            _player.Velocity = Vector2.zero;

            // 强制切换到 Idle 状态
            _player.StateMachine?.ForceTransition(BuiltInStateID.Idle);
        }
    }
}
