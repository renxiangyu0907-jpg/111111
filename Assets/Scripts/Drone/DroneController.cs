// ============================================================================
// DroneController.cs — 跟随无人机核心控制器
// ============================================================================
//
// 功能：
//   1. 延迟跟随 Player（基于时间的位置历史，取 N 秒前的位置作为目标）
//   2. 悬浮动画（Sine 波上下浮动）
//   3. 自主朝向（面向最近敌人，无敌人时跟随移动方向或 Player 朝向）
//   4. 支持自定义 Sprite（Inspector 拖入图片）或代码生成占位外观
//   5. 管理推进器粒子特效（DroneVFX 组件）
//   6. 运动时微倾斜，增强视觉表现
//
// ┌─────────────────────────────────────────────────────────────────┐
// │  图片导入步骤：                                                  │
// │                                                                 │
// │  1. 将 PNG 图片拖入 Unity 的 Assets 文件夹                       │
// │     （推荐放在 Assets/Art/Drone/ 下）                            │
// │                                                                 │
// │  2. 在 Project 窗口选中图片 → Inspector 面板中设置：              │
// │     · Texture Type  →  Sprite (2D and UI)                       │
// │     · Sprite Mode   →  Single（整张图）                          │
// │     · Pixels Per Unit → 32 或 64（控制图片在游戏中的大小）        │
// │       - 值越大，图在游戏中越小                                    │
// │       - 值越小，图在游戏中越大                                    │
// │     · Filter Mode   →  Point（像素风）或 Bilinear（平滑）         │
// │     · 点击右下角 Apply                                           │
// │                                                                 │
// │  3. 在 DroneManager 的 Inspector 面板中：                         │
// │     · 将图片拖到 "Drone Sprite" 字段                             │
// │     · 调整 "Sprite Scale" 控制大小                               │
// │                                                                 │
// │  如果不设置 Sprite，会自动使用代码生成的占位外观。                  │
// └─────────────────────────────────────────────────────────────────┘
//
// 挂载方式：
//   由 DroneManager 在拾取道具时动态创建，不需手动挂载。
//

using UnityEngine;
using System.Collections.Generic;
using GhostVeil.Combat;

namespace GhostVeil.Drone
{
    public class DroneController : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 跟随参数 ===")]
        [Tooltip("跟随延迟时间（秒，越大越 '拖尾'）")]
        [SerializeField] private float followDelayTime = 0.25f;

        [Tooltip("跟随平滑速度（Lerp 插值速度）")]
        [SerializeField] private float followSmoothSpeed = 10f;

        [Tooltip("相对 Player 的偏移（X 为身后方向，Y 为上方）")]
        [SerializeField] private Vector2 followOffset = new Vector2(-0.8f, 1.2f);

        [Header("=== 悬浮动画 ===")]
        [Tooltip("上下浮动幅度")]
        [SerializeField] private float hoverAmplitude = 0.15f;

        [Tooltip("上下浮动频率")]
        [SerializeField] private float hoverFrequency = 2f;

        [Header("=== 外观 ===")]
        [Tooltip("无人机 Sprite（留空则使用代码生成的占位外观）\n" +
                 "导入方法：将 PNG 拖入 Assets → Inspector 中 Texture Type 设为 Sprite → 拖到此字段")]
        [SerializeField] private Sprite droneSprite;

        [Tooltip("Sprite 缩放（调整图片在游戏中的大小）")]
        [SerializeField] private float spriteScale = 1f;

        [Tooltip("Sprite 排序层级")]
        [SerializeField] private int spriteSortingOrder = 100;

        [Header("=== 朝向 ===")]
        [Tooltip("搜索敌人的范围半径")]
        [SerializeField] private float enemySearchRadius = 10f;

        [Tooltip("敌人所在 Layer")]
        [SerializeField] private LayerMask enemyLayerMask;

        [Header("=== 运动倾斜 ===")]
        [Tooltip("最大倾斜角度（度）")]
        [SerializeField] private float maxTiltAngle = 15f;

        [Tooltip("倾斜平滑速度")]
        [SerializeField] private float tiltSmoothSpeed = 8f;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private Transform _player;

        // 基于时间的位置历史（环形缓冲区）
        private struct TimedPosition
        {
            public float Time;
            public Vector2 Position;
        }
        private readonly List<TimedPosition> _positionHistory = new();
        private const int MaxHistorySize = 300; // 最多存 300 条记录（约 5 秒 @ 60fps）

