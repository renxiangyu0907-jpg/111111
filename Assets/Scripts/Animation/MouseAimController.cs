// ============================================================================
// MouseAimController.cs — 鼠标瞄准 IK 控制器（手臂/枪口实时跟随鼠标）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  核心功能：                                                              │
// │                                                                          │
// │  1. 每帧读取鼠标屏幕位置 → 转换为世界坐标                              │
// │  2. 调用 SpineAnimator.SetAimTarget() 驱动 IK 约束                     │
// │  3. Spine 的 Bone IK 让手臂骨骼实时指向鼠标位置                        │
// │  4. 支持启用/禁用（对话/过场时自动禁用瞄准）                            │
// │  5. 支持平滑跟随（避免瞬间转向的突兀感）                                │
// │                                                                          │
// │  前置条件：                                                               │
// │    · Spine 编辑器中已设置 IK 约束（如 "aim_arm"）                       │
// │    · SpineAnimator 的 aimIKConstraintName 填入对应名称                   │
// │                                                                          │
// │  挂载方式：                                                               │
// │    · 挂在 Player 物体上（与 PlayerController 同物体）                    │
// └──────────────────────────────────────────────────────────────────────────┘

using UnityEngine;
using GhostVeil.Animation.Spine;
using GhostVeil.Core.Event;
using GhostVeil.Data;

namespace GhostVeil.Animation
{
    public class MouseAimController : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 引用（留空自动查找） ===")]
        [SerializeField] private SpineAnimator spineAnimator;

        [Header("=== IK 设置 ===")]
        [Tooltip("IK 约束名称（必须与 Spine 编辑器中的名称一致）")]
        [SerializeField] private string ikConstraintName = "aim";

        [Tooltip("IK 权重（0=无效果，1=完全跟随）")]
        [SerializeField, Range(0f, 1f)] private float ikMix = 1f;

        [Tooltip("瞄准位置的平滑速度（越大越灵敏）")]
        [SerializeField] private float smoothSpeed = 15f;

        [Header("=== 行为设置 ===")]
        [Tooltip("是否默认启用瞄准")]
        [SerializeField] private bool enabledByDefault = true;

        [Tooltip("是否在叙事接管时自动禁用瞄准")]
        [SerializeField] private bool disableDuringNarrative = true;

        [Tooltip("瞄准超出此距离时降低 IK 权重（避免过度拉伸）")]
        [SerializeField] private float maxAimDistance = 20f;

        [Tooltip("最小 IK 权重（距离很远时）")]
        [SerializeField] private float minDistanceMix = 0.3f;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private bool _aimEnabled;
        private Vector2 _currentAimPos;
        private Vector2 _targetAimPos;
        private bool _narrativeLocked;

        /// <summary>当前是否启用瞄准</summary>
        public bool IsAiming => _aimEnabled && !_narrativeLocked;

        /// <summary>当前鼠标世界坐标</summary>
        public Vector2 AimWorldPosition => _targetAimPos;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            if (spineAnimator == null)
                spineAnimator = GetComponent<SpineAnimator>();
            if (spineAnimator == null)
                spineAnimator = GetComponentInChildren<SpineAnimator>();

            _aimEnabled = enabledByDefault;
        }

        private void OnEnable()
        {
            if (disableDuringNarrative)
            {
                GameEvent.Subscribe<NarrativeAuthorityRequestEvent>(OnNarrativeRequest);
                GameEvent.Subscribe<NarrativeAuthorityReleaseEvent>(OnNarrativeRelease);
            }
        }

        private void OnDisable()
        {
            if (disableDuringNarrative)
            {
                GameEvent.Unsubscribe<NarrativeAuthorityRequestEvent>(OnNarrativeRequest);
                GameEvent.Unsubscribe<NarrativeAuthorityReleaseEvent>(OnNarrativeRelease);
            }
        }

        private void Update()
        {
            if (!IsAiming || spineAnimator == null) return;

            UpdateAimTarget();
            ApplyAim();
        }

        // ══════════════════════════════════════════════
        //  瞄准逻辑
        // ══════════════════════════════════════════════

        private void UpdateAimTarget()
        {
            // 获取主摄像机
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null) return;

            // 鼠标屏幕坐标 → 世界坐标
            Vector3 mouseScreen = UnityEngine.Input.mousePosition;
            mouseScreen.z = Mathf.Abs(cam.transform.position.z); // 2D 场景的 Z 距离
            Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);

            _targetAimPos = new Vector2(mouseWorld.x, mouseWorld.y);

            // 平滑跟随
            _currentAimPos = Vector2.Lerp(_currentAimPos, _targetAimPos, smoothSpeed * Time.deltaTime);
        }

        private void ApplyAim()
        {
            // 根据距离调整 IK 权重（远距离时降低，避免骨骼过度拉伸）
            float distance = Vector2.Distance(transform.position, _currentAimPos);
            float distanceFactor = Mathf.InverseLerp(maxAimDistance, 0f, distance);
            float finalMix = Mathf.Lerp(minDistanceMix, ikMix, distanceFactor);

            // 通过 SpineAnimator 的专用 IK 方法驱动
            spineAnimator.SetAimTarget(_currentAimPos, finalMix);
        }

        // ══════════════════════════════════════════════
        //  叙事系统联动
        // ══════════════════════════════════════════════

        private void OnNarrativeRequest(NarrativeAuthorityRequestEvent evt)
        {
            if (evt.RequestedLevel >= NarrativeAuthorityLevel.Dialogue)
            {
                _narrativeLocked = true;
                spineAnimator?.DisableAim();
            }
        }

        private void OnNarrativeRelease(NarrativeAuthorityReleaseEvent evt)
        {
            _narrativeLocked = false;
        }

        // ══════════════════════════════════════════════
        //  公共方法
        // ══════════════════════════════════════════════

        /// <summary>启用 IK 瞄准</summary>
        public void EnableAim()
        {
            _aimEnabled = true;
        }

        /// <summary>禁用 IK 瞄准（手臂回到动画默认姿态）</summary>
        public void DisableAim()
        {
            _aimEnabled = false;
            spineAnimator?.DisableAim();
        }

        /// <summary>手动设置瞄准目标（非鼠标输入，如手柄右摇杆）</summary>
        public void SetAimTarget(Vector2 worldPosition)
        {
            _targetAimPos = worldPosition;
        }
    }
}
