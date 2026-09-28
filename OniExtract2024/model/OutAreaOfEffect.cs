using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace OniExtract2024
{
    // One entry per area-of-effect a building projects onto the cells around it:
    // light cast, gas/liquid intake reach, machine operating range, radiation, sky scans.
    // Built by AreaOfEffectBuilder from the BuildingComplete prefab. See AREA_OF_EFFECT.md
    // for the full contract, per-kind geometry semantics and worked examples.
    //
    // Offset convention: `origin` and `cells` use the same anchor as utilities[].offset —
    // cell offsets from the building's origin cell, pre-rotation. `cells` is the nominal
    // (unobstructed) affected area; solid tiles shrink it at runtime when blockedBySolids.
    //
    // Optional fields carry NullValueHandling.Ignore individually (the game's bundled
    // Newtonsoft.Json predates JsonObject.ItemNullValueHandling).
    public class OutAreaOfEffect
    {
        public string kind;         // "light" | "elementIntake" | "operationRange" | "radiation" | "skyScan"
        public string source;       // game component the entry came from, e.g. "Light2D"
        public string shape;        // "circle" | "cone" | "quad" | "diamond" | "rect" | "ellipse" | "ellipseArc" | "skyColumns"
        public BVector2 origin;     // cell the effect emanates from (utilities[] offset convention)
        public bool blockedBySolids;// true when solid tiles occlude/shrink the area at runtime

        // Nominal affected cells relative to the BUILDING origin cell (origin already applied).
        // Omitted for shapes the website derives from params (ellipse*, skyColumns) and for
        // oversized lists (> MaxCells safety cap, e.g. a modded extreme).
        [JsonConverter(typeof(CellPairListConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<BVector2> cells;

        // light (Light2D)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? range;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? lux;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? falloffRate;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public BColor lightColor;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? width;          // quad only
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string direction;    // quad only: North/East/South/West

        // elementIntake (ElementConsumer): raw consumptionRadius; cells span |dx|+|dy| <= radius-1
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? radius;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string element;      // element consumed, or "AllGas"/"AllLiquid"
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? consumptionRate;

        // operationRange (RangeVisualizer): rect relative to `origin`, inclusive
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public BVector2 rectMin;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public BVector2 rectMax;

        // radiation (RadiationEmitter): ellipse (dx/radiusX)^2 + (dy/radiusY)^2 <= 1 around origin
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? radiusX;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? radiusY;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? rads;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? arcAngle;     // only when a partial arc (< 360)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? arcDirection; // only when a partial arc; degrees, 0 = east, CCW
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string emitType;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? radiusScalesWithRads; // radius is recomputed from emitted rads at runtime

        // skyScan (ScannerNetworkVisualizer / SkyVisibilityVisualizer): columns
        // origin.x+scanMinX .. origin.x+scanMaxX, from origin.y up to the top of the world
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? scanMinX;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? scanMaxX;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? verticalStep;
    }

    // Serializes a cell list as one compact array of [x,y] integer pairs, e.g.
    // [[0,-1],[1,-1],[0,-2]] — Formatting.Indented would otherwise spend 4+ lines per cell.
    public class CellPairListConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(IEnumerable<BVector2>).IsAssignableFrom(objectType);
        }

        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException("CellPairListConverter is write-only");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (BVector2 cell in (IEnumerable<BVector2>)value)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append("[").Append((int)cell.x).Append(",").Append((int)cell.y).Append("]");
            }
            sb.Append("]");
            writer.WriteRawValue(sb.ToString());
        }
    }
}
