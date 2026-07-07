using UnityEngine;
using EcoSim.Core;

namespace EcoSim.Interaction
{
    /// <summary>화면 좌표 → 세계 셀 좌표. 쿼드 MeshCollider의 UV를 이용.</summary>
    public static class CellPicker
    {
        public static bool TryPick(Camera cam, Collider quadCollider, WorldState world,
                                   Vector2 screenPos, out int cx, out int cy)
        {
            cx = cy = 0;
            if (cam == null || quadCollider == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPos);
            if (!quadCollider.Raycast(ray, out RaycastHit hit, 100f)) return false;

            Vector2 uv = hit.textureCoord;
            cx = Mathf.Clamp((int)(uv.x * world.Width), 0, world.Width - 1);
            cy = Mathf.Clamp((int)(uv.y * world.Height), 0, world.Height - 1);
            return true;
        }
    }
}
