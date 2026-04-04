// ============================================================================
// DroneController.cs — 跟随无人机核心控制器
// ============================================================================
//
// 功能：
//   1. 延迟跟随 Player（记录 Player 位置历史，取若干帧前的位置作为目标）
//   2. 悬浮动画（Sine 波上下浮动）
//   3. 自主朝向（面向最近敌人，无敌人时跟随移动方向或 Player 朝向）
//   4. 支持 Inspector 拖入 Sprite 作为外观；未赋值时用代码生成占位图
//   5. 管理推进器粒子特效（DroneVFX 组件）
//
// 挂载方式：
//   由 DroneManager 在拾取道具时动态创建，不需手动挂载。
//

using UnityEngine;
using GhostVeil.Combat;

namespace GhostVeil.Drone
{
    public class DroneController : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 跟随参数 ===")]
        [Tooltip("跟随延迟帧数（越大越 '拖尾'）")]
        [SerializeField] private int followDelay = 12;

        [Tooltip("跟随平滑速度")]
        [SerializeField] private float followSmoothSpeed = 8f;

        [Tooltip("相对 Player 的偏移（X 为身后方向，Y 为上方）")]
        [SerializeField] private Vector2 followOffset = new Vector2(-0.8f, 1.2f);

        [Header("=== 悬浮动画 ===")]
        [Tooltip("上下浮动幅度")]
        [SerializeField] private float hoverAmplitude = 0.15f;

        [Tooltip("上下浮动频率")]
        [SerializeField] private float hoverFrequency = 2f;

        [Header("=== 外观 ===")]
        [Tooltip("无人机 Sprite（从 Inspector 拖入图片）。留空则使用代码生成的占位图。")]
        [SerializeField] private Sprite droneSprite;

        [Tooltip("Sprite 缩放倍率（根据图片实际大小调整）")]
        [SerializeField] private float spriteScale = 1f;

        [Tooltip("Sprite 排序层级")]
        [SerializeField] private int spriteSortingOrder = 5;

        [Header("=== 朝向 ===")]
        [Tooltip("搜索敌人的范围半径")]
        [SerializeField] private float enemySearchRadius = 10f;

        [Tooltip("敌人所在 Layer")]
        [SerializeField] private LayerMask enemyLayerMask;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private Transform _player;
        private Vector2[] _positionHistory;
        private int _historyIndex;
        private int _historySize;
        private bool _historyFilled;
        private float _hoverPhase;
        private Vector2 _smoothVelocity;
        private int _facingSign = 1; // 1 = right, -1 = left

        // 视觉组件
        private SpriteRenderer _bodyRenderer;
        private SpriteRenderer _coreLightRenderer;
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
        public void Initialize(Transform player, int formationIndex)
        {
            _player = player;
            FormationIndex = formationIndex;

            // 初始化位置历史缓冲区
            _historySize = followDelay + 1;
            _positionHistory = new Vector2[_historySize];
            _historyIndex = 0;
            _historyFilled = false;

            // 用 player 当前位置填充历史
            Vector2 startPos = (Vector2)player.position + GetFormationOffset();
            for (int i = 0; i < _historySize; i++)
                _positionHistory[i] = startPos;
            _historyFilled = true;

            // 随机悬浮相位（多个无人机不同步）
            _hoverPhase = Random.Range(0f, Mathf.PI * 2f);

            // 放到 player 身后
            transform.position = startPos;

            // 创建视觉外观
            CreateVisuals();

            // 添加子组件
            _vfx = gameObject.AddComponent<DroneVFX>();
            _weapon = gameObject.AddComponent<DroneWeapon>();
            _weapon.Initialize(this);
        }

        // ══════════════════════════════════════════════
        //  每帧更新
        // ══════════════════════════════════════════════

        private void Update()
        {
            if (_player == null) return;

            float dt = Time.deltaTime;

            // ── 1. 记录 Player 位置（含编队偏移） ──
            RecordPlayerPosition();

            // ── 2. 获取延迟目标位置 ──
            Vector2 targetPos = GetDelayedTargetPosition();

            // ── 3. 加入悬浮动画 ──
            _hoverPhase += hoverFrequency * dt * Mathf.PI * 2f;
            float hoverY = Mathf.Sin(_hoverPhase) * hoverAmplitude;
            targetPos.y += hoverY;

            // ── 4. 平滑移动到目标位置 ──
            Vector2 currentPos = transform.position;
            Vector2 newPos = Vector2.SmoothDamp(
                currentPos, targetPos, ref _smoothVelocity, 1f / followSmoothSpeed);
            transform.position = newPos;

            // ── 5. 搜索最近敌人 & 更新朝向 ──
            UpdateTargetAndFacing();

            // ── 6. 更新核心灯闪烁 ──
            UpdateCoreLightPulse();
        }

