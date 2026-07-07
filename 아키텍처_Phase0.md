# 아키텍처_Phase0 (LOOP WORLD)

> 대상: Claude Code 구현용. Unity + C#. `LevelUpYourCode` 스타일 표준 준수.
> Phase 0 목표: **창발이 실제로 나오는지 검증.** 예쁨·성능 최적화는 뒤로.

---

## 0. 설계 원칙

1. **데이터와 로직 분리.** 셀 상태는 순수 데이터(배열), 규칙은 별도 클래스.
2. **더블 버퍼.** 읽기 배열 → 쓰기 배열로 계산 후 swap. 순차 갱신 편향 방지.
3. **SoA(Structure of Arrays).** `float[] soil, water, ...` 각각 한 배열. 캐시 친화 + 나중에 GPU 이식 쉬움.
4. **MonoBehaviour는 얇게.** 시뮬 코어는 plain C# 클래스. Unity 의존 최소화 → 테스트 가능.
5. namespace로 레이어 분리: `EcoSim.Core / Time / View / Interaction`.

---

## 1. 데이터 구조

### 1-1. WorldState (순수 C#, MonoBehaviour 아님)

```csharp
namespace EcoSim.Core
{
    public sealed class WorldState
    {
        public readonly int Width;
        public readonly int Height;

        // SoA. 길이 = Width * Height. 인덱스 = y * Width + x
        public float[] Soil;
        public float[] Water;
        public float[] Plant;
        public float[] Herb;
        public float[] Pred;
        public bool[]  Barrier;

        public WorldState(int width, int height)
        {
            Width = width; Height = height;
            int n = width * height;
            Soil = new float[n];  Water = new float[n];
            Plant = new float[n]; Herb = new float[n]; Pred = new float[n];
            Barrier = new bool[n];
        }

        public int Index(int x, int y) => y * Width + x;

        // 토러스 wrap. 음수·초과 좌표를 세계 안으로 감는다.
        public int Wrap(int v, int max) => (v % max + max) % max;
    }
}
```

### 1-2. SimulationConfig (ScriptableObject — 모든 파라미터 한 곳)

```csharp
namespace EcoSim.Core
{
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

        [Header("Time")]
        public float realSecondsPerTick = 2f;   // 실시간 2초 = 게임 1일
        public int   maxCatchupTicks    = 30;   // 오프라인 최대 30일치
    }
}
```
> **모든 수치는 시작값일 뿐.** 진동이 안 나오면 여기 튜닝. 특히 `assim*`/`death*`/`graze*` 비율이 진동을 좌우한다.

---

## 2. 시뮬레이션 틱 파이프라인

`기획_생태계.md` 3장 규칙을 순서대로. 더블 버퍼로 read→write.

```csharp
namespace EcoSim.Core
{
    public sealed class SimulationTick
    {
        private readonly SimulationConfig _cfg;
        private WorldState _write; // 재사용 버퍼

        public SimulationTick(SimulationConfig cfg, WorldState template)
        {
            _cfg = cfg;
            _write = new WorldState(template.Width, template.Height);
        }

        // read를 기반으로 한 틱 전진. 결과는 read에 반영(swap).
        public void Step(WorldState read)
        {
            CopyStatic(read, _write); // barrier 등 정적값 복사
            LocalRules(read, _write);  // 성장/초식/포식/환원 (셀 내부)
            Diffuse(read, _write);     // 확산 (이웃 참조)
            Clamp01(_write);
            Swap(ref read, ref _write); // 실제론 배열 참조 교환
        }

        // --- 셀 내부 규칙 (이웃 불필요) ---
        private void LocalRules(WorldState r, WorldState w)
        {
            int n = r.Width * r.Height;
            for (int i = 0; i < n; i++)
            {
                float soil = r.Soil[i],  water = r.Water[i];
                float pl = r.Plant[i], hb = r.Herb[i], pr = r.Pred[i];

                // 물 순환
                water += _cfg.rainfall;

                // 식물 성장 (자원 제약)
                float grow = _cfg.growthRate * pl * (soil * water) * (1f - pl);
                pl   += grow;
                soil -= grow * _cfg.soilCost;
                water -= grow * _cfg.waterCost;

                // 초식
                float herbBorn = _cfg.assimHerb * pl * hb;
                float herbDie  = _cfg.deathHerb * hb;
                hb += herbBorn - herbDie;
                pl -= _cfg.grazeRate * pl * hb;

                // 포식
                float predBorn = _cfg.assimPred * hb * pr;
                float predDie  = _cfg.deathPred * pr;
                pr += predBorn - predDie;
                hb -= _cfg.predationRate * hb * pr;

                // 사망 환원 → 토양
                soil += (herbDie + predDie) * _cfg.returnRate;

                w.Soil[i] = soil; w.Water[i] = water;
                w.Plant[i] = pl;  w.Herb[i] = hb; w.Pred[i] = pr;
            }
        }
    }
}
```

