using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OniExtract2024
{
    // Limit valve / meter valve (LimitValve): GasLimitValve, LiquidLimitValve, SolidLimitValve.
    // The blueprint `buildingData.LimitValve.Limit` setting is in [0, maxLimitKg], in kg (or
    // in item units when displayUnitsInsteadOfMass is true — the solid variant).
    public class OutLimitValve
    {
        // ConduitType enum name: "Gas" | "Liquid" | "Solid".
        [JsonConverter(typeof(StringEnumConverter))]
        public ConduitType conduitType;
        // Upper bound of the limit slider (LimitValve.maxLimitKg).
        public float maxLimitKg;
        // true → the UI shows the limit as a unit count rather than a mass.
        public bool displayUnitsInsteadOfMass;

        public OutLimitValve(LimitValve limitValve)
        {
            this.conduitType = limitValve.conduitType;
            this.maxLimitKg = limitValve.maxLimitKg;
            this.displayUnitsInsteadOfMass = limitValve.displayUnitsInsteadOfMass;
        }
    }
}