        private float _hoverPhase;
        private int _facingSign = 1; // 1 = right, -1 = left
        private float _currentTilt;
        private Vector3 _previousPosition;

        // 视觉组件
        private SpriteRenderer _bodyRenderer;
        private SpriteRenderer _coreLightRenderer; // 仅占位模式使用
        private Transform _bodyTransform; // body 子物体的 transform，用于倾斜
        private bool _usingCustomSprite;
        private DroneVFX _vfx;
        private DroneWeapon _weapon;

        /// <summary>当前无人机面朝方向 (1=右, -1=左)</summary>
        public int FacingSign => _facingSign;

        /// <summary>当前锁定的敌人 Transform（可为 null）</summary>
        public Transform CurrentTarget { get; private set; }

        /// <summary>所属 Player</summary>
        public Transform Player => _player;

        /// <summary>无人机在编队中的索引（DroneManager 设置）</summary>
        public int FormationIndex { get; set; }

        // ══════════════════════════════════════════════
        //  初始化
        // ══════════════════════════════════════════════

        /// <summary>由 DroneManager 调用初始化</summary>
        /// <param name="player">跟随目标</param>
        /// <param name="formationIndex">编队索引</param>
        /// <param name="customSprite">自定义 Sprite（可选，覆盖 Inspector 设置）</param>
        /// <param name="customScale">自定义缩放（可选，&lt;=0 则用默认值）</param>
        public void Initialize(Transform player, int formationIndex,
                               Sprite customSprite = null, float customScale = -1f)
        {
            // 如果外部传入了 Sprite，覆盖 Inspector 字段
            if (customSprite != null)
                droneSprite = customSprite;
            if (customScale > 0f)
                spriteScale = customScale;

            _player = player;
            FormationIndex = formationIndex;

            // 初始化位置历史：用 player 当前位置填充
            Vector2 startPos = (Vector2)player.position + GetFormationOffset();
            _positionHistory.Clear();
            float now = Time.time;
            // 预填充足够历史数据，覆盖 followDelayTime
            for (int i = 0; i < 10; i++)
            {
                _positionHistory.Add(new TimedPosition
                {
                    Time = now - followDelayTime * (1f - i / 10f),
                    Position = startPos
                });
            }

            // 随机悬浮相位（多个无人机不同步）
            _hoverPhase = Random.Range(0f, Mathf.PI * 2f);

            // 放到 player 身后
            transform.position = startPos;
            _previousPosition = startPos;

            // 创建视觉外观
            CreateVisuals();

            // 添加子组件
            _vfx = gameObject.AddComponent<DroneVFX>();
            _weapon = gameObject.AddComponent<DroneWeapon>();
            _weapon.Initialize(this);

            Debug.Log($"[DroneController] 初始化完成, followDelay={followDelayTime}s, sortingOrder={spriteSortingOrder}");
        }

        // ══════════════════════════════════════════════
        //  每帧更新
        // ══════════════════════════════════════════════

        private void Update()
        {
            if (_player == null) return;

            float dt = Time.deltaTime;

            // ── 1. 记录 Player 位置（含编队偏移）到时间轴 ──
            RecordPlayerPosition();

            // ── 2. 获取延迟目标位置（基于时间） ──
            Vector2 targetPos = GetDelayedTargetPosition();

            // ── 3. 加入悬浮动画 ──
            _hoverPhase += hoverFrequency * dt * Mathf.PI * 2f;
            float hoverY = Mathf.Sin(_hoverPhase) * hoverAmplitude;
            targetPos.y += hoverY;

            // ── 4. 平滑移动到目标位置（使用 Lerp 避免 SmoothDamp 的震荡） ──
            Vector2 currentPos = transform.position;
            float lerpFactor = 1f - Mathf.Exp(-followSmoothSpeed * dt);
            Vector2 newPos = Vector2.Lerp(currentPos, targetPos, lerpFactor);
            transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

            // ── 5. 搜索最近敌人 & 更新朝向 ──
            UpdateTargetAndFacing();

            // ── 6. 运动倾斜 ──
            UpdateTilt(dt);

            // ── 7. 更新核心灯闪烁（仅占位模式） ──
            UpdateCoreLightPulse();

            _previousPosition = transform.position;
        }

