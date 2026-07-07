using UnityEngine;
using EcoSim.Core;

namespace EcoSim.Time
{
    /// <summary>
    /// 실시간 누적 → 틱 실행. realSecondsPerTick(기본 2초)마다 SimulationTick.Step 1회.
    /// 한 프레임에 여러 틱이 밀리면 몰아서 실행하고 OnTicksAdvanced(개수)로 알린다.
    /// </summary>
    public sealed class TickScheduler : MonoBehaviour
    {
        private SimulationConfig _cfg;
        private WorldState _world;
        private SimulationTick _sim;
        private float _accum;

        public int TotalTicks { get; private set; }

        /// 0=정지, 1/4/16 배속. 배속 = 한 인터벌마다 Step을 여러 번.
        public int SpeedMultiplier { get; set; } = 1;

        public System.Action<int> OnTicksAdvanced; // 렌더/그래프/로그 갱신 신호

        public void Init(SimulationConfig cfg, WorldState world, SimulationTick sim)
        {
            _cfg = cfg; _world = world; _sim = sim;
        }

        private void Update()
        {
            if (_sim == null) return;
            if (SpeedMultiplier <= 0) { _accum = 0f; return; } // 정지(재개 시 몰림 방지)

            // namespace가 EcoSim.Time이라 UnityEngine.Time은 풀네임 필수.
            _accum += UnityEngine.Time.deltaTime;

            int ticks = 0;
            while (_accum >= _cfg.realSecondsPerTick)
            {
                for (int s = 0; s < SpeedMultiplier; s++) _sim.Step(_world);
                _accum -= _cfg.realSecondsPerTick;
                ticks += SpeedMultiplier;
            }

            if (ticks > 0)
            {
                TotalTicks += ticks;
                OnTicksAdvanced?.Invoke(ticks);
            }
        }
    }
}
