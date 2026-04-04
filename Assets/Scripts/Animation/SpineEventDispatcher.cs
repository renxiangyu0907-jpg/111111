// ============================================================================
// SpineEventDispatcher.cs — Spine 帧事件分发器（脚步声、攻击判定、特效触发）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  工作原理：                                                              │
// │                                                                          │
// │  Spine 编辑器中可以在动画的特定帧打上 Event 标记，例如：                │
// │    · "footstep"   — 脚踩地的瞬间                                       │
// │    · "attack_hit" — 攻击判定帧                                          │
// │    · "dust"       — 扬尘特效触发                                        │
// │    · "whoosh"     — 挥刀音效触发                                        │
// │                                                                          │
// │  本脚本订阅 SpineAnimator 的 OnAnimationEvent，                         │
// │  根据事件名称执行对应的回调（播放音效、实例化粒子、发布全局事件等）     │
// │                                                                          │
// │  挂载方式：                                                               │
// │    · 与 SpineAnimator 挂在同一物体上                                    │
// │    · Inspector 中配置事件名称 → 音效/特效的映射表                       │
// └──────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using UnityEngine;
using GhostVeil.Animation.Spine;
using GhostVeil.Core.Event;

namespace GhostVeil.Animation
{
    /// <summary>
    /// Spine 帧事件分发器。
    /// 监听 SpineAnimator 的动画事件，分发给音效系统和特效系统。
    /// </summary>
    public class SpineEventDispatcher : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== Spine 引用（留空自动查找） ===")]
        [SerializeField] private SpineAnimator spineAnimator;

        [Header("=== 事件映射表 ===")]
        [Tooltip("Spine 事件名 → 回调配置")]
        [SerializeField] private SpineEventMapping[] eventMappings = new SpineEventMapping[]
        {
            new SpineEventMapping { eventName = "footstep", audioClip = null, vfxPrefab = null, spawnAtFoot = true },
            new SpineEventMapping { eventName = "attack_hit", audioClip = null, vfxPrefab = null, spawnAtFoot = false },
        };

        [Header("=== 特效生成点 ===")]
        [Tooltip("脚部特效生成位置（留空使用角色底部）")]
        [SerializeField] private Transform footTransform;

        [Tooltip("中心特效生成位置（留空使用角色中心）")]
        [SerializeField] private Transform centerTransform;

