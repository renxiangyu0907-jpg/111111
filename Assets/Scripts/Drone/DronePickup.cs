// ============================================================================
// DronePickup.cs — 无人机拾取道具
// ============================================================================
//
// 两种拾取方式（Inspector 可选）：
//   方式 A：自动拾取（默认）— 玩家走到附近自动获得无人机
//   方式 B：按键拾取 — 需要 InteractionDetector，按 E 拾取
//
// 场景使用：
//   1. 创建一个 GameObject，挂上此脚本
//   2. 添加一个 Collider2D (isTrigger = true) 作为检测范围
//   3. 确保 Player 有 "Player" Tag
//

using UnityEngine;
using GhostVeil.Interaction;
using GhostVeil.Core.Event;

namespace GhostVeil.Drone
{
    [RequireComponent(typeof(Collider2D))]
    public class DronePickup : AbstractInteractable
    {
        [Header("=== 无人机道具设置 ===")]
        [Tooltip("无人机型号 ID（预留不同型号扩展）")]
        [SerializeField] private string droneType = "standard";

        [Tooltip("拾取提示文本")]
        [SerializeField] private string pickupText = "拾取无人机";

        [Header("=== 拾取模式 ===")]
        [Tooltip("勾选 = 玩家走到附近自动拾取（不需要按 E）\n" +
                 "不勾 = 需要 InteractionDetector + 按 E 拾取")]
        [SerializeField] private bool autoPickup = true;

        [Tooltip("自动拾取时的检测 Tag")]
        [SerializeField] private string playerTag = "Player";

        // ── 视觉效果 ──
        private SpriteRenderer _iconRenderer;
        private float _bobPhase;
        private bool _pickedUp;

        protected override void Awake()
        {
            base.Awake();
            interactionType = Data.InteractionType.PickUp;
            if (string.IsNullOrEmpty(promptText) || promptText == "Interact")
                promptText = pickupText;

            // 确保 Collider 是 Trigger
            var col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
                Debug.Log("[DronePickup] 已自动将 Collider2D 设为 isTrigger=true");
            }

            CreatePickupVisual();
            Debug.Log($"[DronePickup] 初始化完成 (autoPickup={autoPickup})");
        }

        private void Update()
        {
            if (_pickedUp) return;

            // 浮动动画
            if (_iconRenderer != null)
            {
                _bobPhase += Time.deltaTime * 3f;
                float bob = Mathf.Sin(_bobPhase) * 0.12f;
                _iconRenderer.transform.localPosition = new Vector3(0f, bob + 0.4f, 0f);

                // 缓慢旋转
                _iconRenderer.transform.Rotate(0, 0, 60f * Time.deltaTime);

                // 发光脉冲
                float pulse = Mathf.Sin(Time.time * 4f) * 0.3f + 0.7f;
                _iconRenderer.color = new Color(0f, pulse, 1f, 0.9f);
            }
        }

        // ══════════════════════════════════════════════
        //  自动拾取（Trigger 碰撞检测）
        // ══════════════════════════════════════════════

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!autoPickup || _pickedUp) return;

            // 检查是否是 Player
            if (other.CompareTag(playerTag) ||
                other.GetComponent<GhostVeil.Character.Player.PlayerController>() != null)
            {
                Debug.Log("[DronePickup] 玩家进入自动拾取范围");
                DoPickup(other.gameObject);
            }
        }

        // ══════════════════════════════════════════════
        //  按键拾取（AbstractInteractable 回调）
        // ══════════════════════════════════════════════

        protected override void OnInteract(GameObject instigator)
        {
            if (_pickedUp) return;
            DoPickup(instigator);
        }

        // ══════════════════════════════════════════════
        //  统一拾取逻辑
        // ══════════════════════════════════════════════

        private void DoPickup(GameObject instigator)
        {
            if (_pickedUp) return;
            _pickedUp = true;

            Debug.Log($"[DronePickup] ★ 拾取无人机！(类型: {droneType}, 拾取者: {instigator.name})");

            // 发布拾取事件
            GameEvent.Publish(new DronePickupEvent
            {
                DroneType = droneType,
                PickedUpBy = instigator,
                PickupPosition = transform.position
            });

            // 锁定交互
            Lock();

            // 播放拾取特效
            SpawnPickupEffect();

            // 销毁
            Destroy(gameObject, 0.2f);
        }

        // ══════════════════════════════════════════════
        //  视觉
        // ══════════════════════════════════════════════

        private void CreatePickupVisual()
        {
            // 发光小无人机图标
            var iconObj = new GameObject("DroneIcon");
            iconObj.transform.SetParent(transform, false);
            iconObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            _iconRenderer = iconObj.AddComponent<SpriteRenderer>();
            _iconRenderer.sprite = CreateDiamondSprite();
            _iconRenderer.color = new Color(0f, 0.8f, 1f, 0.9f);
            _iconRenderer.sortingOrder = 8;

            iconObj.transform.localScale = Vector3.one * 0.5f;

            // 额外：在道具位置添加一个底部光圈提示
            var glowObj = new GameObject("PickupGlow");
            glowObj.transform.SetParent(transform, false);
            glowObj.transform.localPosition = Vector3.zero;

            var glowRen = glowObj.AddComponent<SpriteRenderer>();
            glowRen.sprite = CreateCircleSprite(32);
            glowRen.color = new Color(0f, 0.6f, 1f, 0.25f);
            glowRen.sortingOrder = 1;
            glowObj.transform.localScale = Vector3.one * 0.8f;
        }

        private void SpawnPickupEffect()
        {
            var effectObj = new GameObject("DronePickupFX");
            effectObj.transform.position = transform.position + Vector3.up * 0.3f;

            var ps = effectObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = 0.4f;
            main.startSpeed = 2f;
            main.startSize = 0.08f;
            main.startColor = new Color(0f, 0.9f, 1f, 1f);
            main.maxParticles = 15;
            main.loop = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 10, 15)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            var psr = effectObj.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = 12;

            Destroy(effectObj, 1f);
        }

        // ══════════════════════════════════════════════
        //  Sprite 工厂
        // ══════════════════════════════════════════════

        private static Sprite CreateDiamondSprite()
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = Mathf.Abs(y - center);
                    float dist = (dx + dy) / center;
                    float alpha = Mathf.Clamp01(1.2f - dist);
                    alpha *= alpha;
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
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
                    alpha *= alpha;
                    tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
        }
    }

    // ══════════════════════════════════════════════
    //  无人机拾取事件
    // ══════════════════════════════════════════════

    public struct DronePickupEvent
    {
        public string DroneType;
        public GameObject PickedUpBy;
        public Vector3 PickupPosition;
    }
}
