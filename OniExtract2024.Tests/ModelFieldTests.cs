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
