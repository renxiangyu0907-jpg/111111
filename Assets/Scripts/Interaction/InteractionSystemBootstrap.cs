// ============================================================================
// InteractionSystemBootstrap.cs — 交互系统启动引导器
// ============================================================================
//
//  放在场景中任意 GameObject 上，自动创建交互系统所需的管理器：
//    · InteractionPromptUI（头顶按键提示）
//    · DialogueBoxUI（底部对话框）
//
//  如果场景中已有这些组件，则跳过创建。
//

using UnityEngine;
using GhostVeil.UI;

namespace GhostVeil.Interaction
{
    /// <summary>
    /// 交互系统自动引导器。
    /// 放在场景中即可自动初始化所有 UI 管理器。
    /// </summary>
    public class InteractionSystemBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // ── 交互提示 UI ──────────────────────────────
            if (FindObjectOfType<InteractionPromptUI>() == null)
            {
                var promptGO = new GameObject("[InteractionPromptUI]");
                promptGO.AddComponent<InteractionPromptUI>();
                DontDestroyOnLoad(promptGO);
            }

            // ── 对话框 UI ────────────────────────────────
            if (FindObjectOfType<DialogueBoxUI>() == null)
            {
                var dialogueGO = new GameObject("[DialogueBoxUI]");
                dialogueGO.AddComponent<DialogueBoxUI>();
                DontDestroyOnLoad(dialogueGO);
            }

            Debug.Log("[InteractionSystem] 交互系统初始化完成。");
        }
    }
}
