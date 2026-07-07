// EcoSim.Core / SimulationTick — Phase 0 시뮬레이션 틱 파이프라인 + Phase 2 환경(계절·위도·물흐름).
namespace EcoSim.Core
{
    /// <summary>
    /// 한 틱(=게임 1일) 전진. 더블 버퍼로 read→write 하여 순차 갱신 편향을 막는다.
    /// 순서: LocalRules(셀 내부, 계절·위도 변조) → WaterFlow(고도 방향 흐름) → Diffuse → Clamp01.
    ///
    /// 버퍼 흐름: read → LocalRules → _buf → (WaterFlow로 _buf.Water 갱신) → Diffuse → read.
    /// 결과적으로 Step 종료 시 read가 최신 상태를 담는다.
    ///
    /// 계절/위도/물흐름은 config 플래그로 개별 on/off. 끄면 Phase 0 동작과 동일.
    /// </summary>
    public sealed class SimulationTick
    {
        private readonly SimulationConfig _cfg;
        private readonly WorldState _buf;       // 재사용 스크래치 버퍼
        private readonly float[] _waterScratch; // 물 흐름 계산용

        private int _day;

        public SimulationTick(SimulationConfig cfg, WorldState template)
        {
            _cfg = cfg;
            _buf = new WorldState(template.Width, template.Height);
            _waterScratch = new float[template.Width * template.Height];
        }

        public int Day => _day;
        public void SetDay(int day) => _day = day;

        /// 현재 계절값 -1(한겨울)~+1(한여름). 계절 꺼져 있으면 0.
        public float Season()
        {
            if (!_cfg.seasonEnabled || _cfg.yearLength <= 0) return 0f;
            float phase = (_day % _cfg.yearLength) / (float)_cfg.yearLength;
            return (float)System.Math.Sin(phase * 2.0 * System.Math.PI);
        }

        public void Step(WorldState read)
        {
            _day++;
            float season = Season();

            CopyStatic(read, _buf);       // barrier 스냅샷
            LocalRules(read, _buf, season);
            if (_cfg.waterFlowEnabled)
                WaterFlow(_buf.Water, read); // 고도 낮은 이웃으로 물 이동
            Diffuse(_buf, read);
            Clamp01(read);
        }

        private static void CopyStatic(WorldState r, WorldState w)
        {
            System.Array.Copy(r.Barrier, w.Barrier, r.Barrier.Length);
        }

        // --- 셀 내부 규칙 (계절·위도 변조) ---
        private void LocalRules(WorldState r, WorldState w, float season)
        {
            float rainNow = _cfg.rainfall   * (1f + season * _cfg.seasonRainAmp);
            float growSeason = _cfg.growthRate * (1f + season * _cfg.seasonGrowAmp);
            if (rainNow < 0f) rainNow = 0f;
            if (growSeason < 0f) growSeason = 0f;

            for (int y = 0; y < r.Height; y++)
            {
                // 위도: 토러스 y-wrap 유지 위해 코사인(중앙=따뜻, 위아래 끝=추움).
                float tempFactor = 1f;
                if (_cfg.latitudeEnabled)
                {
                    float lat = (float)System.Math.Cos((y / (double)r.Height) * 2.0 * System.Math.PI);
                    float t = 0.5f + 0.5f * lat;                 // 0~1
                    tempFactor = 1f - _cfg.latitudeAmp * (1f - t); // amp만큼 추운 곳 성장 억제
                }
                float growNow = growSeason * tempFactor;

                int rowBase = y * r.Width;
                for (int x = 0; x < r.Width; x++)
                {
                    int i = rowBase + x;
                    float soil = r.Soil[i],  water = r.Water[i];
                    float pl = r.Plant[i], hb = r.Herb[i], pr = r.Pred[i];

                    water += rainNow;

                    float grow = growNow * pl * (soil * water) * (1f - pl);
                    pl    += grow;
                    soil  -= grow * _cfg.soilCost;
                    water -= grow * _cfg.waterCost;

                    float herbBorn = _cfg.assimHerb * pl * hb;
                    float herbDie  = _cfg.deathHerb * hb;
                    hb += herbBorn - herbDie;
                    pl -= _cfg.grazeRate * pl * hb;

                    float predBorn = _cfg.assimPred * hb * pr;
                    float predDie  = _cfg.deathPred * pr;
                    pr += predBorn - predDie;
                    hb -= _cfg.predationRate * hb * pr;

                    soil += (herbDie + predDie) * _cfg.returnRate;

                    w.Soil[i]  = soil;  w.Water[i] = water;
                    w.Plant[i] = pl;    w.Herb[i]  = hb;   w.Pred[i] = pr;
                }
            }
        }