        // ══════════════════════════════════════════════
        //  基于时间的位置历史 & 延迟跟随
        // ══════════════════════════════════════════════

        private void RecordPlayerPosition()
        {
            Vector2 targetBase = (Vector2)_player.position + GetFormationOffset();

            _positionHistory.Add(new TimedPosition
            {
                Time = Time.time,
                Position = targetBase
            });

            // 清理过期记录（保留比 followDelayTime 多一些的余量）
            float cutoffTime = Time.time - followDelayTime - 1f;
            while (_positionHistory.Count > 2 && _positionHistory[0].Time < cutoffTime)
            {
                _positionHistory.RemoveAt(0);
            }

            // 限制总条数，防止极端情况下内存泄漏
            while (_positionHistory.Count > MaxHistorySize)
            {
                _positionHistory.RemoveAt(0);
            }
        }

        private Vector2 GetDelayedTargetPosition()
        {
            float targetTime = Time.time - followDelayTime;

            // 在历史中找到最接近 targetTime 的两个点，做插值
            if (_positionHistory.Count == 0)
                return transform.position;

            // 如果 targetTime 比最早的记录还早，返回最早的
            if (targetTime <= _positionHistory[0].Time)
                return _positionHistory[0].Position;

            // 如果 targetTime 比最新的记录还晚（不应该发生），返回最新的
            int last = _positionHistory.Count - 1;
            if (targetTime >= _positionHistory[last].Time)
                return _positionHistory[last].Position;

            // 二分查找或线性查找插值点
            for (int i = 0; i < _positionHistory.Count - 1; i++)
            {
                if (_positionHistory[i].Time <= targetTime && _positionHistory[i + 1].Time >= targetTime)
                {
                    float span = _positionHistory[i + 1].Time - _positionHistory[i].Time;
                    if (span < 0.0001f) return _positionHistory[i].Position;

                    float t = (targetTime - _positionHistory[i].Time) / span;
                    return Vector2.Lerp(_positionHistory[i].Position, _positionHistory[i + 1].Position, t);
                }
            }

            return _positionHistory[last].Position;
        }

        /// <summary>根据编队索引计算偏移</summary>
        private Vector2 GetFormationOffset()
        {
            // 编队模式：奇数在左上、偶数在右上，依次交替排列
            int slot = FormationIndex;
            float side = (slot % 2 == 0) ? -1f : 1f;
            float tier = (slot / 2) + 1;

            float x = followOffset.x * side * tier * 0.7f;
            float y = followOffset.y + (tier - 1) * 0.4f;

            return new Vector2(x, y);
        }

        // ══════════════════════════════════════════════
        //  朝向 & 敌人搜索
        // ══════════════════════════════════════════════

        private void UpdateTargetAndFacing()
        {
            // 搜索最近的敌人（IDamageable 标记）
            CurrentTarget = FindNearestEnemy();

            if (CurrentTarget != null)
            {
                // 面向敌人
                float dirX = CurrentTarget.position.x - transform.position.x;
                _facingSign = dirX >= 0 ? 1 : -1;
            }
            else
            {
                // 无敌人：跟随移动方向
                float velocityX = (transform.position.x - _previousPosition.x) / Mathf.Max(Time.deltaTime, 0.001f);
                if (Mathf.Abs(velocityX) > 0.1f)
                    _facingSign = velocityX > 0 ? 1 : -1;
            }

            // 翻转视觉（只翻转 body 子物体，不翻转整个 transform，避免影响粒子）
            if (_bodyTransform != null)
            {
                Vector3 bodyScale = _bodyTransform.localScale;
                bodyScale.x = Mathf.Abs(bodyScale.x) * _facingSign;
                _bodyTransform.localScale = bodyScale;
            }
        }

        private Transform FindNearestEnemy()
        {
            // 使用 Physics2D.OverlapCircle 搜索敌人
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position, enemySearchRadius, enemyLayerMask);

            Transform nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                // 只攻击有 IDamageable 接口的目标
                var damageable = hit.GetComponent<IDamageable>();
                if (damageable == null || !damageable.CanBeDamaged)
                    continue;

