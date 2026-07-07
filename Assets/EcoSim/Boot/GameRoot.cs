using UnityEngine;
using EcoSim.Core;
using EcoSim.Interaction;
using EcoSim.Time;
using EcoSim.View;

namespace EcoSim.Boot
{
    /// <summary>
    /// 런타임 부트스트랩. 빈 GameObject에 이 컴포넌트만 붙이고 Play 하면
    /// 세계 생성 → 랜덤 시드 → 쿼드/카메라 코드 생성 → 틱 시작까지 전부 자동.
    /// _config 비워두면 기본값 SimulationConfig를 즉석 생성한다.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private SimulationConfig _config;
        [SerializeField] private int _randomSeed = 12345;

        public WorldState World { get; private set; }
        public SimulationTick Sim { get; private set; }
        public TickScheduler Scheduler { get; private set; }
        public Renderer WorldQuad { get; private set; }

        /// 이번 부팅에서 오프라인 catch-up으로 돌린 틱 수(=지난 날 수). HUD 요약용.
        public int OfflineTicksRun { get; private set; }
        public bool OfflineCapped { get; private set; }

        public ObservationController Observation { get; private set; }
        public InterventionController Intervention { get; private set; }
        public int ConfigYearLength => _config != null ? _config.yearLength : 120;

        private WorldRenderer _renderer;
        private string SavePath => System.IO.Path.Combine(
            Application.persistentDataPath, "ecosim_world.sav");

        private void Awake()
        {
            if (_config == null)
                _config = Resources.Load<SimulationConfig>("EcoSimConfig");
            if (_config == null)
                _config = ScriptableObject.CreateInstance<SimulationConfig>();

            // 저장 있으면 로드(크기 일치 시) + 오프라인 catch-up, 없으면 새 세계.
            if (SaveSystem.TryLoad(SavePath, out WorldState loaded, out System.DateTime lastPlayed, out int savedDay)
                && loaded.Width == _config.width && loaded.Height == _config.height)
            {
                World = loaded;
                Sim = new SimulationTick(_config, World);
                Sim.SetDay(savedDay); // 계절 위상 복원 (catch-up 전에)
                var result = new OfflineProgress().CatchUp(
                    Sim, World, _config, lastPlayed, System.DateTime.UtcNow);
                OfflineTicksRun = result.TicksRun;
                OfflineCapped = result.Capped;
            }
            else
            {
                World = new WorldState(_config.width, _config.height);
                SeedWorld(World, _randomSeed);
                Sim = new SimulationTick(_config, World);
            }

            WorldQuad = CreateWorldQuad();
            _renderer = gameObject.AddComponent<WorldRenderer>();
            _renderer.Init(World, WorldQuad);
            _renderer.Draw(World);

            Camera cam = SetupCamera();

            Scheduler = gameObject.AddComponent<TickScheduler>();
            Scheduler.Init(_config, World, Sim);
            // 배속 1일 때만 색 보간(고속에선 어차피 안 보이니 즉시).
            Scheduler.OnTicksAdvanced += _ => _renderer.Draw(
                World,
                instant: Scheduler.SpeedMultiplier != 1,
                interpDuration: _config.realSecondsPerTick);

            Intervention = gameObject.AddComponent<InterventionController>();
            Intervention.Init(World, WorldQuad.GetComponent<Collider>(), cam,
                              () => _renderer.Draw(World));

            Observation = gameObject.AddComponent<ObservationController>();
            Observation.Init(World, WorldQuad.GetComponent<Collider>(), cam,
                             _renderer, Scheduler);

            var brush = gameObject.AddComponent<BrushPreview>();
            brush.Init(World, WorldQuad.transform, Observation, Intervention);

            var ambient = gameObject.AddComponent<AmbientSoundController>();
            ambient.Init(World, Scheduler);
        }

        private void OnApplicationQuit() => SaveNow();

        // 모바일 대비: 백그라운드 전환 시에도 저장.
        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        private void SaveNow()
        {
            if (World == null) return;
            SaveSystem.Save(World, System.DateTime.UtcNow, Sim != null ? Sim.Day : 0, SavePath);
        }

        // 초기 시드: 토양/수분 랜덤 베이스 + 식물 약간 + 초식/포식 군데군데 패치 + 지형.
        private static void SeedWorld(WorldState w, int seed)
        {
            var rng = new System.Random(seed);
            GenerateElevation(w, seed);

            int n = w.Width * w.Height;
            for (int i = 0; i < n; i++)
            {
                w.Soil[i]  = 0.6f + 0.4f * (float)rng.NextDouble();
                // 저지대일수록 초기 수분 많게(강바닥 씨앗).
                w.Water[i] = Mathf.Lerp(0.7f, 0.3f, w.Elevation[i]) + 0.1f * (float)rng.NextDouble();
                w.Plant[i] = 0.3f * (float)rng.NextDouble();
            }
            SeedPatches(w, rng, w.Herb, count: 8, radius: 4, value: 0.3f);
            SeedPatches(w, rng, w.Pred, count: 3, radius: 3, value: 0.2f);
        }

        // Perlin 노이즈로 지형 높이 생성. 토러스 이음새는 Phase 2에선 무시(허용 오차).
        private static void GenerateElevation(WorldState w, int seed)
        {
            var rng = new System.Random(seed * 7919);
            float ox = (float)rng.NextDouble() * 1000f;
            float oy = (float)rng.NextDouble() * 1000f;
            float scale = 5f / Mathf.Max(w.Width, w.Height);

            for (int y = 0; y < w.Height; y++)
            for (int x = 0; x < w.Width; x++)
            {
                float e = Mathf.PerlinNoise(ox + x * scale, oy + y * scale);
                // 대비 강화(골짜기/능선 뚜렷하게)
                e = Mathf.Clamp01((e - 0.5f) * 1.6f + 0.5f);
                w.Elevation[w.Index(x, y)] = e;
            }
        }

        private static void SeedPatches(WorldState w, System.Random rng,
                                        float[] field, int count, int radius, float value)
        {
            for (int p = 0; p < count; p++)
            {
                int cx = rng.Next(w.Width), cy = rng.Next(w.Height);
                for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;
                    int x = w.Wrap(cx + dx, w.Width);
                    int y = w.Wrap(cy + dy, w.Height);
                    field[w.Index(x, y)] = value;
                }
            }
        }

        // 세계 표시용 쿼드. 높이 10유닛 고정, 폭은 종횡비 따라. MeshCollider는 클릭 개입용으로 유지.
        private Renderer CreateWorldQuad()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "WorldQuad";
            float aspect = (float)_config.width / _config.height;
            go.transform.position = Vector3.zero;
            go.transform.localScale = new Vector3(10f * aspect, 10f, 1f);

            var quad = go.GetComponent<Renderer>();
            // URP: CreatePrimitive 기본 머티리얼은 빌드에서 깨질 수 있어 명시 셰이더 지정.
            quad.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            return quad;
        }

        private Camera SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            return cam;
        }
    }
}
