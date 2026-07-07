// EcoSim.Core / SimulationTick — Phase 0 시뮬레이션 틱 파이프라인.
namespace EcoSim.Core
{
    /// <summary>
    /// 한 틱(=게임 1일) 전진. 더블 버퍼로 read→write 하여 순차 갱신 편향을 막는다.
    /// 순서: LocalRules(셀 내부) → Diffuse(이웃 참조) → Clamp01.
    ///
    /// 버퍼 흐름 주의(설계 문서 경고): LocalRules의 출력을 Diffuse의 입력으로 넘긴다.
    /// - read → LocalRules → _buf (셀 내부 결과)
    /// - _buf → Diffuse → read (확산 결과를 read에 되씀)
    /// LocalRules가 read를 모두 소비한 뒤에 Diffuse가 read를 목적지로 덮어쓰므로 충돌이 없다.
    /// 결과적으로 Step 종료 시 read가 최신 상태를 담는다(별도 swap 불필요).
    /// </summary>
    public sealed class SimulationTick
    {
        private readonly SimulationConfig _cfg;
        private readonly WorldState _buf; // 재사용 스크래치 버퍼

        public SimulationTick(SimulationConfig cfg, WorldState template)
        {
            _cfg = cfg;
            _buf = new WorldState(template.Width, template.Height);
        }

        public void Step(WorldState read)
        {
            CopyStatic(read, _buf);   // barrier 스냅샷 (Diffuse 이웃 판정용)
            LocalRules(read, _buf);   // read → _buf : 성장/초식/포식/환원
            Diffuse(_buf, read);      // _buf → read : 확산 (LocalRules 출력을 입력으로)
            Clamp01(read);
        }

        private static void CopyStatic(WorldState r, WorldState w)
        {
            System.Array.Copy(r.Barrier, w.Barrier, r.Barrier.Length);
        }

        // --- 셀 내부 규칙 (이웃 불필요) ---
        private void LocalRules(WorldState r, WorldState w)
        {
            int n = r.Width * r.Height;
            for (int i = 0; i < n; i++)
            {
                float soil = r.Soil[i],  water = r.Water[i];
                float pl = r.Plant[i], hb = r.Herb[i], pr = r.Pred[i];

                // 물 순환 (전역 상수 강우)
                water += _cfg.rainfall;

                // 식물 성장 (자원 제약 로지스틱)
                float grow = _cfg.growthRate * pl * (soil * water) * (1f - pl);
                pl    += grow;
                soil  -= grow * _cfg.soilCost;
                water -= grow * _cfg.waterCost;

                // 초식 (식물 → 초식)
                float herbBorn = _cfg.assimHerb * pl * hb;
                float herbDie  = _cfg.deathHerb * hb;
                hb += herbBorn - herbDie;
                pl -= _cfg.grazeRate * pl * hb;

                // 포식 (초식 → 포식자)
                float predBorn = _cfg.assimPred * hb * pr;
                float predDie  = _cfg.deathPred * pr;
                pr += predBorn - predDie;
                hb -= _cfg.predationRate * hb * pr;

                // 사망 환원 → 토양 (영양분 순환)
                soil += (herbDie + predDie) * _cfg.returnRate;

                w.Soil[i]  = soil;  w.Water[i] = water;
                w.Plant[i] = pl;    w.Herb[i]  = hb;   w.Pred[i] = pr;
            }
        }

        // --- 확산 (토러스 라플라시안, barrier 차단) ---
        // src(=_buf, LocalRules 출력)을 읽어 dst(=read)에 쓴다.
        private void Diffuse(WorldState src, WorldState dst)
        {
            DiffuseField(src.Plant, dst.Plant, src);
            DiffuseField(src.Herb,  dst.Herb,  src);
            DiffuseField(src.Pred,  dst.Pred,  src);

            // soil/water는 Phase 0에서 확산 생략 → src 값을 그대로 이월.
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

                // 토러스 4이웃. barrier 이웃은 자기 값으로 대체 → 흐름 차단.
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
