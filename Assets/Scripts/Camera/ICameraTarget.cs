// ============================================================================
// ICameraTarget.cs — 相机跟踪目标接口
// ============================================================================
using UnityEngine;

namespace GhostVeil.Camera
{
    /// <summary>
    /// Cinemachine 虚拟相机的跟随目标抽象。
    /// 叙事系统可临时将目标切换到 NPC / 场景焦点，
    /// 演出结束后切回玩家。
    /// </summary>
    public interface ICameraTarget
    {
        /// <summary>相机跟随的 Transform</summary>
        Transform FollowTarget { get; }

        /// <summary>相机注视的 Transform（可与 FollowTarget 不同）</summary>
        Transform LookAtTarget { get; }

        /// <summary>请求相机震动</summary>
        /// <param name="intensity">震动强度</param>
        /// <param name="duration">持续时间</param>
        void RequestShake(float intensity, float duration);

        /// <summary>请求相机聚焦到指定世界坐标（平滑移动）</summary>
        void FocusOn(Vector3 worldPosition, float transitionTime);

        /// <summary>恢复默认跟随行为</summary>
        void ResetToDefault();
    }
}
