// ============================================================================
// DronePickup.cs — 无人机拾取道具
// ============================================================================
//
// 功能：
//   玩家靠近并按 E 拾取后，发布 DronePickupEvent 事件，
//   由 DroneManager 监听并生成一架无人机。
//   继承自 AbstractInteractable，复用交互系统框架。
//
// 场景使用：
//   1. 创建一个 GameObject，挂上此脚本
//   2. 添加一个 Collider2D (isTrigger = true) 作为交互范围
//   3. 可选设置 droneType 字段来区分不同型号的无人机
//

using UnityEngine;
using GhostVeil.Interaction;
using GhostVeil.Core.Event;

namespace GhostVeil.Drone
{
    public class DronePickup : AbstractInteractable
    {
        [Header("=== 无人机道具设置 ===")]
        [Tooltip("无人机型号 ID（预留不同型号扩展）")]
        [SerializeField] private string droneType = "standard";

        [Tooltip("拾取提示文本")]
        [SerializeField] private string pickupText = "拾取无人机";

        // ── 视觉效果 ──
        private SpriteRenderer _iconRenderer;
        private float _bobPhase;

        protected override void Awake()
        {
            base.Awake();
            interactionType = Data.InteractionType.PickUp;
            if (string.IsNullOrEmpty(promptText) || promptText == "Interact")
                promptText = pickupText;

            CreatePickupVisual();
        }

        private void Update()
        {
            // 浮动动画
            if (_iconRenderer != null)
            {
                _bobPhase += Time.deltaTime * 3f;
                float bob = Mathf.Sin(_bobPhase) * 0.08f;
                _iconRenderer.transform.localPosition = new Vector3(0f, bob + 0.3f, 0f);

                // 缓慢旋转
                _iconRenderer.transform.Rotate(0, 0, 60f * Time.deltaTime);

                // 发光脉冲
                float pulse = Mathf.Sin(Time.time * 4f) * 0.3f + 0.7f;
                _iconRenderer.color = new Color(0f, pulse, 1f, 0.9f);
            }
        }

        protected override void OnInteract(GameObject instigator)
        {
            Debug.Log($"[DronePickup] 拾取无人机 (类型: {droneType})");

            // 发布拾取事件
            GameEvent.Publish(new DronePickupEvent
            {
                DroneType = droneType,
                PickedUpBy = instigator,
                PickupPosition = transform.position
            });

            // 拾取后销毁
            Lock();

            // 播放拾取特效
            SpawnPickupEffect();

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
            iconObj.transform.localPosition = new Vector3(0f, 0.3f, 0f);

            _iconRenderer = iconObj.AddComponent<SpriteRenderer>();
            _iconRenderer.sprite = CreateDiamondSprite();
            _iconRenderer.color = new Color(0f, 0.8f, 1f, 0.9f);
            _iconRenderer.sortingOrder = 8;

            iconObj.transform.localScale = Vector3.one * 0.4f;
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
            int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 菱形距离
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
