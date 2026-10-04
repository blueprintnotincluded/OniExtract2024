using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniExtract2024.building;
using Xunit;

namespace OniExtract2024.Tests
{
    public class ModelFieldTests
    {
        // Mirrors BBuildingEntity's uiImageRect field exactly, so we can validate the
        // omit-when-null / emit-when-set serialization contract against the real export
        // settings without constructing a KPrefabID (Unity type the test host can't make).
        private class RectProbe
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public UiImageRect? uiImageRect = null;
        }

        [Fact]
        public void UiImageRect_OmittedWhenNull_WithExportSettings()
        {
            var json = JsonConvert.SerializeObject(new RectProbe(), BaseExport.BuildSerializerSettings());
            var j = JObject.Parse(json);
            Assert.Null(j["uiImageRect"]); // never emit null — that misleads the website
        }

        [Fact]
        public void UiImageRect_EmittedAsXYWH_WhenSet()
        {
            var probe = new RectProbe
            {
                uiImageRect = new UiImageRect { x = 0f, y = -1.24f, w = 5f, h = 4.24f },
            };
            var j = JObject.Parse(JsonConvert.SerializeObject(probe, BaseExport.BuildSerializerSettings()));
            var r = (JObject)j["uiImageRect"];
            Assert.NotNull(r);
            Assert.Equal(0f, r["x"].Value<float>());
            Assert.Equal(-1.24f, r["y"].Value<float>());
            Assert.Equal(5f, r["w"].Value<float>());
            Assert.Equal(4.24f, r["h"].Value<float>());
        }

        // Mirrors BBuildingEntity's deprecated field. Plain bool, no NullValueHandling: it
        // must be emitted on every building, including when false, because the website
        // distinguishes "present and false" from "absent" -- an export taken before this
        // field existed has to keep converting, and absent has to read as not-deprecated.
        private class DeprecatedProbe
        {
            public bool deprecated;
            public bool debugOnly;
        }

        [Fact]
        public void Deprecated_IsEmittedEvenWhenFalse()
        {
            var json = JsonConvert.SerializeObject(new DeprecatedProbe(), BaseExport.BuildSerializerSettings());
            var j = JObject.Parse(json);
            Assert.NotNull(j["deprecated"]);
            Assert.False(j["deprecated"].Value<bool>());
        }

        [Fact]
        public void Deprecated_IsEmittedWhenTrue()
        {
            var probe = new DeprecatedProbe { deprecated = true };
            var j = JObject.Parse(JsonConvert.SerializeObject(probe, BaseExport.BuildSerializerSettings()));
            Assert.True(j["deprecated"].Value<bool>());
        }

        [Fact]
        public void DebugOnly_IsEmittedAlongsideDeprecated()
        {
            // Separate flags on purpose: BuildingDef gates on
            // `!Deprecated && (!DebugOnly || Game.Instance.DebugOnlyBuildingsAllowed)`, so a
            // DebugOnly building is reachable in a debug build menu where a deprecated one
            // never is. Collapsing them into one "hidden" bool would lose that.
            var probe = new DeprecatedProbe { debugOnly = true };
            var j = JObject.Parse(JsonConvert.SerializeObject(probe, BaseExport.BuildSerializerSettings()));
            Assert.True(j["debugOnly"].Value<bool>());
            Assert.False(j["deprecated"].Value<bool>());
        }

        // Mirrors BBuildingEntity's replacement fields exactly.
        private class ReplacementProbe
        {
            public bool replaceable;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? tileLayer = null;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? replacementLayer = null;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<int> replacementCandidateLayers = null;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<string> replacementTags = null;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<string> runtimeTags = null;
        }

        [Fact]
        public void Replacement_UnsetFieldsAreOmitted_ReplaceableAlwaysEmitted()
        {
            // An ordinary building (ReplacementLayer == NumLayers, null lists) adds one key.
            // replaceable stays even when false: that is the value that carries information.
            var j = JObject.Parse(JsonConvert.SerializeObject(new ReplacementProbe(), BaseExport.BuildSerializerSettings()));
            Assert.Equal(new[] { "replaceable" }, j.Properties().Select(p => p.Name).ToArray());
            Assert.False(j["replaceable"].Value<bool>());
        }

        [Fact]
        public void Replacement_LayersAreInts_TagsAreNames()
        {
            // The Tile values. Layers must be plain ints so they compare with objectLayer;
            // tags plain names, unlike the {Name, IsValid} objects in `tags`.
            var probe = new ReplacementProbe
            {
                replaceable = true,
                tileLayer = 9,
                replacementLayer = 11,
                replacementCandidateLayers = new List<int> { 9, 24, 2 },
                replacementTags = new List<string> { "FloorTiles", "Ladders", "Backwall" },
                runtimeTags = new List<string> { "Ladders" },
            };
            var j = JObject.Parse(JsonConvert.SerializeObject(probe, BaseExport.BuildSerializerSettings()));
            Assert.Equal(JTokenType.Integer, j["tileLayer"].Type);
            Assert.Equal(11, j["replacementLayer"].Value<int>());
            Assert.Equal(new[] { 9, 24, 2 }, j["replacementCandidateLayers"].Values<int>().ToArray());
            Assert.Equal(new[] { "FloorTiles", "Ladders", "Backwall" }, j["replacementTags"].Values<string>().ToArray());
            Assert.Equal(new[] { "Ladders" }, j["runtimeTags"].Values<string>().ToArray());
        }

        [Fact]
        public void ObjectLayerNames_AreIndexedByEnumValue()
        {
            // The root objectLayerNames array is Enum.GetNames(typeof(ObjectLayer)) and is
            // documented as "index == layer int". That holds only while the game's enum stays
            // contiguous from 0 with NumLayers last.
            string[] names = System.Enum.GetNames(typeof(ObjectLayer));
            for (int i = 0; i < names.Length; i++)
                Assert.Equal(((ObjectLayer)i).ToString(), names[i]);
            Assert.Equal("NumLayers", names[names.Length - 1]);
            Assert.Equal((int)ObjectLayer.NumLayers, names.Length - 1);
        }

        [Fact]
        public void BVector2_SerializesToXY()
        {
            var v = new BVector2(1.5f, 2.5f);
            var j = JObject.Parse(JsonConvert.SerializeObject(v));
            Assert.Equal(1.5f, j["x"].Value<float>());
            Assert.Equal(2.5f, j["y"].Value<float>());
        }

        [Fact]
        public void BColor_SerializesToRGBA()
        {
            var c = new BColor(0.25f, 0.5f, 0.75f, 1.0f);
            var j = JObject.Parse(JsonConvert.SerializeObject(c));
            Assert.Equal(0.25f, j["r"].Value<float>());
            Assert.Equal(0.5f, j["g"].Value<float>());
            Assert.Equal(0.75f, j["b"].Value<float>());
            Assert.Equal(1.0f, j["a"].Value<float>());
        }
    }
}
