using UnityEngine;
using UnityEngine.InputSystem;
using EcoSim.Core;
using EcoSim.Time;
using EcoSim.View;

namespace EcoSim.Interaction
{
    /// <summary>
    /// 관찰 입력. 개입(1~4, 좌클릭)과 충돌하지 않는 키만 사용:
    ///   Tab   = 뷰 모드 순환 (Combined→Plant→Herb→Pred→Soil→Water)
    ///   Space = 정지/재개
    ///   5/6/7 = 1x/4x/16x
    ///   우클릭 = 추적 셀 선택(미니 그래프 대상)
    /// 마우스 호버 셀은 매 프레임 갱신 → HUD 인스펙터가 읽는다.
    /// </summary>
    public sealed class ObservationController : MonoBehaviour
    {
        private WorldState _world;
        private Collider _quadCollider;
        private Camera _cam;
        private WorldRenderer _renderer;
        private TickScheduler _scheduler;

        private int _resumeSpeed = 1;

        public bool HasHover { get; private set; }
        public int HoverX { get; private set; }
        public int HoverY { get; private set; }

        public bool HasSelection { get; private set; }
        public int SelX { get; private set; }
        public int SelY { get; private set; }

        public ViewMode Mode => _renderer != null ? _renderer.Mode : ViewMode.Combined;
        public int Speed => _scheduler != null ? _scheduler.SpeedMultiplier : 1;

        public void Init(WorldState world, Collider quadCollider, Camera cam,
                         WorldRenderer renderer, TickScheduler scheduler)
        {
            _world = world; _quadCollider = quadCollider; _cam = cam;
            _renderer = renderer; _scheduler = scheduler;
        }

        private void Update()
        {
            if (_world == null) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.tabKey.wasPressedThisFrame) _renderer.CycleMode();

                if (kb.spaceKey.wasPressedThisFrame)
                {
                    if (_scheduler.SpeedMultiplier > 0)
                    {
                        _resumeSpeed = _scheduler.SpeedMultiplier;
                        _scheduler.SpeedMultiplier = 0;
                    }
                    else _scheduler.SpeedMultiplier = _resumeSpeed;
                }

                if (kb.digit5Key.wasPressedThisFrame) _scheduler.SpeedMultiplier = 1;
                if (kb.digit6Key.wasPressedThisFrame) _scheduler.SpeedMultiplier = 4;
                if (kb.digit7Key.wasPressedThisFrame) _scheduler.SpeedMultiplier = 16;
            }

            var mouse = Mouse.current;
            if (mouse == null) { HasHover = false; return; }

            Vector2 pos = mouse.position.ReadValue();
            HasHover = CellPicker.TryPick(_cam, _quadCollider, _world, pos, out int hx, out int hy);
            HoverX = hx; HoverY = hy;

            if (HasHover && mouse.rightButton.wasPressedThisFrame)
            {
                HasSelection = true;
                SelX = hx; SelY = hy;
            }
        }
    }
}
