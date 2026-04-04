// ============================================================================
// PlayerRaycastController.cs — 玩家专用射线碰撞控制器（完整实现）
// ============================================================================
//
// 设计文档：
// ──────────────────────────────────────────────────────────────────────────
// 本类是整个游戏物理手感的基石。所有移动都经过这里的射线检测与修正，
// 确保角色在任何地形上都表现出确定性的、像素级精准的运动。
//
// 射线布局示意（以 4 根水平射线、4 根垂直射线为例）：
//
//        TL ────────── TR          ← 顶部垂直射线组（检测头顶碰撞）
//        │  ↑  ↑  ↑  ↑  │
//        │              │
//   → →  │              │  ← ←    ← 水平射线组（检测左右墙壁）
//   → →  │              │  ← ←
//   → →  │              │  ← ←
//   → →  │              │  ← ←
//        │              │
//        │  ↓  ↓  ↓  ↓  │
//        BL ────────── BR          ← 底部垂直射线组（检测地面 / 坡道）
//
//        ← skinWidth →              射线从 bounds 内缩 skinWidth 处发出
//
// 管线执行顺序（由 AbstractRaycastController.Move 驱动）：
//   1. DescendSlope   — 下坡贴地
//   2. Horizontal     — 撞墙 + 上坡入口检测
//   3. Vertical       — 落地 / 撞头 + 上坡顶部修正
//
// 坡道处理核心思路：
//   · 上坡：水平射线命中倾斜面 → 计算角度 → 将水平速度分解为沿坡面的 X/Y
//   · 下坡：向下发射中心射线 → 检测脚下坡面 → 将水平速度映射为沿坡面下滑
//   · 坡道 → 坡道过渡：当 previousSlopeAngle != currentSlopeAngle 时
//     使用原始 velocityOld.x 重新分解，避免速度丢失
//
// 单向平台逻辑：
//   · 向上移动时（movement.y > 0）：垂直射线忽略 oneWayPlatformMask
//   · FallingThroughPlatform = true 时：垂直射线也忽略 oneWayPlatformMask
//   · 外部（状态机）设置 FallingThroughPlatform，控制器内用计时器自动重置
// ──────────────────────────────────────────────────────────────────────────

using UnityEngine;
using GhostVeil.Data;

namespace GhostVeil.Physics
{
    public class PlayerRaycastController : AbstractRaycastController
    {
        // ══════════════════════════════════════════════
        //  额外 Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== One-Way Platform ===")]
        [Tooltip("下落穿透单向平台后的冷却时间（秒），防止立刻重新站上去")]
        [SerializeField] private float fallThroughDuration = 0.15f;

        // ── 运行时 ────────────────────────────────────
        private float _fallThroughTimer;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        protected override void Start()
        {
            base.Start();
            // 初始朝向默认朝右
            _collisions.FaceDir = 1;
        }

        private void Update()
        {
            // 单向平台穿透计时器衰减
            if (_fallThroughTimer > 0f)
            {
                _fallThroughTimer -= Time.deltaTime;
                if (_fallThroughTimer <= 0f)
                {
                    _collisions.FallingThroughPlatform = false;
                    _fallThroughTimer = 0f;
                }
            }
        }

        // ══════════════════════════════════════════════
        //  公共 API：单向平台穿透触发
        // ══════════════════════════════════════════════

        /// <summary>
        /// 外部调用（如"按下+跳跃"时），开始穿透脚下的单向平台。
        /// </summary>
        public void StartFallThroughPlatform()
        {
            _collisions.FallingThroughPlatform = true;
            _fallThroughTimer = fallThroughDuration;
        }

        // ══════════════════════════════════════════════
        //  水平碰撞检测
        // ══════════════════════════════════════════════

