// ============================================================================
// IDialogueRunner.cs — 对话运行器接口
// ============================================================================
using System;

namespace GhostVeil.Narrative.Dialogue
{
    /// <summary>
    /// 对话运行器的抽象。
    /// 底层可对接 Yarn Spinner / Ink / 自研 DSL，上层只面向此接口。
    /// </summary>
    public interface IDialogueRunner
    {
        /// <summary>是否正在播放对话</summary>
        bool IsRunning { get; }

        /// <summary>开始一段对话</summary>
        /// <param name="dialogueID">对话资源标识（节点名 / 文件路径）</param>
        void StartDialogue(string dialogueID);

        /// <summary>推进到下一句（玩家确认 / 自动播放）</summary>
        void Advance();

        /// <summary>选择分支选项</summary>
        void SelectChoice(int index);

        /// <summary>强制终止当前对话</summary>
        void Abort();

        /// <summary>对话自然结束时触发</summary>
        event Action OnDialogueCompleted;

        /// <summary>需要显示一行对话时触发（UI 层订阅）</summary>
        event Action<string /*speaker*/, string /*content*/> OnLinePresented;

        /// <summary>需要显示选项时触发（UI 层订阅）</summary>
        event Action<string[] /*choices*/> OnChoicesPresented;
    }
}
