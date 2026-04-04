// ============================================================================
// LaserWeapon.cs — 激光射击武器（左键长按持续发射白色激光子弹）
// ============================================================================
//
// 核心功能：
//   1. 左键长按时持续向鼠标方向发射白色长条激光弹
//   2. 激光弹使用 LineRenderer 渲染（白色发光长条效果）
//   3. 支持射速控制（fireRate）
//   4. 支持枪口闪光特效
//   5. 对话/过场时自动停止射击
//
// 挂载方式：
//   挂在 Player 物体上（与 PlayerController 同物体）
//

using System.Collections.Generic;
using UnityEngine;
using GhostVeil.Input;
using GhostVeil.Animation;
using GhostVeil.Core.Event;
using GhostVeil.Data;

namespace GhostVeil.Combat
{
    public class LaserWeapon : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 射击参数 ===")]
        [Tooltip("每秒发射次数")]
        [SerializeField] private float fireRate = 8f;

        [Tooltip("激光弹飞行速度（单位/秒）")]
        [SerializeField] private float bulletSpeed = 30f;

        [Tooltip("激光弹最大存活时间（秒）")]
        [SerializeField] private float bulletLifetime = 1.5f;

        [Tooltip("激光弹伤害")]
        [SerializeField] private float damage = 10f;

        [Header("=== 激光弹外观 ===")]
        [Tooltip("激光弹长度（单位）")]
        [SerializeField] private float bulletLength = 1.2f;

        [Tooltip("激光弹宽度")]
        [SerializeField] private float bulletWidth = 0.08f;

        [Tooltip("激光弹核心颜色")]
        [SerializeField] private Color bulletCoreColor = Color.white;

        [Tooltip("激光弹外发光颜色")]
        [SerializeField] private Color bulletGlowColor = new Color(0.8f, 0.9f, 1f, 0.5f);

        [Header("=== 枪口设置 ===")]
        [Tooltip("枪口发射点（留空则使用角色中心 + 偏移）")]
        [SerializeField] private Transform muzzlePoint;

