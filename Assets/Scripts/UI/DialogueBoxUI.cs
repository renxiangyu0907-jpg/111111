// ============================================================================
// DialogueBoxUI.cs — 简易对话框 UI（OnGUI 实现，零依赖）
// ============================================================================
//
//  监听 DialogueLineEvent，在屏幕底部显示对话文本。
//  自动在几秒后消失，或按 E 立即关闭。
//  后续可替换为 TextMeshPro + Canvas 版本。
//

using UnityEngine;
using GhostVeil.Core.Event;

namespace GhostVeil.UI
{
    public class DialogueBoxUI : MonoBehaviour
    {
        [Header("=== 显示设置 ===")]
        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private int fontSize = 20;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color speakerColor = Color.cyan;
        [SerializeField] private Color bgColor = new Color(0f, 0f, 0f, 0.8f);

        // ── 运行时 ──────────────────────────────────
        private bool _showing;
        private string _speakerName;
        private string _content;
        private float _hideTimer;
        private GUIStyle _textStyle;
        private GUIStyle _speakerStyle;
        private GUIStyle _bgStyle;
        private Texture2D _bgTexture;

        private void OnEnable()
        {
            GameEvent.Subscribe<DialogueLineEvent>(OnDialogueLine);
        }

        private void OnDisable()
        {
            GameEvent.Unsubscribe<DialogueLineEvent>(OnDialogueLine);
        }

        private void Start()
        {
            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, bgColor);
            _bgTexture.Apply();
        }

        private void Update()
        {
            if (!_showing) return;

            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f)
                _showing = false;
        }

        private void OnDialogueLine(DialogueLineEvent evt)
        {
            _speakerName = evt.SpeakerName;
            _content = evt.Content;
            _showing = true;
            _hideTimer = displayDuration;
        }

        private void OnGUI()
        {
            if (!_showing) return;

            // ── 初始化样式 ──────────────────────────────
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = textColor }
                };
            }

            if (_speakerStyle == null)
            {
                _speakerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize + 2,
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

            // ── 绘制对话框（屏幕底部） ──────────────────
            float boxWidth = Screen.width * 0.8f;
            float boxHeight = 100f;
            float boxX = (Screen.width - boxWidth) / 2f;
            float boxY = Screen.height - boxHeight - 30f;
            float pad = 16f;

            // 背景
            GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), GUIContent.none, _bgStyle);

            float contentY = boxY + pad;

            // 说话人名字
            if (!string.IsNullOrEmpty(_speakerName))
            {
                GUI.Label(new Rect(boxX + pad, contentY, boxWidth - pad * 2, 30f),
                    _speakerName, _speakerStyle);
                contentY += 28f;
            }

            // 对话内容
            GUI.Label(new Rect(boxX + pad, contentY, boxWidth - pad * 2, boxHeight - pad * 2 - 28f),
                _content, _textStyle);
        }

        private void OnDestroy()
        {
            if (_bgTexture != null)
                Destroy(_bgTexture);
        }
    }
}
