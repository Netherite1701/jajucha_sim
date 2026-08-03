using NUnit.Framework;

namespace JajuchaSim.Course.Tests
{
    public class GridRegionTests
    {
        [Test]
        public void Constructor_SetsProperties()
        {
            var region = new GridRegion(5, 10, 3, 4);
            Assert.AreEqual(5, region.x);
            Assert.AreEqual(10, region.z);
            Assert.AreEqual(3, region.width);
            Assert.AreEqual(4, region.height);
        }

        [Test]
        public void Constructor_ClampsNegativeToZero()
        {
            var region = new GridRegion(0, 0, -1, -5);
            Assert.AreEqual(0, region.width);
            Assert.AreEqual(0, region.height);
        }

        [Test]
        public void IsValid_ValidRegion_ReturnsTrue()
        {
            var region = new GridRegion(0, 0, 1, 1);
            Assert.IsTrue(region.IsValid);
        }

        [Test]
        public void IsValid_ZeroWidth_ReturnsFalse()
        {
            var region = new GridRegion(0, 0, 0, 1);
            Assert.IsFalse(region.IsValid);
        }

        [Test]
        public void IsValid_ZeroHeight_ReturnsFalse()
        {
            var region = new GridRegion(0, 0, 1, 0);
            Assert.IsFalse(region.IsValid);
        }

        [Test]
        public void TileCount_ReturnsProduct()
        {
            var region = new GridRegion(0, 0, 4, 8);
            Assert.AreEqual(32, region.TileCount);
        }

        [Test]
        public void ToCoordinates_EnumeratesAllTiles()
        {
            var region = new GridRegion(1, 2, 2, 3);
            var coords = region.ToCoordinates();

            Assert.AreEqual(6, coords.Length);
            Assert.Contains(new GridCoordinate(1, 2), coords);
            Assert.Contains(new GridCoordinate(2, 2), coords);
            Assert.Contains(new GridCoordinate(1, 3), coords);
            Assert.Contains(new GridCoordinate(2, 3), coords);
            Assert.Contains(new GridCoordinate(1, 4), coords);
            Assert.Contains(new GridCoordinate(2, 4), coords);
        }

        [Test]
        public void ToCoordinates_SingleTile()
        {
            var region = new GridRegion(5, 5, 1, 1);
            var coords = region.ToCoordinates();
            Assert.AreEqual(1, coords.Length);
            Assert.AreEqual(new GridCoordinate(5, 5), coords[0]);
        }

        [Test]
        public void Contains_Inside_ReturnsTrue()
        {
            var region = new GridRegion(2, 3, 5, 5);
            Assert.IsTrue(region.Contains(new GridCoordinate(4, 5)));
            Assert.IsTrue(region.Contains(new GridCoordinate(2, 3)));
            Assert.IsTrue(region.Contains(new GridCoordinate(6, 7)));
        }

        [Test]
        public void Contains_Outside_ReturnsFalse()
        {
            var region = new GridRegion(2, 3, 5, 5);
            Assert.IsFalse(region.Contains(new GridCoordinate(1, 3)));  // left
            Assert.IsFalse(region.Contains(new GridCoordinate(7, 3)));  // right (2+5=7, so 7 is exclusive)
            Assert.IsFalse(region.Contains(new GridCoordinate(4, 2)));  // above
            Assert.IsFalse(region.Contains(new GridCoordinate(4, 8)));  // below (3+5=8, so 8 is exclusive)
        }

        [Test]
        public void Overlaps_Overlapping_ReturnsTrue()
        {
            var a = new GridRegion(0, 0, 5, 5);
            var b = new GridRegion(3, 3, 5, 5);
            Assert.IsTrue(a.Overlaps(b));
            Assert.IsTrue(b.Overlaps(a));
        }

        [Test]
        public void Overlaps_NonOverlapping_ReturnsFalse()
        {
            var a = new GridRegion(0, 0, 5, 5);
            var b = new GridRegion(10, 10, 5, 5);
            Assert.IsFalse(a.Overlaps(b));
        }

        [Test]
        public void Overlaps_Adjacent_ReturnsFalse()
        {
            // Adjacent edges (touching but not overlapping)
            var a = new GridRegion(0, 0, 5, 5);
            var b = new GridRegion(5, 0, 5, 5); // right edge of a == left edge of b
            Assert.IsFalse(a.Overlaps(b));
        }

        [Test]
        public void TileWidthCm_ComputesCorrectly()
        {
            var region = new GridRegion(0, 0, 4, 8);
            Assert.AreEqual(80, region.TileWidthCm(20f));
            Assert.AreEqual(40, region.TileWidthCm(10f));
        }

        [Test]
        public void TileHeightCm_ComputesCorrectly()
        {
            var region = new GridRegion(0, 0, 4, 8);
            Assert.AreEqual(160, region.TileHeightCm(20f));
            Assert.AreEqual(80, region.TileHeightCm(10f));
        }

        [Test]
        public void Left_Right_Near_Far_Correct()
        {
            var region = new GridRegion(2, 3, 4, 5);
            Assert.AreEqual(2, region.Left);
            Assert.AreEqual(5, region.Right);  // 2 + 4 - 1 = 5
            Assert.AreEqual(3, region.Near);
            Assert.AreEqual(7, region.Far);    // 3 + 5 - 1 = 7
        }

        [Test]
        public void ToString_ContainsInfo()
        {
            var region = new GridRegion(1, 2, 3, 4);
            var str = region.ToString();
            Assert.IsTrue(str.Contains("1"));
            Assert.IsTrue(str.Contains("2"));
            Assert.IsTrue(str.Contains("3x4"));
        }
    }
}