        /// <summary>
        /// 从角色行进方向一侧发射水平射线组。
        /// 功能：
        ///   1. 检测墙壁碰撞 → 修正 movement.x（防止卡入墙体）
        ///   2. 最底部射线检测坡道 → 触发 ClimbSlope
        ///   3. 记录 WallCollider 供状态机查询（墙壁滑行等）
        /// </summary>
        protected override void HorizontalCollisions(ref Vector2 movement)
        {
            float dirX = _collisions.FaceDir;
            // 射线长度 = 本帧水平位移量 + skinWidth（确保极小位移也能检测到）
            float rayLength = Mathf.Abs(movement.x) + skinWidth;

            // 极小位移时保证最低检测距离（防止贴墙时射线太短漏检）
            if (Mathf.Abs(movement.x) < skinWidth)
                rayLength = skinWidth * 2f;

            for (int i = 0; i < horizontalRayCount; i++)
            {
                // 射线起点：从行进方向对侧的底角开始，逐条向上排列
                //   向右移动 → 从 BottomRight 开始
                //   向左移动 → 从 BottomLeft 开始
                Vector2 rayOrigin = (dirX < 0) ? _raycastOrigins.BottomLeft : _raycastOrigins.BottomRight;
                rayOrigin += Vector2.up * (_horizontalRaySpacing * i);

                // 水平射线不检测单向平台（单向平台只在垂直方向有效）
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * dirX, rayLength, collisionMask);

                // ── Debug 可视化 ────────────────────────
                Debug.DrawRay(rayOrigin, Vector2.right * dirX * rayLength, Color.red);

                if (!hit) continue;

                // ── 命中处理 ────────────────────────────

                // 如果角色已经在碰撞体内部（hit.distance == 0），跳过
                // 防止从内侧误判碰撞（如穿过移动平台时）
                if (hit.distance == 0) continue;

                // ── 坡道检测（仅最底部射线） ────────────
                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

                if (i == 0 && slopeAngle <= maxSlopeAngle)
                {
                    // 如果正在下坡中途突然遇到上坡（坡道衔接），
                    // 需要先还原下坡修正，再重新计算上坡
                    if (_collisions.DescendingSlope)
                    {
                        _collisions.DescendingSlope = false;
                        movement = _velocityOld;
                    }

                    // 角色与坡面之间的距离（扣除 skinWidth 后才是真实间距）
                    float distanceToSlopeStart = 0f;
                    if (slopeAngle != _collisions.PreviousSlopeAngle)
                    {
                        // 坡度变化（如从平地进入坡道、从缓坡进入陡坡）
                        // 先走到坡面起始点，再开始坡道运动
                        distanceToSlopeStart = hit.distance - skinWidth;
                        movement.x -= distanceToSlopeStart * dirX;
                    }

                    ClimbSlope(ref movement, slopeAngle, hit.normal);

                    // 走完坡道后把之前扣除的距离补回来
                    movement.x += distanceToSlopeStart * dirX;
                }

                // ── 非坡道面 或 非最底部射线 → 墙壁碰撞 ──
                if (!_collisions.ClimbingSlope || slopeAngle > maxSlopeAngle)
                {
                    // 修正水平位移：只能走到碰撞点 - skinWidth 的位置
                    movement.x = (hit.distance - skinWidth) * dirX;

                    // 缩短后续射线长度（已经有更近的碰撞了）
                    rayLength = hit.distance;

                    // 如果正在爬坡且撞墙，需要同步修正 Y 分量
                    // 否则角色会"穿入"墙壁上方
                    if (_collisions.ClimbingSlope)
                    {
                        movement.y = Mathf.Tan(_collisions.SlopeAngle * Mathf.Deg2Rad) * Mathf.Abs(movement.x);
                    }

                    // 标记碰撞方向
                    _collisions.Left  = (dirX < 0);
                    _collisions.Right = (dirX > 0);
                    _collisions.WallCollider = hit.collider;
                }
            }
        }

        // ══════════════════════════════════════════════
        //  垂直碰撞检测
        // ══════════════════════════════════════════════

