// ============================================================================
// ICutsceneDirector.cs — 过场演出控制器接口
// ============================================================================
using System;

namespace GhostVeil.Narrative.Cutscene
{
    /// <summary>
    /// 过场动画 / 脚本演出的控制器接口。
    /// 底层可对接 Unity Timeline、自研序列化系统或纯代码 Coroutine 编排。
    /// </summary>
    public interface ICutsceneDirector
    {
        /// <summary>是否正在播放过场</summary>
        bool IsPlaying { get; }

        /// <summary>播放指定过场序列</summary>
        /// <param name="cutsceneID">过场资源标识</param>
        /// <param name="onCompleted">播放完成回调（可 null）</param>
        void Play(string cutsceneID, Action onCompleted = null);

        /// <summary>暂停 / 恢复</summary>
        void Pause();
        void Resume();

        /// <summary>跳过当前过场（需玩家确认后调用）</summary>
        void Skip();

        /// <summary>过场播放完毕事件</summary>
        event Action<string /*cutsceneID*/> OnCutsceneCompleted;
    }
}