                float dist = Vector2.Distance(transform.position, hit.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = hit.transform;
                }
            }

            return nearest;
        }

        // ══════════════════════════════════════════════
        //  运动倾斜
        // ══════════════════════════════════════════════

        private void UpdateTilt(float dt)
        {
            // 根据水平速度计算目标倾斜角度
            float velocityX = (transform.position.x - _previousPosition.x) / Mathf.Max(dt, 0.001f);
            float targetTilt = -Mathf.Clamp(velocityX * 2f, -1f, 1f) * maxTiltAngle;

            _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, 1f - Mathf.Exp(-tiltSmoothSpeed * dt));

            // 应用倾斜到 body 子物体
            if (_bodyTransform != null)
            {
                _bodyTransform.localRotation = Quaternion.Euler(0f, 0f, _currentTilt);
            }
        }

        // ══════════════════════════════════════════════
        //  外观创建
        // ══════════════════════════════════════════════

        private void CreateVisuals()
        {
            if (droneSprite != null)
            {
                // ════════════════════════════════════════
                //  自定义 Sprite 模式
                // ════════════════════════════════════════
                _usingCustomSprite = true;
                CreateCustomSpriteVisual();
            }
            else
            {
                // ════════════════════════════════════════
                //  占位外观模式（代码生成）
                // ════════════════════════════════════════
                _usingCustomSprite = false;
                CreatePlaceholderVisuals();
            }
        }

        /// <summary>
        /// 使用自定义图片创建无人机外观。
        /// 只需一个 SpriteRenderer，翻转和缩放由 DroneController 管理。
        /// </summary>
        private void CreateCustomSpriteVisual()
        {
            var bodyObj = new GameObject("DroneBody");
            bodyObj.transform.SetParent(transform, false);
            bodyObj.transform.localPosition = Vector3.zero;
            bodyObj.transform.localScale = Vector3.one * spriteScale;

            _bodyRenderer = bodyObj.AddComponent<SpriteRenderer>();
            _bodyRenderer.sprite = droneSprite;
            _bodyRenderer.color = Color.white; // 不染色，显示原图颜色
            _bodyRenderer.sortingOrder = spriteSortingOrder;

            _bodyTransform = bodyObj.transform;
        }

        /// <summary>
        /// 代码生成占位外观（无自定义 Sprite 时的回退方案）。
        /// 所有占位零件放在一个容器子物体中，方便整体翻转和倾斜。
        /// </summary>
        private void CreatePlaceholderVisuals()
        {
            // ── 容器物体（用于统一翻转和倾斜） ──
            var containerObj = new GameObject("DroneBody");
            containerObj.transform.SetParent(transform, false);
            containerObj.transform.localPosition = Vector3.zero;
            _bodyTransform = containerObj.transform;

            // ── 机体（圆角矩形） ──
            var bodyObj = new GameObject("BodySprite");
            bodyObj.transform.SetParent(containerObj.transform, false);
            bodyObj.transform.localPosition = Vector3.zero;

            _bodyRenderer = bodyObj.AddComponent<SpriteRenderer>();
            _bodyRenderer.sprite = CreateRoundedRectSprite(48, 28, 6);
            _bodyRenderer.color = new Color(0.25f, 0.28f, 0.35f, 1f);
            _bodyRenderer.sortingOrder = spriteSortingOrder;
            bodyObj.transform.localScale = Vector3.one * 0.025f;

            // ── 中心能量灯 ──
            var coreObj = new GameObject("DroneCore");
            coreObj.transform.SetParent(containerObj.transform, false);
            coreObj.transform.localPosition = new Vector3(0.05f, 0f, 0f);

            _coreLightRenderer = coreObj.AddComponent<SpriteRenderer>();
            _coreLightRenderer.sprite = CreateCircleSprite(24);
            _coreLightRenderer.color = new Color(0f, 0.9f, 1f, 0.9f);
            _coreLightRenderer.sortingOrder = spriteSortingOrder + 1;
            coreObj.transform.localScale = Vector3.one * 0.012f;

            // ── 上翼 ──
            CreateWing(containerObj.transform, "TopWing", new Vector3(0f, 0.18f, 0f),
                new Vector3(0.02f, 0.006f, 1f), new Color(0.35f, 0.38f, 0.45f));
            // ── 下翼 ──
            CreateWing(containerObj.transform, "BottomWing", new Vector3(0f, -0.12f, 0f),
                new Vector3(0.015f, 0.005f, 1f), new Color(0.35f, 0.38f, 0.45f));

            // ── 天线 ──
            var antennaObj = new GameObject("Antenna");
            antennaObj.transform.SetParent(containerObj.transform, false);
            antennaObj.transform.localPosition = new Vector3(-0.1f, 0.2f, 0f);
            var antennaRen = antennaObj.AddComponent<SpriteRenderer>();
            antennaRen.sprite = CreateRectSprite(2, 12);
            antennaRen.color = new Color(0.5f, 0.55f, 0.6f);
            antennaRen.sortingOrder = spriteSortingOrder;
            antennaObj.transform.localScale = Vector3.one * 0.01f;

            // ── 天线灯 ──
            var antLightObj = new GameObject("AntennaLight");
            antLightObj.transform.SetParent(antennaObj.transform, false);
            antLightObj.transform.localPosition = new Vector3(0f, 7f, 0f);
            var antLightRen = antLightObj.AddComponent<SpriteRenderer>();
            antLightRen.sprite = CreateCircleSprite(8);
            antLightRen.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            antLightRen.sortingOrder = spriteSortingOrder + 2;
        }

        private void CreateWing(Transform parent, string name, Vector3 localPos,
                                Vector3 scale, Color color)
        {
            var wing = new GameObject(name);
            wing.transform.SetParent(parent, false);
            wing.transform.localPosition = localPos;
            wing.transform.localScale = scale;

            var ren = wing.AddComponent<SpriteRenderer>();
            ren.sprite = CreateRoundedRectSprite(32, 12, 3);
            ren.color = color;
            ren.sortingOrder = spriteSortingOrder - 1;
        }

        /// <summary>运行时替换无人机 Sprite（可在游戏中动态换皮）</summary>
        public void SetSprite(Sprite newSprite, float scale = -1f)
        {
            if (newSprite == null) return;

            droneSprite = newSprite;
            if (scale > 0f) spriteScale = scale;

            if (!_usingCustomSprite)
            {
                // 从占位模式切换到自定义模式：销毁旧 body 容器
                if (_bodyTransform != null)
                    Destroy(_bodyTransform.gameObject);

                _coreLightRenderer = null;
                _usingCustomSprite = true;
                CreateCustomSpriteVisual();
            }
            else
            {
                // 已经是自定义模式，直接换图
                if (_bodyRenderer != null)
                {
                    _bodyRenderer.sprite = newSprite;
                    _bodyRenderer.transform.localScale = Vector3.one * spriteScale;
                }
            }
        }

        // ══════════════════════════════════════════════
        //  核心灯脉冲（仅占位模式）
        // ══════════════════════════════════════════════

        private void UpdateCoreLightPulse()
        {
            // 自定义 Sprite 模式下没有核心灯，跳过
            if (_usingCustomSprite || _coreLightRenderer == null) return;

            float pulse;
            Color baseColor;

            if (CurrentTarget != null)
            {
                // 有目标：红色快闪
                pulse = (Mathf.Sin(Time.time * 12f) * 0.5f + 0.5f);
                baseColor = Color.Lerp(
                    new Color(1f, 0.2f, 0.1f, 0.7f),
                    new Color(1f, 0.5f, 0.2f, 1f),
                    pulse);
            }
            else
            {
                // 待机：青色慢闪
                pulse = (Mathf.Sin(Time.time * 3f) * 0.5f + 0.5f);
                baseColor = Color.Lerp(
                    new Color(0f, 0.7f, 0.9f, 0.6f),
                    new Color(0.2f, 1f, 1f, 1f),
                    pulse);
            }

            _coreLightRenderer.color = baseColor;
        }

        // ══════════════════════════════════════════════
        //  Sprite 工厂方法（占位模式用）
        // ══════════════════════════════════════════════

        private static Sprite CreateRoundedRectSprite(int width, int height, int radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (width - 1 - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y, y - (height - 1 - radius)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - (dist - radius + 1f));
                    float metallic = 0.7f + 0.3f * ((float)y / height);
                    tex.SetPixel(x, y, new Color(metallic, metallic, metallic, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), Mathf.Max(width, height));
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = size * 0.5f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    alpha = alpha * alpha;
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateRectSprite(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), Mathf.Max(width, height));
        }

        // ══════════════════════════════════════════════
        //  Gizmos（编辑器调试）
        // ══════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, enemySearchRadius);

            if (CurrentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, CurrentTarget.position);
            }
        }
#endif
    }
}
