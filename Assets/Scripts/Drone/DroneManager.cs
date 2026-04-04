// ============================================================================
// DroneManager.cs — 无人机编队管理器
// ============================================================================
//
// 功能：
//   1. 监听 DronePickupEvent 事件，拾取道具时生成无人机
//   2. 管理所有活跃无人机的编队位置
//   3. 支持多架无人机同时存在
//   4. 自动查找 Player Transform
//
// 挂载方式：
//   挂在场景中任意 GameObject 上（推荐挂在 Player 上或由 Bootstrap 创建）。
//   如果场景中没有 DroneManager，InteractionSystemBootstrap 可以自动创建。
//

using System.Collections.Generic;
using UnityEngine;
using GhostVeil.Core.Event;

namespace GhostVeil.Drone
{
    public class DroneManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  配置
        // ══════════════════════════════════════════════

        [Header("=== 无人机管理 ===")]
        [Tooltip("最大无人机数量（0 = 无限制）")]
        [SerializeField] private int maxDrones = 5;

        [Tooltip("生成无人机时的出场动画时间（预留）")]
#pragma warning disable CS0414
        [SerializeField] private float spawnAnimDuration = 0.5f;
#pragma warning restore CS0414

        [Header("=== 方式一：Prefab 模式（推荐） ===")]
        [Tooltip("无人机 Prefab（已配置好 Sprite）。\n" +
                 "留空则用下面的 Drone Sprite 或代码占位图。\n\n" +
                 "制作方法：\n" +
                 "1. 在 Hierarchy 右键 → Create Empty\n" +
                 "2. 挂上 DroneController 脚本\n" +
                 "3. 把无人机图片拖到 Drone Sprite 字段\n" +
                 "4. 调整 Sprite Scale\n" +
                 "5. 拖到 Project 窗口变成 Prefab\n" +
                 "6. 把 Prefab 拖到这里")]
        [SerializeField] private GameObject dronePrefab;

        [Header("=== 方式二：直接拖图片（简单） ===")]
        [Tooltip("无人机 Sprite 图片。\n" +
                 "没有 Prefab 时用这个：直接把 PNG 图片拖到这里。\n" +
                 "图片需要先设置为 Sprite 格式（Inspector → Texture Type → Sprite）")]
        [SerializeField] private Sprite droneSprite;

        [Tooltip("Sprite 缩放倍率（调整图片在游戏中的大小）")]
        [SerializeField] private float droneSpriteScale = 1f;

        // ══════════════════════════════════════════════
        //  运行时
        // ══════════════════════════════════════════════

        private Transform _player;
        private readonly List<DroneController> _activeDrones = new();

        /// <summary>当前活跃无人机数量</summary>
        public int DroneCount => _activeDrones.Count;

