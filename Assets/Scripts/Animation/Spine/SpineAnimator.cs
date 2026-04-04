// ============================================================================
// SpineAnimator.cs — Spine 动画具体实现（对接 SkeletonAnimation）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  职责：                                                                  │
// │                                                                          │
// │  1. 获取自身或子物体上的 SkeletonAnimation 组件                           │
// │  2. 提供 PlayAnim() 供状态机调用，自动处理重复调用防抖                    │
// │  3. 根据 PlayerController 的朝向，通过 Skeleton.ScaleX 实现左右翻转       │
// │  4. 预留 IK 瞄准接口 SetAimTarget()                                      │
// │  5. 转发 Spine 动画事件（footstep / attack_hit 等）                       │
// │                                                                          │
// │  挂载方式：                                                               │
// │    · 挂在 Player 物体本身（如果 SkeletonAnimation 就在 Player 上）        │
// │    · 或挂在持有 SkeletonAnimation 的子物体上                              │
// │    · PlayerController 在 GatherDependencies 中自动从自身或子物体查找      │
// │                                                                          │
// │  注意：                                                                   │
// │    · 本脚本依赖 Spine-Unity Runtime（spine-unity 包）                     │
// │    · 如果项目尚未导入 Spine Runtime，请先注释掉或用条件编译隔离            │
// │    · 为了让项目在未导入 Spine 时也能编译，                                │
// │      所有 Spine API 调用都包裹在 #if HAS_SPINE_UNITY 条件编译中           │
// └──────────────────────────────────────────────────────────────────────────┘

using System;
using UnityEngine;

// ── 条件编译说明 ────────────────────────────────────────────────────────────
//
//  当你导入 Spine-Unity 包后，请在 Project Settings → Player →
//  Scripting Define Symbols 中添加：HAS_SPINE_UNITY
//
//  这样所有 #if HAS_SPINE_UNITY 包裹的代码就会激活。
//  在此之前，本脚本会编译为一个"模拟版"——所有方法正常调用但不操作真实骨骼。
//
#if HAS_SPINE_UNITY
using Spine;
using Spine.Unity;
#endif

namespace GhostVeil.Animation.Spine
{
    /// <summary>
    /// Spine 动画控制器 —— ISpineBridge 的正式实现。
    /// 
    /// 设计要点：
    /// · 继承 AbstractSpineBridge（已实现事件管线）
    /// · 对接 SkeletonAnimation 组件（场景内角色用）
    /// · 如需 UI 骨骼（SkeletonGraphic），请创建 SpineUIAnimator 并复用相同接口
    /// </summary>
    public class SpineAnimator : AbstractSpineBridge
    {
        // ══════════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════════

        [Header("=== Spine 引用 ===")]
        [Tooltip("SkeletonAnimation 组件引用。留空则自动从自身或子物体查找")]
        [SerializeField] private Component skeletonAnimationRef;
        // 用 Component 而非 SkeletonAnimation 类型，避免未导入 Spine 时编译报错

        [Header("=== 翻转设置 ===")]
        [Tooltip("基础 ScaleX（Spine 导出时的默认缩放，通常为 1 或 -1）")]
        [SerializeField] private float baseScaleX = 1f;

        [Tooltip("是否由本脚本控制翻转（false 则交给 PlayerController 的 localScale 翻转）")]
        [SerializeField] private bool controlFlip = true;

        [Header("=== 过渡融合 ===")]
        [Tooltip("默认动画过渡时长（秒）。-1 表示使用 Spine 编辑器中设置的 Mix")]
        [SerializeField] private float defaultMixDuration = 0.15f;

        [Header("=== IK 瞄准（预留） ===")]
        [Tooltip("IK 约束名称（在 Spine 编辑器中设置的名字，如 \"aim_arm\"）")]
        [SerializeField] private string aimIKConstraintName = "aim";

        // ══════════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════════

        // 缓存当前各轨道正在播放的动画名，用于防重复调用
        private string[] _currentAnimNames = new string[4]; // 预留 4 条轨道

        // 当前朝向（1 = 右，-1 = 左）
        private float _currentFaceSign = 1f;