        /// <summary>
        /// 从角色顶部或底部发射垂直射线组。
        /// 功能：
        ///   1. 检测地面着地 → 修正 movement.y + 标记 Below
        ///   2. 检测头顶碰撞 → 修正 movement.y + 标记 Above
        ///   3. 爬坡修正 → 防止坡顶"飞出去"
        ///   4. 单向平台逻辑 → 向上移动时忽略 / 穿透中忽略
        ///   5. 记录 GroundCollider
        /// </summary>
        protected override void VerticalCollisions(ref Vector2 movement)
        {
            // ── 零速度地面探测模式 ────────────────────────
            // 当 movement.y ≈ 0 时，仍需向下探测以维持 Collisions.Below。
            // 但此模式下只设置碰撞标记，不修正 movement.y，
            // 避免因 (hit.distance - skinWidth) 的微小正值导致角色向上抖动。
            bool isGroundProbeOnly = Mathf.Approximately(movement.y, 0f);

            float dirY;
            float rayLength;

            if (isGroundProbeOnly)
            {
                dirY = -1f;              // 固定向下探测
                rayLength = skinWidth * 3f; // 宽松一点的探测距离
            }
            else
            {
                dirY = Mathf.Sign(movement.y);
                rayLength = Mathf.Abs(movement.y) + skinWidth;
            }

            // ── 构建本帧使用的碰撞掩码 ────────────────
            // 基础掩码 = 实心碰撞层
            LayerMask effectiveMask = collisionMask;

            // 单向平台：仅在向下移动 且 未处于穿透状态时 才检测
            bool includeOneWay = (dirY < 0) && !_collisions.FallingThroughPlatform;
            if (includeOneWay)
                effectiveMask |= oneWayPlatformMask;

            for (int i = 0; i < verticalRayCount; i++)
            {
                // 射线起点：
                //   向下（着地检测） → 从 BottomLeft 开始
                //   向上（撞头检测） → 从 TopLeft 开始
                // 加上本帧已修正的 movement.x 偏移，确保射线位置与水平修正同步
                Vector2 rayOrigin = (dirY < 0) ? _raycastOrigins.BottomLeft : _raycastOrigins.TopLeft;
                rayOrigin += Vector2.right * (_verticalRaySpacing * i + movement.x);

                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * dirY, rayLength, effectiveMask);

                // ── Debug 可视化 ────────────────────────
                Debug.DrawRay(rayOrigin, Vector2.up * dirY * rayLength, Color.green);

                if (!hit) continue;

                // ── 单向平台额外校验 ────────────────────
                // 单向平台只在从上方落下时生效；
                // 如果角色底部已经在平台表面以下（已经穿入），也忽略
                if (IsOneWayPlatform(hit.collider))
                {
                    // 向上跳跃时绝对不挡（理论上 effectiveMask 已排除，双重保险）
                    if (dirY > 0) continue;

                    // 角色从内部穿过时跳过（hit.distance == 0 表示原点在碰撞体内）
                    if (hit.distance == 0) continue;

                    // 穿透状态中跳过
                    if (_collisions.FallingThroughPlatform) continue;
                }

                // ── 命中处理 ────────────────────────────

                if (isGroundProbeOnly)
                {
                    // 纯探测模式：只设标记，不修正 movement
                    // 避免 (hit.distance - skinWidth) 产生微小正值导致角色上下抖动
                    _collisions.Below = true;
                    _collisions.GroundCollider = hit.collider;
                    _collisions.GroundNormal = hit.normal;
                    return; // 探测到地面即可，无需继续
                }

                // 修正垂直位移：只能走到碰撞点 - skinWidth
                movement.y = (hit.distance - skinWidth) * dirY;

                // 缩短后续射线（更近的碰撞优先）
                rayLength = hit.distance;

                // ── 爬坡中的垂直碰撞修正 ────────────────
                // 场景：角色正在爬坡，头顶撞到天花板
                // 此时需要用修正后的 Y 反推 X，防止 X 方向超出
                if (_collisions.ClimbingSlope)
                {
                    movement.x = movement.y / Mathf.Tan(_collisions.SlopeAngle * Mathf.Deg2Rad)
                                 * Mathf.Sign(_velocityOld.x);
                }

                // 标记碰撞方向
                _collisions.Below = (dirY < 0);
                _collisions.Above = (dirY > 0);

                // 记录地面碰撞体
                if (dirY < 0)
                {
                    _collisions.GroundCollider = hit.collider;
                    _collisions.GroundNormal = hit.normal;
                }
            }

