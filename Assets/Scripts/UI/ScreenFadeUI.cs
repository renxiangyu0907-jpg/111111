// ============================================================================
// ScreenFadeUI.cs — 全屏渐变遮罩 UI（配合 CutsceneDirector 淡入/淡出）
// ============================================================================
//
//  监听 CutsceneDirector.OnFadeUpdate 事件，在屏幕上绘制半透明覆盖层。
//  使用 OnGUI 实现，零依赖。
//

using UnityEngine;
using GhostVeil.Narrative.Cutscene;

namespace GhostVeil.UI
{
    public class ScreenFadeUI : MonoBehaviour
    {
        // ── 运行时状态 ──────────────────────────────
        private float _alpha;
        private Color _fadeColor = Color.black;
        private Texture2D _fadeTexture;
        private CutsceneDirector _director;

        private void Start()
        {
            _fadeTexture = new Texture2D(1, 1);
            _fadeTexture.SetPixel(0, 0, Color.white);
            _fadeTexture.Apply();

            BindDirector();
        }

        private void OnEnable()
        {
            BindDirector();
        }

        private void OnDisable()
        {
            UnbindDirector();
        }

        private void OnDestroy()
        {
            UnbindDirector();
            if (_fadeTexture != null)
                Destroy(_fadeTexture);
        }

        private void BindDirector()
        {
            if (_director != null) return;
            _director = FindObjectOfType<CutsceneDirector>();
            if (_director != null)
                _director.OnFadeUpdate += OnFadeUpdate;
        }

        private void UnbindDirector()
        {
            if (_director != null)
            {
                _director.OnFadeUpdate -= OnFadeUpdate;
            }
        }

        private void OnFadeUpdate(float alpha, Color color)
        {
            _alpha = alpha;
            _fadeColor = color;
        }

        private void OnGUI()
        {
            if (_alpha <= 0.001f) return;

            Color c = _fadeColor;
            c.a = _alpha;
            GUI.color = c;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _fadeTexture);
            GUI.color = Color.white; // 恢复
        }
    }
}
