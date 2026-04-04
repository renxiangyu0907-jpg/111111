// ============================================================================
// InteractionSystemBootstrap.cs — 交互 + 叙事系统启动引导器
// ============================================================================
//
//  放在场景中任意 GameObject 上，自动创建所有子系统所需的管理器：
//    · InteractionPromptUI  （头顶按键提示）
//    · DialogueBoxUI        （底部对话框 + 逐字打印）
//    · NarrativeController  （控制权仲裁 + 输入锁定）
//    · DialogueRunner       （对话运行器 + 逐字打印驱动）
//    · CutsceneDirector     （过场演出控制器）
//    · ScreenFadeUI         （全屏渐变遮罩）
//
//  如果场景中已有这些组件，则跳过创建。
//

using UnityEngine;
using GhostVeil.UI;
using GhostVeil.Narrative.Controller;
using GhostVeil.Narrative.Dialogue;
using GhostVeil.Narrative.Cutscene;
using GhostVeil.Drone;

namespace GhostVeil.Interaction
{
    /// <summary>
    /// 交互 + 叙事系统自动引导器。
    /// 放在场景中即可自动初始化所有子系统。
    /// </summary>
    public class InteractionSystemBootstrap : MonoBehaviour
    {
        [Header("=== 设置 ===")]
        [Tooltip("是否跨场景持久化（DontDestroyOnLoad）")]
        [SerializeField] private bool persistent = true;

        private void Awake()
        {
            // ══════════════════════════════════════════════
            //  叙事系统核心（必须先于 UI 初始化）
            // ══════════════════════════════════════════════

            // ── NarrativeController（控制权仲裁） ────────
            if (FindObjectOfType<NarrativeController>() == null)
            {
                var go = new GameObject("[NarrativeController]");
                go.AddComponent<NarrativeController>();
                MakePersistent(go);
                Debug.Log("[InteractionSystem] NarrativeController 已创建");
            }

            // ── DialogueRunner（对话运行器） ────────────
            if (FindObjectOfType<DialogueRunner>() == null)
            {
                var go = new GameObject("[DialogueRunner]");
                go.AddComponent<DialogueRunner>();
                MakePersistent(go);
                Debug.Log("[InteractionSystem] DialogueRunner 已创建");
            }

            // ── CutsceneDirector（过场控制器） ──────────
            if (FindObjectOfType<CutsceneDirector>() == null)
            {
                var go = new GameObject("[CutsceneDirector]");
                go.AddComponent<CutsceneDirector>();
                MakePersistent(go);
                Debug.Log("[InteractionSystem] CutsceneDirector 已创建");
            }

            // ══════════════════════════════════════════════
            //  UI 组件
            // ══════════════════════════════════════════════

            // ── 交互提示 UI ────────────────────────────
            if (FindObjectOfType<InteractionPromptUI>() == null)
            {
                var go = new GameObject("[InteractionPromptUI]");
                go.AddComponent<InteractionPromptUI>();
                MakePersistent(go);
                Debug.Log("[InteractionSystem] InteractionPromptUI 已创建");
            }

            // ── 对话框 UI ──────────────────────────────
            if (FindObjectOfType<DialogueBoxUI>() == null)
            {
                var go = new GameObject("[DialogueBoxUI]");
                go.AddComponent<DialogueBoxUI>();
                MakePersistent(go);
                Debug.Log("[InteractionSystem] DialogueBoxUI 已创建");
            }

            // ── 全屏渐变遮罩 ──────────────────────────
            if (FindObjectOfType<ScreenFadeUI>() == null)
            {
                var go = new GameObject("[ScreenFadeUI]");
                go.AddComponent<ScreenFadeUI>();
                MakePersistent(go);
                Debug.Log("[InteractionSystem] ScreenFadeUI 已创建");
            }

            // ── 准星 UI ──────────────────────────────────
            if (FindObjectOfType<CrosshairUI>() == null)
            {
                var go = new GameObject("[CrosshairUI]");
                go.AddComponent<CrosshairUI>();
                MakePersistent(go);
                Debug.Log("[InteractionSystem] CrosshairUI 已创建");
            }

            // ══════════════════════════════════════════════
            //  无人机系统
            // ══════════════════════════════════════════════

            // ── DroneManager（无人机编队管理器） ──────────
            if (FindObjectOfType<DroneManager>() == null)
            {
                var go = new GameObject("[DroneManager]");
                go.AddComponent<DroneManager>();
                MakePersistent(go);
                Debug.Log("[InteractionSystem] DroneManager 已创建");
            }

            Debug.Log("[InteractionSystem] 交互 + 叙事系统初始化完成。");
        }

        private void MakePersistent(GameObject go)
        {
            if (persistent)
                DontDestroyOnLoad(go);
        }
    }
}