        [Tooltip("枪口偏移（无 muzzlePoint 时使用）")]
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.5f, 0.3f);

        [Tooltip("枪口闪光持续时间")]
        [SerializeField] private float muzzleFlashDuration = 0.05f;

        [Tooltip("枪口闪光大小")]
        [SerializeField] private float muzzleFlashSize = 0.3f;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private IInputProvider _input;
        private float _fireCooldown;
        private bool _narrativeLocked;
        private readonly List<LaserBullet> _activeBullets = new();
        private MouseAimController _aimController;

        // ── 枪口闪光 ──
        private GameObject _muzzleFlashObj;
        private SpriteRenderer _muzzleFlashRenderer;
        private float _muzzleFlashTimer;

        /// <summary>是否正在射击</summary>
        public bool IsFiring { get; private set; }

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            // 获取输入源
            _input = GetComponent<InputSystemProvider>() as IInputProvider;
            if (_input == null)
                _input = FindObjectOfType<InputSystemProvider>() as IInputProvider;

            // 获取瞄准控制器
            _aimController = GetComponent<MouseAimController>();

            // 创建枪口闪光对象
            CreateMuzzleFlash();
        }

        private void OnEnable()
        {
            GameEvent.Subscribe<NarrativeAuthorityRequestEvent>(OnNarrativeRequest);
            GameEvent.Subscribe<NarrativeAuthorityReleaseEvent>(OnNarrativeRelease);
        }

        private void OnDisable()
        {
            GameEvent.Unsubscribe<NarrativeAuthorityRequestEvent>(OnNarrativeRequest);
            GameEvent.Unsubscribe<NarrativeAuthorityReleaseEvent>(OnNarrativeRelease);
        }

        private void Update()
        {
            // 更新射击冷却
            if (_fireCooldown > 0f)
                _fireCooldown -= Time.deltaTime;

            // 更新枪口闪光
            UpdateMuzzleFlash();

            // 检查射击输入
            bool wantsFire = _input != null && _input.AttackHeld && !_narrativeLocked;
            IsFiring = wantsFire;

            if (wantsFire && _fireCooldown <= 0f)
            {
                Fire();
                _fireCooldown = 1f / fireRate;
            }

            // 更新所有活跃子弹
            UpdateBullets();
        }

        // ══════════════════════════════════════════════
        //  射击逻辑
        // ══════════════════════════════════════════════

        private void Fire()
        {
            // 计算枪口位置
            Vector2 muzzlePos = GetMuzzlePosition();

            // 计算射击方向（朝向鼠标）
            Vector2 aimPos;
            if (_aimController != null)
            {
                aimPos = _aimController.AimWorldPosition;
            }
            else
            {
                // 没有 MouseAimController 时直接用鼠标位置
                var cam = UnityEngine.Camera.main;
                if (cam == null) return;
                Vector3 mouseScreen = UnityEngine.Input.mousePosition;
                mouseScreen.z = Mathf.Abs(cam.transform.position.z);
                aimPos = cam.ScreenToWorldPoint(mouseScreen);
            }

            Vector2 direction = (aimPos - muzzlePos).normalized;
            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right; // 防止零方向

            // 创建激光弹
            SpawnBullet(muzzlePos, direction);

            // 触发枪口闪光
            TriggerMuzzleFlash(muzzlePos);
        }

        private Vector2 GetMuzzlePosition()
        {
            if (muzzlePoint != null)
                return muzzlePoint.position;

            // 根据朝向翻转 X 偏移
            float facingSign = transform.localScale.x >= 0 ? 1f : -1f;
            // 也考虑 Spine 的 ScaleX 翻转
            var playerCtrl = GetComponent<GhostVeil.Character.Player.PlayerController>();
            if (playerCtrl != null)
                facingSign = (float)(int)playerCtrl.Facing;

            return (Vector2)transform.position +
                   new Vector2(muzzleOffset.x * facingSign, muzzleOffset.y);
        }

        // ══════════════════════════════════════════════
        //  激光弹管理
        // ══════════════════════════════════════════════

        private void SpawnBullet(Vector2 position, Vector2 direction)
        {
            // 创建子弹 GameObject
            var bulletObj = new GameObject("LaserBullet");
            bulletObj.transform.position = position;

            // 计算旋转角度
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);

            // 添加 LineRenderer 作为激光弹视觉
            var lr = bulletObj.AddComponent<LineRenderer>();
            SetupLaserRenderer(lr);

            // 设置 LineRenderer 的两个端点（本地坐标）
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.right * bulletLength);

            // 添加碰撞检测
            var col = bulletObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(bulletLength, bulletWidth * 2f);
            col.offset = new Vector2(bulletLength * 0.5f, 0f);
            col.isTrigger = true;

            // 添加 Rigidbody2D（用于 trigger 检测）
            var rb = bulletObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.velocity = direction * bulletSpeed;

            // 记录子弹
            var bullet = new LaserBullet
            {
                obj = bulletObj,
                direction = direction,
                timer = bulletLifetime,
                lineRenderer = lr
            };
            _activeBullets.Add(bullet);
        }

        private void SetupLaserRenderer(LineRenderer lr)
        {
            // 使用默认 Sprites/Default 材质（支持颜色）
            lr.material = new Material(Shader.Find("Sprites/Default"));

            // 宽度渐变（中间粗两头细）
            lr.startWidth = bulletWidth;
            lr.endWidth = bulletWidth * 0.6f;

            // 颜色渐变（核心白 → 边缘淡蓝）
            var colorGrad = new Gradient();
            colorGrad.SetKeys(
                new GradientColorKey[]
                {
                    new(bulletCoreColor, 0f),
                    new(bulletCoreColor, 0.5f),
                    new(bulletGlowColor, 1f)
                },
                new GradientAlphaKey[]
                {
                    new(1f, 0f),
                    new(1f, 0.7f),
                    new(0.3f, 1f)
                }
            );
            lr.colorGradient = colorGrad;

            // 世界空间坐标
            lr.useWorldSpace = false;

            // 排序层（确保在角色前方）
            lr.sortingOrder = 10;

            // 圆头圆尾
            lr.numCapVertices = 4;
        }

        private void UpdateBullets()
        {
            for (int i = _activeBullets.Count - 1; i >= 0; i--)
            {
                var bullet = _activeBullets[i];
                bullet.timer -= Time.deltaTime;

                if (bullet.timer <= 0f || bullet.obj == null)
                {
                    // 销毁过期子弹
                    if (bullet.obj != null)
                        Destroy(bullet.obj);
                    _activeBullets.RemoveAt(i);
                    continue;
                }

                // 淡出效果（最后 0.2 秒）
                if (bullet.timer < 0.2f && bullet.lineRenderer != null)
                {
                    float alpha = bullet.timer / 0.2f;
                    Color c = bullet.lineRenderer.startColor;
                    c.a = alpha;
                    bullet.lineRenderer.startColor = c;
                    bullet.lineRenderer.endColor = new Color(c.r, c.g, c.b, alpha * 0.3f);
                }
            }
        }

        // ══════════════════════════════════════════════
        //  枪口闪光
        // ══════════════════════════════════════════════

        private void CreateMuzzleFlash()
        {
            _muzzleFlashObj = new GameObject("MuzzleFlash");
            _muzzleFlashObj.transform.SetParent(transform);
            _muzzleFlashObj.transform.localPosition = Vector3.zero;

            _muzzleFlashRenderer = _muzzleFlashObj.AddComponent<SpriteRenderer>();
            _muzzleFlashRenderer.sprite = CreateCircleSprite();
            _muzzleFlashRenderer.color = new Color(1f, 1f, 1f, 0f); // 初始透明
            _muzzleFlashRenderer.sortingOrder = 11;

            _muzzleFlashObj.transform.localScale = Vector3.one * muzzleFlashSize;
        }

        private void TriggerMuzzleFlash(Vector2 worldPos)
        {
            if (_muzzleFlashObj == null) return;

            _muzzleFlashObj.transform.position = worldPos;
            _muzzleFlashTimer = muzzleFlashDuration;

            if (_muzzleFlashRenderer != null)
                _muzzleFlashRenderer.color = new Color(1f, 1f, 0.9f, 0.8f);
        }

        private void UpdateMuzzleFlash()
        {
            if (_muzzleFlashTimer <= 0f) return;

            _muzzleFlashTimer -= Time.deltaTime;
            if (_muzzleFlashTimer <= 0f)
            {
                if (_muzzleFlashRenderer != null)
                    _muzzleFlashRenderer.color = new Color(1f, 1f, 1f, 0f);
            }
            else
            {
                // 快速淡出
                float alpha = Mathf.Clamp01(_muzzleFlashTimer / muzzleFlashDuration) * 0.8f;
                if (_muzzleFlashRenderer != null)
                    _muzzleFlashRenderer.color = new Color(1f, 1f, 0.9f, alpha);
            }
        }

        /// <summary>
        /// 运行时创建一个简单的圆形 Sprite（用于枪口闪光）。
        /// </summary>
        private static Sprite CreateCircleSprite()
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
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

        // ══════════════════════════════════════════════
        //  叙事系统联动
        // ══════════════════════════════════════════════

        private void OnNarrativeRequest(NarrativeAuthorityRequestEvent evt)
        {
            if (evt.RequestedLevel >= NarrativeAuthorityLevel.Dialogue)
                _narrativeLocked = true;
        }

        private void OnNarrativeRelease(NarrativeAuthorityReleaseEvent evt)
        {
            _narrativeLocked = false;
        }

        // ══════════════════════════════════════════════
        //  清理
        // ══════════════════════════════════════════════

        private void OnDestroy()
        {
            // 清理所有活跃子弹
            foreach (var bullet in _activeBullets)
            {
                if (bullet.obj != null)
                    Destroy(bullet.obj);
            }
            _activeBullets.Clear();

            if (_muzzleFlashObj != null)
                Destroy(_muzzleFlashObj);
        }

        // ══════════════════════════════════════════════
        //  内部数据结构
        // ══════════════════════════════════════════════

        private class LaserBullet
        {
            public GameObject obj;
            public Vector2 direction;
            public float timer;
            public LineRenderer lineRenderer;
        }
    }
}
