using UnityEngine;
using UnityEngine.UI;

namespace EcoSim.View
{
    /// <summary>
    /// 선택 셀 추적 미니 그래프. 한 셀의 plant/herb/pred 시계열 — 진동을 눈으로 확인하는 도구.
    /// 값 범위가 0~1로 고정이라 오토스케일 없이 절대 스케일로 그린다(출렁임 왜곡 방지).
    /// 프리팹 요구: UI/HUD/CellGraphHUD.prefab — 자식 RawImage "CellGraphImage".
    /// </summary>
    public sealed class CellGraphHUD : UIHUD
    {
        private enum EImg { CellGraphImage }

        private const int W = 192;
        private const int H = 96;

        private RawImage _img;
        private Texture2D _tex;
        private Color32[] _px;

        private readonly float[] _plant = new float[W];
        private readonly float[] _herb  = new float[W];
        private readonly float[] _pred  = new float[W];
        private int _count;

        private static readonly Color32 Bg     = new Color32(12, 12, 16, 255);
        private static readonly Color32 CPlant = new Color32(60, 220, 60, 255);
        private static readonly Color32 CHerb  = new Color32(235, 220, 40, 255);
        private static readonly Color32 CPred  = new Color32(230, 50, 50, 255);

        public override void Init()
        {
            Bind<RawImage>(typeof(EImg));
            _img = Get<RawImage>((int)EImg.CellGraphImage);

            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _px = new Color32[W * H];
            if (_img != null) _img.texture = _tex;
            Redraw();
        }

        /// 추적 셀 변경 시 히스토리 리셋.
        public void ResetHistory()
        {
            _count = 0;
            System.Array.Clear(_plant, 0, W);
            System.Array.Clear(_herb, 0, W);
            System.Array.Clear(_pred, 0, W);
            Redraw();
        }

        public void Push(float plant, float herb, float pred)
        {
            Shift(_plant, plant);
            Shift(_herb, herb);
            Shift(_pred, pred);
            if (_count < W) _count++;
            Redraw();
        }

        private static void Shift(float[] buf, float v)
        {
            System.Array.Copy(buf, 1, buf, 0, buf.Length - 1);
            buf[buf.Length - 1] = v;
        }

        private void Redraw()
        {
            if (_tex == null) return;
            for (int i = 0; i < _px.Length; i++) _px[i] = Bg;

            for (int x = W - _count; x < W; x++)
            {
                Plot(x, _plant[x], CPlant);
                Plot(x, _herb[x],  CHerb);
                Plot(x, _pred[x],  CPred);
            }

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        // 절대 스케일: 0~1 → 0~H-1.
        private void Plot(int x, float value, Color32 c)
        {
            int y = Mathf.Clamp(Mathf.RoundToInt(value * (H - 1)), 0, H - 1);
            _px[y * W + x] = c;
        }
    }
}
