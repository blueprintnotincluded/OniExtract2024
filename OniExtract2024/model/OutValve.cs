using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OniExtract2024
{
    // Flow valve (ValveBase + Valve): GasValve / LiquidValve and modded equivalents. The
    // blueprint `buildingData.Valve.DesiredFlow` setting is a kg/s value in [0, maxFlow].
    public class OutValve
    {
        // ConduitType enum name: "Gas" | "Liquid".
        [JsonConverter(typeof(StringEnumConverter))]
        public ConduitType conduitType;
        // Upper bound of the flow slider, kg/s (ValveBase.maxFlow).
        public float maxFlow;

        public OutValve(ValveBase valveBase)
        {
            this.conduitType = valveBase.conduitType;
            this.maxFlow = valveBase.maxFlow;
        }
    }
}
