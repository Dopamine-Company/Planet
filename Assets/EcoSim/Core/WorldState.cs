namespace EcoSim.Core
{
    /// <summary>
    /// 순수 C# 세계 상태. MonoBehaviour 아님 → 헤드리스 테스트 가능.
    /// SoA(Structure of Arrays): 각 상태값이 독립 배열. 인덱스 = y * Width + x.
    /// </summary>
    public sealed class WorldState
    {
        public readonly int Width;
        public readonly int Height;

        // SoA. 길이 = Width * Height.
        public float[] Soil;
        public float[] Water;
        public float[] Plant;
        public float[] Herb;
        public float[] Pred;
        public bool[]  Barrier;
        public float[] Elevation; // 고정 지형 높이 0~1 (물 흐름용, 세계 생성 시 1회)

        public WorldState(int width, int height)
        {
            Width = width; Height = height;
            int n = width * height;
            Soil = new float[n];  Water = new float[n];
            Plant = new float[n]; Herb = new float[n]; Pred = new float[n];
            Barrier = new bool[n];
            Elevation = new float[n];
        }

        public int Index(int x, int y) => y * Width + x;

        // 토러스 wrap. 음수·초과 좌표를 세계 안으로 감는다.
        public int Wrap(int v, int max) => (v % max + max) % max;
    }
}
