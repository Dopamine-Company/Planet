using UnityEngine;
using EcoSim.Core;

namespace EcoSim.View
{
    public enum ViewMode { Combined, Plant, Herb, Pred, Soil, Water, Elevation }

    /// <summary>
    /// 세계 = Texture2D 한 장. 픽셀 = 셀. 밀도 → 색.
    /// 틱은 이산이라 툭툭 끊기므로, 다음 틱까지 이전 색↔새 색을 프레임 단위 보간(게임필).
    /// 배속이 높거나 즉시 갱신(개입·모드전환)일 땐 보간 없이 바로.
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        private Renderer _quad;
        private Texture2D _tex;
        private Color32[] _prev;     // 보간 시작 색
        private Color32[] _target;   // 보간 목표 색
        private Color32[] _display;  // 현재 표시 색
        private WorldState _world;

        private ViewMode _mode = ViewMode.Combined;

        private float _lerpT = 1f;   // 0→1
        private float _duration = 0.0001f;
        private bool _interp;

        public ViewMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                if (_world != null) Draw(_world, instant: true);
            }
        }

        public void CycleMode() => Mode = (ViewMode)(((int)_mode + 1) % 7);

        public void Init(WorldState w, Renderer quad)
        {
            _quad = quad;
            _world = w;
            _tex = new Texture2D(w.Width, w.Height, TextureFormat.RGBA32, false)
                   { filterMode = FilterMode.Point };
            int n = w.Width * w.Height;
            _prev = new Color32[n];
            _target = new Color32[n];
            _display = new Color32[n];
            _quad.material.mainTexture = _tex;
        }

        /// instant=false + interpDuration>0 이면 다음 프레임들에 걸쳐 부드럽게 전환.
        public void Draw(WorldState w, bool instant = true, float interpDuration = 0f)
        {
            _world = w;
            int n = w.Width * w.Height;
            for (int i = 0; i < n; i++) _target[i] = ComputeColor(w, i);

            if (instant || interpDuration <= 0.0001f)
            {
                System.Array.Copy(_target, _display, n);
                _interp = false;
                _lerpT = 1f;
                Upload();
            }
            else
            {
                System.Array.Copy(_display, _prev, n); // 현재 화면에서 출발
                _duration = interpDuration;
                _lerpT = 0f;
                _interp = true;
            }
        }

        private void Update()
        {
            if (!_interp) return;
            _lerpT += UnityEngine.Time.deltaTime / _duration;
            float t = _lerpT >= 1f ? 1f : _lerpT;

            int n = _display.Length;
            for (int i = 0; i < n; i++) _display[i] = Lerp32(_prev[i], _target[i], t);
            Upload();

            if (_lerpT >= 1f) _interp = false;
        }

        private void Upload()
        {
            _tex.SetPixels32(_display);
            _tex.Apply(false);
        }

        private Color32 ComputeColor(WorldState w, int i)
        {
            if (w.Barrier[i]) return new Color32(90, 90, 90, 255);
            switch (_mode)
            {
                case ViewMode.Plant: return Ramp(w.Plant[i], 40, 200, 60);
                case ViewMode.Herb:  return Ramp(w.Herb[i], 220, 210, 40);
                case ViewMode.Pred:  return Ramp(w.Pred[i], 220, 50, 50);
                case ViewMode.Soil:  return Ramp(w.Soil[i], 140, 90, 50);
                case ViewMode.Water: return Ramp(w.Water[i], 40, 90, 200);
                case ViewMode.Elevation: return Elevation(w.Elevation[i]);
                default:             return Combined(w, i);
            }
        }

        private static Color32 Ramp(float t, int r, int g, int b)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return new Color32((byte)(r * t), (byte)(g * t), (byte)(b * t), 255);
        }

        // 지형: 저지대=짙은 청록(물길), 고지대=밝은 갈회색.
        private static Color32 Elevation(float e)
        {
            e = e < 0f ? 0f : (e > 1f ? 1f : e);
            byte r = (byte)Mathf.Lerp(30, 200, e);
            byte g = (byte)Mathf.Lerp(70, 190, e);
            byte b = (byte)Mathf.Lerp(90, 170, e);
            return new Color32(r, g, b, 255);
        }

        private static Color32 Combined(WorldState w, int i)
        {
            byte g = (byte)(w.Plant[i] * 255f);
            byte r = (byte)(w.Pred[i] * 255f);
            byte b = (byte)(w.Water[i] * 200f);
            r = (byte)Mathf.Min(255f, r + w.Herb[i] * 200f);
            g = (byte)Mathf.Min(255f, g + w.Herb[i] * 200f);
            return new Color32(r, g, b, 255);
        }

        private static Color32 Lerp32(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                255);
        }
    }
}