        // ══════════════════════════════════════════════
        //  位置历史 & 延迟跟随
        // ══════════════════════════════════════════════

        private void RecordPlayerPosition()
        {
            Vector2 targetBase = (Vector2)_player.position + GetFormationOffset();
            _positionHistory[_historyIndex] = targetBase;
            _historyIndex = (_historyIndex + 1) % _historySize;
            if (!_historyFilled && _historyIndex == 0)
                _historyFilled = true;
        }

        private Vector2 GetDelayedTargetPosition()
        {
            // 读取 followDelay 帧前的位置
            int delayedIndex;
            if (_historyFilled)
            {
                delayedIndex = (_historyIndex - followDelay - 1 + _historySize) % _historySize;
            }
            else
            {
                delayedIndex = 0; // 缓冲区还没填满，用最早的
            }
            return _positionHistory[delayedIndex];
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
                if (Mathf.Abs(_smoothVelocity.x) > 0.1f)
                    _facingSign = _smoothVelocity.x > 0 ? 1 : -1;
            }

            // 翻转视觉
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * _facingSign;
            transform.localScale = scale;
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
        //  外观创建（优先用 Inspector 拖入的 Sprite）
        // ══════════════════════════════════════════════

        private void CreateVisuals()
        {
            if (droneSprite != null)
            {
                // ════════════════════════════════════════
                //  模式 A：使用用户提供的 Sprite 图片
                // ════════════════════════════════════════
                CreateSpriteVisuals();
            }
            else
            {
                // ════════════════════════════════════════
                //  模式 B：代码生成占位图（无图片时的兜底）
                // ════════════════════════════════════════
                CreatePlaceholderVisuals();
            }
        }

        /// <summary>
        /// 使用 Inspector 中赋值的 Sprite 创建外观。
        /// 
        /// 导入步骤：
        ///   1. 把 PNG/PSD 文件放到 Assets/ 下任意位置（推荐 Assets/Sprites/Drone/）
        ///   2. 在 Unity 的 Project 窗口选中图片 → Inspector 面板设置：
        ///      · Texture Type = Sprite (2D and UI)
        ///      · Sprite Mode = Single（整张图一个 Sprite）
        ///      · Pixels Per Unit = 根据你的图片大小设置
        ///        （例如 128x128 的图，PPU=128 则在游戏中为 1 个 Unity 单位大小）
        ///      · Filter Mode = Bilinear 或 Point（像素风选 Point）
        ///      · 点击 Apply
        ///   3. 在 Hierarchy 选中挂有 DroneController 的 GameObject（或 Prefab）
        ///   4. 把图片从 Project 窗口拖到 Inspector 的 "Drone Sprite" 字段
        ///   5. 调整 "Sprite Scale" 控制大小
        /// </summary>
        private void CreateSpriteVisuals()
        {
            var bodyObj = new GameObject("DroneBody");
            bodyObj.transform.SetParent(transform, false);
            bodyObj.transform.localPosition = Vector3.zero;
            bodyObj.transform.localScale = Vector3.one * spriteScale;

            _bodyRenderer = bodyObj.AddComponent<SpriteRenderer>();
            _bodyRenderer.sprite = droneSprite;
            _bodyRenderer.color = Color.white; // 不染色，保持原图颜色
            _bodyRenderer.sortingOrder = spriteSortingOrder;

            // 用 Sprite 模式时不需要代码生成的核心灯，
            // 但保留 _coreLightRenderer 引用为 null，
            // UpdateCoreLightPulse() 会自动跳过。
            _coreLightRenderer = null;
        }

