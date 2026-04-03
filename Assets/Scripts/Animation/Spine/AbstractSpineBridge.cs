// ============================================================================
// AbstractSpineBridge.cs — Spine 动画桥接层抽象基类
// ============================================================================
using System;
using UnityEngine;

namespace GhostVeil.Animation.Spine
{
    /// <summary>
    /// ISpineBridge 的 MonoBehaviour 抽象基类。
    /// 
    /// 设计意图：
    /// - 封装 Spine Runtime 的具体 API（SkeletonAnimation / SkeletonMecanim），
    ///   使上层只通过 ISpineBridge 接口通信。
    /// - 子类（SpineSkeletonAnimBridge）负责对接真实的 Spine.Unity 组件。
    /// - 提供公共的事件转发管线和缓存机制。
    /// 
    /// 为什么是抽象类而非直接实现？
    /// → 项目可能同时使用 SkeletonAnimation（大部分角色）
    ///   和 SkeletonGraphic（UI 立绘 / 对话头像），需要两套子类。
    /// </summary>
    public abstract class AbstractSpineBridge : MonoBehaviour, ISpineBridge
    {
        // ── 事件 ──────────────────────────────────────
        public event Action<string> OnAnimationEvent;
        public event Action<int>    OnAnimationComplete;

        // ── 子类触发事件的辅助方法 ────────────────────
        protected void RaiseAnimationEvent(string eventName)
        {
            OnAnimationEvent?.Invoke(eventName);
        }

        protected void RaiseAnimationComplete(int trackIndex)
        {
            OnAnimationComplete?.Invoke(trackIndex);
        }

        // ── ISpineBridge 抽象方法（子类必须实现） ──────
        public abstract void PlayAnimation(int trackIndex, string animationName, bool loop, float mixDuration = -1f);
        public abstract void QueueAnimation(int trackIndex, string animationName, bool loop, float delay = 0f);
        public abstract void ClearTrack(int trackIndex);
        public abstract string GetCurrentAnimation(int trackIndex);
        public abstract bool IsAnimationComplete(int trackIndex);
        public abstract void SetCombinedSkins(params string[] skinNames);
        public abstract void AddSkin(string skinName);
        public abstract void SetIKTarget(string constraintName, Vector2 worldPosition, float mix = 1f);
        public abstract void SetMaterialProperty(string propertyName, float value);
        public abstract void SetMaterialProperty(string propertyName, Color value);
    }
}
