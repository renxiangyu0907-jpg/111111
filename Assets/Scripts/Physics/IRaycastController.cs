// ============================================================================
// IRaycastController.cs — 射线碰撞控制器接口
// ============================================================================
using UnityEngine;
using GhostVeil.Data;

namespace GhostVeil.Physics
{
    /// <summary>
    /// 纯射线 2D 物理控制器的公共契约。
    /// 替代 Rigidbody2D，提供精确的碰撞检测与移动。
    /// </summary>
    public interface IRaycastController
    {
        /// <summary>本帧的碰撞聚合结果</summary>
        ref CollisionInfo Collisions { get; }

        /// <summary>
        /// 按指定位移量移动角色，内部执行水平 + 垂直射线检测并修正位移。
        /// 调用者传入期望位移，方法返回实际位移（被碰撞修正后的）。
        /// </summary>
        /// <param name="desiredMovement">期望位移（世界空间）</param>
        /// <param name="standingOnPlatform">是否站在移动平台上</param>
        /// <returns>实际移动量</returns>
        Vector2 Move(Vector2 desiredMovement, bool standingOnPlatform = false);

        /// <summary>更新射线原点（每帧在 Move 之前由框架调用）</summary>
        void UpdateRaycastOrigins();

        /// <summary>地面层级掩码（运行时可切换，用于特殊穿透逻辑）</summary>
        LayerMask CollisionMask { get; set; }
    }
}
