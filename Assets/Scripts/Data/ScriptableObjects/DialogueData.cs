// ============================================================================
// DialogueData.cs — 对话数据容器（ScriptableObject）
// ============================================================================
//
//  创建方式：Project 面板右键 → Create → GhostVeil → Dialogue Data
//
//  数据结构：
//    DialogueData
//      └── DialogueLine[]
//            ├── speakerName   （说话人名字）
//            ├── content       （对话内容）
//            ├── portraitKey   （立绘/表情 key，供 UI 切换头像）
//            ├── typeSpeed     （打字速度覆盖，0 = 使用默认）
//            └── autoAdvance   （是否自动翻页）
//

using UnityEngine;

namespace GhostVeil.Data.ScriptableObjects
{
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "GhostVeil/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [Header("=== 对话基本信息 ===")]
        [Tooltip("对话 ID（唯一标识）")]
        public string dialogueID = "dialogue_001";

        [Tooltip("对话标题（编辑器用，运行时不显示）")]
        public string title = "新对话";

        [Header("=== 对话内容 ===")]
        public DialogueLine[] lines = new DialogueLine[]
        {
            new DialogueLine { speakerName = "???", content = "......" }
        };

        [Header("=== 设置 ===")]
        [Tooltip("对话结束后触发的事件 ID（可为空）")]
        public string completionEventID = "";

        [Tooltip("对话期间的控制权级别")]
        public NarrativeAuthorityLevel authorityLevel = NarrativeAuthorityLevel.Dialogue;
    }

    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("说话人名字（空 = 旁白）")]
        public string speakerName = "";

        [Tooltip("对话内容")]
        [TextArea(2, 5)]
        public string content = "";

        [Tooltip("立绘/头像 key（对应 Sprite 名称或路径）")]
        public string portraitKey = "";

        [Tooltip("逐字打印速度（字/秒）。0 = 使用全局默认值")]
        public float typeSpeed = 0f;

        [Tooltip("显示完毕后是否自动翻到下一句（true = 等待 autoAdvanceDelay 后自动翻页）")]
        public bool autoAdvance = false;

        [Tooltip("自动翻页等待时间（秒）")]
        public float autoAdvanceDelay = 2f;
    }
}