        // IK 瞄准目标（世界坐标）
        private Vector2 _aimTarget;
        private bool _aimEnabled;
        private float _aimMix = 1f;

        // ── Spine 组件缓存（条件编译） ─────────────────────
#if HAS_SPINE_UNITY
        private SkeletonAnimation _skeletonAnim;
        private Skeleton _skeleton;
        private IkConstraint _aimConstraint;
#endif

        // ══════════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════════

        private void Awake()
        {
            InitializeSpineComponents();
        }

        /// <summary>
        /// 查找并缓存 Spine 组件，注册事件回调。
        /// </summary>
        private void InitializeSpineComponents()
        {
#if HAS_SPINE_UNITY
            // ── 1. 获取 SkeletonAnimation ────────────────
            if (skeletonAnimationRef != null)
            {
                _skeletonAnim = skeletonAnimationRef as SkeletonAnimation;
            }

            if (_skeletonAnim == null)
            {
                // 先从自身找，再从子物体找
                _skeletonAnim = GetComponent<SkeletonAnimation>();
                if (_skeletonAnim == null)
                    _skeletonAnim = GetComponentInChildren<SkeletonAnimation>();
            }

            if (_skeletonAnim == null)
            {
                Debug.LogError("[SpineAnimator] 找不到 SkeletonAnimation 组件！" +
                               "请确认 Spine 骨骼已挂在本物体或子物体上。", this);
                return;
            }

            // ── 2. 缓存 Skeleton 引用 ────────────────────
            _skeleton = _skeletonAnim.Skeleton;

            // ── 3. 注册动画事件回调 ──────────────────────
            _skeletonAnim.AnimationState.Event += OnSpineEvent;
            _skeletonAnim.AnimationState.Complete += OnSpineComplete;

            // ── 4. 查找 IK 约束（可选） ─────────────────
            if (!string.IsNullOrEmpty(aimIKConstraintName))
            {
                // Spine 4.3: FindIkConstraint 已移除，改用 FindConstraint<IkConstraint>()
                _aimConstraint = _skeleton.FindConstraint<IkConstraint>(aimIKConstraintName);
                // 找不到也不报错 —— IK 是可选功能
                if (_aimConstraint == null)
                    Debug.Log($"[SpineAnimator] IK 约束 \"{aimIKConstraintName}\" 未找到（可选功能，不影响运行）。");
            }

            Debug.Log("[SpineAnimator] Spine 组件初始化成功。", this);
#else
            // 未导入 Spine Runtime 时的提示
            if (Application.isPlaying)
                Debug.LogWarning("[SpineAnimator] Spine Runtime 未导入（缺少 HAS_SPINE_UNITY 定义）。" +
                                 "动画调用将被忽略。", this);
#endif
        }

        // ══════════════════════════════════════════════════
        //  核心公共方法 —— 状态机调用入口
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 简化版动画播放接口 —— 状态机最常用的调用方式。
        /// 
        /// 特性：
        /// · 自动防重复：如果当前轨道已经在播放同名动画，不会重新开始
        /// · 使用默认轨道 0（主体动画）
        /// · 使用默认过渡时长
        /// 
        /// 使用示例：
        ///   ctx.Animator.PlayAnim("idle", true);     // 循环播放 idle
        ///   ctx.Animator.PlayAnim("attack_1", false); // 播放一次 attack_1
        /// </summary>
        /// <param name="animName">Spine 中的动画名称</param>
        /// <param name="loop">是否循环播放</param>
        /// <returns>true = 成功切换动画，false = 同一动画已在播放（跳过）</returns>
        public bool PlayAnim(string animName, bool loop)
        {
            return PlayAnim(0, animName, loop, defaultMixDuration);
        }

        /// <summary>
        /// 完整版动画播放接口 —— 指定轨道和过渡时长。
        /// </summary>
        /// <param name="trackIndex">Spine 轨道索引</param>
        /// <param name="animName">动画名称</param>
        /// <param name="loop">是否循环</param>
        /// <param name="mixDuration">过渡融合时长，-1 = 使用 Spine 默认 mix</param>
        /// <returns>true = 成功切换，false = 跳过（同名动画防重复）</returns>
        public bool PlayAnim(int trackIndex, string animName, bool loop, float mixDuration = -1f)
        {
            // ── 防重复：同一轨道、同一动画正在播放 → 跳过 ──
            if (trackIndex < _currentAnimNames.Length &&
                _currentAnimNames[trackIndex] == animName)
            {
                return false;
            }

            // 更新缓存
            if (trackIndex < _currentAnimNames.Length)
                _currentAnimNames[trackIndex] = animName;

            // 调用 ISpineBridge 的标准方法
            PlayAnimation(trackIndex, animName, loop, mixDuration);
            return true;
        }

