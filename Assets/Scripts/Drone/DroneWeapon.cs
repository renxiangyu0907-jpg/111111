// ============================================================================
// DroneWeapon.cs — 无人机自动武器系统
// ============================================================================
//
// 功能：
//   1. 自动锁定 DroneController 找到的最近敌人
//   2. 以配置射速向目标发射能量弹
//   3. 带枪口闪光效果
//   4. 无目标时不射击
//
// 挂载方式：
//   由 DroneController.Initialize() 自动添加。
//

using System.Collections.Generic;
using UnityEngine;
using GhostVeil.Data;

namespace GhostVeil.Drone
{
    public class DroneWeapon : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  配置
        // ══════════════════════════════════════════════

        [Header("=== 射击参数 ===")]
        [Tooltip("每秒射击次数")]
        [SerializeField] private float fireRate = 4f;

        [Tooltip("子弹飞行速度")]
        [SerializeField] private float bulletSpeed = 20f;

        [Tooltip("子弹存活时间")]
        [SerializeField] private float bulletLifetime = 2f;

        [Tooltip("每颗子弹伤害")]
        [SerializeField] private float damage = 5f;

        [Header("=== 子弹外观 ===")]
        [Tooltip("子弹大小")]
        [SerializeField] private float bulletSize = 0.12f;

        [Tooltip("子弹拖尾长度")]
        [SerializeField] private float trailLength = 0.4f;

        [Tooltip("子弹核心颜色")]
        [SerializeField] private Color bulletColor = new Color(0f, 0.9f, 1f, 1f); // 青色

        [Tooltip("子弹拖尾颜色")]
        [SerializeField] private Color trailColor = new Color(0f, 0.5f, 0.8f, 0.6f);

