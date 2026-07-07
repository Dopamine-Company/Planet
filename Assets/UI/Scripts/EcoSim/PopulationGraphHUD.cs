using UnityEngine;
using UnityEngine.UI;

namespace EcoSim.View
{
    /// <summary>
    /// 개체군 그래프 HUD. 매 틱 전역 합계(plant/herb/pred)를 링버퍼에 push하고
    /// 라인을 Texture2D에 그려 프리팹의 RawImage("GraphImage")에 표시.
    /// Assembly-CSharp 소속(UIHUD 상속 위해) — EcoSim asmdef 밖.
    ///
    /// 프리팹 요구: UI/HUD/PopulationGraphHUD.prefab
    ///   └ 자식에 RawImage 컴포넌트, 오브젝트 이름 "GraphImage".
    /// </summary>
    public sealed class PopulationGraphHUD : UIHUD
    {
        private enum EImg { GraphImage }

        private const int W = 256;   // 시간축 픽셀(=보관 샘플 수)
        private const int H = 128;   // 밀도축 픽셀

        private RawImage _img;
        private Texture2D _tex;
        private Color32[] _px;

        private readonly float[] _plant = new float[W];
        private readonly float[] _herb  = new float[W];
        private readonly float[] _pred  = new float[W];
        private int _count;

        private static readonly Color32 Bg    = new Color32(12, 12, 16, 255);
        private static readonly Color32 CPlant = new Color32(60, 220, 60, 255);
        private static readonly Color32 CHerb  = new Color32(235, 220, 40, 255);
        private static readonly Color32 CPred  = new Color32(230, 50, 50, 255);

        public override void Init()
        {
            Bind<RawImage>(typeof(EImg));
            _img = Get<RawImage>((int)EImg.GraphImage);

            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            _px = new Color32[W * H];
            if (_img != null) _img.texture = _tex;
            Clear();
        }

        // 매 틱 호출. 링버퍼 밀어넣고 전체 재드로우.
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
            Clear();

            // 세 종 공통 스케일로 오토스케일(상대 비교가 목적).
            float max = 1e-4f;
            for (int i = W - _count; i < W; i++)
            {
                if (_plant[i] > max) max = _plant[i];
                if (_herb[i]  > max) max = _herb[i];
                if (_pred[i]  > max) max = _pred[i];
            }

            for (int x = W - _count; x < W; x++)
            {
                Plot(x, _plant[x], max, CPlant);
                Plot(x, _herb[x],  max, CHerb);
                Plot(x, _pred[x],  max, CPred);
            }

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        private void Plot(int x, float value, float max, Color32 c)
        {
            int y = Mathf.Clamp(Mathf.RoundToInt(value / max * (H - 1)), 0, H - 1);
            _px[y * W + x] = c;
        }

        private void Clear()
        {
            for (int i = 0; i < _px.Length; i++) _px[i] = Bg;
        }
    }
}
