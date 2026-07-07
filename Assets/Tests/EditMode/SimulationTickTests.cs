using NUnit.Framework;
using UnityEngine;
using EcoSim.Core;

namespace EcoSim.Tests
{
    /// <summary>
    /// Phase 0 핵심 검증: Unity를 켜지 않고 헤드리스로 창발(진동)·사막화 안정·벽 분리·토러스 wrap을
    /// 수치로 증명한다. 기대값은 동일 로직을 순수 C#로 사전 실행해 확인한 실측치에 근거한다.
    /// </summary>
    public sealed class SimulationTickTests
    {
        private const int Size = 32;

        private static SimulationConfig DefaultConfig()
        {
            // 기본 필드값이 그대로 시작 파라미터 → 별도 세팅 없이 사용.
            return ScriptableObject.CreateInstance<SimulationConfig>();
        }

        private static WorldState NewWorld(float soil, float water)
        {
            var w = new WorldState(Size, Size);
            for (int i = 0; i < Size * Size; i++) { w.Soil[i] = soil; w.Water[i] = water; }
            return w;
        }

        private static void SeedAll(WorldState w, float[] field, float value)
        {
            for (int i = 0; i < field.Length; i++) field[i] = value;
        }

        private static float Sum(float[] a)
        {
            float s = 0f;
            for (int i = 0; i < a.Length; i++) s += a[i];
            return s;
        }

        private static bool AnyBad(float[] a)
        {
            for (int i = 0; i < a.Length; i++)
                if (float.IsNaN(a[i]) || float.IsInfinity(a[i])) return true;
            return false;
        }

        private static bool InRange01(float[] a)
        {
            for (int i = 0; i < a.Length; i++)
                if (a[i] < 0f || a[i] > 1f) return false;
            return true;
        }

        // 시계열의 방향 전환(파도) 횟수. 진동 판정용.
        private static int Reversals(float[] s, float eps = 1e-3f)
        {
            int rev = 0, dir = 0;
            for (int i = 1; i < s.Length; i++)
            {
                float d = s[i] - s[i - 1];
                int nd = Mathf.Abs(d) < eps ? dir : (d > 0f ? 1 : -1);
                if (nd != 0 && dir != 0 && nd != dir) rev++;
                if (nd != 0) dir = nd;
            }
            return rev;
        }

        // ── 1. 사막화/포화 안정: 식물만 → 퍼지다 자원 한계에서 멈춤, 발산·NaN 없음 ──
        [Test]
        public void PlantOnly_Stabilizes_NoNaN_Clamped()
        {
            var cfg = DefaultConfig();
            var w = NewWorld(0.8f, 0.5f);
            SeedAll(w, w.Plant, 0.1f);
            var sim = new SimulationTick(cfg, w);

            for (int t = 0; t < 400; t++) sim.Step(w);

            Assert.IsFalse(AnyBad(w.Plant), "식물 밀도에 NaN/Inf가 발생하면 안 된다.");
            Assert.IsTrue(InRange01(w.Plant) && InRange01(w.Soil) && InRange01(w.Water),
                "모든 값은 0~1로 clamp 되어야 한다.");
            Assert.Greater(Sum(w.Plant), 0f, "식물만 심으면 소멸하지 않고 안정 상태로 남아야 한다.");
        }

        // ── 2. 개체군 진동(핵심): 식물+초식 → plant/herb 전역 합이 파도치듯 진동 ──
        [Test]
        public void PlantHerb_ProducesOscillation()
        {
            var cfg = DefaultConfig();
            var w = NewWorld(0.8f, 0.5f);
            SeedAll(w, w.Plant, 0.3f);
            SeedAll(w, w.Herb, 0.1f);
            var sim = new SimulationTick(cfg, w);

            const int ticks = 500;
            var plant = new float[ticks];
            var herb = new float[ticks];
            for (int t = 0; t < ticks; t++)
            {
                sim.Step(w);
                plant[t] = Sum(w.Plant);
                herb[t] = Sum(w.Herb);
            }

            Assert.IsFalse(AnyBad(w.Plant) || AnyBad(w.Herb), "진동 중에도 NaN/Inf는 없어야 한다.");
            Assert.GreaterOrEqual(Reversals(plant), 2,
                "식물 개체군이 단조가 아니라 최소 2회 이상 방향을 바꿔야 한다(로트카-볼테라 진동).");
            Assert.GreaterOrEqual(Reversals(herb), 2,
                "초식 개체군도 진동(방향 전환)을 보여야 한다.");
        }

        // ── 3. 3종 공존: 포식자 추가 시 붕괴 없이 지속(또는 진동) ──
        [Test]
        public void PlantHerbPred_ThreeSpeciesPersist()
        {
            var cfg = DefaultConfig();
            var w = NewWorld(0.8f, 0.5f);
            SeedAll(w, w.Plant, 0.3f);
            SeedAll(w, w.Herb, 0.15f);
            SeedAll(w, w.Pred, 0.1f);
            var sim = new SimulationTick(cfg, w);

            for (int t = 0; t < 500; t++) sim.Step(w);

            Assert.IsFalse(AnyBad(w.Pred), "포식자 밀도에 NaN/Inf가 없어야 한다.");
            Assert.IsTrue(InRange01(w.Pred), "포식자 값도 0~1로 clamp 되어야 한다.");
            Assert.Greater(Sum(w.Pred), 0f, "포식자가 즉시 멸종하지 않고 개체군을 유지해야 한다.");
        }

