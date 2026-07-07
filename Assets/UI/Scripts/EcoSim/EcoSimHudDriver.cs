using UnityEngine;
using EcoSim.Boot;
using EcoSim.Core;
using EcoSim.Interaction;
using EcoSim.View;

namespace EcoSim.View
{
    /// <summary>
    /// GameRoot(EcoSim asmdef)와 UI HUD(Assembly-CSharp)를 잇는 다리.
    /// EcoSim asmdef는 Assembly-CSharp을 참조 못 하므로 UI 구동은 이쪽에서 한다.
    /// 빈 GameObject에 이 컴포넌트만 붙이면 됨(UIManager 없으면 자동 생성).
    /// </summary>
    public sealed class EcoSimHudDriver : MonoBehaviour
    {
        private GameRoot _root;
        private PopulationGraphHUD _graph;
        private EventLogHUD _log;
        private CellInspectorHUD _inspector;
        private CellGraphHUD _cellGraph;
        private ToolBarHUD _toolBar;
        private EventFlashHUD _flash;

        private static readonly Color[] SpeciesColor =
        {
            new Color(0.3f, 0.9f, 0.3f),   // 숲
            new Color(0.95f, 0.9f, 0.2f),  // 초식
            new Color(0.95f, 0.25f, 0.25f) // 포식
        };

        private int _trackedX = -1, _trackedY = -1;

        private static readonly string[] SpeedLabel = { "정지", "1x", "4x", "16x" };

        private void Start()
        {
            _root = FindFirstObjectByType<GameRoot>();
            if (_root == null)
            {
                Debug.LogWarning("[EcoSimHudDriver] GameRoot를 찾지 못했습니다. HUD 미표시.");
                enabled = false;
                return;
            }

            if (UIManager.Instance == null)
                new GameObject("@UIManager").AddComponent<UIManager>();

            _graph = UIManager.Instance.ShowHUDUI<PopulationGraphHUD>();
            _log = UIManager.Instance.ShowHUDUI<EventLogHUD>();
            _inspector = UIManager.Instance.ShowHUDUI<CellInspectorHUD>();
            _cellGraph = UIManager.Instance.ShowHUDUI<CellGraphHUD>();
            _toolBar = UIManager.Instance.ShowHUDUI<ToolBarHUD>();
            _flash = UIManager.Instance.ShowHUDUI<EventFlashHUD>();
            _log.Configure(_root.World.Width * _root.World.Height);
            _toolBar.Configure(_root.Intervention);
            _log.OnEvent += OnEcoEvent;

            if (_root.OfflineTicksRun > 0)
            {
                string cap = _root.OfflineCapped ? " (상한 적용)" : "";
                _log.AddLine($"네가 없는 동안 {_root.OfflineTicksRun}일 지남{cap}");
            }

            _root.Scheduler.OnTicksAdvanced += OnTicks;
        }

        private void OnDestroy()
        {
            if (_root != null && _root.Scheduler != null)
                _root.Scheduler.OnTicksAdvanced -= OnTicks;
            if (_log != null) _log.OnEvent -= OnEcoEvent;
        }

        // 붕괴=해당 종 어두운 붉은 톤, 지배=해당 종 색으로 화면 플래시.
        private void OnEcoEvent(int idx, bool dominance)
        {
            if (_flash == null) return;
            Color c = dominance ? SpeciesColor[idx] : new Color(0.8f, 0.1f, 0.1f);
            _flash.Flash(c, dominance ? 0.3f : 0.4f);
        }

        private void Update()
        {
            if (_root == null) return;
            var obs = _root.Observation;
            if (obs == null || _inspector == null) return;

            // 추적 셀 변경 감지 → 히스토리 리셋
            if (obs.HasSelection && (obs.SelX != _trackedX || obs.SelY != _trackedY))
            {
                _trackedX = obs.SelX; _trackedY = obs.SelY;
                _cellGraph.ResetHistory();
            }

            _inspector.SetInfo(BuildInfo(obs));
        }

        private string BuildInfo(ObservationController obs)
        {
            WorldState w = _root.World;
            string cell = "셀에 커서를 올려보세요";
            if (obs.HasHover)
            {
                int i = w.Index(obs.HoverX, obs.HoverY);
                cell = $"({obs.HoverX},{obs.HoverY})  식물{w.Plant[i]:F2} 초식{w.Herb[i]:F2} " +
                       $"포식{w.Pred[i]:F2} 흙{w.Soil[i]:F2} 물{w.Water[i]:F2}" +
                       (w.Barrier[i] ? " [벽]" : "");
            }

            string track = _trackedX >= 0 ? $"추적 셀: ({_trackedX},{_trackedY})" : "추적 셀: 없음 (우클릭 선택)";
            int sp = _root.Scheduler.SpeedMultiplier;
            string speed = sp == 0 ? SpeedLabel[0] : sp == 1 ? SpeedLabel[1] : sp == 4 ? SpeedLabel[2] : SpeedLabel[3];

            return $"{cell}\n뷰: {obs.Mode}   배속: {speed}   {_root.Scheduler.TotalTicks}일차\n{track}\n" +
                   "[Tab]뷰 [Space]정지 [5/6/7]배속 [1~4]도구 [우클릭]추적";
        }

        private void OnTicks(int advanced)
        {
            WorldState w = _root.World;
            float p = Sum(w.Plant), h = Sum(w.Herb), r = Sum(w.Pred);
            _graph.Push(p, h, r);
            _log.Feed(_root.Scheduler.TotalTicks, p, h, r);

            if (_trackedX >= 0 && _cellGraph != null)
            {
                int i = w.Index(_trackedX, _trackedY);
                _cellGraph.Push(w.Plant[i], w.Herb[i], w.Pred[i]);
            }
        }

        private static float Sum(float[] a)
        {
            float s = 0f;
            for (int i = 0; i < a.Length; i++) s += a[i];
            return s;
        }
    }
}
