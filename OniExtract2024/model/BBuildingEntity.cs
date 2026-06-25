using System.Collections.Generic;
using Newtonsoft.Json;
using OniExtract2024.building;

namespace OniExtract2024
{
    public class BBuildingEntity
    {
        public string name;
        public string nameString;
        public BKprefabID kPrefabID;
        public HashSet<Tag> tags;

        // Rendered ui_image placement in footprint cells (see UiImageRect / the website
        // contract). Measured by the in-game building-image pass and carried here via the
        // UiImageRectStore sidecar so it survives a main-menu-only export. Omitted when we
        // have no measurement for this building — that means "image == footprint" to the
        // website (do NOT emit null). See UIIMAGERECT_DURABILITY.md.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public UiImageRect? uiImageRect = null;

        // BuildingDef display fields (missing from original 2024 export)
        public int widthInCells;
        public int heightInCells;
        public string[] materialCategory;
        public float[] materialMass;
        public bool isFoundation;
        public bool isKAnimTile;
        public bool isUtility;
        public bool dragBuild;
        public int buildLocationRule;
        public int permittedRotations;
        public int sceneLayer;
        public int objectLayer;
        public string viewMode;
        public string defaultAnimState;
        public string uiSpriteName;
        public OutEnergyGenerator energyGenerator;
        public OutEnergyConsumer energyConsumer;
        public OutConduitConsumer conduitConsumer;
        public OutConduitDispenser conduitDispenser;

        // Power port cell offsets — non-null only when the corresponding connection exists.
        // powerInputOffset: where a wire plugs in for buildings that consume power (RequiresPowerInput=true).
        // powerOutputOffset: where a wire plugs in for buildings that generate power (have EnergyGenerator).
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public CellOffset? powerInputOffset = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public CellOffset? powerOutputOffset = null;

        // Every connection port on this building (power / gas / liquid / solid / logic),
        // each with its cell offset from the building's bottom-left corner (pre-rotation).
        // This is the authoritative port list the website uses for placement; the power
        // *Offset fields above are kept for backward compatibility. Built by
        // ExportBuilding.BuildUtilityPorts(). See EXPORT_SCHEMA.md.
        public List<OutUtilityPort> utilities = new List<OutUtilityPort>();
        public OutPlantablePlot plantablePlot;
        public List<OutElementConverter> elementConverters = new List<OutElementConverter>();
        public List<OutElementConsumer> elementConsumers = new List<OutElementConsumer>();
        public List<OutPassiveElementConsumer> passiveElementConsumers = new List<OutPassiveElementConsumer>();
        public OutStorage storage = null;
        public AttachableBuilding attachableBuilding = null;
        public BuildingAttachPoint buildingAttachPoint = null;
        public RocketModule rocketModule = null;
        public ReorderableBuilding reorderableBuilding = null;
        public OutRocketEngineCluster rocketEngineCluster = null;
        public RocketModuleCluster rocketModuleCluster = null;
        public OutRocketEngine rocketEngine = null;
        public PassengerRocketModule passengerRocketModule = null;
        public OutCargoBay cargoBay = null;
        public CargoBayConduit cargoBayConduit = null;
        public OutCargoBayCluster cargoBayCluster = null;
        public OutTreeFilterable treeFilterable = null;
        public Deconstructable deconstructable = null;
        public Demolishable demolishable = null;
        public OutBattery battery = null;
        public RoomTracker roomTracker = null;
        public RocketUsageRestriction.Def rocketUsageRestrictionDef = null;

        public BBuildingEntity(string name, KPrefabID kPrefabID)
        {
            this.name = name;
            this.nameString = kPrefabID.GetProperName();
            this.tags = kPrefabID.Tags;
            this.kPrefabID = new BKprefabID(kPrefabID);
        }
    }
}