        // ── 4. 벽 분리: barrier로 나눈 반대편으로 종이 넘어가지 못한다 ──
        [Test]
        public void WallSplit_IsolatesRegions()
        {
            var cfg = DefaultConfig();
            var w = NewWorld(0.8f, 0.5f);
            SeedAll(w, w.Plant, 0.3f);

            // 토러스를 두 영역으로 완전히 가르려면 벽이 두 줄 필요(x=0, x=16).
            for (int y = 0; y < Size; y++)
            {
                w.Barrier[w.Index(0, y)] = true;
                w.Barrier[w.Index(16, y)] = true;
            }
            // 초식은 왼쪽 영역(x=1..15)에만 시드.
            for (int y = 0; y < Size; y++)
                for (int x = 1; x < 16; x++)
                    w.Herb[w.Index(x, y)] = 0.15f;

            var sim = new SimulationTick(cfg, w);
            for (int t = 0; t < 400; t++) sim.Step(w);

            float rightHerb = 0f, leftHerb = 0f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 17; x < Size; x++) rightHerb += w.Herb[w.Index(x, y)];
                for (int x = 1; x < 16; x++)    leftHerb  += w.Herb[w.Index(x, y)];
            }

            Assert.Less(rightHerb, 1e-3f, "벽 반대편으로 초식이 확산되면 안 된다.");
            Assert.Greater(leftHerb, 0f, "벽 안쪽 영역의 초식은 살아 있어야 한다.");
        }

        // ── 5. 토러스 wrap 산술 ──
        [Test]
        public void TorusWrap_Arithmetic()
        {
            var w = new WorldState(Size, Size);
            Assert.AreEqual(Size - 1, w.Wrap(-1, Size));
            Assert.AreEqual(0, w.Wrap(Size, Size));
            Assert.AreEqual(1, w.Wrap(Size + 1, Size));
        }

        // ── 7. 계절: 여름 성장 > 겨울 성장 (연 주기 흥망의 스위치) ──
        [Test]
        public void Season_SummerGrowsMoreThanWinter()
        {
            var cfg = DefaultConfig();
            cfg.seasonEnabled = true;
            cfg.latitudeEnabled = false;   // 계절만 격리 검증
            cfg.waterFlowEnabled = false;
            cfg.yearLength = 40;

            float summer = GrowOverSeasonQuarter(cfg, startAtPeak: true);   // 한여름 근처
            float winter = GrowOverSeasonQuarter(cfg, startAtPeak: false);  // 한겨울 근처

            Assert.Greater(summer, winter,
                "여름 구간 식물 증가가 겨울 구간보다 커야 한다(계절 변조).");
        }

        // 지정한 계절 위상에서 짧게 돌려 식물 총량 증가분 측정.
        private static float GrowOverSeasonQuarter(SimulationConfig cfg, bool startAtPeak)
        {
            var w = NewWorld(0.9f, 0.6f);
            SeedAll(w, w.Plant, 0.2f);
            SeedAll(w, w.Herb, 0.02f);
            var sim = new SimulationTick(cfg, w);
            // 여름 피크: sin=+1 → phase=0.25 → day=yearLength/4. 겨울: phase=0.75.
            sim.SetDay(startAtPeak ? cfg.yearLength / 4 - 5 : (3 * cfg.yearLength) / 4 - 5);

            float before = Sum(w.Plant);
            for (int t = 0; t < 10; t++) sim.Step(w);
            return Sum(w.Plant) - before;
        }

        // ── 8. 물 흐름: 물이 고지대→저지대로 이동해 저지대에 고인다 ──
        [Test]
        public void WaterFlow_AccumulatesInLowlands()
        {
            var cfg = DefaultConfig();
            cfg.seasonEnabled = false;
            cfg.latitudeEnabled = false;
            cfg.waterFlowEnabled = true;
            cfg.flowRate = 0.3f;
            cfg.rainfall = 0f; // 강우 제거 → 순수 흐름만

            var w = NewWorld(0.5f, 0.5f);
            // 왼쪽 절반 높음, 오른쪽 절반 낮음 (경사)
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    w.Elevation[w.Index(x, y)] = 1f - x / (float)(Size - 1);

            var sim = new SimulationTick(cfg, w);
            for (int t = 0; t < 50; t++) sim.Step(w);

            float highWater = 0f, lowWater = 0f;
            for (int y = 0; y < Size; y++)
            {
                highWater += w.Water[w.Index(0, y)];         // 최고지대 열
                lowWater  += w.Water[w.Index(Size - 1, y)];  // 최저지대 열
            }
            Assert.Greater(lowWater, highWater, "저지대에 물이 더 고여야 한다.");
        }

        // ── 9. 토러스 wrap 확산: 경계 셀의 확산이 반대편 끝으로 이어진다 ──
        [Test]
        public void TorusWrap_DiffusionCrossesBoundary()
        {
            var cfg = DefaultConfig();
            var w = NewWorld(0.8f, 0.5f);
            SeedAll(w, w.Plant, 0.5f);
            for (int y = 0; y < Size; y++) w.Herb[w.Index(0, y)] = 0.5f; // 왼쪽 끝 열에만 초식

            var sim = new SimulationTick(cfg, w);
            for (int t = 0; t < 5; t++) sim.Step(w);

            float wrapCol = 0f; // x=0의 왼쪽 이웃 = x=Width-1 (wrap)
            for (int y = 0; y < Size; y++) wrapCol += w.Herb[w.Index(Size - 1, y)];

            Assert.Greater(wrapCol, 0f, "경계를 넘어 반대편 끝 열로 확산이 이어져야 한다(토러스).");
        }
    }
}
