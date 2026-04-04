// ============================================================================
// DroneVFX.cs — 无人机推进器粒子特效（尾焰 + 悬浮微粒）
// ============================================================================
//
// 功能：
//   1. 后方主推进器尾焰（持续发射，跟随移动方向调整）
//   2. 底部悬浮微粒（微弱下喷粒子，模拟反重力悬浮）
//   3. 加速/减速时尾焰强度变化
//
// 挂载方式：
//   由 DroneController.Initialize() 自动添加。
//

using UnityEngine;

namespace GhostVeil.Drone
{
    public class DroneVFX : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  配置
        // ══════════════════════════════════════════════

        [Header("=== 主推进器 ===")]
        [Tooltip("尾焰基础发射速率")]
        [SerializeField] private float thrusterBaseRate = 30f;

        [Tooltip("尾焰加速时发射速率")]
        [SerializeField] private float thrusterBoostRate = 60f;

        [Tooltip("尾焰粒子速度")]
        [SerializeField] private float thrusterSpeed = 2f;

        [Tooltip("尾焰粒子生命周期")]
        [SerializeField] private float thrusterLifetime = 0.3f;

        [Tooltip("尾焰粒子大小")]
        [SerializeField] private float thrusterSize = 0.06f;

        [Header("=== 底部悬浮粒子 ===")]
        [Tooltip("悬浮粒子发射速率")]
        [SerializeField] private float hoverParticleRate = 8f;

        [Tooltip("悬浮粒子速度")]
        [SerializeField] private float hoverSpeed = 0.5f;

        // ══════════════════════════════════════════════
        //  运行时
        // ══════════════════════════════════════════════

        private ParticleSystem _thrusterPS;
        private ParticleSystem _hoverPS;
        private DroneController _drone;
        private Vector3 _lastPosition;

        // ══════════════════════════════════════════════
        //  初始化
        // ══════════════════════════════════════════════

        private void Start()
        {
            _drone = GetComponent<DroneController>();
            _lastPosition = transform.position;

            CreateThrusterParticles();
            CreateHoverParticles();
        }

        // ══════════════════════════════════════════════
        //  每帧更新
        // ══════════════════════════════════════════════

        private void Update()
        {
            if (_drone == null) return;

            // 计算移动速度
            Vector3 velocity = (transform.position - _lastPosition) / Time.deltaTime;
            _lastPosition = transform.position;
            float speed = velocity.magnitude;

            // ── 调整推进器强度 ──
            if (_thrusterPS != null)
            {
                var emission = _thrusterPS.emission;
                float rate = Mathf.Lerp(thrusterBaseRate, thrusterBoostRate,
                    Mathf.Clamp01(speed / 5f));
                emission.rateOverTime = rate;

                // 推进器方向：与移动方向相反
                var thrusterTransform = _thrusterPS.transform;
                if (speed > 0.1f)
                {
                    float angle = Mathf.Atan2(-velocity.y, -velocity.x) * Mathf.Rad2Deg;
                    thrusterTransform.localRotation = Quaternion.Euler(0, 0,
                        angle + 90f); // +90 因为粒子默认向上
                }
                else
                {
                    // 静止时推进器朝身后
                    float backAngle = _drone.FacingSign > 0 ? 180f : 0f;
                    thrusterTransform.localRotation = Quaternion.Euler(0, 0,
                        backAngle + 90f);
                }
            }
        }

        // ══════════════════════════════════════════════
        //  创建粒子系统
        // ══════════════════════════════════════════════

        private void CreateThrusterParticles()
        {
            var thrusterObj = new GameObject("ThrusterVFX");
            thrusterObj.transform.SetParent(transform, false);
            thrusterObj.transform.localPosition = new Vector3(-0.2f, 0f, 0f);

            _thrusterPS = thrusterObj.AddComponent<ParticleSystem>();

            // ── Main Module ──
            var main = _thrusterPS.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = thrusterLifetime;
            main.startSpeed = thrusterSpeed;
            main.startSize = thrusterSize;
            main.startColor = new Color(0f, 0.8f, 1f, 0.8f); // 青色尾焰
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            // ── Emission ──
            var emission = _thrusterPS.emission;
            emission.rateOverTime = thrusterBaseRate;

            // ── Shape ──
            var shape = _thrusterPS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.02f;

            // ── Color over Lifetime ──
            var colorOverLifetime = _thrusterPS.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new(new Color(0.8f, 0.95f, 1f), 0f),    // 白热核心
                    new(new Color(0f, 0.7f, 1f), 0.3f),      // 青蓝过渡
                    new(new Color(0f, 0.3f, 0.8f), 0.7f),    // 深蓝
                    new(new Color(0.1f, 0.1f, 0.3f), 1f)     // 暗淡消散
                },
                new GradientAlphaKey[]
                {
                    new(0.9f, 0f),
                    new(0.7f, 0.3f),
                    new(0.3f, 0.7f),
                    new(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // ── Size over Lifetime ──
            var sizeOverLifetime = _thrusterPS.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            // ── 渲染器 ──
            var psr = thrusterObj.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = 3;
        }

        private void CreateHoverParticles()
        {
            var hoverObj = new GameObject("HoverVFX");
            hoverObj.transform.SetParent(transform, false);
            hoverObj.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            hoverObj.transform.localRotation = Quaternion.Euler(0, 0, 0); // 向下

            _hoverPS = hoverObj.AddComponent<ParticleSystem>();

            // ── Main Module ──
            var main = _hoverPS.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.4f;
            main.startSpeed = hoverSpeed;
            main.startSize = 0.03f;
            main.startColor = new Color(0.3f, 0.6f, 1f, 0.4f); // 淡蓝
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.2f; // 轻微下坠

            // ── Emission ──
            var emission = _hoverPS.emission;
            emission.rateOverTime = hoverParticleRate;

            // ── Shape ──
            var shape = _hoverPS.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            // ── Velocity over Lifetime（向下散射） ──
            var vel = _hoverPS.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(-hoverSpeed);
            vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            // ── Color over Lifetime ──
            var colorOverLifetime = _hoverPS.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new(new Color(0.4f, 0.7f, 1f), 0f),
                    new(new Color(0.2f, 0.4f, 0.8f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new(0.5f, 0f),
                    new(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // ── Size over Lifetime ──
            var sizeOverLifetime = _hoverPS.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // ── 渲染器 ──
            var psr = hoverObj.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = 3;
        }

        // ══════════════════════════════════════════════
        //  清理
        // ══════════════════════════════════════════════

        private void OnDestroy()
        {
            if (_thrusterPS != null) Destroy(_thrusterPS.gameObject);
            if (_hoverPS != null) Destroy(_hoverPS.gameObject);
        }
    }
}
