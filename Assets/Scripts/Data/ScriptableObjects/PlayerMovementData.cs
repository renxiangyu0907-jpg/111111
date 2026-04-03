// ============================================================================
// PlayerMovementData.cs — 玩家移动参数 ScriptableObject（策划可调热重载）
// ============================================================================
using UnityEngine;

namespace GhostVeil.Data.ScriptableObjects
{
    /// <summary>
    /// 集中存放所有移动手感参数。
    /// 挂在 Assets/Data/ 下作为 .asset 文件，Inspector 拖拽到 PlayerController。
    /// 运行时修改立即生效，无需重新编译。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerMovementData", menuName = "GhostVeil/Player Movement Data")]
    public class PlayerMovementData : ScriptableObject
    {
        // ═══════════════════════════════════════════════
        //  水平移动
        // ═══════════════════════════════════════════════

        [Header("=== 水平移动 ===")]
        [Tooltip("最大水平速度（单位/秒）")]
        public float maxRunSpeed = 8f;

        [Tooltip("地面加速时间（秒）—— 从 0 加速到 maxRunSpeed 的时长")]
        [Range(0.01f, 1f)]
        public float groundAccelTime = 0.08f;

        [Tooltip("地面减速时间（秒）—— 从 maxRunSpeed 减速到 0 的时长")]
        [Range(0.01f, 1f)]
        public float groundDecelTime = 0.05f;

        [Tooltip("空中加速时间（秒）—— 空中操控手感，通常比地面慢")]
        [Range(0.01f, 2f)]
        public float airAccelTime = 0.15f;

        [Tooltip("空中减速时间（秒）—— 空中松手后的惯性")]
        [Range(0.01f, 2f)]
        public float airDecelTime = 0.25f;

        // ═══════════════════════════════════════════════
        //  跳跃
        // ═══════════════════════════════════════════════

        [Header("=== 跳跃 ===")]
        [Tooltip("跳跃最大高度（单位）—— 长按跳跃键达到的最高点")]
        public float jumpHeight = 3.5f;

        [Tooltip("到达跳跃顶点的时间（秒）—— 越短手感越\"脆\"")]
        public float timeToJumpApex = 0.4f;

        [Tooltip("松开跳跃键后的速度削减系数 —— 实现小跳。0.5 = 速度减半")]
        [Range(0f, 1f)]
        public float jumpCutMultiplier = 0.5f;

        // ═══════════════════════════════════════════════
        //  跳跃辅助（Jump Buffer + Coyote Time）
        // ═══════════════════════════════════════════════

        [Header("=== 跳跃辅助 ===")]
        [Tooltip("跳跃缓冲时间（秒）—— 落地前这么多秒内按的跳跃会被记住")]
        [Range(0f, 0.3f)]
        public float jumpBufferTime = 0.1f;

        [Tooltip("郊狼时间（秒）—— 离开平台边缘后仍可起跳的窗口")]
        [Range(0f, 0.3f)]
        public float coyoteTime = 0.08f;

        // ═══════════════════════════════════════════════
        //  重力
        // ═══════════════════════════════════════════════

        [Header("=== 重力 ===")]
        [Tooltip("下落重力倍率 —— 下落比上升更快让跳跃更\"有重量感\"")]
        [Range(1f, 5f)]
        public float fallGravityMultiplier = 1.5f;

        [Tooltip("最大下落速度（防止长距离坠落时速度过快穿透地面）")]
        public float maxFallSpeed = -25f;

        // ═══════════════════════════════════════════════
        //  运行时计算属性（由 jumpHeight + timeToJumpApex 推导）
        // ═══════════════════════════════════════════════

        /// <summary>
        /// 上升阶段重力加速度（负值）。
        /// 公式推导：
        ///   h = v0 * t + 0.5 * g * t^2
        ///   在 apex 时 v = 0 → v0 = -g * t
        ///   代入 h = (-g*t)*t + 0.5*g*t^2 = -0.5*g*t^2
        ///   → g = -2h / t^2
        /// </summary>
        public float Gravity => -(2f * jumpHeight) / (timeToJumpApex * timeToJumpApex);

        /// <summary>
        /// 跳跃初速度（正值，向上）。
        /// v0 = -g * t = 2h / t
        /// </summary>
        public float JumpVelocity => (2f * jumpHeight) / timeToJumpApex;

        /// <summary>下落阶段重力 = 基础重力 × 下落倍率</summary>
        public float FallGravity => Gravity * fallGravityMultiplier;
    }
}
