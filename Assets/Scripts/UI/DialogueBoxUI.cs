// ============================================================================
// DialogueBoxUI.cs — 对话框 UI（逐字打印 + 角色立绘 + 翻页提示）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  双模式工作：                                                            │
// │                                                                          │
// │  模式 A：事件驱动（兼容旧版 ExaminableObject / NPCInteractable）         │
// │    · 监听 DialogueLineEvent，直接显示一行文字，几秒后自动消失            │
// │                                                                          │
// │  模式 B：DialogueRunner 驱动（正式对话系统）                              │
// │    · 订阅 DialogueRunner 的 OnTypewriterUpdate → 逐字打印              │
// │    · 订阅 OnLineComplete → 显示翻页箭头                                 │
// │    · 订阅 OnDialogueBoxHide → 隐藏对话框                               │
// │    · 显示角色立绘/头像（通过 portraitKey → Resources 加载 Sprite）      │
// │                                                                          │
// │  使用 OnGUI 实现，零依赖（无需 Canvas / TextMeshPro）                    │
// │  后续可替换为 Canvas + TextMeshPro 版本以获得更好的视觉效果              │
// └──────────────────────────────────────────────────────────────────────────┘

using UnityEngine;
using GhostVeil.Core.Event;
using GhostVeil.Narrative.Dialogue;

namespace GhostVeil.UI
{
    public class DialogueBoxUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 显示设置 ===")]
        [Tooltip("快捷对话事件的默认显示时长")]
        [SerializeField] private float displayDuration = 4f;

        [Tooltip("对话文本字体大小")]
        [SerializeField] private int fontSize = 22;

        [Tooltip("说话人名字字体大小")]
        [SerializeField] private int speakerFontSize = 24;

        [Tooltip("文本颜色")]
        [SerializeField] private Color textColor = Color.white;

        [Tooltip("说话人名字颜色")]
        [SerializeField] private Color speakerColor = new Color(0.4f, 0.9f, 1f, 1f); // 浅青色

        [Tooltip("对话框背景颜色")]
        [SerializeField] private Color bgColor = new Color(0.05f, 0.05f, 0.12f, 0.92f);

        [Tooltip("翻页箭头闪烁速度")]
        [SerializeField] private float arrowBlinkSpeed = 3f;

        [Header("=== 立绘设置 ===")]
        [Tooltip("立绘/头像显示区域宽度（屏幕占比）")]
#pragma warning disable CS0414
        [SerializeField] private float portraitWidthRatio = 0.1f;
#pragma warning restore CS0414

        [Header("=== DialogueRunner 引用（留空自动查找） ===")]
        [SerializeField] private DialogueRunner dialogueRunnerRef;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        // ── 通用显示状态 ────────────────────────────
        private bool _showing;
        private string _speakerName = "";
        private string _displayText = "";
        private string _portraitKey = "";

        // ── 模式 A：事件驱动计时 ────────────────────
        private float _hideTimer;
        private bool _isEventMode; // true = DialogueLineEvent 触发，有自动隐藏

        // ── 模式 B：逐字打印状态 ────────────────────
        private bool _isTypewriterMode;
        private bool _lineComplete;
        private float _arrowTimer;

        // ── 立绘缓存 ────────────────────────────────
        private Sprite _currentPortrait;
        private string _cachedPortraitKey = "";

        // ── GUIStyle ────────────────────────────────
        private GUIStyle _textStyle;
        private GUIStyle _speakerStyle;
        private GUIStyle _bgStyle;
        private GUIStyle _arrowStyle;
        private Texture2D _bgTexture;

        // ── DialogueRunner 引用 ─────────────────────
        private DialogueRunner _runner;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void OnEnable()
        {
            // 模式 A：订阅全局事件
            GameEvent.Subscribe<DialogueLineEvent>(OnDialogueLine);
        }

        private void OnDisable()
        {
            GameEvent.Unsubscribe<DialogueLineEvent>(OnDialogueLine);
            UnbindRunner();
        }

        private void Start()
        {
            // 创建背景纹理
            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, bgColor);
            _bgTexture.Apply();

