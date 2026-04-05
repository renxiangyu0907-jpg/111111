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
// 管线执行顺序（由 Move 方法驱动）：
//   1. DescendSlope   — 下坡贴地
//   2. Horizontal     — 撞墙 + 上坡入口检测（含台阶攀登）
//   3. Vertical       — 纯碰撞修正（撞地/撞头）
//   4. GroundCheck    — 独立地面探测（与碰撞修正分离）
//   5. SnapToGround   — 地面吸附（防止走下小坡时飞起来）
//
// 关键设计决策：
//   · GroundCheck 与 VerticalCollisions 完全分离。
//     VerticalCollisions 只负责修正 movement.y（裁剪碰撞距离），
//     GroundCheck 独立判定 Below 标记。
//   · GroundCheck 仅在 movement.y <= 0（下落/站立）时向下探测，
//     跳跃时（movement.y > 0）直接设 Below=false，
//     避免首帧向下探测到刚离开的地面导致跳跃被取消。
//   · 爬坡（ClimbSlope）期间 movement.y > 0 但角色实际在地面上，
//     GroundCheck 通过检测 ClimbingSlope 标记正确设置 Below=true。
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
        //  覆写 Move：增加台阶攀登 + 独立地面探测 + 地面吸附
        // ══════════════════════════════════════════════

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

            // ── Step 7: 垂直碰撞检测（纯碰撞修正） ──
            // 台阶跨越后跳过垂直检测，保留 TryStepUp 设置的 Below=true。
            if (!didStepUp)
            {
                VerticalCollisions(ref movement);
            }

            // ── Step 7.5: 着地时消除垂直微抖动 ──
            // 当 Below=true 且垂直位移极小时（浮点噪声），钳位为 0。
            if (_collisions.Below && Mathf.Abs(movement.y) < skinWidth)
            {
                movement.y = 0f;
            }

            // ── Step 8: 独立地面探测 ──
            // GroundCheck 与碰撞修正完全分离，确保 Below 标记准确。
            // 仅在非台阶攀登帧执行（台阶攀登已设置了 Below）。
            if (!didStepUp)
            {
                GroundCheck(ref movement);
            }

            // ── Step 9: 地面吸附 ──
            // 条件：移动前在地面、本帧地面检测丢失、不在上升中、未台阶攀登
            if (!didStepUp && _wasGroundedBeforeMove && !_collisions.Below
                && movement.y <= 0.001f)
            {
                SnapToGround(ref movement);
            }

            // ── Step 10: 应用最终位移 ──
            transform.Translate(movement);

            // ── Step 11: 台阶攀登后补充向下探测 ──
            if (didStepUp)
            {
                UpdateRaycastOrigins();
                SnapToGroundAfterStepUp();
            }

            // ── Step 12: 移动平台强制着地 ──
            if (standingOnPlatform)
                _collisions.Below = true;

            return movement;
        }

        // ══════════════════════════════════════════════
        //  独立地面探测 (GroundCheck)
        // ══════════════════════════════════════════════

        /// <summary>
        /// 独立于 VerticalCollisions 的地面状态判定。
        /// 
        /// 核心规则：
        ///   · movement.y <= 0（下落/站立）→ 向下发射短射线探测地面
        ///   · movement.y > 0 且正在爬坡（ClimbingSlope）→ Below = true（坡面行走）
        ///   · movement.y > 0 且非爬坡（跳跃）→ Below = false（空中）
        /// 
        /// 为什么要分离？
        ///   原来 Below 由 VerticalCollisions 根据 dirY 方向判定，
        ///   跳跃首帧 movement.y > 0 → dirY = +1 → 射线朝上 → Below=false 是正确的，
        ///   但 SupplementaryGroundProbe 随后又向下探测到地面 → Below=true → 跳跃被取消。
        ///   分离后，跳跃帧直接 Below=false，无需安全网，彻底消除误判。
        /// </summary>
        private void GroundCheck(ref Vector2 movement)
        {
            // ── 爬坡中：已由 ClimbSlope 设置 Below=true，无需重复探测 ──
            if (_collisions.ClimbingSlope)
                return;

            // ── 下坡中：已由 DescendSlope 设置 Below=true，无需重复探测 ──
            if (_collisions.DescendingSlope)
                return;

            // ── 跳跃中（movement.y > 0 且非坡道）→ 明确为空中 ──
            if (movement.y > skinWidth)
            {
                // 不设 Below=false（可能已被 VerticalCollisions 设为 true，
                // 例如 movement.y 很小时向上射线碰到了天花板附近的面）。
                // 但在真正跳跃时（movement.y 远大于 skinWidth），
                // Below 应该为 false。VerticalCollisions 的 dirY=+1
                // 不会设置 Below=true，所以此处无需操作。
                return;
            }

            // ── 站立/下落（movement.y <= skinWidth）→ 向下探测 ──
            // 探测距离：skinWidth * 3，足以跨越 Tilemap 接缝但不会误判远处地面
            float probeLength = skinWidth * 3f;

            LayerMask probeMask = collisionMask;
            if (!_collisions.FallingThroughPlatform)
                probeMask |= oneWayPlatformMask;

            bool foundGround = false;

            for (int i = 0; i < verticalRayCount; i++)
            {
                Vector2 rayOrigin = _raycastOrigins.BottomLeft
                    + Vector2.right * (_verticalRaySpacing * i + movement.x);

                RaycastHit2D hit = Physics2D.Raycast(
                    rayOrigin, Vector2.down, probeLength, probeMask);

                Debug.DrawRay(rayOrigin, Vector2.down * probeLength, Color.cyan);

                if (hit && hit.distance > 0f)
                {
                    float angle = Vector2.Angle(hit.normal, Vector2.up);
                    if (angle <= maxSlopeAngle)
                    {
                        foundGround = true;
                        _collisions.GroundNormal = hit.normal;
                        _collisions.GroundCollider = hit.collider;
                        break;
                    }
                }
            }

            // 只有在探测到地面时才设 Below=true。
            // 如果 VerticalCollisions 已经设了 Below=true（向下碰撞），不覆盖。
            if (foundGround)
            {
                _collisions.Below = true;
            }
            // 注意：不主动设 Below=false。
            // 如果 VerticalCollisions 已经设了 Below=true，
            // 这里不应该覆盖掉（VerticalCollisions 碰到了地面说明确实着地了）。
        }

        // ══════════════════════════════════════════════
        //  公共 API：单向平台穿透触发
        // ══════════════════════════════════════════════

        public void StartFallThroughPlatform()
        {
            _collisions.FallingThroughPlatform = true;
            _fallThroughTimer = fallThroughDuration;
        }

        // ══════════════════════════════════════════════
        //  水平碰撞检测
        // ══════════════════════════════════════════════

        protected override void HorizontalCollisions(ref Vector2 movement)
        {
            float dirX = _collisions.FaceDir;
            float rayLength = Mathf.Abs(movement.x) + skinWidth;

            if (Mathf.Abs(movement.x) < skinWidth)
                rayLength = skinWidth * 2f;

            // ── 台阶攀登预检测 ──
            bool didStepUp = false;
            if (maxStepHeight > 0f && _wasGroundedBeforeMove)
            {
                didStepUp = TryStepUp(ref movement, dirX, rayLength);
            }
            if (didStepUp) return;

            for (int i = 0; i < horizontalRayCount; i++)
            {
                Vector2 rayOrigin = (dirX < 0) ? _raycastOrigins.BottomLeft : _raycastOrigins.BottomRight;
                rayOrigin += Vector2.up * (_horizontalRaySpacing * i);

                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * dirX, rayLength, collisionMask);

                Debug.DrawRay(rayOrigin, Vector2.right * dirX * rayLength, Color.red);

                if (!hit) continue;
                if (hit.distance == 0) continue;

                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);

                if (i == 0 && slopeAngle <= maxSlopeAngle)
                {
                    if (_collisions.DescendingSlope)
                    {
                        _collisions.DescendingSlope = false;
                        movement = _velocityOld;
                    }

                    float distanceToSlopeStart = 0f;
                    if (slopeAngle != _collisions.PreviousSlopeAngle)
                    {
                        distanceToSlopeStart = hit.distance - skinWidth;
                        movement.x -= distanceToSlopeStart * dirX;
                    }

                    ClimbSlope(ref movement, slopeAngle, hit.normal);
                    movement.x += distanceToSlopeStart * dirX;
                }

                if (!_collisions.ClimbingSlope || slopeAngle > maxSlopeAngle)
                {
                    movement.x = (hit.distance - skinWidth) * dirX;
                    rayLength = hit.distance;

                    if (_collisions.ClimbingSlope)
                    {
                        movement.y = Mathf.Tan(_collisions.SlopeAngle * Mathf.Deg2Rad) * Mathf.Abs(movement.x);
                    }

                    _collisions.Left = (dirX < 0);
                    _collisions.Right = (dirX > 0);
                    _collisions.WallCollider = hit.collider;
                }
            }
        }

        // ══════════════════════════════════════════════
        //  台阶攀登 (Step-Up)
        // ══════════════════════════════════════════════

        private bool TryStepUp(ref Vector2 movement, float dirX, float rayLength)
        {
            _didStepUpThisFrame = false;

            Vector2 bottomOrigin = (dirX < 0) ? _raycastOrigins.BottomLeft : _raycastOrigins.BottomRight;
            RaycastHit2D bottomHit = Physics2D.Raycast(bottomOrigin, Vector2.right * dirX, rayLength, collisionMask);

            if (!bottomHit || bottomHit.distance == 0) return false;

            float slopeAngle = Vector2.Angle(bottomHit.normal, Vector2.up);
            if (slopeAngle <= maxSlopeAngle) return false;

            Vector2 stepCheckOrigin = bottomOrigin + Vector2.up * maxStepHeight;
            RaycastHit2D upperHit = Physics2D.Raycast(
                stepCheckOrigin, Vector2.right * dirX, rayLength, collisionMask);

            Debug.DrawRay(stepCheckOrigin, Vector2.right * dirX * rayLength, Color.yellow);

            if (upperHit && upperHit.distance <= bottomHit.distance + skinWidth)
                return false;

            float forwardDist = bottomHit.distance;
            Vector2 aboveStepOrigin = bottomOrigin
                + Vector2.up * (maxStepHeight + skinWidth)
                + Vector2.right * dirX * (forwardDist + skinWidth * 2f);

            RaycastHit2D downHit = Physics2D.Raycast(
                aboveStepOrigin, Vector2.down,
                maxStepHeight + skinWidth * 2f, collisionMask | oneWayPlatformMask);

            Debug.DrawRay(aboveStepOrigin, Vector2.down * (maxStepHeight + skinWidth * 2f), Color.magenta);

            if (!downHit) return false;

            float stepHeight = maxStepHeight + skinWidth - downHit.distance;
            if (stepHeight <= 0f || stepHeight > maxStepHeight) return false;

            movement.y = Mathf.Max(movement.y, stepHeight + skinWidth);
            movement.x = (bottomHit.distance - skinWidth) * dirX
                         + dirX * skinWidth * 2f;

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
        /// 使用多根射线，防止所有射线恰好落在拼接接缝上而全部漏检。
        /// </summary>
        private void SnapToGround(ref Vector2 movement)
        {
            if (groundSnapDistance <= 0f) return;

            LayerMask snapMask = collisionMask | oneWayPlatformMask;
            float bestDist = float.MaxValue;
            RaycastHit2D bestHit = default;

            for (int i = 0; i < verticalRayCount; i++)
            {
                Vector2 rayOrigin = _raycastOrigins.BottomLeft
                    + Vector2.right * (_verticalRaySpacing * i)
                    + movement;

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
                movement.y = -(bestDist - skinWidth);

                _collisions.Below = true;
                _collisions.GroundNormal = bestHit.normal;
                _collisions.GroundCollider = bestHit.collider;
            }
        }

        /// <summary>
        /// 台阶攀登后的地面吸附。
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
                    float snapDist = hit.distance - skinWidth;
                    transform.Translate(Vector2.down * snapDist);

                    _collisions.Below = true;
                    _collisions.GroundNormal = hit.normal;
                    _collisions.GroundCollider = hit.collider;
                }
            }
        }

        // ══════════════════════════════════════════════
        //  垂直碰撞检测（纯碰撞修正）
        // ══════════════════════════════════════════════

        /// <summary>
        /// 垂直碰撞检测 —— 只负责修正 movement.y，不负责设置 Below。
        /// 
        /// Below 的设置由独立的 GroundCheck() 负责。
        /// 此方法仅在碰撞发生时标记 Above（撞头），
        /// 以及在向下碰撞时暂时标记 Below（用于 Step 7.5 微抖动钳位）。
        /// 最终 Below 状态由 GroundCheck 决定。
        /// </summary>
        protected override void VerticalCollisions(ref Vector2 movement)
        {
            // 零速时仅做向下探测，不修正 movement
            if (Mathf.Approximately(movement.y, 0f))
            {
                // 纯站立帧：不做碰撞修正，Below 由 GroundCheck 设置
                return;
            }

            float dirY = Mathf.Sign(movement.y);
            float rayLength = Mathf.Abs(movement.y) + skinWidth;

            // 碰撞掩码
            LayerMask effectiveMask = collisionMask;
            bool includeOneWay = (dirY < 0) && !_collisions.FallingThroughPlatform;
            if (includeOneWay)
                effectiveMask |= oneWayPlatformMask;

            for (int i = 0; i < verticalRayCount; i++)
            {
                Vector2 rayOrigin = (dirY < 0) ? _raycastOrigins.BottomLeft : _raycastOrigins.TopLeft;
                rayOrigin += Vector2.right * (_verticalRaySpacing * i + movement.x);

                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * dirY, rayLength, effectiveMask);

                Debug.DrawRay(rayOrigin, Vector2.up * dirY * rayLength, Color.green);

                if (!hit) continue;

                // 单向平台额外校验
                if (IsOneWayPlatform(hit.collider))
                {
                    if (dirY > 0) continue;
                    if (hit.distance == 0) continue;
                    if (_collisions.FallingThroughPlatform) continue;
                }

                // 修正垂直位移
                movement.y = (hit.distance - skinWidth) * dirY;
                rayLength = hit.distance;

                // 爬坡中撞头修正
                if (_collisions.ClimbingSlope)
                {
                    movement.x = movement.y / Mathf.Tan(_collisions.SlopeAngle * Mathf.Deg2Rad)
                                 * Mathf.Sign(_velocityOld.x);
                }

                // 标记碰撞方向（Below 在此仅用于 Step 7.5 微抖动钳位参考）
                if (dirY < 0)
                {
                    _collisions.Below = true;
                    _collisions.GroundCollider = hit.collider;
                    _collisions.GroundNormal = hit.normal;
                }
                _collisions.Above = (dirY > 0);
            }

            // 坡道过渡修正
            if (_collisions.ClimbingSlope)
            {
                float dirX = Mathf.Sign(movement.x);
                rayLength = Mathf.Abs(movement.x) + skinWidth;

                Vector2 rayOrigin = (dirX < 0 ? _raycastOrigins.BottomLeft : _raycastOrigins.BottomRight)
                                    + Vector2.up * movement.y;

                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * dirX, rayLength, collisionMask);

                if (hit)
                {
                    float newSlopeAngle = Vector2.Angle(hit.normal, Vector2.up);
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

        protected override void ClimbSlope(ref Vector2 movement, float slopeAngle, Vector2 slopeNormal)
        {
            float moveDistance = Mathf.Abs(movement.x);
            float climbY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

            if (movement.y > climbY)
                return;

            movement.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(movement.x);
            movement.y = climbY;

            _collisions.Below = true;
            _collisions.ClimbingSlope = true;
            _collisions.SlopeAngle = slopeAngle;
            _collisions.GroundNormal = slopeNormal;
        }

        // ══════════════════════════════════════════════
        //  下坡处理
        // ══════════════════════════════════════════════

        protected override void DescendSlope(ref Vector2 movement)
        {
            RaycastHit2D hitLeft = Physics2D.Raycast(
                _raycastOrigins.BottomLeft, Vector2.down,
                Mathf.Abs(movement.y) + skinWidth, collisionMask | oneWayPlatformMask);

            RaycastHit2D hitRight = Physics2D.Raycast(
                _raycastOrigins.BottomRight, Vector2.down,
                Mathf.Abs(movement.y) + skinWidth, collisionMask | oneWayPlatformMask);

            if (hitLeft ^ hitRight)
            {
                SlideDownSingleHit(ref movement, hitLeft ? hitLeft : hitRight);
                return;
            }

            if (!hitLeft && !hitRight)
                return;

            RaycastHit2D primaryHit = (hitLeft.distance <= hitRight.distance) ? hitLeft : hitRight;
            SlideDownMainHit(ref movement, primaryHit);
        }

        private void SlideDownMainHit(ref Vector2 movement, RaycastHit2D hit)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle == 0) return;
            if (slopeAngle > maxSlopeAngle) return;

            float normalDirX = Mathf.Sign(hit.normal.x);
            if (normalDirX != Mathf.Sign(movement.x) && movement.x != 0)
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

        private void SlideDownSingleHit(ref Vector2 movement, RaycastHit2D hit)
        {
            if (!hit) return;

            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle == 0) return;
            if (slopeAngle > maxSlopeAngle) return;

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

        private bool IsOneWayPlatform(Collider2D collider)
        {
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

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

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
