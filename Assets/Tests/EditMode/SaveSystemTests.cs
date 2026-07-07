using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using EcoSim.Core;
using EcoSim.Time;

namespace EcoSim.Tests
{
    /// <summary>저장/로드 라운드트립 + 오프라인 catch-up 검증.</summary>
    public sealed class SaveSystemTests
    {
        private string _path;

        [SetUp]
        public void SetUp() => _path = Path.Combine(Path.GetTempPath(), "ecosim_test.sav");

        [TearDown]
        public void TearDown() { if (File.Exists(_path)) File.Delete(_path); }

        [Test]
        public void SaveLoad_Roundtrip_PreservesAllFields()
        {
            var w = new WorldState(16, 16);
            var rng = new System.Random(7);
            for (int i = 0; i < 256; i++)
            {
                w.Soil[i] = (float)rng.NextDouble();
                w.Water[i] = (float)rng.NextDouble();
                w.Plant[i] = (float)rng.NextDouble();
                w.Herb[i] = (float)rng.NextDouble();
                w.Pred[i] = (float)rng.NextDouble();
                w.Elevation[i] = (float)rng.NextDouble();
                w.Barrier[i] = rng.Next(2) == 1;
            }
            var stamp = new DateTime(2026, 7, 7, 3, 0, 0, DateTimeKind.Utc);

            SaveSystem.Save(w, stamp, day: 137, _path);
            Assert.IsTrue(SaveSystem.TryLoad(_path, out WorldState r, out DateTime loadedStamp, out int loadedDay));

            Assert.AreEqual(stamp, loadedStamp);
            Assert.AreEqual(137, loadedDay);
            Assert.AreEqual(w.Width, r.Width);
            CollectionAssert.AreEqual(w.Soil, r.Soil);
            CollectionAssert.AreEqual(w.Water, r.Water);
            CollectionAssert.AreEqual(w.Plant, r.Plant);
            CollectionAssert.AreEqual(w.Herb, r.Herb);
            CollectionAssert.AreEqual(w.Pred, r.Pred);
            CollectionAssert.AreEqual(w.Elevation, r.Elevation);
            CollectionAssert.AreEqual(w.Barrier, r.Barrier);
        }

        [Test]
        public void TryLoad_MissingFile_ReturnsFalse()
        {
            Assert.IsFalse(SaveSystem.TryLoad(_path + ".nope", out _, out _, out _));
        }

        [Test]
        public void CatchUp_RunsElapsedTicks_AndCaps()
        {
            var cfg = ScriptableObject.CreateInstance<SimulationConfig>();
            cfg.realSecondsPerTick = 2f;
            cfg.maxCatchupTicks = 30;

            var w = new WorldState(8, 8);
            var sim = new SimulationTick(cfg, w);
            var op = new OfflineProgress();
            var now = new DateTime(2026, 7, 7, 12, 0, 0, DateTimeKind.Utc);

            // 10틱치(20초) → 10틱, 상한 미적용
            var r1 = op.CatchUp(sim, w, cfg, now.AddSeconds(-20), now);
            Assert.AreEqual(10, r1.TicksRun);
            Assert.IsFalse(r1.Capped);

            // 1시간(1800틱치) → 30틱 상한
            var r2 = op.CatchUp(sim, w, cfg, now.AddHours(-1), now);
            Assert.AreEqual(30, r2.TicksRun);
            Assert.IsTrue(r2.Capped);

            // 시계 역행 → 0틱
            var r3 = op.CatchUp(sim, w, cfg, now.AddMinutes(10), now);
            Assert.AreEqual(0, r3.TicksRun);
        }
    }
}