            // 查找并绑定 DialogueRunner
            BindRunner();
        }

        private void Update()
        {
            // 模式 A 的自动隐藏计时
            if (_showing && _isEventMode)
            {
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0f)
                {
                    _showing = false;
                    _isEventMode = false;
                }
            }

            // 翻页箭头闪烁
            if (_lineComplete)
            {
                _arrowTimer += Time.deltaTime * arrowBlinkSpeed;
            }
        }

        private void OnDestroy()
        {
            if (_bgTexture != null)
                Object.Destroy(_bgTexture);
            UnbindRunner();
        }

        // ══════════════════════════════════════════════
        //  DialogueRunner 绑定
        // ══════════════════════════════════════════════

        private void BindRunner()
        {
            _runner = dialogueRunnerRef;
            if (_runner == null)
                _runner = FindObjectOfType<DialogueRunner>();

            if (_runner != null)
            {
                _runner.OnTypewriterUpdate += OnTypewriterUpdate;
                _runner.OnLineComplete += OnLineCompleteHandler;
                _runner.OnDialogueBoxHide += OnDialogueHide;
            }
        }

        private void UnbindRunner()
        {
            if (_runner != null)
            {
                _runner.OnTypewriterUpdate -= OnTypewriterUpdate;
                _runner.OnLineComplete -= OnLineCompleteHandler;
                _runner.OnDialogueBoxHide -= OnDialogueHide;
            }
        }

        // ══════════════════════════════════════════════
        //  模式 A：DialogueLineEvent 处理
        // ══════════════════════════════════════════════

        private void OnDialogueLine(DialogueLineEvent evt)
        {
            // 如果 DialogueRunner 正在运行，忽略原始事件（避免重复）
            if (_runner != null && _runner.IsRunning) return;

            _speakerName = evt.SpeakerName;
            _displayText = evt.Content;
            _portraitKey = evt.PortraitKey;
            _showing = true;
            _isEventMode = true;
            _isTypewriterMode = false;
            _lineComplete = false;
            _hideTimer = displayDuration;

            LoadPortrait(evt.PortraitKey);
        }

        // ══════════════════════════════════════════════
        //  模式 B：DialogueRunner 回调
        // ══════════════════════════════════════════════

        private void OnTypewriterUpdate(string speaker, string partialText, string portraitKey)
        {
            _speakerName = speaker;
            _displayText = partialText;
            _portraitKey = portraitKey;
            _showing = true;
            _isEventMode = false;
            _isTypewriterMode = true;
            _lineComplete = false;

            LoadPortrait(portraitKey);
        }

        private void OnLineCompleteHandler()
        {
            _lineComplete = true;
            _arrowTimer = 0f;
        }

        private void OnDialogueHide()
        {
            _showing = false;
            _isTypewriterMode = false;
            _lineComplete = false;
            _displayText = "";
            _speakerName = "";
            _currentPortrait = null;
            _cachedPortraitKey = "";
        }

        // ══════════════════════════════════════════════
        //  立绘加载
        // ══════════════════════════════════════════════

        private void LoadPortrait(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                _currentPortrait = null;
                _cachedPortraitKey = "";
                return;
            }

            if (key == _cachedPortraitKey) return; // 同一立绘无需重新加载

            _cachedPortraitKey = key;
            _currentPortrait = Resources.Load<Sprite>($"Portraits/{key}");

            if (_currentPortrait == null)
            {
                // 退而求其次尝试直接路径
                _currentPortrait = Resources.Load<Sprite>(key);
            }
        }

        // ══════════════════════════════════════════════
        //  OnGUI 渲染
        // ══════════════════════════════════════════════

        private void OnGUI()
        {
            if (!_showing) return;

            InitStyles();

            // ── 计算对话框区域 ────────────────────────
            float boxWidth = Screen.width * 0.85f;
            float boxHeight = 120f;
            float boxX = (Screen.width - boxWidth) / 2f;
            float boxY = Screen.height - boxHeight - 20f;
            float pad = 16f;

            // ── 绘制半透明背景 ────────────────────────
            GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), GUIContent.none, _bgStyle);

            // ── 绘制分隔线（顶部装饰线） ──────────────
            Rect lineRect = new Rect(boxX + pad, boxY + 2f, boxWidth - pad * 2, 2f);
            GUI.DrawTexture(lineRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                speakerColor * new Color(1, 1, 1, 0.5f), 0, 0);

            float contentX = boxX + pad;
            float contentY = boxY + pad;
            float contentWidth = boxWidth - pad * 2;

            // ── 绘制角色立绘/头像（左侧） ──────────────
            if (_currentPortrait != null)
            {
                float portraitSize = boxHeight - pad * 2;
                Rect portraitRect = new Rect(contentX, contentY, portraitSize, portraitSize);
                GUI.DrawTexture(portraitRect, _currentPortrait.texture, ScaleMode.ScaleToFit);

                contentX += portraitSize + pad;
                contentWidth -= portraitSize + pad;
            }

            // ── 绘制说话人名字 ────────────────────────
            if (!string.IsNullOrEmpty(_speakerName))
            {
                Rect speakerRect = new Rect(contentX, contentY, contentWidth, 28f);
                GUI.Label(speakerRect, _speakerName, _speakerStyle);
                contentY += 30f;
            }

            // ── 绘制对话文本（逐字打印 or 完整文本） ──
            float textHeight = boxHeight - (contentY - boxY) - pad;
            Rect textRect = new Rect(contentX, contentY, contentWidth - 30f, textHeight);
            GUI.Label(textRect, _displayText, _textStyle);

            // ── 绘制翻页箭头（逐字打印完成后） ─────────
            if (_isTypewriterMode && _lineComplete)
            {
                float alpha = (Mathf.Sin(_arrowTimer * Mathf.PI) + 1f) * 0.5f;
                Color arrowColor = new Color(1f, 1f, 1f, alpha);
                _arrowStyle.normal.textColor = arrowColor;

                float arrowX = boxX + boxWidth - pad - 20f;
                float arrowY = boxY + boxHeight - pad - 20f;
                GUI.Label(new Rect(arrowX, arrowY, 20f, 20f), "\u25BC", _arrowStyle); // ▼
            }
        }

        // ══════════════════════════════════════════════
        //  GUI 样式初始化
        // ══════════════════════════════════════════════

        private void InitStyles()
        {
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    wordWrap = true,
                    richText = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = textColor }
                };
            }

            if (_speakerStyle == null)
            {
                _speakerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = speakerFontSize,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = speakerColor }
                };
            }

            if (_bgStyle == null)
            {
                _bgStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = _bgTexture }
                };
            }

            if (_arrowStyle == null)
            {
                _arrowStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }
        }
    }
}
