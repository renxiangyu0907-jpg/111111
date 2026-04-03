// ============================================================================
// ISpineBridge.cs — Spine 动画桥接层接口
// ============================================================================
using System;

namespace GhostVeil.Animation
{
    /// <summary>
    /// Spine 动画系统与游戏逻辑之间的桥接接口。
    /// 将 Spine Runtime API 封装为游戏层可理解的语义操作，
    /// 使状态机 / 控制器无需直接依赖 Spine.Unity 命名空间。
    /// </summary>
    public interface ISpineBridge
    {
        // ── 动画播放 ──────────────────────────────────

        /// <summary>播放指定轨道动画（立即切换）</summary>
        /// <param name="trackIndex">Spine 轨道索引（0 = 主体，1 = 上半身覆盖...）</param>
        /// <param name="animationName">Spine 中的动画名称</param>
        /// <param name="loop">是否循环</param>
        /// <param name="mixDuration">过渡融合时长（秒），-1 使用默认 mix</param>
        void PlayAnimation(int trackIndex, string animationName, bool loop, float mixDuration = -1f);

        /// <summary>在当前轨道动画结束后排队播放下一个</summary>
        void QueueAnimation(int trackIndex, string animationName, bool loop, float delay = 0f);

        /// <summary>清空指定轨道</summary>
        void ClearTrack(int trackIndex);

        // ── 动画查询 ──────────────────────────────────

        /// <summary>当前指定轨道正在播放的动画名</summary>
        string GetCurrentAnimation(int trackIndex);

        /// <summary>指定轨道的动画是否已播放完毕（非循环动画用）</summary>
        bool IsAnimationComplete(int trackIndex);

        // ── 动画事件 ──────────────────────────────────

        /// <summary>
        /// Spine 动画事件回调。
        /// eventName 对应 Spine 编辑器中设置的 Event 名称（如 "footstep", "attack_hit"）。
        /// </summary>
        event Action<string /*eventName*/> OnAnimationEvent;

        /// <summary>某轨道动画播放完成（非循环动画结束时触发）</summary>
        event Action<int /*trackIndex*/> OnAnimationComplete;

        // ── Skin / 换装 ──────────────────────────────

        /// <summary>设置组合皮肤（Spine 4.x combineSkins API）</summary>
        /// <param name="skinNames">要组合的皮肤名称列表</param>
        void SetCombinedSkins(params string[] skinNames);

        /// <summary>附加一个皮肤到当前组合皮肤</summary>
        void AddSkin(string skinName);

        // ── IK / 瞄准 ────────────────────────────────

        /// <summary>设置 IK 约束目标位置（世界坐标）</summary>
        /// <param name="constraintName">Spine IK 约束名称</param>
        /// <param name="worldPosition">目标世界坐标</param>
        /// <param name="mix">IK 权重 0~1</param>
        void SetIKTarget(string constraintName, UnityEngine.Vector2 worldPosition, float mix = 1f);

        // ── 材质 / 着色 ──────────────────────────────

        /// <summary>设置 Spine 渲染材质属性（用于受击闪白、URP 自发光等）</summary>
        void SetMaterialProperty(string propertyName, float value);
        void SetMaterialProperty(string propertyName, UnityEngine.Color value);
    }
}
