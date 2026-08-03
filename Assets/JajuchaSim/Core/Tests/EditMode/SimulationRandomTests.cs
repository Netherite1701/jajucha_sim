using JajuchaSim.Core;
using NUnit.Framework;

namespace JajuchaSim.Core.Tests
{
    public class SimulationRandomTests
    {
        [Test]
        public void Same_Seed_Produces_Identical_Sequences()
        {
            var a = new SimulationRandom(123);
            var b = new SimulationRandom(123);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.NextUInt64(), b.NextUInt64(),
                    $"divergence at index {i}");
        }

        [Test]
        public void Floats_In_Range()
        {
            var r = new SimulationRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                float f = r.NextFloat();
                Assert.IsTrue(f >= 0f && f < 1f, $"float out of range: {f}");
            }
        }

        [Test]
        public void NextIntRange_IsInclusive()
        {
            var r = new SimulationRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                int v = r.NextInt(1, 5); // [1, 5]
                Assert.IsTrue(v >= 1 && v <= 5, $"out of range: {v}");
            }
        }

        [Test]
        public void Different_Seeds_Differ()
        {
            var a = new SimulationRandom(1);
            var b = new SimulationRandom(2);
            int diffs = 0;
            for (int i = 0; i < 64; i++)
                if (a.NextUInt64() != b.NextUInt64()) diffs++;
            Assert.IsTrue(diffs >= 60, "different seeds should mostly diverge");
        }

        [Test]
        public void Reset_Restores_Sequence()
        {
            var r = new SimulationRandom(99);
            ulong first1 = r.NextUInt64();
            ulong first2 = r.NextUInt64();
            r.Reset();
            Assert.AreEqual(first1, r.NextUInt64());
            Assert.AreEqual(first2, r.NextUInt64());
        }
    }
}