        // ══════════════════════════════════════════════════
        //  朝向翻转
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 设置 Spine 骨骼的朝向。
        /// 
        /// 为什么用 Skeleton.ScaleX 而不是 Transform.localScale.x？
        /// · Spine 骨骼翻转应通过 ScaleX，这样 IK / 物理约束不会反向
        /// · Transform.localScale 翻转会影响所有子物体（粒子、光源等）
        /// · 如果你的 Spine 导出时朝右，baseScaleX = 1；朝左则 baseScaleX = -1
        /// </summary>
        /// <param name="faceSign">1 = 朝右，-1 = 朝左</param>
        public void SetFaceDirection(float faceSign)
        {
            if (!controlFlip) return;
            if (Mathf.Approximately(_currentFaceSign, faceSign)) return;

            _currentFaceSign = faceSign;

#if HAS_SPINE_UNITY
            if (_skeleton != null)
            {
                // Skeleton.ScaleX 在 4.3 中未变（只有 Bone/Slot/Constraint 移到了 .Pose）
                _skeleton.ScaleX = baseScaleX * faceSign;
            }
#endif
        }

        /// <summary>
        /// 便捷重载：直接传入 FacingDirection 枚举。
        /// </summary>
        public void SetFaceDirection(Data.FacingDirection direction)
        {
            SetFaceDirection((float)(int)direction);
        }

        // ══════════════════════════════════════════════════
        //  IK 瞄准（预留）
        // ══════════════════════════════════════════════════

        /// <summary>
        /// 设置 IK 瞄准目标位置（世界坐标）。
        /// 
        /// 后续使用方式：
        ///   在 Update 或 LateUpdate 中，将鼠标世界坐标传入此方法，
        ///   Spine 的 IK 约束会自动让手臂/武器指向该位置。
        /// 
        /// 注意：需要先在 Spine 编辑器中设置好 IK 约束，
        ///       并在 Inspector 的 aimIKConstraintName 中填入对应名称。
        /// </summary>
        /// <param name="targetWorldPos">瞄准目标的世界坐标</param>
        /// <param name="mix">IK 权重 0~1（0=无效果，1=完全跟随）</param>
        public void SetAimTarget(Vector2 targetWorldPos, float mix = 1f)
        {
            _aimTarget = targetWorldPos;
            _aimMix = Mathf.Clamp01(mix);
            _aimEnabled = true;
        }

        /// <summary>禁用 IK 瞄准（手臂回到默认动画姿态）</summary>
        public void DisableAim()
        {
            _aimEnabled = false;
            _aimMix = 0f;
        }

        /// <summary>
        /// 在 LateUpdate 中应用 IK，确保在 Spine 动画更新之后修改骨骼。
        /// </summary>
        private void LateUpdate()
        {
            ApplyIK();
        }

        private void ApplyIK()
        {
#if HAS_SPINE_UNITY
            if (!_aimEnabled || _aimConstraint == null || _skeleton == null) return;

            // 将世界坐标转换为骨骼本地坐标
            var bone = _aimConstraint.Target;
            if (bone == null) return;

            Vector3 localPos = transform.InverseTransformPoint(_aimTarget);
            // Spine 4.3: Bone.X/Y → Bone.Pose.X/Y
            bone.Pose.X = localPos.x;
            bone.Pose.Y = localPos.y;
            // Spine 4.3: IkConstraint.Mix → IkConstraint.Pose.Mix
            _aimConstraint.Pose.Mix = _aimMix;
#endif
        }

        // ══════════════════════════════════════════════════
        //  ISpineBridge 接口实现
        // ══════════════════════════════════════════════════