        /// <summary>代码生成的占位外观（无图片时兜底）</summary>
        private void CreatePlaceholderVisuals()
        {
            // ── 机体（圆角矩形） ──
            var bodyObj = new GameObject("DroneBody");
            bodyObj.transform.SetParent(transform, false);
            bodyObj.transform.localPosition = Vector3.zero;

            _bodyRenderer = bodyObj.AddComponent<SpriteRenderer>();
            _bodyRenderer.sprite = CreateRoundedRectSprite(48, 28, 6);
            _bodyRenderer.color = new Color(0.25f, 0.28f, 0.35f, 1f);
            _bodyRenderer.sortingOrder = 5;
            bodyObj.transform.localScale = Vector3.one * 0.025f;

            // ── 中心能量灯 ──
            var coreObj = new GameObject("DroneCore");
            coreObj.transform.SetParent(transform, false);
            coreObj.transform.localPosition = new Vector3(0.05f, 0f, 0f);

            _coreLightRenderer = coreObj.AddComponent<SpriteRenderer>();
            _coreLightRenderer.sprite = CreateCircleSprite(24);
            _coreLightRenderer.color = new Color(0f, 0.9f, 1f, 0.9f);
            _coreLightRenderer.sortingOrder = 6;
            coreObj.transform.localScale = Vector3.one * 0.012f;

            // ── 上翼 ──
            CreateWing("TopWing", new Vector3(0f, 0.18f, 0f), new Vector3(0.02f, 0.006f, 1f),
                new Color(0.35f, 0.38f, 0.45f));
            // ── 下翼 ──
            CreateWing("BottomWing", new Vector3(0f, -0.12f, 0f), new Vector3(0.015f, 0.005f, 1f),
                new Color(0.35f, 0.38f, 0.45f));

            // ── 天线 ──
            var antennaObj = new GameObject("Antenna");
            antennaObj.transform.SetParent(transform, false);
            antennaObj.transform.localPosition = new Vector3(-0.1f, 0.2f, 0f);
            var antennaRen = antennaObj.AddComponent<SpriteRenderer>();
            antennaRen.sprite = CreateRectSprite(2, 12);
            antennaRen.color = new Color(0.5f, 0.55f, 0.6f);
            antennaRen.sortingOrder = 5;
            antennaObj.transform.localScale = Vector3.one * 0.01f;

            // ── 天线灯 ──
            var antLightObj = new GameObject("AntennaLight");
            antLightObj.transform.SetParent(antennaObj.transform, false);
            antLightObj.transform.localPosition = new Vector3(0f, 7f, 0f);
            var antLightRen = antLightObj.AddComponent<SpriteRenderer>();
            antLightRen.sprite = CreateCircleSprite(8);
            antLightRen.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            antLightRen.sortingOrder = 7;
        }

        private void CreateWing(string name, Vector3 localPos, Vector3 scale, Color color)
        {
            var wing = new GameObject(name);
            wing.transform.SetParent(transform, false);
            wing.transform.localPosition = localPos;
            wing.transform.localScale = scale;

            var ren = wing.AddComponent<SpriteRenderer>();
            ren.sprite = CreateRoundedRectSprite(32, 12, 3);
            ren.color = color;
            ren.sortingOrder = 4;
        }

        // ══════════════════════════════════════════════
        //  核心灯脉冲
        // ══════════════════════════════════════════════

        private void UpdateCoreLightPulse()
        {
            if (_coreLightRenderer == null) return;

            // 脉冲：根据是否有目标改变颜色和闪烁频率
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
        //  Sprite 工厂方法
        // ══════════════════════════════════════════════

        /// <summary>创建一个圆角矩形 Sprite</summary>
        private static Sprite CreateRoundedRectSprite(int width, int height, int radius)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 计算到圆角矩形边缘的距离
                    float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (width - 1 - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y, y - (height - 1 - radius)));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(1f - (dist - radius + 1f));

                    // 添加金属质感渐变
                    float metallic = 0.7f + 0.3f * ((float)y / height);
                    tex.SetPixel(x, y, new Color(metallic, metallic, metallic, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), Mathf.Max(width, height));
        }

        /// <summary>创建一个圆形 Sprite</summary>
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
                    alpha = alpha * alpha; // 柔和衰减
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>创建一个简单矩形 Sprite</summary>
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
            // 绘制敌人搜索范围
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, enemySearchRadius);

            // 绘制目标连线
            if (CurrentTarget != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, CurrentTarget.position);
            }
        }
#endif
    }
}
