using TMPro;

namespace EcoSim.View
{
    /// <summary>
    /// 셀 인스펙터 HUD. 호버 셀의 5값 + 좌표 + 뷰모드/배속/조작 안내를 표시.
    /// 프리팹 요구: UI/HUD/CellInspectorHUD.prefab — 자식 TMP_Text "InfoText".
    /// </summary>
    public sealed class CellInspectorHUD : UIHUD
    {
        private enum ETxt { InfoText }

        private TMP_Text _text;

        public override void Init()
        {
            Bind<TMP_Text>(typeof(ETxt));
            _text = Get<TMP_Text>((int)ETxt.InfoText);
        }

        public void SetInfo(string info)
        {
            if (_text != null) _text.text = info;
        }
    }
}
