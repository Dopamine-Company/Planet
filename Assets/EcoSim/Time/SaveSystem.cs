using System;
using System.IO;
using EcoSim.Core;

namespace EcoSim.Time
{
    /// <summary>
    /// 세계 저장/로드. 5개 float 배열 + barrier + elevation + day + lastPlayedUtc를 바이너리 덤프.
    /// 순수 C# — 헤드리스 테스트 가능. 경로는 호출자가 결정.
    /// </summary>
    public static class SaveSystem
    {
        private const int Version = 2; // v2: elevation + day 추가

        public static void Save(WorldState w, DateTime lastPlayedUtc, int day, string path)
        {
            using var bw = new BinaryWriter(File.Open(path, FileMode.Create));
            bw.Write(Version);
            bw.Write(w.Width);
            bw.Write(w.Height);
            bw.Write(lastPlayedUtc.Ticks);
            bw.Write(day);

            WriteFloats(bw, w.Soil);
            WriteFloats(bw, w.Water);
            WriteFloats(bw, w.Plant);
            WriteFloats(bw, w.Herb);
            WriteFloats(bw, w.Pred);
            WriteFloats(bw, w.Elevation);
            foreach (bool b in w.Barrier) bw.Write(b);
        }

        public static bool TryLoad(string path, out WorldState world,
                                   out DateTime lastPlayedUtc, out int day)
        {
            world = null;
            lastPlayedUtc = default;
            day = 0;
            if (!File.Exists(path)) return false;

            try
            {
                using var br = new BinaryReader(File.Open(path, FileMode.Open));
                int version = br.ReadInt32();
                if (version != Version) return false; // 구버전 세이브는 폐기(새 세계로)

                int width = br.ReadInt32();
                int height = br.ReadInt32();
                lastPlayedUtc = new DateTime(br.ReadInt64(), DateTimeKind.Utc);
                day = br.ReadInt32();

                var w = new WorldState(width, height);
                ReadFloats(br, w.Soil);
                ReadFloats(br, w.Water);
                ReadFloats(br, w.Plant);
                ReadFloats(br, w.Herb);
                ReadFloats(br, w.Pred);
                ReadFloats(br, w.Elevation);
                for (int i = 0; i < w.Barrier.Length; i++) w.Barrier[i] = br.ReadBoolean();

                world = w;
                return true;
            }
            catch (Exception)
            {
                return false; // 손상 파일 → 새 세계로 시작.
            }
        }

        private static void WriteFloats(BinaryWriter bw, float[] a)
        {
            var bytes = new byte[a.Length * sizeof(float)];
            Buffer.BlockCopy(a, 0, bytes, 0, bytes.Length);
            bw.Write(bytes);
        }

        private static void ReadFloats(BinaryReader br, float[] a)
        {
            var bytes = br.ReadBytes(a.Length * sizeof(float));
            Buffer.BlockCopy(bytes, 0, a, 0, bytes.Length);
        }
    }
}