### 2-1. 확산 (토러스 라플라시안, barrier 차단)

```csharp
private void Diffuse(WorldState r, WorldState w)
{
    DiffuseField(r.Plant, w.Plant, r);
    DiffuseField(r.Herb,  w.Herb,  r);
    DiffuseField(r.Pred,  w.Pred,  r);
    // soil/water는 Phase 0에선 확산 생략 (원하면 추가)
}

private void DiffuseField(float[] src, float[] dst, WorldState s)
{
    float D = _cfg.diffusion;
    for (int y = 0; y < s.Height; y++)
    for (int x = 0; x < s.Width; x++)
    {
        int i = s.Index(x, y);
        if (s.Barrier[i]) { dst[i] = src[i]; continue; }

        // 토러스 4이웃. barrier 이웃은 자기 값으로 대체 → 흐름 차단
        float sum = 0f; int cnt = 0;
        AddNeighbor(ref sum, ref cnt, src, s, x + 1, y, src[i]);
        AddNeighbor(ref sum, ref cnt, src, s, x - 1, y, src[i]);
        AddNeighbor(ref sum, ref cnt, src, s, x, y + 1, src[i]);
        AddNeighbor(ref sum, ref cnt, src, s, x, y - 1, src[i]);

        float avg = sum / cnt;
        dst[i] = src[i] + D * (avg - src[i]);
    }
}

private void AddNeighbor(ref float sum, ref float cnt,
                         float[] src, WorldState s, int nx, int ny, float self)
{
    nx = s.Wrap(nx, s.Width);
    ny = s.Wrap(ny, s.Height);
    int ni = s.Index(nx, ny);
    sum += s.Barrier[ni] ? self : src[ni];
    cnt += 1;
}
```
> 확산은 `src`(직전 상태)를 읽어 `dst`에 쓴다. LocalRules 결과 위에 확산을 다시 얹는 구조라
> 실제 구현 시 **버퍼 흐름 순서 주의** — LocalRules의 출력 배열을 확산의 입력으로 넘길 것.

---

## 3. 시간 & 오프라인 진행

```csharp
namespace EcoSim.Time
{
    public sealed class TickScheduler : MonoBehaviour
    {
        [SerializeField] private SimulationConfig _cfg;
        private WorldState _world;
        private SimulationTick _sim;
        private float _accum;

        public System.Action<int> OnTicksAdvanced; // 그래프/로그 갱신 신호

        private void Update()
        {
            _accum += UnityEngine.Time.deltaTime;
            while (_accum >= _cfg.realSecondsPerTick)
            {
                _sim.Step(_world);
                _accum -= _cfg.realSecondsPerTick;
                OnTicksAdvanced?.Invoke(1);
            }
        }
    }
}
```

### 3-1. OfflineProgress (켤 때 몰아서 catch-up)

```csharp
namespace EcoSim.Time
{
    public sealed class OfflineProgress
    {
        public struct Result { public int TicksRun; public bool Capped; }

        public Result CatchUp(SimulationTick sim, WorldState world,
                              SimulationConfig cfg,
                              System.DateTime lastPlayed, System.DateTime now)
        {
            double elapsedSec = (now - lastPlayed).TotalSeconds;
            int wantTicks = (int)(elapsedSec / cfg.realSecondsPerTick);

            bool capped = wantTicks > cfg.maxCatchupTicks;
            int runTicks = capped ? cfg.maxCatchupTicks : wantTicks;

            for (int t = 0; t < runTicks; t++) sim.Step(world);
            return new Result { TicksRun = runTicks, Capped = capped };
        }
    }
}
```
- `lastPlayed`는 저장 시 `System.DateTime.UtcNow`로 기록.
- catch-up 결과로 "네가 없는 동안 N일 지남" 요약 UI를 띄운다.

---

## 4. 렌더링 (Texture2D 한 장)

```csharp
namespace EcoSim.View
{
    public sealed class WorldRenderer : MonoBehaviour
    {
        [SerializeField] private Renderer _quad; // 평면 메시
        private Texture2D _tex;
        private Color32[] _pixels;

        public void Init(WorldState w)
        {
            _tex = new Texture2D(w.Width, w.Height, TextureFormat.RGBA32, false)
                   { filterMode = FilterMode.Point };
            _pixels = new Color32[w.Width * w.Height];
            _quad.material.mainTexture = _tex;
        }

        public void Draw(WorldState w)
        {
            int n = w.Width * w.Height;
            for (int i = 0; i < n; i++)
            {
                // 밀도 → 색. 우세종 강조 방식(예시).
                byte g = (byte)(w.Plant[i] * 255); // 숲=초록
                byte r = (byte)(w.Pred[i]  * 255); // 포식=빨강
                byte b = (byte)(w.Water[i] * 200); // 물=파랑 베이스
                // 초식(노랑)은 r+g 혼합으로 표현 가능
                r = (byte)Mathf.Min(255, r + w.Herb[i] * 200);
                g = (byte)Mathf.Min(255, g + w.Herb[i] * 200);
                _pixels[i] = new Color32(r, g, b, 255);
            }
            _tex.SetPixels32(_pixels);
            _tex.Apply(false);
        }
    }
}
```
> 매 틱마다 `Draw` 호출. 셀 수십만까지 이 방식으로 충분. Point 필터로 픽셀 선명하게.