        [Header("=== 枪口设置 ===")]
        [Tooltip("枪口偏移（相对无人机中心）")]
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.3f, -0.02f);

        [Tooltip("枪口闪光持续时间")]
        [SerializeField] private float muzzleFlashDuration = 0.06f;

        // ══════════════════════════════════════════════
        //  运行时
        // ══════════════════════════════════════════════

        private DroneController _drone;
        private float _fireCooldown;
        private readonly List<DroneBullet> _activeBullets = new();

        // 枪口闪光
        private GameObject _muzzleFlashObj;
        private SpriteRenderer _muzzleFlashRenderer;
        private float _muzzleFlashTimer;

        // ══════════════════════════════════════════════
        //  初始化
        // ══════════════════════════════════════════════

        public void Initialize(DroneController drone)
        {
            _drone = drone;
            CreateMuzzleFlash();
        }

        // ══════════════════════════════════════════════
        //  每帧更新
        // ══════════════════════════════════════════════

        private void Update()
        {
            if (_drone == null) return;

            // 冷却倒计时
            if (_fireCooldown > 0f)
                _fireCooldown -= Time.deltaTime;

            // 更新枪口闪光
            UpdateMuzzleFlash();

            // 有目标且冷却结束 → 开火
            if (_drone.CurrentTarget != null && _fireCooldown <= 0f)
            {
                Fire(_drone.CurrentTarget);
                _fireCooldown = 1f / fireRate;
            }

            // 更新所有活跃子弹
            UpdateBullets();
        }

        // ══════════════════════════════════════════════
        //  射击
        // ══════════════════════════════════════════════

        private void Fire(Transform target)
        {
            Vector2 muzzlePos = GetMuzzlePosition();
            Vector2 direction = ((Vector2)target.position - muzzlePos).normalized;

            if (direction.sqrMagnitude < 0.001f)
                direction = Vector2.right * _drone.FacingSign;

            SpawnBullet(muzzlePos, direction);
            TriggerMuzzleFlash(muzzlePos);
        }

        private Vector2 GetMuzzlePosition()
        {
            float sign = _drone.FacingSign;
            return (Vector2)transform.position +
                   new Vector2(muzzleOffset.x * sign, muzzleOffset.y);
        }

        // ══════════════════════════════════════════════
        //  子弹管理
        // ══════════════════════════════════════════════

        private void SpawnBullet(Vector2 position, Vector2 direction)
        {
            var bulletObj = new GameObject("DroneBullet");
            bulletObj.transform.position = position;

            // 旋转对齐方向
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bulletObj.transform.rotation = Quaternion.Euler(0, 0, angle);

            // ── 子弹核心 Sprite ──
            var coreObj = new GameObject("Core");
            coreObj.transform.SetParent(bulletObj.transform, false);
            var coreRen = coreObj.AddComponent<SpriteRenderer>();
            coreRen.sprite = CreateBulletSprite();
            coreRen.color = bulletColor;
            coreRen.sortingOrder = 10;
            coreObj.transform.localScale = Vector3.one * bulletSize;

            // ── 拖尾 LineRenderer ──
            var lr = bulletObj.AddComponent<LineRenderer>();
            SetupTrailRenderer(lr);

            // ── 碰撞检测 ──
            var col = bulletObj.AddComponent<CircleCollider2D>();
            col.radius = bulletSize * 0.5f;
            col.isTrigger = true;

            var rb = bulletObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.velocity = direction * bulletSpeed;

            // ── DroneBullet 组件（处理碰撞伤害） ──
            var bulletComp = bulletObj.AddComponent<DroneBullet>();
            bulletComp.Initialize(damage, bulletLifetime, _drone.transform);

            _activeBullets.Add(bulletComp);
        }

        private void SetupTrailRenderer(LineRenderer lr)
        {
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = bulletSize * 0.8f;
            lr.endWidth = 0f;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.left * trailLength);
            lr.useWorldSpace = false;
            lr.sortingOrder = 9;
            lr.numCapVertices = 3;

            var colorGrad = new Gradient();
            colorGrad.SetKeys(
                new GradientColorKey[]
                {
                    new(bulletColor, 0f),
                    new(trailColor, 1f)
                },
                new GradientAlphaKey[]
                {
                    new(0.9f, 0f),
                    new(0f, 1f)
                }
            );
            lr.colorGradient = colorGrad;
        }

        private void UpdateBullets()
        {
            for (int i = _activeBullets.Count - 1; i >= 0; i--)
            {
                if (_activeBullets[i] == null || _activeBullets[i].IsExpired)
                {
                    if (_activeBullets[i] != null)
                        Destroy(_activeBullets[i].gameObject);
                    _activeBullets.RemoveAt(i);
                }
            }
        }

        // ══════════════════════════════════════════════
        //  枪口闪光
        // ══════════════════════════════════════════════

        private void CreateMuzzleFlash()
        {
            _muzzleFlashObj = new GameObject("DroneMuzzleFlash");
            _muzzleFlashObj.transform.SetParent(transform, false);

            _muzzleFlashRenderer = _muzzleFlashObj.AddComponent<SpriteRenderer>();
            _muzzleFlashRenderer.sprite = CreateFlashSprite();
            _muzzleFlashRenderer.color = new Color(0f, 1f, 1f, 0f); // 初始透明
            _muzzleFlashRenderer.sortingOrder = 11;
            _muzzleFlashObj.transform.localScale = Vector3.one * 0.2f;
        }

        private void TriggerMuzzleFlash(Vector2 worldPos)
        {
            if (_muzzleFlashObj == null) return;
            _muzzleFlashObj.transform.position = worldPos;
            _muzzleFlashTimer = muzzleFlashDuration;
            if (_muzzleFlashRenderer != null)
                _muzzleFlashRenderer.color = new Color(0.5f, 1f, 1f, 0.9f);
        }

        private void UpdateMuzzleFlash()
        {
            if (_muzzleFlashTimer <= 0f) return;
            _muzzleFlashTimer -= Time.deltaTime;

            if (_muzzleFlashTimer <= 0f)
            {
                if (_muzzleFlashRenderer != null)
                    _muzzleFlashRenderer.color = new Color(0f, 1f, 1f, 0f);
            }
            else
            {
                float alpha = Mathf.Clamp01(_muzzleFlashTimer / muzzleFlashDuration) * 0.9f;
                if (_muzzleFlashRenderer != null)
                    _muzzleFlashRenderer.color = new Color(0.5f, 1f, 1f, alpha);
            }
        }

        // ══════════════════════════════════════════════
        //  Sprite 工厂
        // ══════════════════════════════════════════════

        private static Sprite CreateBulletSprite()
        {
            int size = 16;
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
                    // 中心亮，边缘暗
                    float brightness = alpha * alpha;
                    tex.SetPixel(x, y, new Color(1, 1, 1, brightness));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateFlashSprite()
        {
            int size = 24;
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
                    alpha *= alpha * alpha; // 更集中的发光
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
        }

        // ══════════════════════════════════════════════
        //  清理
        // ══════════════════════════════════════════════

        private void OnDestroy()
        {
            foreach (var bullet in _activeBullets)
            {
                if (bullet != null && bullet.gameObject != null)
                    Destroy(bullet.gameObject);
            }
            _activeBullets.Clear();

            if (_muzzleFlashObj != null)
                Destroy(_muzzleFlashObj);
        }
    }
}
