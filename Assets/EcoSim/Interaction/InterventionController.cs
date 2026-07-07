using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using EcoSim.Core;

namespace EcoSim.Interaction
{
    /// <summary>
    /// 플레이어 개입 3종: 나무 심기 / 벌레(초식·포식) 추가 / 벽 토글.
    /// 도구 전환: 1=나무, 2=초식, 3=포식, 4=벽 (또는 UI 버튼).
    /// 좌클릭/드래그로 페인트. 벽은 드래그 경로를 브레젠험 보간해 끊김 없이,
    /// 한 드래그에서 같은 셀 중복 토글 방지.
    /// </summary>
    public sealed class InterventionController : MonoBehaviour
    {
        public enum Tool { PlantTree, AddHerb, AddPred, ToggleBarrier }

        [SerializeField] private Tool _tool = Tool.PlantTree;
        [SerializeField] private int _brush = 2;      // 브러시 반경(셀)
        [SerializeField] private float _amount = 0.6f;

        private WorldState _world;
        private Collider _quadCollider;
        private Camera _cam;
        private System.Action _onWorldChanged; // 즉시 재렌더 신호

        private bool _dragging;
        private int _lastX, _lastY;
        private readonly HashSet<int> _barrierTouched = new HashSet<int>(); // 이번 드래그에 토글한 셀

        // 심기/추가 발생 지점 알림 (뷰 피드백용): (cx, cy, tool)
        public System.Action<int, int, Tool> OnIntervention;
        public System.Action OnToolChanged;

        public Tool CurrentTool
        {
            get => _tool;
            set { if (_tool != value) { _tool = value; OnToolChanged?.Invoke(); } }
        }

        public int Brush
        {
            get => _brush;
            set => _brush = Mathf.Clamp(value, 0, 16);
        }

        public void Init(WorldState world, Collider quadCollider, Camera cam,
                         System.Action onWorldChanged)
        {
            _world = world; _quadCollider = quadCollider; _cam = cam;
            _onWorldChanged = onWorldChanged;
        }

        private void Update()
        {
            if (_world == null) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) CurrentTool = Tool.PlantTree;
                if (kb.digit2Key.wasPressedThisFrame) CurrentTool = Tool.AddHerb;
                if (kb.digit3Key.wasPressedThisFrame) CurrentTool = Tool.AddPred;
                if (kb.digit4Key.wasPressedThisFrame) CurrentTool = Tool.ToggleBarrier;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                _dragging = false;
                _barrierTouched.Clear();
            }

            if (!mouse.leftButton.isPressed) return;
            if (!TryGetCell(mouse.position.ReadValue(), out int cx, out int cy)) return;

            if (!_dragging)
            {
                _dragging = true;
                _lastX = cx; _lastY = cy;
                Stamp(cx, cy);
            }
            else
            {
                // 이전 셀 → 현재 셀 경로 보간(끊김 없는 페인트/벽).
                PaintLine(_lastX, _lastY, cx, cy);
                _lastX = cx; _lastY = cy;
            }

            _onWorldChanged?.Invoke();
        }

        private bool TryGetCell(Vector2 screenPos, out int cx, out int cy)
            => CellPicker.TryPick(_cam, _quadCollider, _world, screenPos, out cx, out cy);

        // 브레젠험 라인으로 두 셀 사이를 채운다.
        private void PaintLine(int x0, int y0, int x1, int y1)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Stamp(x0, y0);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        // 한 지점에 브러시 적용.
        private void Stamp(int cx, int cy)
        {
            var w = _world;
            for (int dy = -_brush; dy <= _brush; dy++)
            for (int dx = -_brush; dx <= _brush; dx++)
            {
                if (dx * dx + dy * dy > _brush * _brush) continue; // 원형 브러시
                int x = w.Wrap(cx + dx, w.Width);
                int y = w.Wrap(cy + dy, w.Height);
                int i = w.Index(x, y);
                switch (_tool)
                {
                    case Tool.PlantTree: w.Plant[i] = Mathf.Min(1f, w.Plant[i] + _amount); break;
                    case Tool.AddHerb:   w.Herb[i]  = Mathf.Min(1f, w.Herb[i]  + _amount); break;
                    case Tool.AddPred:   w.Pred[i]  = Mathf.Min(1f, w.Pred[i]  + _amount); break;
                    case Tool.ToggleBarrier:
                        if (_barrierTouched.Add(i)) w.Barrier[i] = !w.Barrier[i]; // 드래그당 1회
                        break;
                }
            }
            OnIntervention?.Invoke(cx, cy, _tool);
        }
    }
}
