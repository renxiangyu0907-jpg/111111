// ============================================================================
// CameraController.cs — 相机控制器（ICameraTarget 实现）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  职责：                                                                  │
// │                                                                          │
// │  1. 管理主相机的跟随行为（默认跟随玩家）                                │
// │  2. 提供 FocusOn() 让叙事系统平滑推镜到 NPC / 场景焦点                 │
// │  3. 提供 ResetToDefault() 恢复默认跟随                                  │
// │  4. 提供 RequestShake() 相机震动效果                                    │
// │                                                                          │
// │  设计说明：                                                               │
// │    · 当项目导入 Cinemachine 后，此控制器可升级为 Cinemachine 虚拟相机    │
// │    · 当前版本使用手写平滑跟随，零额外依赖                               │
// │    · 跟随逻辑在 LateUpdate 中执行（在角色位移之后）                     │
// │                                                                          │
// │  挂载方式：                                                               │
// │    · 挂在场景中的 Main Camera 上（或 CharacterCamera 上）               │
// └──────────────────────────────────────────────────────────────────────────┘

using UnityEngine;

namespace GhostVeil.Camera
{
    public class CameraController : MonoBehaviour, ICameraTarget
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 跟随设置 ===")]
        [Tooltip("默认跟随目标（通常为 Player）")]
        [SerializeField] private Transform defaultFollowTarget;

        [Tooltip("跟随平滑时间")]
        [SerializeField] private float followSmoothTime = 0.15f;

        [Tooltip("相机距离目标的 Z 轴偏移（2D 游戏中通常为负值）")]
        [SerializeField] private float zOffset = -10f;

        [Tooltip("跟随时的 Y 轴偏移（略微向上看）")]
        [SerializeField] private Vector2 followOffset = new Vector2(0f, 1.5f);

        [Header("=== 聚焦设置 ===")]
        [Tooltip("聚焦移动的默认过渡时间")]
        [SerializeField] private float defaultFocusTransitionTime = 1f;

        [Header("=== 震动设置 ===")]
        [Tooltip("震动衰减曲线")]
        [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        // ══════════════════════════════════════════════
        //  ICameraTarget 属性
        // ══════════════════════════════════════════════

        public Transform FollowTarget => _currentFollowTarget;
        public Transform LookAtTarget => _currentFollowTarget;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        // ── 跟随 ──────────────────────────────────────
        private Transform _currentFollowTarget;
        private Vector3 _smoothVelocity;

        // ── 聚焦 ──────────────────────────────────────
        private bool _isFocusing;
        private Vector3 _focusStartPos;
        private Vector3 _focusTargetPos;
        private float _focusTransitionTime;
        private float _focusElapsed;

        // ── 震动 ──────────────────────────────────────
        private bool _isShaking;
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeElapsed;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            _currentFollowTarget = defaultFollowTarget;

            // 如果未设置跟随目标，自动查找 Player
            if (_currentFollowTarget == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    _currentFollowTarget = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (_isFocusing)
            {
                UpdateFocus();
            }
            else if (_currentFollowTarget != null)
            {
                UpdateFollow();
            }

            if (_isShaking)
            {
                ApplyShake();
            }
        }

        // ══════════════════════════════════════════════
        //  跟随逻辑
        // ══════════════════════════════════════════════

        private void UpdateFollow()
        {
            Vector3 targetPos = _currentFollowTarget.position +
                                new Vector3(followOffset.x, followOffset.y, zOffset);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref _smoothVelocity,
                followSmoothTime
            );
        }

        // ══════════════════════════════════════════════
        //  聚焦逻辑（平滑推镜到指定位置）
        // ══════════════════════════════════════════════

        private void UpdateFocus()
        {
            _focusElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_focusElapsed / _focusTransitionTime);

            // 使用 SmoothStep 缓动曲线
            float smoothT = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(_focusStartPos, _focusTargetPos, smoothT);

            if (t >= 1f)
            {
                _isFocusing = false;
            }
        }

        // ══════════════════════════════════════════════
        //  震动逻辑
        // ══════════════════════════════════════════════

        private void ApplyShake()
        {
            _shakeElapsed += Time.deltaTime;
            if (_shakeElapsed >= _shakeDuration)
            {
                _isShaking = false;
                return;
            }

            float progress = _shakeElapsed / _shakeDuration;
            float decay = shakeCurve.Evaluate(progress);
            float currentIntensity = _shakeIntensity * decay;

            Vector2 shakeOffset = Random.insideUnitCircle * currentIntensity;
            transform.position += new Vector3(shakeOffset.x, shakeOffset.y, 0f);
        }

        // ══════════════════════════════════════════════
        //  ICameraTarget 实现
        // ══════════════════════════════════════════════

        /// <summary>
        /// 请求相机震动。
        /// </summary>
        public void RequestShake(float intensity, float duration)
        {
            _isShaking = true;
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeElapsed = 0f;
        }

        /// <summary>
        /// 将镜头平滑推向指定世界坐标。
        /// 常用于：过场中推镜到 NPC 脸部、推镜到远处的场景焦点等。
        /// </summary>
        public void FocusOn(Vector3 worldPosition, float transitionTime)
        {
            _isFocusing = true;
            _focusStartPos = transform.position;
            _focusTargetPos = new Vector3(worldPosition.x, worldPosition.y, zOffset);
            _focusTransitionTime = transitionTime > 0 ? transitionTime : defaultFocusTransitionTime;
            _focusElapsed = 0f;

            // 重置平滑速度，避免聚焦结束后回弹
            _smoothVelocity = Vector3.zero;
        }

        /// <summary>
        /// 恢复默认跟随行为（回到玩家）。
        /// </summary>
        public void ResetToDefault()
        {
            _isFocusing = false;
            _currentFollowTarget = defaultFollowTarget;
            _smoothVelocity = Vector3.zero;
        }

        // ══════════════════════════════════════════════
        //  公共方法（供过场系统扩展使用）
        // ══════════════════════════════════════════════

        /// <summary>
        /// 临时切换跟随目标（如过场中切到 NPC）。
        /// 调用 ResetToDefault() 恢复。
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            _currentFollowTarget = target;
            _isFocusing = false;
        }

        /// <summary>
        /// 聚焦到指定 GameObject 的位置。
        /// </summary>
        public void FocusOnTarget(Transform target, float transitionTime)
        {
            if (target == null) return;
            FocusOn(target.position, transitionTime);
        }

        /// <summary>
        /// 聚焦到指定 Tag 的 GameObject。
        /// </summary>
        public void FocusOnTag(string tag, float transitionTime)
        {
            var go = GameObject.FindGameObjectWithTag(tag);
            if (go != null)
                FocusOn(go.transform.position, transitionTime);
            else
                Debug.LogWarning($"[CameraController] 找不到 Tag 为 \"{tag}\" 的对象");
        }
    }
}