        [Header("=== 音效设置 ===")]
        [Tooltip("AudioSource 引用（留空自动创建）")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("音效音量")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

        [Tooltip("音效随机音高范围（让脚步声不单调）")]
        [SerializeField] private float pitchVariation = 0.1f;

        // ══════════════════════════════════════════════
        //  C# 事件（供外部监听）
        // ══════════════════════════════════════════════

        /// <summary>任何 Spine 事件触发时广播</summary>
        public event Action<string /*eventName*/> OnSpineEventFired;

        /// <summary>脚步事件专用（供灰尘特效、屏幕震动等订阅）</summary>
        public event Action OnFootstep;

        /// <summary>攻击判定事件专用（供战斗系统订阅）</summary>
        public event Action OnAttackHit;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private Dictionary<string, SpineEventMapping> _mappingLookup;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            // ── 查找 SpineAnimator ──────────────────────
            if (spineAnimator == null)
                spineAnimator = GetComponent<SpineAnimator>();
            if (spineAnimator == null)
                spineAnimator = GetComponentInChildren<SpineAnimator>();

            // ── 创建 AudioSource（如果需要） ────────────
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 1f; // 3D 音效
                }
            }

            // ── 构建查找表 ──────────────────────────────
            _mappingLookup = new Dictionary<string, SpineEventMapping>();
            if (eventMappings != null)
            {
                foreach (var mapping in eventMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.eventName))
                        _mappingLookup[mapping.eventName] = mapping;
                }
            }
        }

        private void OnEnable()
        {
            if (spineAnimator != null)
                spineAnimator.OnAnimationEvent += HandleSpineEvent;
        }

        private void OnDisable()
        {
            if (spineAnimator != null)
                spineAnimator.OnAnimationEvent -= HandleSpineEvent;
        }

        // ══════════════════════════════════════════════
        //  事件处理
        // ══════════════════════════════════════════════

        private void HandleSpineEvent(string eventName)
        {
            // ── 广播通用事件 ────────────────────────────
            OnSpineEventFired?.Invoke(eventName);

            // ── 专用事件快捷通道 ────────────────────────
            switch (eventName)
            {
                case "footstep":
                case "foot_step":
                case "step":
                    OnFootstep?.Invoke();
                    break;

                case "attack_hit":
                case "hit":
                case "attack":
                    OnAttackHit?.Invoke();
                    break;
            }

            // ── 查找映射表 ──────────────────────────────
            if (_mappingLookup.TryGetValue(eventName, out var mapping))
            {
                ExecuteMapping(mapping);
            }

            // ── 发布全局事件（供任何系统监听） ──────────
            GameEvent.Publish(new SpineAnimationEventFired
            {
                Source = gameObject,
                EventName = eventName
            });

            Debug.Log($"[SpineEventDispatcher] 事件触发: {eventName}");
        }

        private void ExecuteMapping(SpineEventMapping mapping)
        {
            // ── 播放音效 ────────────────────────────────
            if (mapping.audioClip != null && audioSource != null)
            {
                audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
                audioSource.PlayOneShot(mapping.audioClip, volume);
            }

            // ── 播放随机音效（多个脚步声随机选一） ──────
            if (mapping.randomAudioClips != null && mapping.randomAudioClips.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, mapping.randomAudioClips.Length);
                var clip = mapping.randomAudioClips[idx];
                if (clip != null)
                {
                    audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
                    audioSource.PlayOneShot(clip, volume);
                }
            }

            // ── 生成特效 ────────────────────────────────
            if (mapping.vfxPrefab != null)
            {
                Vector3 spawnPos;
                if (mapping.spawnAtFoot && footTransform != null)
                    spawnPos = footTransform.position;
                else if (!mapping.spawnAtFoot && centerTransform != null)
                    spawnPos = centerTransform.position;
                else
                    spawnPos = transform.position;

                // 加上偏移
                spawnPos += new Vector3(mapping.vfxOffset.x, mapping.vfxOffset.y, 0f);

                var vfx = Instantiate(mapping.vfxPrefab, spawnPos, Quaternion.identity);

                // 自动销毁粒子特效
                if (mapping.vfxLifetime > 0)
                    Destroy(vfx, mapping.vfxLifetime);
                else
                    Destroy(vfx, 3f); // 默认 3 秒
            }
        }
    }

    // ══════════════════════════════════════════════════
    //  数据结构
    // ══════════════════════════════════════════════════

    /// <summary>
    /// Spine 事件 → 音效/特效的映射配置。
    /// </summary>
    [System.Serializable]
    public class SpineEventMapping
    {
        [Tooltip("Spine 编辑器中设定的事件名称")]
        public string eventName = "";

        [Header("--- 音效 ---")]
        [Tooltip("固定音效（优先使用）")]
        public AudioClip audioClip;

        [Tooltip("随机音效池（多个脚步声随机选一，更自然）")]
        public AudioClip[] randomAudioClips;

        [Header("--- 特效 ---")]
        [Tooltip("特效预制体（粒子系统 / 精灵动画）")]
        public GameObject vfxPrefab;

        [Tooltip("特效是否在脚部生成（false = 角色中心）")]
        public bool spawnAtFoot = true;

        [Tooltip("特效生成位置偏移")]
        public Vector2 vfxOffset = Vector2.zero;

        [Tooltip("特效存活时间（秒）")]
        public float vfxLifetime = 2f;
    }

    /// <summary>全局 Spine 动画事件（供任何系统监听）</summary>
    public struct SpineAnimationEventFired
    {
        public GameObject Source;
        public string EventName;
    }
}