### 4-1. 시각화 컴포넌트
- `PopulationGraph`: 매 틱 전역 합계(`sum(Plant)`, `sum(Herb)`, `sum(Pred)`)를 링버퍼에 push, UI 라인으로 그림.
- `EventLog`: 전역 합계가 임계값 통과 시 문자열 로그 추가.
  - 붕괴: 어떤 종 합계가 직전 대비 X% 아래로, 또는 0 근처로 → `"N일차: OO 붕괴"`
  - 지배: 어떤 종이 세계 밀도의 Y% 초과 → `"N일차: OO 지배"`

---

## 5. 개입 (Interaction)

```csharp
namespace EcoSim.Interaction
{
    public sealed class InterventionController : MonoBehaviour
    {
        public enum Tool { PlantTree, AddHerb, AddPred, ToggleBarrier }
        [SerializeField] private Tool _tool;
        [SerializeField] private int _brush = 2;   // 브러시 반경
        [SerializeField] private float _amount = 0.6f;

        // 화면 클릭 → 셀 좌표 변환 후 호출
        public void Apply(WorldState w, int cx, int cy)
        {
            for (int dy = -_brush; dy <= _brush; dy++)
            for (int dx = -_brush; dx <= _brush; dx++)
            {
                int x = w.Wrap(cx + dx, w.Width);
                int y = w.Wrap(cy + dy, w.Height);
                int i = w.Index(x, y);
                switch (_tool)
                {
                    case Tool.PlantTree:     w.Plant[i] = Mathf.Min(1, w.Plant[i] + _amount); break;
                    case Tool.AddHerb:       w.Herb[i]  = Mathf.Min(1, w.Herb[i]  + _amount); break;
                    case Tool.AddPred:       w.Pred[i]  = Mathf.Min(1, w.Pred[i]  + _amount); break;
                    case Tool.ToggleBarrier: w.Barrier[i] = !w.Barrier[i]; break;
                }
            }
        }
    }
}
```

---

## 6. 저장 / 로드

- 저장 대상: 5개 float 배열 + barrier + `lastPlayedUtc`.
- Phase 0은 단순하게: 배열을 바이너리로 덤프(`BinaryWriter`) 또는 JSON(느림, 소형 세계만).
- 큰 세계면 배열을 `byte[]`로 직렬화 후 파일 저장. 압축은 나중.

---

## 7. 씬 구성 (부트스트랩)

```
[GameRoot] (MonoBehaviour)
 ├─ SimulationConfig (ScriptableObject 참조)
 ├─ WorldState 생성 → 초기 시드(랜덤 soil/water, 군데군데 plant/herb/pred)
 ├─ SimulationTick 생성
 ├─ OfflineProgress.CatchUp() 실행 (저장 있으면)
 ├─ TickScheduler.Init(world, sim)
 ├─ WorldRenderer.Init(world) / OnTicksAdvanced → Draw + Graph + Log
 └─ InterventionController (입력 → world 직접 수정)
```

---

## 8. Phase 0 검증 체크리스트

구현 후 이게 되면 Phase 0 성공:

- [ ] 식물만 심으면 → 퍼지다가 자원 한계에서 멈춤(사막화 안정).
- [ ] 초식 풀면 → 식물·초식 개체군이 **파도치듯 진동**한다. (핵심)
- [ ] 포식자 풀면 → 3단계 진동, 또는 한쪽 붕괴.
- [ ] 벽으로 세계를 반 가르면 → 양쪽이 다른 운명으로 갈라진다.
- [ ] 게임 껐다 켜면 → 껐던 시간만큼 진행돼 있고 요약 로그가 뜬다.
- [ ] 이벤트 로그에 붕괴·지배가 자동 기록된다.

진동이 안 나오면 → `assimHerb`↑ 또는 `deathHerb`↓ 부터 만져라. 그게 로트카-볼테라 진동의 스위치.

---

## 9. 확장 경로 (Phase 2)

- SoA 배열 그대로 `ComputeBuffer`에 올려 compute shader로 이식 → 백만 셀.
- LocalRules/Diffuse를 커널 두 개로. 지금 CPU 구조가 그대로 GPU 커널에 매핑됨(설계 의도).