        public override void PlayAnimation(int trackIndex, string animationName, bool loop, float mixDuration = -1f)
        {
#if HAS_SPINE_UNITY
            if (_skeletonAnim == null || _skeletonAnim.AnimationState == null) return;

            var entry = _skeletonAnim.AnimationState.SetAnimation(trackIndex, animationName, loop);
            if (entry != null && mixDuration >= 0f)
            {
                entry.MixDuration = mixDuration;
            }
#else
            // 模拟模式下仅输出日志（仅在 Editor 下）
            #if UNITY_EDITOR
            Debug.Log($"[SpineAnimator·模拟] PlayAnimation: track={trackIndex}, " +
                      $"anim=\"{animationName}\", loop={loop}");
            #endif
#endif
        }

        public override void QueueAnimation(int trackIndex, string animationName, bool loop, float delay = 0f)
        {
#if HAS_SPINE_UNITY
            if (_skeletonAnim == null || _skeletonAnim.AnimationState == null) return;

            _skeletonAnim.AnimationState.AddAnimation(trackIndex, animationName, loop, delay);
#endif
        }

        public override void ClearTrack(int trackIndex)
        {
#if HAS_SPINE_UNITY
            if (_skeletonAnim == null || _skeletonAnim.AnimationState == null) return;

            _skeletonAnim.AnimationState.ClearTrack(trackIndex);
#endif
            // 清除缓存
            if (trackIndex < _currentAnimNames.Length)
                _currentAnimNames[trackIndex] = null;
        }

        public override string GetCurrentAnimation(int trackIndex)
        {
#if HAS_SPINE_UNITY
            if (_skeletonAnim == null || _skeletonAnim.AnimationState == null) return null;

            var current = _skeletonAnim.AnimationState.GetCurrent(trackIndex);
            return current?.Animation?.Name;
#else
            return trackIndex < _currentAnimNames.Length ? _currentAnimNames[trackIndex] : null;
#endif
        }

        public override bool IsAnimationComplete(int trackIndex)
        {
#if HAS_SPINE_UNITY
            if (_skeletonAnim == null || _skeletonAnim.AnimationState == null) return true;

            var current = _skeletonAnim.AnimationState.GetCurrent(trackIndex);
            if (current == null) return true;
            if (current.Loop) return false; // 循环动画永远不算"完成"

            return current.IsComplete;
#else
            return true;
#endif
        }

        // ── Skin / 换装 ──────────────────────────────────

        public override void SetCombinedSkins(params string[] skinNames)
        {
#if HAS_SPINE_UNITY
            if (_skeleton == null || skinNames == null || skinNames.Length == 0) return;

            var combined = new Skin("combined");
            foreach (var name in skinNames)
            {
                var skin = _skeleton.Data.FindSkin(name);
                if (skin != null)
                    combined.AddSkin(skin);
                else
                    Debug.LogWarning($"[SpineAnimator] 找不到皮肤 \"{name}\"", this);
            }

            _skeleton.SetSkin(combined);
            // Spine 4.3: SetSlotsToSetupPose() → SetupPoseSlots()
            _skeleton.SetupPoseSlots();

            // 通知 AnimationState 刷新附件
            _skeletonAnim.AnimationState.Apply(_skeleton);
#endif
        }

        public override void AddSkin(string skinName)
        {
#if HAS_SPINE_UNITY
            if (_skeleton == null) return;

            var currentSkin = _skeleton.Skin;
            if (currentSkin == null)
            {
                SetCombinedSkins(skinName);
                return;
            }

            var additionalSkin = _skeleton.Data.FindSkin(skinName);
            if (additionalSkin == null)
            {
                Debug.LogWarning($"[SpineAnimator] 找不到皮肤 \"{skinName}\"", this);
                return;
            }

            currentSkin.AddSkin(additionalSkin);
            // Spine 4.3: SetSlotsToSetupPose() → SetupPoseSlots()
            _skeleton.SetupPoseSlots();
            _skeletonAnim.AnimationState.Apply(_skeleton);
#endif
        }

        // ── IK（标准接口） ──────────────────────────────

