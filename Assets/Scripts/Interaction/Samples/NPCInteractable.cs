// ============================================================================
// NPCInteractable.cs — NPC 对话交互
// ============================================================================
using UnityEngine;
using GhostVeil.Core.Event;

namespace GhostVeil.Interaction.Samples
{
    /// <summary>
    /// NPC 对话交互 —— 按 E 触发对话。
    /// 当前为简单实现（输出对话文本），后续对接 Dialogue System。
    /// </summary>
    public class NPCInteractable : AbstractInteractable
    {
        [Header("=== NPC 信息 ===")]
        [Tooltip("NPC 名称")]
        [SerializeField] private string npcName = "神秘人";

        [Tooltip("NPC 的对话内容（简单模式，一句话）")]
        [SerializeField, TextArea(3, 10)]
        private string[] dialogueLines = new string[]
        {
            "你好，旅行者...",
            "这里发生了很多事情。",
            "小心前方。"
        };

        [Tooltip("对话后是否锁定（一次性对话）")]
        [SerializeField] private bool oneTimeDialogue = false;

        private int _currentLineIndex = 0;

        protected override void Awake()
        {
            base.Awake();
            interactionType = Data.InteractionType.Dialogue;
            if (string.IsNullOrEmpty(promptText) || promptText == "Interact")
                promptText = "对话";
        }

        protected override void OnInteract(GameObject instigator)
        {
            if (dialogueLines == null || dialogueLines.Length == 0) return;

            string line = dialogueLines[_currentLineIndex];
            Debug.Log($"[对话] {npcName}: {line}");

            // 发布对话事件
            GameEvent.Publish(new DialogueLineEvent
            {
                SpeakerName = npcName,
                Content = line,
                PortraitKey = ""
            });

            // 推进到下一句
            _currentLineIndex++;
            if (_currentLineIndex >= dialogueLines.Length)
            {
                _currentLineIndex = 0; // 循环

                if (oneTimeDialogue)
                    Lock();
            }
        }
    }
}
