using NUnit.Framework;
using UnityEngine.TestTools;

namespace JajuchaSim.Course.Tests
{
    public class CourseSerializerJsonTests
    {
        [Test]
        public void ToJson_RoundTrip_PreservesData()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(10, 10));
            grid.SetRoad(new GridCoordinate(10, 11));
            grid.SetRoad(new GridCoordinate(10, 12));
            grid.SetStructure(new GridCoordinate(10, 11), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(10, 12), StructureType.Tunnel);
            grid.SetObject(new GridCoordinate(9, 11), ObjectType.Sign);
            grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SlowZone);

            string json = CourseSerializer.ToJson(grid, true);
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Length > 0);

            var grid2 = CourseSerializer.FromJson(json);
            Assert.IsNotNull(grid2);
            Assert.AreEqual(20f, grid2.TileSizeCm, 1e-6f);

            Assert.IsTrue(grid2.HasRoad(new GridCoordinate(10, 10)));
            Assert.IsTrue(grid2.HasRoad(new GridCoordinate(10, 11)));
            Assert.IsTrue(grid2.HasRoad(new GridCoordinate(10, 12)));
            Assert.AreEqual(3, grid2.RoadTileCount);

            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(10, 11)));
            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(10, 12)));

            Assert.AreEqual(ObjectType.Sign, grid2.GetObject(new GridCoordinate(9, 11)));

            Assert.AreEqual(TriggerType.SlowZone, grid2.GetTrigger(new GridCoordinate(5, 5)));
        }

        [Test]
        public void ToJson_Minified()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));

            string pretty = CourseSerializer.ToJson(grid, true);
            string minified = CourseSerializer.ToJson(grid, false);

            Assert.IsTrue(pretty.Length >= minified.Length);
        }

        [Test]
        public void FromJson_NullJson_ReturnsNull()
        {
            var grid = CourseSerializer.FromJson(null);
            Assert.IsNull(grid);
        }

        [Test]
        public void FromJson_EmptyString_ReturnsNull()
        {
            var grid = CourseSerializer.FromJson("");
            Assert.IsNull(grid);
        }

        [Test]
        public void FromJson_InvalidJson_ReturnsNull()
        {
            LogAssert.Expect(UnityEngine.LogType.Error, "[CourseSerializer] Failed to parse course JSON: JSON parse error: Invalid value.");
            var grid = CourseSerializer.FromJson("this is not json");
            Assert.IsNull(grid);
        }

        [Test]
        public void ToJson_EmptyGrid()
        {
            var grid = new CourseGrid(20f);
            string json = CourseSerializer.ToJson(grid, false);
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Contains("tileSizeCm"));
            Assert.IsTrue(json.Contains("road"));

            var grid2 = CourseSerializer.FromJson(json);
            Assert.IsNotNull(grid2);
            Assert.AreEqual(0, grid2.RoadTileCount);
            Assert.AreEqual(0, grid2.StructureTileCount);
            Assert.AreEqual(0, grid2.ObjectTileCount);
            Assert.AreEqual(0, grid2.TriggerTileCount);
        }

        [Test]
        public void ToJson_ContainsExpectedFields()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(10, 10));
            grid.SetRoad(new GridCoordinate(10, 11));
            grid.SetRoad(new GridCoordinate(10, 12));
            grid.SetStructure(new GridCoordinate(10, 11), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(10, 12), StructureType.Tunnel);
            grid.SetObject(new GridCoordinate(9, 11), ObjectType.Sign);

            string json = CourseSerializer.ToJson(grid, false);

            Assert.IsTrue(json.Contains("\"tileSizeCm\":20"));
            Assert.IsTrue(json.Contains("\"road\""));
            Assert.IsTrue(json.Contains("\"structures\""));
            Assert.IsTrue(json.Contains("\"objects\""));
            Assert.IsTrue(json.Contains("\"triggers\""));
        }

        [Test]
        public void FromJson_HandlesAllEnumNames()
        {
            var grid = new CourseGrid(20f);
            grid.SetStructure(new GridCoordinate(0, 0), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(1, 1), StructureType.Ramp);
            grid.SetObject(new GridCoordinate(2, 2), ObjectType.Obstacle);
            grid.SetObject(new GridCoordinate(3, 3), ObjectType.Sign);
            grid.SetObject(new GridCoordinate(4, 4), ObjectType.StartSignal);
            grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(6, 6), TriggerType.SpeedGate);
            grid.SetTrigger(new GridCoordinate(7, 7), TriggerType.EventTrigger);

            string json = CourseSerializer.ToJson(grid, false);
            var grid2 = CourseSerializer.FromJson(json);
            Assert.IsNotNull(grid2);

            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.Ramp, grid2.GetStructure(new GridCoordinate(1, 1)));

            Assert.AreEqual(ObjectType.Obstacle, grid2.GetObject(new GridCoordinate(2, 2)));
            Assert.AreEqual(ObjectType.Sign, grid2.GetObject(new GridCoordinate(3, 3)));
            Assert.AreEqual(ObjectType.StartSignal, grid2.GetObject(new GridCoordinate(4, 4)));

            Assert.AreEqual(TriggerType.SlowZone, grid2.GetTrigger(new GridCoordinate(5, 5)));
            Assert.AreEqual(TriggerType.SpeedGate, grid2.GetTrigger(new GridCoordinate(6, 6)));
            Assert.AreEqual(TriggerType.EventTrigger, grid2.GetTrigger(new GridCoordinate(7, 7)));
        }

        [Test]
        public void FromJson_ParsesAllEnumFormats()
        {
            var grid = new CourseGrid(20f);
            grid.SetStructure(new GridCoordinate(0, 0), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(1, 0), StructureType.Ramp);
            grid.SetObject(new GridCoordinate(2, 0), ObjectType.Obstacle);
            grid.SetObject(new GridCoordinate(3, 0), ObjectType.Sign);
            grid.SetObject(new GridCoordinate(4, 0), ObjectType.StartSignal);
            grid.SetTrigger(new GridCoordinate(5, 0), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(6, 0), TriggerType.SpeedGate);
            grid.SetTrigger(new GridCoordinate(7, 0), TriggerType.EventTrigger);

            var data = CourseSerializer.ToData(grid);

            Assert.AreEqual("tunnel", data.structures[0].type);
            Assert.AreEqual("ramp", data.structures[1].type);
            Assert.AreEqual("obstacle", data.objects[0].type);
            Assert.AreEqual("sign", data.objects[1].type);
            Assert.AreEqual("startsignal", data.objects[2].type);
            Assert.AreEqual("slowzone", data.triggers[0].type);
            Assert.AreEqual("speed_terminal", data.triggers[1].type);
            Assert.AreEqual("eventtrigger", data.triggers[2].type);

            var grid2 = CourseSerializer.ToGrid(data);
            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.Ramp, grid2.GetStructure(new GridCoordinate(1, 0)));
            Assert.AreEqual(ObjectType.Obstacle, grid2.GetObject(new GridCoordinate(2, 0)));
            Assert.AreEqual(ObjectType.Sign, grid2.GetObject(new GridCoordinate(3, 0)));
            Assert.AreEqual(ObjectType.StartSignal, grid2.GetObject(new GridCoordinate(4, 0)));
            Assert.AreEqual(TriggerType.SlowZone, grid2.GetTrigger(new GridCoordinate(5, 0)));
            Assert.AreEqual(TriggerType.SpeedGate, grid2.GetTrigger(new GridCoordinate(6, 0)));
            Assert.AreEqual(TriggerType.EventTrigger, grid2.GetTrigger(new GridCoordinate(7, 0)));
        }

        [Test]
        public void MultipleRoundTrips_PreserveData()
        {
            var grid1 = new CourseGrid(20f);
            grid1.SetRoad(new GridCoordinate(0, 0));
            grid1.SetStructure(new GridCoordinate(0, 0), StructureType.Tunnel);
            grid1.SetObject(new GridCoordinate(1, 1), ObjectType.Sign);
            grid1.SetTrigger(new GridCoordinate(2, 2), TriggerType.SlowZone);

            var json1 = CourseSerializer.ToJson(grid1, false);
            var grid2 = CourseSerializer.FromJson(json1);

            var json2 = CourseSerializer.ToJson(grid2, false);
            var grid3 = CourseSerializer.FromJson(json2);

            var json3 = CourseSerializer.ToJson(grid3, false);
            var grid4 = CourseSerializer.FromJson(json3);

            Assert.IsTrue(grid4.HasRoad(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.Tunnel, grid4.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(ObjectType.Sign, grid4.GetObject(new GridCoordinate(1, 1)));
            Assert.AreEqual(TriggerType.SlowZone, grid4.GetTrigger(new GridCoordinate(2, 2)));
        }

        [Test]
        public void FromJson_ParsesAlternativeEnumFormats()
        {
            var json = @"{
                ""tileSizeCm"": 20,
                ""road"": [],
                ""structures"": [],
                ""objects"": [
                    { ""type"": ""start_signal"", ""tile"": { ""x"": 0, ""z"": 0 } }
                ],
                ""triggers"": [
                    { ""type"": ""slow_zone"", ""region"": { ""x"": 1, ""z"": 1, ""width"": 1, ""height"": 1 } },
                    { ""type"": ""speed_gate"", ""cellX"": 2, ""cellZ"": 2, ""edge"": ""north"" },
                    { ""type"": ""event_trigger"", ""region"": { ""x"": 3, ""z"": 3, ""width"": 1, ""height"": 1 } }
                ]
            }";

            var grid = CourseSerializer.FromJson(json);
            Assert.IsNotNull(grid);
            Assert.AreEqual(ObjectType.StartSignal, grid.GetObject(new GridCoordinate(0, 0)));
            Assert.AreEqual(TriggerType.SlowZone, grid.GetTrigger(new GridCoordinate(1, 1)));
            Assert.AreEqual(TriggerType.SpeedGate, grid.GetTrigger(new GridCoordinate(2, 2)));
            Assert.AreEqual(TriggerType.EventTrigger, grid.GetTrigger(new GridCoordinate(3, 3)));
        }

        [Test]
        public void ToGrid_NullTypeString_DefaultsToNone()
        {
            var data = new CourseData
            {
                tileSizeCm = 20f,
                structures = new[]
                {
                    new CourseData.StructureEntry
                    {
                        type = null,
                        region = new GridRegion(0, 0, 1, 1)
                    }
                },
                objects = new[]
                {
                    new CourseData.ObjectEntry
                    {
                        type = null,
                        tile = new CoordPair(1, 1)
                    }
                },
                triggers = new[]
                {
                    new CourseData.TriggerEntry
                    {
                        type = null,
                        region = new GridRegion(2, 2, 1, 1)
                    }
                }
            };

            var grid = CourseSerializer.ToGrid(data);
            Assert.AreEqual(StructureType.None, grid.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(ObjectType.None, grid.GetObject(new GridCoordinate(1, 1)));
            Assert.AreEqual(TriggerType.None, grid.GetTrigger(new GridCoordinate(2, 2)));
        }

        [Test]
        public void ToGrid_NullObjectTile_HandledGracefully()
        {
            var data = new CourseData
            {
                tileSizeCm = 20f,
                objects = new[]
                {
                    new CourseData.ObjectEntry
                    {
                        type = "obstacle",
                        tile = null
                    }
                }
            };

            var grid = CourseSerializer.ToGrid(data);
            Assert.IsNotNull(grid);
            Assert.AreEqual(0, grid.ObjectTileCount);
        }

        [Test]
        public void LegacyTileArrays_AreIgnored()
        {
            // Old format with "tiles" arrays instead of "region"
            var json = @"{
                ""tileSizeCm"": 20,
                ""road"": [{""x"":0,""z"":0}],
                ""structures"": [{""type"":""tunnel"",""tiles"":[{""x"":0,""z"":0}]}],
                ""objects"": [{""type"":""obstacle"",""tile"":{""x"":1,""z"":1}}],
                ""triggers"": [{""type"":""slow_zone"",""tiles"":[{""x"":2,""z"":2}]}]
            }";

            var grid = CourseSerializer.FromJson(json);
            Assert.IsNotNull(grid);
            Assert.IsTrue(grid.HasRoad(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.None, grid.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(TriggerType.None, grid.GetTrigger(new GridCoordinate(2, 2)));
        }

        [Test]
        public void NewFormat_LoadsSuccessfully()
        {
            var json = @"{
                ""tileSizeCm"": 20,
                ""road"": [{""x"":0,""z"":0}],
                ""structures"": [
                    {
                        ""id"": ""tunnel_001"",
                        ""type"": ""tunnel"",
                        ""region"": {""x"":0,""z"":0,""width"":1,""height"":1},
                        ""heightCm"": 55
                    }
                ],
                ""objects"": [
                    {
                        ""id"": ""sign_001"",
                        ""type"": ""slow_sign"",
                        ""tile"": {""x"":0,""z"":0},
                        ""rotationDeg"": 90
                    }
                ],
                ""triggers"": [
                    {
                        ""id"": ""sz_001"",
                        ""type"": ""slow_zone"",
                        ""region"": {""x"":2,""z"":2,""width"":1,""height"":1}
                    }
                ]
            }";

            var grid = CourseSerializer.FromJson(json);
            Assert.IsNotNull(grid);
            Assert.IsTrue(grid.HasRoad(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(ObjectType.Sign, grid.GetObject(new GridCoordinate(0, 0)));
            Assert.AreEqual(TriggerType.SlowZone, grid.GetTrigger(new GridCoordinate(2, 2)));
        }

        [Test]
        public void ComprehensiveSaveLoad_RoundTrip_PreservesAllData()
        {
            // Plan section 7.54: Create 1 tunnel, 1 ramp, 2 obstacles, 1 sign, 2 triggers.
            // Save. Reload. Expected exact semantic match.
            var grid = new CourseGrid(20f);

            // Road
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetRoad(new GridCoordinate(1, 0));
            grid.SetRoad(new GridCoordinate(0, 1));
            grid.SetRoad(new GridCoordinate(1, 1));
            grid.SetRoad(new GridCoordinate(2, 0));
            grid.SetRoad(new GridCoordinate(2, 1));
            grid.SetRoad(new GridCoordinate(0, 2));
            grid.SetRoad(new GridCoordinate(1, 2));
            grid.SetRoad(new GridCoordinate(2, 2));

            // 1 tunnel (4 tiles)
            grid.SetStructure(new GridCoordinate(0, 0), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(1, 0), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(0, 1), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(1, 1), StructureType.Tunnel);

            // 1 ramp (2 tiles)
            grid.SetStructure(new GridCoordinate(2, 0), StructureType.Ramp);
            grid.SetStructure(new GridCoordinate(2, 1), StructureType.Ramp);

            // 2 obstacles
            grid.SetObject(new GridCoordinate(0, 2), ObjectType.Obstacle);
            grid.SetObject(new GridCoordinate(1, 2), ObjectType.Obstacle);

            // 1 sign
            grid.SetObject(new GridCoordinate(2, 2), ObjectType.Sign);

            // 2 triggers (slow zone + speed gate)
            grid.SetTrigger(new GridCoordinate(0, 0), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(1, 0), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(5, 5), TriggerType.SpeedGate);

            // Save
            string json = CourseSerializer.ToJson(grid, false);
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Length > 0);

            // Reload
            var grid2 = CourseSerializer.FromJson(json);
            Assert.IsNotNull(grid2);

            // Verify road
            Assert.IsTrue(grid2.HasRoad(new GridCoordinate(0, 0)));
            Assert.IsTrue(grid2.HasRoad(new GridCoordinate(2, 2)));
            Assert.AreEqual(9, grid2.RoadTileCount);

            // Verify structures
            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(0, 1)));
            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(1, 0)));
            Assert.AreEqual(StructureType.Tunnel, grid2.GetStructure(new GridCoordinate(1, 1)));
            Assert.AreEqual(StructureType.Ramp, grid2.GetStructure(new GridCoordinate(2, 0)));
            Assert.AreEqual(StructureType.Ramp, grid2.GetStructure(new GridCoordinate(2, 1)));
            Assert.AreEqual(StructureType.None, grid2.GetStructure(new GridCoordinate(0, 2)));
            Assert.AreEqual(6, grid2.StructureTileCount);

            // Verify objects
            Assert.AreEqual(ObjectType.Obstacle, grid2.GetObject(new GridCoordinate(0, 2)));
            Assert.AreEqual(ObjectType.Obstacle, grid2.GetObject(new GridCoordinate(1, 2)));
            Assert.AreEqual(ObjectType.Sign, grid2.GetObject(new GridCoordinate(2, 2)));
            Assert.AreEqual(ObjectType.None, grid2.GetObject(new GridCoordinate(0, 0)));
            Assert.AreEqual(3, grid2.ObjectTileCount);

            // Verify triggers
            Assert.AreEqual(TriggerType.SlowZone, grid2.GetTrigger(new GridCoordinate(0, 0)));
            Assert.AreEqual(TriggerType.SlowZone, grid2.GetTrigger(new GridCoordinate(1, 0)));
            Assert.AreEqual(TriggerType.SpeedGate, grid2.GetTrigger(new GridCoordinate(5, 5)));
            Assert.AreEqual(TriggerType.None, grid2.GetTrigger(new GridCoordinate(2, 2)));
            Assert.AreEqual(3, grid2.TriggerTileCount);
        }

        [Test]
        public void JsonRoundTrip_MultipleTimes_PreservesSemantics()
        {
            var grid = new CourseGrid(20f);
            grid.SetRoad(new GridCoordinate(0, 0));
            grid.SetRoad(new GridCoordinate(1, 0));
            grid.SetStructure(new GridCoordinate(0, 0), StructureType.Tunnel);
            grid.SetStructure(new GridCoordinate(1, 0), StructureType.Tunnel);
            grid.SetObject(new GridCoordinate(0, 1), ObjectType.Obstacle);
            grid.SetTrigger(new GridCoordinate(2, 2), TriggerType.SlowZone);
            grid.SetTrigger(new GridCoordinate(3, 3), TriggerType.SpeedGate);

            // Multiple round-trips
            for (int i = 0; i < 5; i++)
            {
                string json = CourseSerializer.ToJson(grid, false);
                grid = CourseSerializer.FromJson(json);
                Assert.IsNotNull(grid);
            }

            // Final verification
            Assert.IsTrue(grid.HasRoad(new GridCoordinate(0, 0)));
            Assert.IsTrue(grid.HasRoad(new GridCoordinate(1, 0)));
            Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(new GridCoordinate(0, 0)));
            Assert.AreEqual(StructureType.Tunnel, grid.GetStructure(new GridCoordinate(1, 0)));
            Assert.AreEqual(ObjectType.Obstacle, grid.GetObject(new GridCoordinate(0, 1)));
            Assert.AreEqual(TriggerType.SlowZone, grid.GetTrigger(new GridCoordinate(2, 2)));
            Assert.AreEqual(TriggerType.SpeedGate, grid.GetTrigger(new GridCoordinate(3, 3)));
        }
    }
}