        public override void SetIKTarget(string constraintName, Vector2 worldPosition, float mix = 1f)
        {
#if HAS_SPINE_UNITY
            if (_skeleton == null) return;

            // Spine 4.3: FindIkConstraint() → FindConstraint<IkConstraint>()
            var constraint = _skeleton.FindConstraint<IkConstraint>(constraintName);
            if (constraint == null) return;

            var bone = constraint.Target;
            if (bone == null) return;

            Vector3 localPos = transform.InverseTransformPoint(worldPosition);
            // Spine 4.3: Bone.X/Y → Bone.Pose.X/Y
            bone.Pose.X = localPos.x;
            bone.Pose.Y = localPos.y;
            // Spine 4.3: IkConstraint.Mix → IkConstraint.Pose.Mix
            constraint.Pose.Mix = Mathf.Clamp01(mix);
#endif
        }

        // ── 材质属性 ────────────────────────────────────

        public override void SetMaterialProperty(string propertyName, float value)
        {
#if HAS_SPINE_UNITY
            if (_skeletonAnim == null) return;

            var mpb = new MaterialPropertyBlock();
            // Spine 4.3: SkeletonAnimation 不再继承 SkeletonRenderer，
            //             渲染器拆分为独立的 SkeletonRenderer 组件
            var skRenderer = _skeletonAnim.GetComponent<SkeletonRenderer>();
            if (skRenderer == null) skRenderer = GetComponent<SkeletonRenderer>();
            if (skRenderer == null) return;

            var meshRenderer = skRenderer.GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(propertyName, value);
            meshRenderer.SetPropertyBlock(mpb);
#endif
        }

        public override void SetMaterialProperty(string propertyName, Color value)
        {
#if HAS_SPINE_UNITY
            if (_skeletonAnim == null) return;

            var mpb = new MaterialPropertyBlock();
            // Spine 4.3: 渲染器拆分为独立的 SkeletonRenderer 组件
            var skRenderer = _skeletonAnim.GetComponent<SkeletonRenderer>();
            if (skRenderer == null) skRenderer = GetComponent<SkeletonRenderer>();
            if (skRenderer == null) return;

            var meshRenderer = skRenderer.GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(propertyName, value);
            meshRenderer.SetPropertyBlock(mpb);
#endif
        }

        // ══════════════════════════════════════════════════
        //  Spine 事件回调（转发到 ISpineBridge 事件）
        // ══════════════════════════════════════════════════

#if HAS_SPINE_UNITY
        /// <summary>
        /// Spine 动画事件回调。
        /// 在 Spine 编辑器中给关键帧添加的 Event（如 "footstep"、"attack_hit"）
        /// 会在播放到该帧时触发此回调。
        /// 
        /// 通过 AbstractSpineBridge.RaiseAnimationEvent() 转发给外部监听者。
        /// </summary>
        private void OnSpineEvent(TrackEntry trackEntry, global::Spine.Event e)
        {
            RaiseAnimationEvent(e.Data.Name);
        }

        /// <summary>
        /// Spine 动画播放完成回调（非循环动画结束时触发）。
        /// </summary>
        private void OnSpineComplete(TrackEntry trackEntry)
        {
            RaiseAnimationComplete(trackEntry.TrackIndex);

            // 清除动画名缓存，允许下次重新播放同一动画
            int idx = trackEntry.TrackIndex;
            if (idx < _currentAnimNames.Length)
                _currentAnimNames[idx] = null;
        }

        private void OnDestroy()
        {
            // 取消事件订阅，防止内存泄漏
            if (_skeletonAnim != null && _skeletonAnim.AnimationState != null)
            {
                _skeletonAnim.AnimationState.Event -= OnSpineEvent;
                _skeletonAnim.AnimationState.Complete -= OnSpineComplete;
            }
        }
#endif

        // ══════════════════════════════════════════════════
        //  Debug 辅助
        // ══════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            // 在 Game 视图左上角显示当前动画状态（开发调试用）
            string anim0 = _currentAnimNames.Length > 0 ? _currentAnimNames[0] : "null";
            string anim1 = _currentAnimNames.Length > 1 ? _currentAnimNames[1] : "null";
            GUI.Label(new Rect(10, 80, 300, 20),
                $"[SpineAnimator] Track0: {anim0}  Track1: {anim1}");
        }
#endif
    }
}
