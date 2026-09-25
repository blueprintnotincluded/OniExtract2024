using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OniExtract2024.Tests
{
    // Pure-logic coverage for the rocketry export (RocketModuleBuilder) and the JSON contract
    // of the new building.json fields under the real export settings. Game types used here
    // (CellOffset, SelectModuleCondition subclasses, RocketModulePerformance) are plain C# and
    // load in the net48 test host. BuildingAttachPoint.HardPoint is NOT usable here: it
    // references AttachableBuilding (a KMonoBehaviour), and loading that hierarchy off-game
    // throws TypeLoadException ("Non-abstract, non-.cctor method in an interface"), so
    // AttachPointsFrom is covered by the in-game export instead.
    public class RocketModuleBuilderTests
    {
        // ── attachPoints ───────────────────────────────────────────────────────

        [Fact]
        public void LaunchPadAttachPoints_IsOneRocketHardpointAtBaseModulePosition()
        {
            var result = RocketModuleBuilder.LaunchPadAttachPoints(RocketModuleBuilder.DefaultLaunchPadBasePosition);
            Assert.Single(result);
            Assert.Equal(0f, result[0].offset.x);
            Assert.Equal(2f, result[0].offset.y); // LaunchPad.baseModulePosition initialiser
            Assert.Equal("Rocket", result[0].tag);
        }

        [Fact]
        public void OutAttachPoint_SerializesAsOffsetAndTag()
        {
            var ap = new OutAttachPoint(new CellOffset(0, 5), "Rocket");
            var j = JObject.Parse(JsonConvert.SerializeObject(ap, BaseExport.BuildSerializerSettings()));
            Assert.Equal(0f, j["offset"]["x"].Value<float>());
            Assert.Equal(5f, j["offset"]["y"].Value<float>());
            Assert.Equal("Rocket", j["tag"].Value<string>());
            Assert.Equal(2, j.Properties().Count()); // nothing else leaks (no attachedBuilding)
        }

        // ── moduleBuildConditions ──────────────────────────────────────────────

        [Fact]
        public void ConditionNames_UsesTypeNames_PreservesOrder_DropsDuplicatesAndNulls()
        {
            var conditions = new List<SelectModuleCondition>
            {
                new ResearchCompleted(),
                new RocketHeightLimit(),
                null,
                new TopOnly(),
                new TopOnly(), // HabitatModuleSmall adds TopOnly after LimitOneCommandModule; guard dupes
                new LimitOneCommandModule(),
            };
            var names = RocketModuleBuilder.ConditionNames(conditions);
            Assert.Equal(
                new[] { "ResearchCompleted", "RocketHeightLimit", "TopOnly", "LimitOneCommandModule" },
                names);
        }

        // ── rocketModuleMenu ───────────────────────────────────────────────────

        [Fact]
        public void OrderModuleMenu_KeepsGameOrder_SkipsMissing_AppendsUnknownSorted()
        {
            var gameOrder = new[] { "CO2Engine", "SugarEngine", "HabitatModuleSmall", "NoseconeBasic", "ScannerModule" };
            // SugarEngine absent from this export (e.g. DLC off); two modded modules unknown to the game list.
            var available = new[] { "NoseconeBasic", "ZModdedTank", "CO2Engine", "AModdedBay", "HabitatModuleSmall" };
            var menu = RocketModuleBuilder.OrderModuleMenu(gameOrder, available);
            Assert.Equal(
                new[] { "CO2Engine", "HabitatModuleSmall", "NoseconeBasic", "AModdedBay", "ZModdedTank" },
                menu);
        }

        [Fact]
        public void OrderModuleMenu_NoAvailable_IsEmpty()
        {
            Assert.Empty(RocketModuleBuilder.OrderModuleMenu(new[] { "CO2Engine" }, new string[0]));
        }

        [Fact]
        public void OrderModuleMenu_DoesNotDuplicateIdsListedTwiceByGame()
        {
            var menu = RocketModuleBuilder.OrderModuleMenu(new[] { "CO2Engine", "CO2Engine" }, new[] { "CO2Engine" });
            Assert.Equal(new[] { "CO2Engine" }, menu);
        }

        // ── rocketModulePerformance ────────────────────────────────────────────

        [Fact]
        public void RocketModulePerformance_CopiesBurdenPowerAndFuel()
        {
            // KeroseneEngineCluster: BURDEN.MAJOR, ENGINE_POWER.MID_VERY_STRONG, FUEL_COST.VERY_HIGH
            var stats = new RocketModulePerformance(burden: 8f, fuelKilogramPerDistance: 1.6f, enginePower: 96f);
            var j = JObject.Parse(JsonConvert.SerializeObject(new OutRocketModulePerformance(stats), BaseExport.BuildSerializerSettings()));
            Assert.Equal(8f, j["burden"].Value<float>());
            Assert.Equal(96f, j["enginePower"].Value<float>());
            Assert.Equal(1.6f, j["fuelKilogramPerDistance"].Value<float>());
        }

        // ── omit-when-absent contract ──────────────────────────────────────────
        // Mirrors the attributes on BBuildingEntity's new fields exactly (BBuildingEntity itself
        // needs a KPrefabID, a Unity type the test host cannot construct).

        private class RocketryProbe
        {
            public bool showInBuildMenu;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool isRocketModule;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string attachableTo = null;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public BVector2 attachablePosition = null;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<OutAttachPoint> attachPoints = null;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public OutRocketModulePerformance rocketModulePerformance = null;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<string> moduleBuildConditions = null;
        }

        [Fact]
        public void OrdinaryBuilding_EmitsOnlyShowInBuildMenu()
        {
            var j = JObject.Parse(JsonConvert.SerializeObject(new RocketryProbe { showInBuildMenu = true }, BaseExport.BuildSerializerSettings()));
            Assert.True(j["showInBuildMenu"].Value<bool>()); // always present, like deprecated/debugOnly
            Assert.Null(j["isRocketModule"]);                // false -> omitted, never emitted as false
            Assert.Null(j["attachableTo"]);
            Assert.Null(j["attachablePosition"]);
            Assert.Null(j["attachPoints"]);
            Assert.Null(j["rocketModulePerformance"]);
            Assert.Null(j["moduleBuildConditions"]);
        }

        [Fact]
        public void RocketModule_EmitsFullRocketryBlock()
        {
            var probe = new RocketryProbe
            {
                showInBuildMenu = false,
                isRocketModule = true,
                attachableTo = "Rocket",
                attachablePosition = new BVector2(new CellOffset(0, 0)),
                // KeroseneEngineCluster: 7x5 engine, hardpoint at (0,5) (KeroseneEngineClusterConfig).
                attachPoints = new List<OutAttachPoint> { new OutAttachPoint(new CellOffset(0, 5), "Rocket") },
                rocketModulePerformance = new OutRocketModulePerformance(new RocketModulePerformance(8f, 1.6f, 96f)),
                moduleBuildConditions = new List<string> { "LimitOneEngine", "EngineOnBottom" },
            };
            var j = JObject.Parse(JsonConvert.SerializeObject(probe, BaseExport.BuildSerializerSettings()));
            Assert.False(j["showInBuildMenu"].Value<bool>());
            Assert.True(j["isRocketModule"].Value<bool>());
            Assert.Equal("Rocket", j["attachableTo"].Value<string>());
            Assert.Equal(0f, j["attachablePosition"]["x"].Value<float>());
            Assert.Equal(5f, j["attachPoints"][0]["offset"]["y"].Value<float>());
            Assert.Equal("Rocket", j["attachPoints"][0]["tag"].Value<string>());
            Assert.Equal(8f, j["rocketModulePerformance"]["burden"].Value<float>());
            Assert.Equal(new[] { "LimitOneEngine", "EngineOnBottom" }, j["moduleBuildConditions"].Values<string>().ToArray());
        }
    }
}
