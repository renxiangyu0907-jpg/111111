// ============================================================================
// NPCInteractable.cs — NPC 对话交互（对接 DialogueRunner 逐字打印系统）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  工作流：                                                                │
// │                                                                          │
// │  1. 玩家走近 NPC → InteractionDetector 检测到 → 显示"按 E 对话"         │
// │  2. 玩家按 E → OnInteract() 触发                                       │
// │  3. 如果有 DialogueData → 启动 DialogueRunner（逐字打印 + 输入锁定）    │
// │     如果没有 DialogueData → 降级为简单行事件模式（兼容旧版）            │
// │  4. 对话进行中再按 E → 推进对话（翻页 / 跳过打字）                     │
// │  5. 对话结束 → 自动释放控制权，恢复玩家操控                             │
// └──────────────────────────────────────────────────────────────────────────┘

using UnityEngine;
using GhostVeil.Core.Event;
using GhostVeil.Data.ScriptableObjects;
using GhostVeil.Narrative.Dialogue;

namespace GhostVeil.Interaction.Samples
{
    /// <summary>
    /// NPC 对话交互 —— 支持 DialogueRunner 逐字打印和简单行事件两种模式。
    /// </summary>
    public class NPCInteractable : AbstractInteractable
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== NPC 信息 ===")]
        [Tooltip("NPC 名称")]
        [SerializeField] private string npcName = "神秘人";

        [Header("=== 对话模式 A：DialogueData（推荐） ===")]
        [Tooltip("对话数据资产（ScriptableObject）。优先使用此模式")]
        [SerializeField] private DialogueData dialogueData;

        [Header("=== 对话模式 B：简单行模式（降级兼容） ===")]
        [Tooltip("NPC 的对话内容（当 DialogueData 为空时使用）")]
        [SerializeField, TextArea(3, 10)]
        private string[] dialogueLines = new string[]
        {
            "你好，旅行者...",
            "这里发生了很多事情。",
            "小心前方。"
        };

        [Header("=== 设置 ===")]
        [Tooltip("对话后是否锁定（一次性对话）")]
        [SerializeField] private bool oneTimeDialogue = false;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private DialogueRunner _runner;
        private int _simpleModeLineIndex;
        private bool _dialogueInProgress;

        // ══════════════════════════════════════════════
        //  初始化
        // ══════════════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();
            interactionType = Data.InteractionType.Dialogue;
            if (string.IsNullOrEmpty(promptText) || promptText == "Interact")
                promptText = "对话";
        }

        private void Start()
        {
            _runner = FindObjectOfType<DialogueRunner>();
        }

        // ══════════════════════════════════════════════
        //  交互入口
        // ══════════════════════════════════════════════

        protected override void OnInteract(GameObject instigator)
        {
            // 如果 DialogueRunner 正在运行 → 推进对话（翻页 / 跳过打字）
            if (_dialogueInProgress && _runner != null && _runner.IsRunning)
            {
                _runner.Advance();
                return;
            }

            // ── 模式 A：使用 DialogueData + DialogueRunner ──
            if (dialogueData != null && _runner != null)
            {
                StartDialogueRunnerMode();
                return;
            }

            // ── 模式 B：降级为简单行事件 ────────────────────
            StartSimpleLineMode();
        }

        // ══════════════════════════════════════════════
        //  模式 A：DialogueRunner 驱动
        // ══════════════════════════════════════════════

        private void StartDialogueRunnerMode()
        {
            _dialogueInProgress = true;

            // 订阅对话完成事件
            _runner.OnDialogueCompleted += OnDialogueFinished;

            // 启动对话
            _runner.StartDialogue(dialogueData);
        }

        private void OnDialogueFinished()
        {
            _dialogueInProgress = false;

            if (_runner != null)
                _runner.OnDialogueCompleted -= OnDialogueFinished;

            if (oneTimeDialogue)
                Lock();

            Debug.Log($"[NPCInteractable] {npcName} 对话结束。");
        }

        // ══════════════════════════════════════════════
        //  模式 B：简单行事件（兼容旧版）
        // ══════════════════════════════════════════════

        private void StartSimpleLineMode()
        {
            if (dialogueLines == null || dialogueLines.Length == 0) return;

            string line = dialogueLines[_simpleModeLineIndex];
            Debug.Log($"[对话] {npcName}: {line}");

            // 发布对话行事件（DialogueBoxUI 监听显示）
            GameEvent.Publish(new DialogueLineEvent
            {
                SpeakerName = npcName,
                Content = line,
                PortraitKey = ""
            });

            // 推进到下一句
            _simpleModeLineIndex++;
            if (_simpleModeLineIndex >= dialogueLines.Length)
            {
                _simpleModeLineIndex = 0;

                if (oneTimeDialogue)
                    Lock();
            }
        }

        // ══════════════════════════════════════════════
        //  清理
        // ══════════════════════════════════════════════

        private void OnDestroy()
        {
            if (_runner != null)
                _runner.OnDialogueCompleted -= OnDialogueFinished;
        }
    }
}
