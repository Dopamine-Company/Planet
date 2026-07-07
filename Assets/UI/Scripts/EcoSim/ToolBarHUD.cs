using UnityEngine;
using UnityEngine.UI;
using EcoSim.Interaction;

namespace EcoSim.View
{
    /// <summary>
    /// 도구 버튼 바 + 브러시 크기 슬라이더. 현재 도구 하이라이트.
    /// 키보드(1~4)와 양방향 동기: OnToolChanged 구독 → 하이라이트 갱신.
    /// 프리팹 요구: UI/HUD/ToolBarHUD.prefab
    ///   Button 자식 "BtnPlant","BtnHerb","BtnPred","BtnBarrier", Slider 자식 "BrushSlider".
    /// </summary>
    public sealed class ToolBarHUD : UIHUD
    {
        private enum EBtn { BtnPlant, BtnHerb, BtnPred, BtnBarrier }
        private enum ESld { BrushSlider }

        private readonly Button[] _btns = new Button[4];
        private Slider _slider;
        private InterventionController _intv;

        private static readonly Color Normal   = new Color(0.18f, 0.18f, 0.22f, 0.9f);
        private static readonly Color Selected = new Color(0.95f, 0.75f, 0.2f, 1f);

        public override void Init()
        {
            Bind<Button>(typeof(EBtn));
            Bind<Slider>(typeof(ESld));
            for (int i = 0; i < 4; i++) _btns[i] = Get<Button>(i);
            _slider = Get<Slider>((int)ESld.BrushSlider);
        }

        public void Configure(InterventionController intv)
        {
            _intv = intv;

            HookButton(0, InterventionController.Tool.PlantTree);
            HookButton(1, InterventionController.Tool.AddHerb);
            HookButton(2, InterventionController.Tool.AddPred);
            HookButton(3, InterventionController.Tool.ToggleBarrier);

            if (_slider != null)
            {
                _slider.wholeNumbers = true;
                _slider.minValue = 0; _slider.maxValue = 8;
                _slider.SetValueWithoutNotify(_intv.Brush);
                _slider.onValueChanged.AddListener(v => _intv.Brush = (int)v);
            }

            _intv.OnToolChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_intv != null) _intv.OnToolChanged -= Refresh;
        }

        private void HookButton(int idx, InterventionController.Tool tool)
        {
            if (_btns[idx] == null) return;
            _btns[idx].onClick.AddListener(() => _intv.CurrentTool = tool);
        }

        private void Refresh()
        {
            int cur = (int)_intv.CurrentTool;
            for (int i = 0; i < 4; i++)
            {
                if (_btns[i] == null) continue;
                var img = _btns[i].targetGraphic as Image;
                if (img != null) img.color = i == cur ? Selected : Normal;
            }
        }
    }
}
