using UnityEngine;
using UnityEngine.UI;

namespace EcoSim.View
{
    /// <summary>
    /// 붕괴/지배 이벤트 시 화면 전체를 짧게 물들이는 플래시.
    /// 프리팹 요구: UI/HUD/EventFlashHUD.prefab — 자식 Image "FlashOverlay"(풀스크린, raycast off).
    /// </summary>
    public sealed class EventFlashHUD : UIHUD
    {
        private enum EImg { FlashOverlay }

        private Image _overlay;
        private Color _color;
        private float _t;         // 1→0
        private float _peakAlpha; // 최대 알파

        public override void Init()
        {
            Bind<Image>(typeof(EImg));
            _overlay = Get<Image>((int)EImg.FlashOverlay);
            if (_overlay != null)
            {
                _overlay.raycastTarget = false;
                SetAlpha(0f);
            }
        }

        public void Flash(Color color, float peakAlpha = 0.35f)
        {
            _color = color;
            _peakAlpha = peakAlpha;
            _t = 1f;
        }

        private void Update()
        {
            if (_overlay == null || _t <= 0f) return;
            _t -= UnityEngine.Time.deltaTime * 1.5f; // ~0.66s
            float a = Mathf.Max(0f, _t) * _peakAlpha;
            _color.a = a;
            _overlay.color = _color;
        }

        private void SetAlpha(float a)
        {
            var c = _overlay.color;
            c.a = a;
            _overlay.color = c;
        }
    }
}
