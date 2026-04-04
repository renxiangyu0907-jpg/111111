// ============================================================================
// DroneBullet.cs — 无人机子弹（碰撞伤害 + 自动销毁）
// ============================================================================
//
// 功能：
//   1. 检测碰撞并对 IDamageable 目标施加伤害
//   2. 命中后销毁自身并播放小型命中特效
//   3. 超时自动销毁
//

using UnityEngine;
using GhostVeil.Combat;
using GhostVeil.Data;

namespace GhostVeil.Drone
{
    public class DroneBullet : MonoBehaviour
    {
        private float _damage;
        private float _lifetime;
        private Transform _source;
        private bool _hasHit;

        /// <summary>子弹是否已过期</summary>
        public bool IsExpired => _lifetime <= 0f || _hasHit;

        /// <summary>由 DroneWeapon 调用初始化</summary>
        public void Initialize(float damage, float lifetime, Transform source)
        {
            _damage = damage;
            _lifetime = lifetime;
            _source = source;
        }

        private void Update()
        {
            if (_hasHit) return;

            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                // 超时淡出
                SpawnExpireEffect();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;

            // 不攻击自己的无人机或 Player
            if (other.GetComponent<DroneController>() != null) return;
            if (other.CompareTag("Player")) return;

            // 查找 IDamageable
            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null && damageable.CanBeDamaged)
            {
                Vector2 knockDir = (other.transform.position - transform.position).normalized;

                var payload = new DamagePayload(
                    _damage,
                    DamageType.Physical,
                    knockDir,
                    2f,                    // 轻微击退
                    HitReaction.Light,
                    _source
                );

                damageable.TakeDamage(payload);
                _hasHit = true;
                SpawnHitEffect();
                return;
            }

            // 命中墙壁等不可伤害物体也销毁
            // （仅当碰撞层不是 Player / Drone 时）
            if (!other.isTrigger)
            {
                _hasHit = true;
                SpawnHitEffect();
            }
        }

        // ══════════════════════════════════════════════
        //  命中 / 过期特效
        // ══════════════════════════════════════════════

        private void SpawnHitEffect()
        {
            // 小型粒子爆炸
            var effectObj = new GameObject("DroneHitFX");
            effectObj.transform.position = transform.position;

            var ps = effectObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.3f;
            main.startLifetime = 0.2f;
            main.startSpeed = 3f;
            main.startSize = 0.06f;
            main.startColor = new Color(0f, 0.9f, 1f, 1f);
            main.maxParticles = 8;
            main.loop = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 6, 8)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new(new Color(0.5f, 1f, 1f), 0f),
                    new(new Color(0f, 0.5f, 1f), 1f)
                },
                new GradientAlphaKey[] {
                    new(1f, 0f),
                    new(0f, 1f)
                }
            );
            col2.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // 设置粒子渲染器材质
            var psr = effectObj.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = 12;

            // 自动销毁
            Destroy(effectObj, 0.5f);
        }

        private void SpawnExpireEffect()
        {
            // 消散小闪光
            var effectObj = new GameObject("DroneExpireFX");
            effectObj.transform.position = transform.position;

            var ren = effectObj.AddComponent<SpriteRenderer>();
            ren.color = new Color(0f, 0.8f, 1f, 0.5f);
            ren.sortingOrder = 10;

            // 快速缩放消失
            Destroy(effectObj, 0.15f);
        }
    }
}
