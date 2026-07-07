using EcoSim.Core;

namespace EcoSim.Time
{
    /// <summary>
    /// 오프라인 catch-up. 꺼져 있던 실제 시간 → 경과 틱 환산 후 몰아서 실행.
    /// maxCatchupTicks 상한 초과분은 버린다(방치형 표준 UX).
    /// </summary>
    public sealed class OfflineProgress
    {
        public struct Result { public int TicksRun; public bool Capped; }

        public Result CatchUp(SimulationTick sim, WorldState world,
                              SimulationConfig cfg,
                              System.DateTime lastPlayedUtc, System.DateTime nowUtc)
        {
            double elapsedSec = (nowUtc - lastPlayedUtc).TotalSeconds;
            if (elapsedSec < 0) elapsedSec = 0; // 시계 역행 방어
            int wantTicks = (int)(elapsedSec / cfg.realSecondsPerTick);

            bool capped = wantTicks > cfg.maxCatchupTicks;
            int runTicks = capped ? cfg.maxCatchupTicks : wantTicks;

            for (int t = 0; t < runTicks; t++) sim.Step(world);
            return new Result { TicksRun = runTicks, Capped = capped };
        }
    }
}
