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

        [Header("=== 台阶攀登 (Step-Up) ===")]
        [Tooltip("自动跨越的最大台阶高度（单位）。\n遇到低于此高度的 90 度障碍时，角色自动跨上去而非被挡住。")]
        [SerializeField] private float maxStepHeight = 0.25f;

        [Header("=== 地面吸附 (Ground Snapping) ===")]
        [Tooltip("角色在地面水平移动时，向下搜索地面的最大距离。\n防止在小地形变化处飞起来。")]
        [SerializeField] private float groundSnapDistance = 0.3f;

        // ── 运行时 ────────────────────────────────────
        private float _fallThroughTimer;
        private bool _wasGroundedBeforeMove;
        private bool _didStepUpThisFrame;

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
        //  覆写 Move：增加台阶攀登 + 地面吸附
        // ══════════════════════════════════════════════

        /// <summary>
        /// 覆写基类 Move，增加台阶攀登和地面吸附检测。
        /// 解决：角色在小地形突起处上下抖动的问题。
        /// </summary>
        public override Vector2 Move(Vector2 desiredMovement, bool standingOnPlatform = false)
        {
            _wasGroundedBeforeMove = _collisions.Below;
            _didStepUpThisFrame = false;

            // ── Step 1: 刷新射线原点 ──
            UpdateRaycastOrigins();

            // ── Step 2: 重置碰撞信息 ──
            _collisions.Reset();

            // ── Step 3: 快照原始速度 ──
            _velocityOld = desiredMovement;

            // ── Step 4: 记录水平朝向 ──
            if (desiredMovement.x != 0)
                _collisions.FaceDir = (int)Mathf.Sign(desiredMovement.x);

            Vector2 movement = desiredMovement;

            // ── Step 5: 下坡检测 ──
            if (movement.y < 0)
                DescendSlope(ref movement);

            // ── Step 6: 水平碰撞检测（含台阶攀登） ──
            bool didStepUp = false;
            if (movement.x != 0)
            {
                HorizontalCollisions(ref movement);
                didStepUp = _didStepUpThisFrame;
            }

            // ── Step 7: 垂直碰撞检测 ──
            // 台阶跨越后 movement.y > 0，VerticalCollisions 会检测天花板而非地面，
            // 导致 Below 被覆写为 false。跳过垂直检测，保留 TryStepUp 设置的 Below=true。
            if (!didStepUp)
            {
                VerticalCollisions(ref movement);
            }

            // ── Step 8: 地面吸附 ──
            // 条件：移动前在地面上、不在上升中、本帧垂直检测没命中地面、未执行台阶攀登
            // 改进：去掉 "必须有水平移动" 的限制。拼接地面的接缝处，即使站立不动，
            // 垂直射线也可能全部落入间隙导致 Below=false。此时仍需吸附。
            if (!didStepUp && _wasGroundedBeforeMove && !_collisions.Below
                && movement.y <= 0.001f)
            {
                SnapToGround(ref movement);
            }

            // ── Step 9: 应用最终位移 ──
            transform.Translate(movement);

            // ── Step 10: 台阶攀登后补充一次向下探测 ──
            // 确保跨上台阶后角色紧贴地面，不会悬浮
            if (didStepUp)
            {
                UpdateRaycastOrigins(); // 位移后刷新射线原点
                SnapToGroundAfterStepUp();
            }

            // ── Step 11: 移动平台强制着地 ──
            if (standingOnPlatform)
                _collisions.Below = true;

            return movement;
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

            // ── 台阶攀登预检测 ────────────────────────────
            bool didStepUp = false;
            if (maxStepHeight > 0f && _wasGroundedBeforeMove)
            {
                didStepUp = TryStepUp(ref movement, dirX, rayLength);
            }
            if (didStepUp) return;

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
        //  台阶攀登 (Step-Up)
        // ══════════════════════════════════════════════

        /// <summary>
        /// 尝试自动跨越低矮台阶。
        /// 原理：最底部射线碰到角度 > maxSlopeAngle 的面 (垂直墙) 时，
        /// 从上方探测台阶顶面，若高度在 maxStepHeight 内则自动跨越。
        /// </summary>
        private bool TryStepUp(ref Vector2 movement, float dirX, float rayLength)
        {
            _didStepUpThisFrame = false;

            // 1. 最底部水平射线检测
            Vector2 bottomOrigin = (dirX < 0) ? _raycastOrigins.BottomLeft : _raycastOrigins.BottomRight;
            RaycastHit2D bottomHit = Physics2D.Raycast(bottomOrigin, Vector2.right * dirX, rayLength, collisionMask);

            if (!bottomHit || bottomHit.distance == 0) return false;

            float slopeAngle = Vector2.Angle(bottomHit.normal, Vector2.up);
            // 只处理超过最大坡度的面（近乎垂直的台阶面）
            if (slopeAngle <= maxSlopeAngle) return false;

            // 2. 从障碍点上方 maxStepHeight 处向前探测
            Vector2 stepCheckOrigin = bottomOrigin + Vector2.up * maxStepHeight;
            RaycastHit2D upperHit = Physics2D.Raycast(
                stepCheckOrigin, Vector2.right * dirX, rayLength, collisionMask);

            Debug.DrawRay(stepCheckOrigin, Vector2.right * dirX * rayLength, Color.yellow);

            if (upperHit && upperHit.distance <= bottomHit.distance + skinWidth)
            {
                // 上方也被挡住 -> 不是台阶，是墙壁
                return false;
            }

            // 3. 从台阶上方向下发射射线，找到台阶顶面
            float forwardDist = bottomHit.distance;
            Vector2 aboveStepOrigin = bottomOrigin
                + Vector2.up * (maxStepHeight + skinWidth)
                + Vector2.right * dirX * (forwardDist + skinWidth * 2f);

            RaycastHit2D downHit = Physics2D.Raycast(
                aboveStepOrigin, Vector2.down,
                maxStepHeight + skinWidth * 2f, collisionMask | oneWayPlatformMask);

            Debug.DrawRay(aboveStepOrigin, Vector2.down * (maxStepHeight + skinWidth * 2f), Color.magenta);

            if (!downHit) return false;

            // 4. 计算台阶高度
            float stepHeight = maxStepHeight + skinWidth - downHit.distance;
            if (stepHeight <= 0f || stepHeight > maxStepHeight) return false;

            // 5. 执行台阶跨越：修正 movement
            movement.y = Mathf.Max(movement.y, stepHeight + skinWidth);

            // 6. 限制水平位移，不能超过碰撞点（防止穿墙）
            movement.x = (bottomHit.distance - skinWidth) * dirX
                         + dirX * skinWidth * 2f; // 多走一点，确保站上台阶顶面

            // 标记为着地（跨台阶应视为地面行走，不触发 Fall 状态）
            _collisions.Below = true;
            _collisions.GroundNormal = downHit.normal;
            _collisions.GroundCollider = downHit.collider;
            _didStepUpThisFrame = true;

            return true;
        }

        // ══════════════════════════════════════════════
        //  地面吸附 (Ground Snapping)
        // ══════════════════════════════════════════════

        /// <summary>
        /// 角色在地面水平移动时，向下搜索地面并吸附。
        /// 防止在小地形高低差处飞起来导致状态机抖动。
        /// 
        /// 改进：发射多根射线（而非单根中心射线），
        /// 防止所有射线恰好落在拼接地形的接缝上而全部漏检。
        /// </summary>
        private void SnapToGround(ref Vector2 movement)
        {
            if (groundSnapDistance <= 0f) return;

            LayerMask snapMask = collisionMask | oneWayPlatformMask;
            float bestDist = float.MaxValue;
            RaycastHit2D bestHit = default;

            // 使用与垂直碰撞相同的多射线布局，增强抗接缝能力
            for (int i = 0; i < verticalRayCount; i++)
            {
                Vector2 rayOrigin = _raycastOrigins.BottomLeft
                    + Vector2.right * (_verticalRaySpacing * i)
                    + movement; // 从移动修正后的位置发射

                RaycastHit2D hit = Physics2D.Raycast(
                    rayOrigin, Vector2.down, groundSnapDistance, snapMask);

                Debug.DrawRay(rayOrigin, Vector2.down * groundSnapDistance, Color.blue);

                if (hit && hit.distance > 0f && hit.distance < bestDist)
                {
                    float snapAngle = Vector2.Angle(hit.normal, Vector2.up);
                    if (snapAngle <= maxSlopeAngle)
                    {
                        bestDist = hit.distance;
                        bestHit = hit;
                    }
                }
            }

            if (bestHit && bestDist > skinWidth)
            {
                // 吸附到地面
                movement.y = -(bestDist - skinWidth);

                _collisions.Below = true;
                _collisions.GroundNormal = bestHit.normal;
                _collisions.GroundCollider = bestHit.collider;
            }
        }

        /// <summary>
        /// 台阶攀登后的地面吸附。
        /// 跨上台阶后角色可能悬浮在台阶顶面上方，需要向下探测并吸附。
        /// 在 transform.Translate(movement) 之后调用。
        /// </summary>
        private void SnapToGroundAfterStepUp()
        {
            Vector2 center = (_raycastOrigins.BottomLeft + _raycastOrigins.BottomRight) * 0.5f;
            float probeDistance = maxStepHeight + skinWidth * 4f;

            RaycastHit2D hit = Physics2D.Raycast(
                center, Vector2.down, probeDistance, collisionMask | oneWayPlatformMask);

            Debug.DrawRay(center, Vector2.down * probeDistance, Color.cyan);

            if (hit && hit.distance > skinWidth)
            {
                float snapAngle = Vector2.Angle(hit.normal, Vector2.up);
                if (snapAngle <= maxSlopeAngle)
                {
                    // 直接移动 transform（已经在 Translate 之后）
                    float snapDist = hit.distance - skinWidth;
                    transform.Translate(Vector2.down * snapDist);

                    _collisions.Below = true;
                    _collisions.GroundNormal = hit.normal;
                    _collisions.GroundCollider = hit.collider;
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

            // ── 关键修正 ────────────────────────────────────
            // ClimbSlope 可能将 movement.y 设为正值（哪怕只是微小斜面），
            // 如果角色之前在地面上且 movement.y 很小（坡道爬升量），
            // 强制进入地面探测模式，避免仅检测天花板而遗漏地面。
            bool climbingSmallSlope = _collisions.ClimbingSlope
                                      && movement.y > 0f
                                      && movement.y < maxStepHeight
                                      && _wasGroundedBeforeMove;

            float dirY;
            float rayLength;

            // ── 最小探测距离 ────────────────────────────────
            // 拼接地面（多个 BoxCollider2D 拼接，Edge Radius=0）的接缝处，
            // 射线可能落在两个碰撞体之间的微小间隙中。
            // 保证向下探测长度至少为 skinWidth * 5，即使 movement.y 很小也不会漏检。
            const float minGroundProbeLength = 0.08f; // ≈ skinWidth(0.015) * 5.3

            if (isGroundProbeOnly)
            {
                dirY = -1f;              // 固定向下探测
                rayLength = Mathf.Max(skinWidth * 3f, minGroundProbeLength);
            }
            else
            {
                dirY = Mathf.Sign(movement.y);
                rayLength = Mathf.Abs(movement.y) + skinWidth;
                // 向下探测时，保证最小探测距离，防止因 movement.y 极小而漏检地面接缝
                if (dirY < 0f)
                    rayLength = Mathf.Max(rayLength, minGroundProbeLength);
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
                    // 不要 return —— 继续检测其余射线以找到最近命中点，
                    // 用于后续坡道过渡修正。但标记已设置，不影响着地判定。
                    continue;
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

            // ── 爬小坡时补充地面探测 ────────────────────────
            // ClimbSlope 将 movement.y 设为正值，导致上面只检测了天花板。
            // 角色实际上还在地面上行走（只是地面微微倾斜），
            // 需要补充一次向下探测以维持 Below = true。
            if (climbingSmallSlope && !_collisions.Below)
            {
                LayerMask downMask = collisionMask;
                if (!_collisions.FallingThroughPlatform)
                    downMask |= oneWayPlatformMask;

                float probeLength = movement.y + skinWidth * 3f;
                for (int i = 0; i < verticalRayCount; i++)
                {
                    Vector2 rayOrigin = _raycastOrigins.BottomLeft
                        + Vector2.right * (_verticalRaySpacing * i + movement.x)
                        + Vector2.up * movement.y; // 从爬坡后的位置向下探测

                    RaycastHit2D hit = Physics2D.Raycast(
                        rayOrigin, Vector2.down, probeLength, downMask);

                    Debug.DrawRay(rayOrigin, Vector2.down * probeLength, Color.white);

                    if (hit && hit.distance > 0f)
                    {
                        float snapAngle = Vector2.Angle(hit.normal, Vector2.up);
                        if (snapAngle <= maxSlopeAngle)
                        {
                            _collisions.Below = true;
                            _collisions.GroundCollider = hit.collider;
                            _collisions.GroundNormal = hit.normal;
                            break; // 探测到地面即可
                        }
                    }
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