            // ── 坡道过渡修正 ────────────────────────────
            // 场景：角色在爬坡途中，坡度突然变化（缓坡 → 陡坡）
            // 需要用一根前方射线提前探测新坡面，修正 X 位移
            if (_collisions.ClimbingSlope)
            {
                float dirX = Mathf.Sign(movement.x);
                rayLength = Mathf.Abs(movement.x) + skinWidth;

                // 从当前修正后的 Y 位置发射一根水平射线
                Vector2 rayOrigin = (dirX < 0 ? _raycastOrigins.BottomLeft : _raycastOrigins.BottomRight)
                                    + Vector2.up * movement.y;

                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * dirX, rayLength, collisionMask);

                if (hit)
                {
                    float newSlopeAngle = Vector2.Angle(hit.normal, Vector2.up);

                    // 坡度确实变化了 → 重新分解速度
                    if (newSlopeAngle != _collisions.SlopeAngle)
                    {
                        movement.x = (hit.distance - skinWidth) * dirX;
                        _collisions.SlopeAngle = newSlopeAngle;
                        _collisions.GroundNormal = hit.normal;
                    }
                }
            }
        }

        // ══════════════════════════════════════════════
        //  上坡处理
        // ══════════════════════════════════════════════

        /// <summary>
        /// 将水平速度分解为沿坡面运动的 X/Y 分量。
        /// 
        /// 物理直觉：
        ///   · 原始水平速度 = moveDistance（标量）
        ///   · 沿坡面的 X 分量 = moveDistance * cos(angle)
        ///   · 沿坡面的 Y 分量 = moveDistance * sin(angle)
        ///   · 这样保证斜面上的"体感速度"与平地一致（等距映射）
        /// 
        /// 坡道上的速度策略：
        ///   · 当前实现 = 等速映射（上坡不减速，保持水平距离一致性）
        ///   · 若需要"上坡减速"效果，可在这里乘以一个 slopeFriction 系数
        /// </summary>
        protected override void ClimbSlope(ref Vector2 movement, float slopeAngle, Vector2 slopeNormal)
        {
            float moveDistance = Mathf.Abs(movement.x);
            float climbY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

            // 如果角色当前 Y 速度已经比爬坡 Y 更大（如跳跃中经过坡道），
            // 则保留跳跃速度，不强制贴地
            if (movement.y > climbY)
                return;

            movement.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(movement.x);
            movement.y = climbY;

            // 爬坡中视为着地（否则状态机会判定为空中）
            _collisions.Below = true;
            _collisions.ClimbingSlope = true;
            _collisions.SlopeAngle = slopeAngle;
            _collisions.GroundNormal = slopeNormal;
        }

        // ══════════════════════════════════════════════
        //  下坡处理
        // ══════════════════════════════════════════════

        /// <summary>
        /// 向下发射一根中心射线探测脚下坡面。
        /// 若检测到下坡（法线偏离角色行进方向反侧），
        /// 将水平速度映射为沿坡面下滑的 X/Y 分量，实现"紧贴坡面"效果。
        /// 
        /// 触发条件：movement.y &lt; 0（重力作用下自然下落）
        /// 不触发的情况：跳跃上升中、角色被向上弹射
        /// 
        /// 为什么需要下坡贴地？
        ///   如果不做处理，角色走下坡时每帧会"微跳"——
        ///   水平位移把角色推出坡面，然后重力把角色拉回来，
        ///   导致 grounded 每隔一帧就闪烁，动画和跳跃判定全部抽搐。
        /// </summary>
        protected override void DescendSlope(ref Vector2 movement)
        {
            // ── 从角色底部两个角分别向下发射探测射线 ────
            // 用两侧射线而非中心点，处理坡面边缘更鲁棒
            RaycastHit2D hitLeft = Physics2D.Raycast(
                _raycastOrigins.BottomLeft, Vector2.down,
                Mathf.Abs(movement.y) + skinWidth, collisionMask | oneWayPlatformMask);

            RaycastHit2D hitRight = Physics2D.Raycast(
                _raycastOrigins.BottomRight, Vector2.down,
                Mathf.Abs(movement.y) + skinWidth, collisionMask | oneWayPlatformMask);

            // 只有一侧命中 → 可能在坡道边缘，用 SlideDownSlope 处理
            if (hitLeft ^ hitRight)
            {
                SlideDownSingleHit(ref movement, hitLeft ? hitLeft : hitRight);
                return;
            }

            // 两侧都没命中 → 没有坡面
            if (!hitLeft && !hitRight)
                return;

            // ── 两侧都命中 → 取较近的一侧做坡面判定 ────
            // （如果两侧距离相同，取法线更偏的那个）
            RaycastHit2D primaryHit = (hitLeft.distance <= hitRight.distance) ? hitLeft : hitRight;
            SlideDownMainHit(ref movement, primaryHit);
        }

        // ── 下坡辅助：标准下坡（两侧都有接触） ───────────
        private void SlideDownMainHit(ref Vector2 movement, RaycastHit2D hit)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            // 平地（角度 ≈ 0）不算下坡
            if (slopeAngle == 0) return;

            // 角度超过最大可行走坡度 → 不做贴地（角色应滑落，由状态机处理）
            if (slopeAngle > maxSlopeAngle) return;

            // 法线 X 分量的符号 = 坡面朝向
            // 只有当水平移动方向与坡面倾斜方向一致时才是"下坡"
            // （朝坡面低处走 = 下坡；朝高处走 = 上坡，上坡由 HorizontalCollisions 处理）
            float normalDirX = Mathf.Sign(hit.normal.x);

            if (normalDirX != Mathf.Sign(movement.x) && movement.x != 0)
                return; // 朝高处走，不是下坡

            float moveDistance = Mathf.Abs(movement.x);
            float descendY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

            // 检查：如果射线命中距离 > 预期下降量，说明角色还在坡面上方较远处
            // 此时不应强制贴地（可能是刚从高处落到坡面附近）
            if (hit.distance - skinWidth > descendY)
                return;

            movement.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(movement.x);
            movement.y -= descendY;

            _collisions.SlopeAngle = slopeAngle;
            _collisions.DescendingSlope = true;
            _collisions.Below = true;
            _collisions.GroundNormal = hit.normal;
            _collisions.GroundCollider = hit.collider;
        }

        // ── 下坡辅助：单侧命中（坡道边缘情况） ──────────
        private void SlideDownSingleHit(ref Vector2 movement, RaycastHit2D hit)
        {
            if (!hit) return;

            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle == 0) return;
            if (slopeAngle > maxSlopeAngle) return;

            // 单侧命中时：法线 X 方向必须与水平移动方向一致才是下坡
            if (Mathf.Sign(hit.normal.x) != Mathf.Sign(movement.x) && movement.x != 0)
                return;

            float moveDistance = Mathf.Abs(movement.x);
            float descendY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

            if (hit.distance - skinWidth > descendY)
                return;

            movement.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(movement.x);
            movement.y -= descendY;

            _collisions.SlopeAngle = slopeAngle;
            _collisions.DescendingSlope = true;
            _collisions.Below = true;
            _collisions.GroundNormal = hit.normal;
            _collisions.GroundCollider = hit.collider;
        }

        // ══════════════════════════════════════════════
        //  辅助方法
        // ══════════════════════════════════════════════

        /// <summary>判断碰撞体是否属于单向平台层</summary>
        private bool IsOneWayPlatform(Collider2D collider)
        {
            // 将碰撞体所在 layer 转为 LayerMask 位掩码，与 oneWayPlatformMask 做 AND
            return (oneWayPlatformMask.value & (1 << collider.gameObject.layer)) != 0;
        }

        // ══════════════════════════════════════════════
        //  Debug 辅助（仅 Editor）
        // ══════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_boxCollider == null) return;

            Bounds bounds = _boxCollider.bounds;
            bounds.Expand(skinWidth * -2f);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // 半透明橙色
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // 绘制射线原点
            Gizmos.color = Color.cyan;
            float radius = 0.02f;
            for (int i = 0; i < horizontalRayCount; i++)
            {
                float y = bounds.min.y + _horizontalRaySpacing * i;
                Gizmos.DrawSphere(new Vector3(bounds.min.x, y, 0), radius);
                Gizmos.DrawSphere(new Vector3(bounds.max.x, y, 0), radius);
            }
            for (int i = 0; i < verticalRayCount; i++)
            {
                float x = bounds.min.x + _verticalRaySpacing * i;
                Gizmos.DrawSphere(new Vector3(x, bounds.min.y, 0), radius);
                Gizmos.DrawSphere(new Vector3(x, bounds.max.y, 0), radius);
            }
        }
#endif
    }
}
