using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OniExtract2024.Tests
{
    // JSON contract of the blueprint-setting range fields (BuildingSettingsBuilder output) under
    // the real export settings. The component readers themselves need live prefabs and are
    // verified by the in-game export; here we pin the shapes the website will parse.
    //
    // OutDoor / OutValve / OutLimitValve are deliberately absent from the probe: their
    // constructors take Door / ValveBase / LimitValve, and Newtonsoft's contract resolution
    // reflects over constructor parameters, which loads the KMonoBehaviour hierarchy and throws
    // TypeLoadException in the net48 test host (see GAME_INTERNALS.md, "Probing the assembly").
    public class BuildingSettingsTests
    {
        // Mirrors the attributes on BBuildingEntity's settings fields exactly.
        private class SettingsProbe
        {
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool prioritizable;
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool userNameable;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public OutUserControlledCapacity userControlledCapacity = null;
        }

        [Fact]
        public void NoSettings_EmitsNothing()
        {
            var j = JObject.Parse(JsonConvert.SerializeObject(new SettingsProbe(), BaseExport.BuildSerializerSettings()));
            Assert.Empty(j.Properties()); // a plain Wire/Tile adds zero keys to building.json
        }

        [Fact]
        public void PresenceFlags_EmittedOnlyWhenTrue()
        {
            var j = JObject.Parse(JsonConvert.SerializeObject(new SettingsProbe { prioritizable = true }, BaseExport.BuildSerializerSettings()));
            Assert.True(j["prioritizable"].Value<bool>());
            Assert.Null(j["userNameable"]);
        }

        [Fact]
        public void UserControlledCapacity_SerializesRangeUnitsAndSource()
        {
            var probe = new SettingsProbe
            {
                userControlledCapacity = new OutUserControlledCapacity
                {
                    minCapacity = 0f, maxCapacity = 20000f, wholeValues = false, units = "kg", source = "StorageLocker",
                },
            };
            var j = JObject.Parse(JsonConvert.SerializeObject(probe, BaseExport.BuildSerializerSettings()));
            var c = (JObject)j["userControlledCapacity"];
            Assert.Equal(0f, c["minCapacity"].Value<float>());
            Assert.Equal(20000f, c["maxCapacity"].Value<float>());
            Assert.False(c["wholeValues"].Value<bool>());
            Assert.Equal("kg", c["units"].Value<string>());
            Assert.Equal("StorageLocker", c["source"].Value<string>());
            Assert.Equal(
                new[] { "minCapacity", "maxCapacity", "wholeValues", "units", "source" },
                c.Properties().Select(p => p.Name).ToArray());
        }

        // Enum-typed fields must serialize as names, not ints: the website should not have to
        // track ONI's enum numbering across game updates. Checked on a stand-in that declares
        // the fields exactly as OutDoor / OutValve / OutLimitValve do (see class comment).
        [Fact]
        public void DoorType_AndConduitType_SerializeAsEnumNames()
        {
            var j = JObject.Parse(JsonConvert.SerializeObject(new EnumProbe
            {
                doorType = Door.DoorType.Sealed,
                conduitType = ConduitType.Liquid,
            }, BaseExport.BuildSerializerSettings()));
            Assert.Equal("Sealed", j["doorType"].Value<string>());
            Assert.Equal("Liquid", j["conduitType"].Value<string>());
        }

        private class EnumProbe
        {
            [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public Door.DoorType doorType;
            [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public ConduitType conduitType;
        }
    }
}
