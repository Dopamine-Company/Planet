using System.Collections.Generic;
using TMPro;

namespace EcoSim.View
{
    /// <summary>
    /// 이벤트 로그 HUD. 전역 합계 임계값 통과를 감지해 붕괴/지배를 자동 기록.
    /// 상태 전이일 때만 로그 → 스팸 방지.
    /// Assembly-CSharp 소속(UIHUD 상속) — EcoSim asmdef 밖.
    ///
    /// 프리팹 요구: UI/HUD/EventLogHUD.prefab
    ///   └ 자식에 TMP_Text(TextMeshProUGUI), 오브젝트 이름 "LogText".
    /// </summary>
    public sealed class EventLogHUD : UIHUD
    {
        private enum ETxt { LogText }

        private const int MaxLines = 12;

        private TMP_Text _text;
        private readonly List<string> _lines = new List<string>();

        private int _cells = 1;
        private readonly float[] _peak = new float[3];      // plant/herb/pred 관측 최대
        private readonly bool[]  _collapsed = { false, false, false };
        private readonly bool[]  _dominant  = { false, false, false };

        private static readonly string[] Names = { "숲", "초식동물", "포식자" };

        // 이벤트 발행: (종 인덱스 0=숲/1=초식/2=포식, 지배 여부). 화면 연출용.
        public System.Action<int, bool> OnEvent;

        public override void Init()
        {
            Bind<TMP_Text>(typeof(ETxt));
            _text = Get<TMP_Text>((int)ETxt.LogText);
            Render();
        }

        public void Configure(int worldCellCount)
        {
            _cells = worldCellCount < 1 ? 1 : worldCellCount;
        }

        public void AddLine(string line)
        {
            _lines.Insert(0, line);
            if (_lines.Count > MaxLines) _lines.RemoveAt(_lines.Count - 1);
            Render();
        }

        // 매 틱 전역 합계 공급 → 임계값 감지.
        public void Feed(int day, float plant, float herb, float pred)
        {
            Check(day, 0, plant);
            Check(day, 1, herb);
            Check(day, 2, pred);
        }

        private void Check(int day, int idx, float sum)
        {
            if (sum > _peak[idx]) _peak[idx] = sum;

            float dominanceLevel = 0.5f * _cells;  // 세계 밀도의 50% 초과 = 지배
            float collapseFloor  = 0.02f * _cells; // 0 근처 = 붕괴
            bool everMeaningful   = _peak[idx] > 0.05f * _cells;

            // 붕괴: 한때 의미있게 존재했는데 0 근처로.
            if (!_collapsed[idx] && everMeaningful && sum < collapseFloor)
            {
                _collapsed[idx] = true;
                AddLine($"{day}일차: {Names[idx]} 개체군 붕괴");
                OnEvent?.Invoke(idx, false);
            }
            else if (_collapsed[idx] && sum > 0.10f * _peak[idx])
            {
                _collapsed[idx] = false; // 회복 → 다음 붕괴 재감지 허용
            }

            // 지배: 세계 절반 초과 점령.
            if (!_dominant[idx] && sum > dominanceLevel)
            {
                _dominant[idx] = true;
                AddLine($"{day}일차: {Names[idx]}이(가) 세계 절반 점령");
                OnEvent?.Invoke(idx, true);
            }
            else if (_dominant[idx] && sum < 0.40f * _cells)
            {
                _dominant[idx] = false;
            }
        }

        private void Render()
        {
            if (_text == null) return;
            _text.text = _lines.Count == 0
                ? "— 관찰 로그 —"
                : string.Join("\n", _lines);
        }
    }
}
