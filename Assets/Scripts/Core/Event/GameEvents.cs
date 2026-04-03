// ============================================================================
// GameEvents.cs — 预定义的全局事件结构体
// ============================================================================
using UnityEngine;
using GhostVeil.Data;

namespace GhostVeil.Core.Event
{
    // ── 角色事件 ───────────────────────────────────────

    public struct CharacterDamagedEvent
    {
        public GameObject Victim;
        public DamagePayload Payload;
    }

    public struct CharacterDiedEvent
    {
        public GameObject Character;
    }

    public struct CharacterStateChangedEvent
    {
        public GameObject Character;
        public int OldStateID;
        public int NewStateID;
    }

    // ── 叙事事件 ───────────────────────────────────────

    /// <summary>叙事系统请求接管控制权</summary>
    public struct NarrativeAuthorityRequestEvent
    {
        public NarrativeAuthorityLevel RequestedLevel;
        public string SequenceID;  // 用于追踪是哪段叙事发出的请求
    }

    /// <summary>叙事系统释放控制权</summary>
    public struct NarrativeAuthorityReleaseEvent
    {
        public NarrativeAuthorityLevel ReleasedLevel;
        public string SequenceID;
    }

    /// <summary>对话行事件（UI 层监听用于显示对话框）</summary>
    public struct DialogueLineEvent
    {
        public string SpeakerName;
        public string Content;
        public string PortraitKey;  // Spine 表情 / 立绘 key
    }

    /// <summary>对话选项结果</summary>
    public struct DialogueChoiceEvent
    {
        public string ChoiceID;
        public int SelectedIndex;
    }

    // ── 交互事件 ───────────────────────────────────────

    /// <summary>玩家进入交互范围</summary>
    public struct InteractionPromptEvent
    {
        public GameObject Interactable;
        public InteractionType Type;
        public bool Show;  // true = 显示提示，false = 隐藏
    }

    // ── 场景 / 关卡事件 ──────────────────────────────────

    public struct LevelTransitionRequestEvent
    {
        public string TargetSceneName;
        public string SpawnPointID;
    }
}
