// ============================================================================
// CrosshairUI.cs — 屏幕准星 UI（跟随鼠标位置绘制十字准星）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  核心功能：                                                              │
// │                                                                          │
// │  1. 在鼠标位置绘制十字准星（+）                                          │
// │  2. 支持自定义颜色、大小、线条粗细                                        │
// │  3. 在叙事接管（对话/过场）时自动隐藏准星                                │
// │  4. 可选：隐藏系统鼠标光标                                               │
// │  5. 使用 OnGUI 实现，零 Canvas 依赖                                      │
// │                                                                          │
// │  联动组件：                                                               │
// │    · MouseAimController — 提供 IK 瞄准逻辑                              │
// │    · 本组件只负责视觉渲染                                                 │
// │                                                                          │
// │  挂载方式：                                                               │
// │    · 挂在任何 GameObject 上（推荐 Player 或 UI Manager）                  │
// │    · InteractionSystemBootstrap 会自动创建                                │
// └──────────────────────────────────────────────────────────────────────────┘

using UnityEngine;
using GhostVeil.Core.Event;
using GhostVeil.Data;

namespace GhostVeil.UI
{
    public class CrosshairUI : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 准星外观 ===")]
        [Tooltip("准星颜色")]
        [SerializeField] private Color crosshairColor = Color.white;

        [Tooltip("准星大小（十字臂长度，像素）")]
        [SerializeField] private float size = 12f;

        [Tooltip("准星线条粗细（像素）")]
        [SerializeField] private float thickness = 2f;

        [Tooltip("准星中心空心半径（像素，0 = 实心十字）")]
        [SerializeField] private float gapRadius = 4f;

        [Tooltip("准星外描边颜色（用于在明亮背景上保持可见）")]
        [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.6f);

        [Tooltip("描边厚度（0 = 不描边）")]
        [SerializeField] private float outlineThickness = 1f;

        [Header("=== 行为设置 ===")]
        [Tooltip("是否隐藏系统鼠标光标")]
        [SerializeField] private bool hideSystemCursor = true;

        [Tooltip("是否默认显示准星")]
        [SerializeField] private bool showByDefault = true;

        [Tooltip("是否在叙事接管时自动隐藏")]
        [SerializeField] private bool hideDuringNarrative = true;

        [Header("=== 点准星模式（可选） ===")]
        [Tooltip("是否绘制中心圆点")]
        [SerializeField] private bool showCenterDot = true;

        [Tooltip("中心圆点半径（像素）")]
        [SerializeField] private float dotRadius = 2f;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        private bool _visible;
        private bool _narrativeLocked;
        private Texture2D _whiteTexture;

        /// <summary>准星是否当前可见</summary>
        public bool IsVisible => _visible && !_narrativeLocked;

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            _visible = showByDefault;

            // 创建 1x1 白色纹理用于绘制
            _whiteTexture = new Texture2D(1, 1);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
        }

        private void Start()
        {
            if (hideSystemCursor)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Confined;
            }
        }

        private void OnEnable()
        {
            if (hideDuringNarrative)
            {
                GameEvent.Subscribe<NarrativeAuthorityRequestEvent>(OnNarrativeRequest);
                GameEvent.Subscribe<NarrativeAuthorityReleaseEvent>(OnNarrativeRelease);
            }
        }

        private void OnDisable()
        {
            if (hideDuringNarrative)
            {
                GameEvent.Unsubscribe<NarrativeAuthorityRequestEvent>(OnNarrativeRequest);
                GameEvent.Unsubscribe<NarrativeAuthorityReleaseEvent>(OnNarrativeRelease);
            }

            // 还原鼠标光标
            if (hideSystemCursor)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void OnDestroy()
        {
            if (_whiteTexture != null)
                Destroy(_whiteTexture);

            // 还原鼠标光标
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // ══════════════════════════════════════════════
        //  OnGUI 渲染
        // ══════════════════════════════════════════════

        private void OnGUI()
        {
            if (!IsVisible || _whiteTexture == null) return;

            // 获取鼠标屏幕位置（OnGUI 坐标系 Y 轴反转）
            Vector2 mousePos = Event.current.mousePosition;

            // ── 描边层（先绘制，作为底层） ────────────────
            if (outlineThickness > 0f)
            {
                DrawCrosshair(mousePos, outlineColor, size + outlineThickness,
                              thickness + outlineThickness * 2f, gapRadius - outlineThickness * 0.5f);
            }

            // ── 主准星层 ──────────────────────────────────
            DrawCrosshair(mousePos, crosshairColor, size, thickness, gapRadius);
        }

        /// <summary>
        /// 绘制十字准星（上下左右四条线 + 可选中心圆点）。
        /// </summary>
        private void DrawCrosshair(Vector2 center, Color color, float armLength, float lineThickness, float gap)
        {
            GUI.color = color;
            float halfThick = lineThickness * 0.5f;
            float safeGap = Mathf.Max(0f, gap);

            // ── 上 ──────────────────────────────────────
            GUI.DrawTexture(new Rect(
                center.x - halfThick,
                center.y - armLength - safeGap,
                lineThickness,
                armLength
            ), _whiteTexture);

            // ── 下 ──────────────────────────────────────
            GUI.DrawTexture(new Rect(
                center.x - halfThick,
                center.y + safeGap,
                lineThickness,
                armLength
            ), _whiteTexture);

            // ── 左 ──────────────────────────────────────
            GUI.DrawTexture(new Rect(
                center.x - armLength - safeGap,
                center.y - halfThick,
                armLength,
                lineThickness
            ), _whiteTexture);

            // ── 右 ──────────────────────────────────────
            GUI.DrawTexture(new Rect(
                center.x + safeGap,
                center.y - halfThick,
                armLength,
                lineThickness
            ), _whiteTexture);

            // ── 中心圆点 ────────────────────────────────
            if (showCenterDot && dotRadius > 0f)
            {
                GUI.DrawTexture(new Rect(
                    center.x - dotRadius,
                    center.y - dotRadius,
                    dotRadius * 2f,
                    dotRadius * 2f
                ), _whiteTexture);
            }

            GUI.color = Color.white; // 恢复
        }

        // ══════════════════════════════════════════════
        //  叙事系统联动
        // ══════════════════════════════════════════════

        private void OnNarrativeRequest(NarrativeAuthorityRequestEvent evt)
        {
            if (evt.RequestedLevel >= NarrativeAuthorityLevel.Dialogue)
            {
                _narrativeLocked = true;
                if (hideSystemCursor)
                {
                    Cursor.visible = true; // 叙事时恢复系统光标
                }
            }
        }

        private void OnNarrativeRelease(NarrativeAuthorityReleaseEvent evt)
        {
            _narrativeLocked = false;
            if (hideSystemCursor)
            {
                Cursor.visible = false;
            }
        }

        // ══════════════════════════════════════════════
        //  公共方法
        // ══════════════════════════════════════════════

        /// <summary>显示准星</summary>
        public void Show()
        {
            _visible = true;
            if (hideSystemCursor)
                Cursor.visible = false;
        }

        /// <summary>隐藏准星</summary>
        public void Hide()
        {
            _visible = false;
            if (hideSystemCursor)
                Cursor.visible = true;
        }

        /// <summary>切换准星可见性</summary>
        public void Toggle()
        {
            if (_visible) Hide();
            else Show();
        }

        /// <summary>设置准星颜色</summary>
        public void SetColor(Color color)
        {
            crosshairColor = color;
        }

        /// <summary>设置准星大小</summary>
        public void SetSize(float newSize)
        {
            size = Mathf.Max(1f, newSize);
        }
    }
}
