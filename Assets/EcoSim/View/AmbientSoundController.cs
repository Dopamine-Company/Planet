using UnityEngine;
using EcoSim.Core;
using EcoSim.Time;

namespace EcoSim.View
{
    /// <summary>
    /// 전역 개체군 밀도에 반응하는 앰비언트 사운드스케이프.
    /// 레이어 3개(숲/생명/긴장)의 볼륨을 밀도에 매핑.
    /// 클립은 Resources/Audio/ambient_forest|life|tension 에서 선택 로드 —
    /// 없으면 조용히 무음(에셋 비용 0 유지). 클립 넣으면 자동 활성.
    /// </summary>
    public sealed class AmbientSoundController : MonoBehaviour
    {
        private WorldState _world;
        private AudioSource _forest, _life, _tension;
        private float _cells = 1f;

        public void Init(WorldState world, TickScheduler scheduler)
        {
            _world = world;
            _cells = Mathf.Max(1, world.Width * world.Height);

            _forest  = MakeLayer("ambient_forest");
            _life    = MakeLayer("ambient_life");
            _tension = MakeLayer("ambient_tension");

            scheduler.OnTicksAdvanced += _ => Refresh();
            Refresh();
        }

        private AudioSource MakeLayer(string clipName)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = Resources.Load<AudioClip>($"Audio/{clipName}");
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 0f;
            if (src.clip != null) src.Play(); // 클립 있을 때만 재생, 볼륨으로 믹스
            return src;
        }

        private void Refresh()
        {
            float plant = Sum(_world.Plant) / _cells; // 0~1
            float herb  = Sum(_world.Herb) / _cells;
            float pred  = Sum(_world.Pred) / _cells;

            // 숲 많으면 잔잔, 생명(초식) 활발하면 레이어 상승, 포식 우세 = 긴장.
            SetVol(_forest, Mathf.Clamp01(plant * 1.5f));
            SetVol(_life,   Mathf.Clamp01(herb * 2.5f));
            SetVol(_tension, Mathf.Clamp01(pred * 3f));
        }

        private static void SetVol(AudioSource src, float target)
        {
            if (src == null || src.clip == null) return;
            src.volume = Mathf.MoveTowards(src.volume, target, 0.1f);
        }

        private static float Sum(float[] a)
        {
            float s = 0f;
            for (int i = 0; i < a.Length; i++) s += a[i];
            return s;
        }
    }
}