        /// <summary>活跃无人机只读列表</summary>
        public IReadOnlyList<DroneController> ActiveDrones => _activeDrones;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            FindPlayer();
            Debug.Log($"[DroneManager] Awake 完成, Player={(_player != null ? _player.name : "未找到")}");
        }

        private void OnEnable()
        {
            GameEvent.Subscribe<DronePickupEvent>(OnDronePickup);
            Debug.Log("[DroneManager] 已订阅 DronePickupEvent");
        }

        private void OnDisable()
        {
            GameEvent.Unsubscribe<DronePickupEvent>(OnDronePickup);
        }

        private void Update()
        {
            // 清理已销毁的无人机
            for (int i = _activeDrones.Count - 1; i >= 0; i--)
            {
                if (_activeDrones[i] == null)
                    _activeDrones.RemoveAt(i);
            }
        }

        // ══════════════════════════════════════════════
        //  事件处理
        // ══════════════════════════════════════════════

        private void OnDronePickup(DronePickupEvent evt)
        {
            Debug.Log($"[DroneManager] ★ 收到 DronePickupEvent! (类型: {evt.DroneType}, 拾取者: {evt.PickedUpBy?.name})");

            // 安全检查
            if (_player == null)
                FindPlayer();

            if (_player == null)
            {
                Debug.LogWarning("[DroneManager] 找不到 Player，无法生成无人机！");
                return;
            }

            // 数量限制
            if (maxDrones > 0 && _activeDrones.Count >= maxDrones)
            {
                Debug.Log($"[DroneManager] 已达上限 ({maxDrones})，忽略拾取。");
                return;
            }

            SpawnDrone(evt.DroneType, evt.PickupPosition);
        }

        // ══════════════════════════════════════════════
        //  生成无人机
        // ══════════════════════════════════════════════

        /// <summary>生成一架无人机（也可由外部直接调用）</summary>
        public DroneController SpawnDrone(string droneType = "standard",
                                          Vector3? spawnPos = null)
        {
            if (_player == null)
            {
                FindPlayer();
                if (_player == null) return null;
            }

            int formationIndex = _activeDrones.Count;

            // 初始位置：拾取点或 Player 头顶
            Vector3 startPos = spawnPos ?? (_player.position + Vector3.up * 1.5f);

            GameObject droneObj;
            DroneController controller;

            if (dronePrefab != null)
            {
                // ══ Prefab 模式：用预制体实例化（已包含 Sprite） ══
                droneObj = Instantiate(dronePrefab, startPos, Quaternion.identity);
                droneObj.name = $"Drone_{formationIndex}_{droneType}";

                controller = droneObj.GetComponent<DroneController>();
                if (controller == null)
                    controller = droneObj.AddComponent<DroneController>();

                controller.Initialize(_player, formationIndex);
            }
            else
            {
                // ══ 代码模式：动态创建 ══
                droneObj = new GameObject($"Drone_{formationIndex}_{droneType}");
                droneObj.transform.position = startPos;

                controller = droneObj.AddComponent<DroneController>();
                // 传入 Sprite（可为 null，DroneController 会自动回退到占位外观）
                controller.Initialize(_player, formationIndex, droneSprite, droneSpriteScale);
            }

            _activeDrones.Add(controller);

            // 播放出场特效
            SpawnAppearEffect(startPos);

            Debug.Log($"[DroneManager] 生成无人机 #{formationIndex} (类型: {droneType})，" +
                      $"当前数量: {_activeDrones.Count}");

            // 发布事件
            GameEvent.Publish(new DroneSpawnedEvent
            {
                Drone = droneObj,
                FormationIndex = formationIndex,
                DroneType = droneType
            });

            return controller;
        }

        /// <summary>移除指定无人机</summary>
        public void RemoveDrone(DroneController drone)
        {
            if (drone == null) return;

            _activeDrones.Remove(drone);

            // 重新分配编队索引
            for (int i = 0; i < _activeDrones.Count; i++)
                _activeDrones[i].FormationIndex = i;

            // 播放消散特效
            SpawnDisappearEffect(drone.transform.position);

            Destroy(drone.gameObject);
        }

        /// <summary>移除所有无人机</summary>
        public void RemoveAllDrones()
        {
            foreach (var drone in _activeDrones)
            {
                if (drone != null)
                {
                    SpawnDisappearEffect(drone.transform.position);
                    Destroy(drone.gameObject);
                }
            }
            _activeDrones.Clear();
        }

        // ══════════════════════════════════════════════
        //  查找 Player
        // ══════════════════════════════════════════════

        private void FindPlayer()
        {
            // 方法 1：通过 tag
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _player = playerObj.transform;
                return;
            }

            // 方法 2：通过 PlayerController 组件
            var playerCtrl = FindObjectOfType<GhostVeil.Character.Player.PlayerController>();
            if (playerCtrl != null)
            {
                _player = playerCtrl.transform;
                return;
            }

            Debug.LogWarning("[DroneManager] 场景中找不到 Player (Tag 或 PlayerController)。");
        }

        // ══════════════════════════════════════════════
        //  出场 / 消散特效
        // ══════════════════════════════════════════════

        private void SpawnAppearEffect(Vector3 position)
        {
            var effectObj = new GameObject("DroneAppearFX");
            effectObj.transform.position = position;

            var ps = effectObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.6f;
            main.startLifetime = 0.5f;
            main.startSpeed = 3f;
            main.startSize = 0.1f;
            main.startColor = new Color(0f, 0.9f, 1f, 1f);
            main.maxParticles = 20;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 15, 20)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new(new Color(0.8f, 1f, 1f), 0f),
                    new(new Color(0f, 0.5f, 1f), 1f)
                },
                new GradientAlphaKey[] {
                    new(1f, 0f),
                    new(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var psr = effectObj.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = 12;

            ps.Play();
            Destroy(effectObj, 1f);
        }

        private void SpawnDisappearEffect(Vector3 position)
        {
            var effectObj = new GameObject("DroneDisappearFX");
            effectObj.transform.position = position;

            var ps = effectObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = 0.3f;
            main.startSpeed = 1.5f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 0.5f, 0.2f, 1f); // 橘色消散
            main.maxParticles = 12;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 8, 12)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var psr = effectObj.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = 12;

            ps.Play();
            Destroy(effectObj, 0.8f);
        }
    }

    // ══════════════════════════════════════════════
    //  无人机生成事件
    // ══════════════════════════════════════════════

    public struct DroneSpawnedEvent
    {
        public GameObject Drone;
        public int FormationIndex;
        public string DroneType;
    }
}
