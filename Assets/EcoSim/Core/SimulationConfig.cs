using UnityEngine;

namespace EcoSim.Core
{
    /// <summary>
    /// 모든 시뮬 파라미터를 한 곳에 모은 ScriptableObject.
    /// 진동이 안 나오면 여기서 튜닝한다. 특히 assim*/death*/graze* 비율이 진동을 좌우한다.
    /// </summary>
    [CreateAssetMenu(menuName = "EcoSim/Simulation Config")]
    public sealed class SimulationConfig : ScriptableObject
    {
        [Header("World")]
        public int width = 256;
        public int height = 256;

        [Header("Water")]
        public float rainfall = 0.01f;
        public float waterCost = 0.3f;

        [Header("Plant")]
        public float growthRate = 0.15f;
        public float soilCost = 0.2f;

        [Header("Herbivore")]
        public float assimHerb = 0.10f;
        public float deathHerb = 0.02f;
        public float grazeRate = 0.20f;

        [Header("Predator")]
        public float assimPred = 0.08f;
        public float deathPred = 0.03f;
        public float predationRate = 0.15f;

        [Header("Cycle & Diffusion")]
        public float returnRate = 0.5f;   // 사망→토양 환원 비율
        public float diffusion  = 0.10f;  // 확산 계수 D (0~0.25 권장)

        [Header("Environment · Season (Phase 2)")]
        public bool  seasonEnabled   = true;
        public int   yearLength      = 120;   // 1년 = N틱(=N일)
        public float seasonRainAmp   = 0.8f;  // 강우 계절 변조 폭
        public float seasonGrowAmp   = 0.6f;  // 성장 계절 변조 폭

        [Header("Environment · Latitude (Phase 2)")]
        public bool  latitudeEnabled = true;
        public float latitudeAmp     = 0.7f;  // 위도 성장 변조 폭(0=균일, 1=극단)

        [Header("Environment · Water Flow (Phase 2)")]
        public bool  waterFlowEnabled = true;
        public float flowRate         = 0.25f; // 물이 최저 이웃으로 흐르는 비율

        [Header("Time")]
        public float realSecondsPerTick = 2f;   // 실시간 2초 = 게임 1일
        public int   maxCatchupTicks    = 30;   // 오프라인 최대 30일치
    }
}
