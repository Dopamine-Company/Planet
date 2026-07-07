using UnityEngine;
using EcoSim.Core;
using EcoSim.Interaction;

namespace EcoSim.View
{
    /// <summary>
    /// 마우스 호버 셀을 따라다니는 브러시 원(반투명 링) + 개입 시 짧은 색 플래시.
    /// 월드 공간 LineRenderer 2개(에셋 불필요). 셀↔월드 변환은 쿼드 기준.
    /// </summary>
    public sealed class BrushPreview : MonoBehaviour
    {
        private const int Segments = 40;

        private WorldState _world;
        private Transform _quad;
        private ObservationController _obs;
        private InterventionController _intv;

        private LineRenderer _ring;   // 상시 브러시 윤곽
        private LineRenderer _flash;  // 클릭 순간 확장·소멸
        private float _flashT;        // 1→0
        private Vector3 _flashCenter;
        private float _flashBaseR;

        public void Init(WorldState world, Transform quad,
                         ObservationController obs, InterventionController intv)
        {
            _world = world; _quad = quad; _obs = obs; _intv = intv;
            _intv.OnIntervention += OnIntervention;

            _ring = MakeLine("BrushRing", 0.03f, Segments + 1);
            _flash = MakeLine("BrushFlash", 0.06f, Segments + 1);
            _flash.enabled = false;
        }

        private void OnDestroy()
        {
            if (_intv != null) _intv.OnIntervention -= OnIntervention;
        }

        private LineRenderer MakeLine(string name, float width, int count)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = count;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            // Sprites/Default: 정점 색 + 알파 블렌딩 지원(URP Unlit은 material.color 무시).
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.textureMode = LineTextureMode.Stretch;
            SetColor(lr, Color.white);
            return lr;
        }

        private void Update()
        {
            if (_world == null) return;

            if (_obs != null && _obs.HasHover)
            {
                _ring.enabled = true;
                Vector3 c = CellToWorld(_obs.HoverX, _obs.HoverY);
                float r = (_intv.Brush + 0.5f) * CellSize();
                DrawCircle(_ring, c, r);
                SetColor(_ring, ToolColor(_intv.CurrentTool, 0.8f));
            }
            else _ring.enabled = false;

            if (_flashT > 0f)
            {
                _flashT -= UnityEngine.Time.deltaTime * 3f; // ~0.33s
                if (_flashT <= 0f) { _flash.enabled = false; }
                else
                {
                    float k = 1f - _flashT;                    // 0→1 확장
                    float r = _flashBaseR * (1f + 1.2f * k);
                    DrawCircle(_flash, _flashCenter, r);
                    Color col = _flash.startColor;
                    col.a = _flashT;                           // 소멸
                    SetColor(_flash, col);
                }
            }
        }

        private void OnIntervention(int cx, int cy, InterventionController.Tool tool)
        {
            _flashCenter = CellToWorld(cx, cy);
            _flashBaseR = (_intv.Brush + 0.5f) * CellSize();
            _flashT = 1f;
            _flash.enabled = true;
            SetColor(_flash, ToolColor(tool, 1f));
            DrawCircle(_flash, _flashCenter, _flashBaseR);
        }

        // 셀 중심 → 월드. 쿼드는 원점 중심, localScale이 월드 크기.
        private Vector3 CellToWorld(int cx, int cy)
        {
            float u = (cx + 0.5f) / _world.Width - 0.5f;
            float v = (cy + 0.5f) / _world.Height - 0.5f;
            Vector3 s = _quad.localScale;
            return new Vector3(u * s.x, v * s.y, _quad.position.z - 0.1f);
        }

        private float CellSize() => _quad.localScale.y / _world.Height;

        private static void SetColor(LineRenderer lr, Color c)
        {
            lr.startColor = c;
            lr.endColor = c;
        }

        private static void DrawCircle(LineRenderer lr, Vector3 center, float radius)
        {
            for (int i = 0; i <= Segments; i++)
            {
                float a = (float)i / Segments * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        private static Color ToolColor(InterventionController.Tool tool, float a)
        {
            switch (tool)
            {
                case InterventionController.Tool.PlantTree:     return new Color(0.3f, 0.9f, 0.3f, a);
                case InterventionController.Tool.AddHerb:       return new Color(0.95f, 0.9f, 0.2f, a);
                case InterventionController.Tool.AddPred:       return new Color(0.95f, 0.25f, 0.25f, a);
                default:                                        return new Color(0.7f, 0.7f, 0.7f, a); // 벽
            }
        }
    }
}