        // --- 물 흐름: 각 셀 물 일부를 가장 낮은(고도) 이웃 하나로 이동. 저지대에 강·호수 형성 ---
        private void WaterFlow(float[] water, WorldState s)
        {
            // water(dst)는 현재값에서 시작, flow 양은 스냅샷에서 읽어 델타 누적.
            System.Array.Copy(water, _waterScratch, water.Length);

            for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
            {
                int i = s.Index(x, y);
                if (s.Barrier[i]) continue;

                int lowest = LowestNeighbor(s, x, y);
                if (lowest < 0) continue; // 더 낮은 이웃 없음(웅덩이)

                float flow = _waterScratch[i] * _cfg.flowRate;
                water[i] -= flow;
                water[lowest] += flow;
            }
        }

        // 4이웃(토러스) 중 barrier 아니고 자기보다 낮은 최저 고도 셀. 없으면 -1.
        private int LowestNeighbor(WorldState s, int x, int y)
        {
            int self = s.Index(x, y);
            float selfE = s.Elevation[self];
            int best = -1;
            float bestE = selfE;

            Consider(s, x + 1, y, ref best, ref bestE);
            Consider(s, x - 1, y, ref best, ref bestE);
            Consider(s, x, y + 1, ref best, ref bestE);
            Consider(s, x, y - 1, ref best, ref bestE);
            return best;
        }

        private static void Consider(WorldState s, int nx, int ny, ref int best, ref float bestE)
        {
            nx = s.Wrap(nx, s.Width);
            ny = s.Wrap(ny, s.Height);
            int ni = s.Index(nx, ny);
            if (s.Barrier[ni]) return;
            if (s.Elevation[ni] < bestE) { bestE = s.Elevation[ni]; best = ni; }
        }

        // --- 확산 (토러스 라플라시안, barrier 차단) ---
        private void Diffuse(WorldState src, WorldState dst)
        {
            DiffuseField(src.Plant, dst.Plant, src);
            DiffuseField(src.Herb,  dst.Herb,  src);
            DiffuseField(src.Pred,  dst.Pred,  src);

            System.Array.Copy(src.Soil,  dst.Soil,  src.Soil.Length);
            System.Array.Copy(src.Water, dst.Water, src.Water.Length);
        }

        private void DiffuseField(float[] src, float[] dst, WorldState s)
        {
            float D = _cfg.diffusion;
            for (int y = 0; y < s.Height; y++)
            for (int x = 0; x < s.Width; x++)
            {
                int i = s.Index(x, y);
                if (s.Barrier[i]) { dst[i] = src[i]; continue; }

                float sum = 0f; int cnt = 0;
                AddNeighbor(ref sum, ref cnt, src, s, x + 1, y, src[i]);
                AddNeighbor(ref sum, ref cnt, src, s, x - 1, y, src[i]);
                AddNeighbor(ref sum, ref cnt, src, s, x, y + 1, src[i]);
                AddNeighbor(ref sum, ref cnt, src, s, x, y - 1, src[i]);

                float avg = sum / cnt;
                dst[i] = src[i] + D * (avg - src[i]);
            }
        }

        private static void AddNeighbor(ref float sum, ref int cnt,
                                        float[] src, WorldState s, int nx, int ny, float self)
        {
            nx = s.Wrap(nx, s.Width);
            ny = s.Wrap(ny, s.Height);
            int ni = s.Index(nx, ny);
            sum += s.Barrier[ni] ? self : src[ni];
            cnt += 1;
        }

        private static void Clamp01(WorldState w)
        {
            int n = w.Width * w.Height;
            ClampField(w.Soil, n);  ClampField(w.Water, n);
            ClampField(w.Plant, n); ClampField(w.Herb, n); ClampField(w.Pred, n);
        }

        private static void ClampField(float[] f, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float v = f[i];
                if (v < 0f) v = 0f;
                else if (v > 1f) v = 1f;
                f[i] = v;
            }
        }
    }
}
