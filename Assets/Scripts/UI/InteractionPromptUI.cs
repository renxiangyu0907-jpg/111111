// ============================================================================
// InteractionPromptUI.cs — 交互提示 UI（头顶浮现"按 E 调查"）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  职责：                                                                  │
// │                                                                          │
// │  1. 监听 InteractionPromptEvent 事件（由 AbstractInteractable 发布）    │
// │  2. 在可交互对象头顶显示/隐藏按键提示（世界空间 Canvas）                │
// │  3. 支持自定义提示文本（"按 E 调查" / "按 E 对话" 等）                │
// │  4. 提示 UI 自动跟随目标对象位置                                        │
// │                                                                          │
// │  挂载方式：                                                               │
// │    · 挂在一个场景中的 Canvas 物体上                                      │
// │    · 或者作为 Prefab 由 InteractionDetector 自动实例化                  │
// │                                                                          │
// │  替代方案（无 Canvas 依赖）：                                             │
// │    · 本脚本使用 OnGUI 绘制，无需预先搭建 Canvas                         │
// │    · 生产环境建议替换为 World Space Canvas + TextMeshPro                 │
// └──────────────────────────────────────────────────────────────────────────┘

using UnityEngine;
using GhostVeil.Core.Event;

namespace GhostVeil.UI
{
    /// <summary>
    /// 轻量级交互提示 UI。
    /// 
    /// 使用 OnGUI 实现，零依赖（无需 Canvas、TextMeshPro）。
    /// 后期可替换为 World Space Canvas 版本以获得更好的视觉效果。
    /// 
    /// 工作原理：
    ///   1. 订阅 InteractionPromptEvent 事件
    ///   2. 收到 Show=true 时记录目标对象和提示文本
    ///   3. 每帧在 OnGUI 中将目标世界坐标转为屏幕坐标，绘制提示标签
    ///   4. 收到 Show=false 时隐藏提示
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 显示设置 ===")]
        [Tooltip("提示文本在目标头顶的偏移（世界单位）")]
        [SerializeField] private Vector2 worldOffset = new Vector2(0f, 2.0f);

        [Tooltip("提示标签的字体大小")]
        [SerializeField] private int fontSize = 18;

        [Tooltip("提示文本颜色")]
        [SerializeField] private Color textColor = Color.white;

        [Tooltip("背景颜色")]
        [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.7f);

        [Tooltip("内边距")]
        [SerializeField] private Vector2 padding = new Vector2(16f, 8f);

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private bool _showPrompt;
        private GameObject _targetObject;
        private string _promptText = "按 E 交互";
        private GUIStyle _labelStyle;
        private GUIStyle _bgStyle;
        private Texture2D _bgTexture;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void OnEnable()
        {
            GameEvent.Subscribe<InteractionPromptEvent>(OnPromptEvent);
        }

        private void OnDisable()
        {
            GameEvent.Unsubscribe<InteractionPromptEvent>(OnPromptEvent);
        }

        private void Start()
        {
            // 创建背景纹理
            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, bgColor);
            _bgTexture.Apply();
        }

        private void OnDestroy()
        {
            if (_bgTexture != null)
                Destroy(_bgTexture);
        }

        // ══════════════════════════════════════════════
        //  事件处理
        // ══════════════════════════════════════════════

        private void OnPromptEvent(InteractionPromptEvent evt)
        {
            if (evt.Show)
            {
                _showPrompt = true;
                _targetObject = evt.Interactable;

                // 根据交互类型生成提示文本
                var interactable = _targetObject.GetComponent<IInteractable>();
                if (interactable != null && !string.IsNullOrEmpty(interactable.PromptText))
                {
                    _promptText = $"按 E  {interactable.PromptText}";
                }
                else
                {
                    _promptText = GetDefaultPromptText(evt.Type);
                }
            }
            else
            {
                // 只有当隐藏的是当前显示的目标时才隐藏
                if (_targetObject == evt.Interactable)
                {
                    _showPrompt = false;
                    _targetObject = null;
                }
            }
        }

        // ══════════════════════════════════════════════
        //  OnGUI 渲染
        // ══════════════════════════════════════════════

        private void OnGUI()
        {
            if (!_showPrompt || _targetObject == null) return;

            // ── 初始化样式（延迟到 OnGUI 中） ──────────
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = textColor }
                };
            }

            if (_bgStyle == null)
            {
                _bgStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = _bgTexture }
                };
            }

            // ── 世界坐标 → 屏幕坐标 ────────────────────
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = _targetObject.transform.position + 
                               new Vector3(worldOffset.x, worldOffset.y, 0f);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // 目标在摄像机后面时不显示
            if (screenPos.z < 0) return;

            // Unity OnGUI 的 Y 轴是从上往下的，需要翻转
            float guiY = Screen.height - screenPos.y;

            // ── 计算标签尺寸 ────────────────────────────
            GUIContent content = new GUIContent(_promptText);
            Vector2 labelSize = _labelStyle.CalcSize(content);

            // 居中绘制
            float boxWidth = labelSize.x + padding.x * 2;
            float boxHeight = labelSize.y + padding.y * 2;
            float boxX = screenPos.x - boxWidth / 2f;
            float boxY = guiY - boxHeight;

            // ── 绘制背景 + 文本 ────────────────────────
            Rect bgRect = new Rect(boxX, boxY, boxWidth, boxHeight);
            GUI.Box(bgRect, GUIContent.none, _bgStyle);

            Rect labelRect = new Rect(boxX + padding.x, boxY + padding.y, 
                                       labelSize.x, labelSize.y);
            GUI.Label(labelRect, content, _labelStyle);
        }

        // ══════════════════════════════════════════════
        //  辅助方法
        // ══════════════════════════════════════════════

        private string GetDefaultPromptText(Data.InteractionType type)
        {
            return type switch
            {
                Data.InteractionType.Examine   => "按 E  调查",
                Data.InteractionType.PickUp    => "按 E  拾取",
                Data.InteractionType.Dialogue  => "按 E  对话",
                Data.InteractionType.Mechanism => "按 E  操作",
                Data.InteractionType.Portal    => "按 E  进入",
                _ => "按 E  交互"
            };
        }
    }
}
