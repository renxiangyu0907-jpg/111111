// ============================================================================
// CutsceneData.cs — 过场演出数据容器（ScriptableObject）
// ============================================================================
//
//  创建方式：Project 面板右键 → Create → GhostVeil → Cutscene Data
//
//  数据结构：
//    CutsceneData
//      └── CutsceneStep[]
//            ├── stepType        （步骤类型：对话 / 镜头移动 / 动画 / 等待 / 事件）
//            ├── 对话参数        （speakerName, content, portraitKey）
//            ├── 镜头参数        （targetPosition, cameraTransitionTime）
//            ├── 动画参数        （targetTag, animationName）
//            ├── 等待参数        （waitDuration）
//            └── 事件参数        （eventID）
//

using UnityEngine;

namespace GhostVeil.Data.ScriptableObjects
{
    [CreateAssetMenu(fileName = "NewCutscene", menuName = "GhostVeil/Cutscene Data")]
    public class CutsceneData : ScriptableObject
    {
        [Header("=== 过场基本信息 ===")]
        [Tooltip("过场 ID（唯一标识）")]
        public string cutsceneID = "cutscene_001";

        [Tooltip("过场标题（编辑器用）")]
        public string title = "新过场";

        [Header("=== 控制权 ===")]
        [Tooltip("过场期间的控制权级别")]
        public NarrativeAuthorityLevel authorityLevel = NarrativeAuthorityLevel.Cutscene;

        [Header("=== 步骤序列 ===")]
        public CutsceneStep[] steps = new CutsceneStep[0];

        [Header("=== 设置 ===")]
        [Tooltip("是否允许玩家跳过此过场")]
        public bool allowSkip = true;

        [Tooltip("过场结束后触发的事件 ID（可为空）")]
        public string completionEventID = "";
    }

    /// <summary>
    /// 过场步骤的类型。
    /// </summary>
    public enum CutsceneStepType
    {
        /// <summary>显示一行对话（逐字打印）</summary>
        Dialogue,

        /// <summary>镜头移动到指定位置/目标</summary>
        CameraMove,

        /// <summary>播放指定对象的 Spine 动画</summary>
        PlayAnimation,

        /// <summary>等待指定时间</summary>
        Wait,

        /// <summary>同时执行多个步骤（并行）</summary>
        Parallel,

        /// <summary>触发自定义事件</summary>
        TriggerEvent,

        /// <summary>屏幕渐变（淡入/淡出）</summary>
        Fade,

        /// <summary>相机震动</summary>
        CameraShake
    }

    /// <summary>
    /// 单个过场步骤的数据。
    /// 根据 stepType 的不同，读取不同的字段。
    /// </summary>
    [System.Serializable]
    public class CutsceneStep
    {
        [Header("=== 步骤类型 ===")]
        public CutsceneStepType stepType = CutsceneStepType.Wait;

        // ── Dialogue 参数 ────────────────────────────
        [Header("--- Dialogue ---")]
        [Tooltip("说话人名字")]
        public string speakerName = "";

        [Tooltip("对话内容")]
        [TextArea(2, 5)]
        public string content = "";

        [Tooltip("立绘/头像 key")]
        public string portraitKey = "";

        [Tooltip("逐字打印速度（字/秒）。0 = 默认")]
        public float typeSpeed = 0f;

        [Tooltip("是否自动翻页")]
        public bool autoAdvance = false;

        [Tooltip("自动翻页等待时间（秒）")]
        public float autoAdvanceDelay = 2f;

        // ── CameraMove 参数 ──────────────────────────
        [Header("--- Camera ---")]
        [Tooltip("镜头移动目标位置（世界坐标）")]
        public Vector3 cameraTargetPosition;

        [Tooltip("目标对象的 Tag（优先使用，如果非空则忽略 cameraTargetPosition）")]
        public string cameraTargetTag = "";

        [Tooltip("镜头移动过渡时间（秒）")]
        public float cameraTransitionTime = 1f;

        // ── PlayAnimation 参数 ───────────────────────
        [Header("--- Animation ---")]
        [Tooltip("目标对象的 Tag（用于查找场景中的对象）")]
        public string animationTargetTag = "";

        [Tooltip("要播放的 Spine 动画名称")]
        public string animationName = "";

        [Tooltip("是否循环播放")]
        public bool animationLoop = false;

        [Tooltip("是否等待动画播放完成后再执行下一步")]
        public bool waitForAnimation = false;

        // ── Wait 参数 ────────────────────────────────
        [Header("--- Wait ---")]
        [Tooltip("等待时长（秒）")]
        public float waitDuration = 1f;

        // ── TriggerEvent 参数 ────────────────────────
        [Header("--- Event ---")]
        [Tooltip("要触发的事件 ID")]
        public string eventID = "";

        // ── Fade 参数 ────────────────────────────────
        [Header("--- Fade ---")]
        [Tooltip("是否淡入（true = 淡入/从黑到透明，false = 淡出/从透明到黑）")]
        public bool fadeIn = false;

        [Tooltip("渐变持续时间（秒）")]
        public float fadeDuration = 1f;

        [Tooltip("渐变颜色")]
        public Color fadeColor = Color.black;

        // ── CameraShake 参数 ─────────────────────────
        [Header("--- Shake ---")]
        [Tooltip("震动强度")]
        public float shakeIntensity = 0.3f;

        [Tooltip("震动持续时间（秒）")]
        public float shakeDuration = 0.5f;

        // ── Parallel 参数 ────────────────────────────
        [Header("--- Parallel ---")]
        [Tooltip("并行执行的子步骤")]
        public CutsceneStep[] parallelSteps;
    }
}
