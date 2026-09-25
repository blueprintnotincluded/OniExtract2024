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

        // BuildingDef.Deprecated. Legacy content the game keeps loadable but never offers:
        // BuildingDef gates menu visibility on `!Deprecated && (!DebugOnly ||
        // Game.Instance.DebugOnlyBuildingsAllowed)`, so a deprecated building is hidden in
        // every mode, unlike a DebugOnly one which a debug build menu reveals. Note this is
        // independent of plan order -- several deprecated buildings still hold a position in
        // TUNING/BUILDINGS.cs PLANORDER, so consumers building a menu from
        // buildingAndSubcategoryDataPairs need this flag to filter them out.
        public bool deprecated;

        // BuildingDef.DebugOnly. Development-only content -- the "Dev *" buildings. Hidden
        // from the build menu unless the game is in debug mode, which is the other half of
        // the gate quoted above: a DebugOnly building IS reachable in a debug build menu,
        // where a deprecated one never is. Exported separately for that reason -- a consumer
        // may reasonably want to hide one and not the other.
        public bool debugOnly;

        // BuildingDef.ShowInBuildMenu. false for content the game offers through some other UI
        // instead of the build menu: every Spaced Out rocket module (placed from the rocket
        // platform's module screen) and a handful of special-cased parts. Always emitted, like
        // deprecated/debugOnly, so a consumer building a menu can read the three flags together.
        // A module with showInBuildMenu=false is still buildable -- see rocketModuleMenu.
        public bool showInBuildMenu;
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
        public OutRocketEngineCluster rocketEngineCluster = null;
        public OutRocketEngine rocketEngine = null;
        public OutCargoBay cargoBay = null;
        public OutCargoBayCluster cargoBayCluster = null;
        public OutTreeFilterable treeFilterable = null;
        public OutBattery battery = null;
        public RocketUsageRestriction.Def rocketUsageRestrictionDef = null;

        // ── Rocketry (Spaced Out module stacking) ─────────────────────────────────────────
        // All optional: omitted (never null/false) when they do not apply. Filled by
        // RocketModuleBuilder; website-side semantics in WEBSITE_ROCKET_MODULES.md.

        // true when the BuildingComplete prefab carries a RocketModule (or RocketModuleCluster)
        // component: engines, tanks, cargo bays, habitats, nosecones, ... Omitted when false.
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool isRocketModule;

        // BuildingDef.AttachmentSlotTag: the hardpoint type this building must sit on ("Rocket"
        // for every module). attachablePosition (BuildingDef.attachablePosition) is the cell of
        // THIS building that must land on the hardpoint, as an offset from its origin cell --
        // (0,0) for all vanilla modules. Both omitted for ordinary buildings.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string attachableTo = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public BVector2 attachablePosition = null;

        // Hardpoints this building offers, offsets from its origin cell (utilities[].offset
        // convention). Modules that can carry another module above them have one Rocket
        // hardpoint at (0, heightInCells); nosecones have none. The LaunchPad's entry is the
        // cell its bottom module is placed at. Omitted when the building offers none.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<OutAttachPoint> attachPoints = null;

        // RocketModuleCluster.performanceStats (burden / enginePower / fuelKilogramPerDistance).
        // Omitted for non-cluster buildings.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public OutRocketModulePerformance rocketModulePerformance = null;

        // ReorderableBuilding.buildConditions type names, the constraints the game's module
        // screen checks before offering this module at a position: e.g. "TopOnly" (nothing may
        // go above it), "EngineOnBottom", "LimitOneEngine", "LimitOneCommandModule",
        // "RocketHeightLimit". Omitted when the building has no ReorderableBuilding.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> moduleBuildConditions = null;

        // ── Settings ranges (blueprint buildingData) ──────────────────────────────────────
        // Static facts behind per-building settings a blueprint may carry. Filled by
        // BuildingSettingsBuilder; all omitted when the component is absent.

        // Has a Prioritizable component: accepts a work priority (buildingData.Prioritizable).
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool prioritizable;
        // Has a UserNameable component: the player can rename it (buildingData.UserNameable).
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool userNameable;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public OutDoor door = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public OutValve valve = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public OutLimitValve limitValve = null;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public OutUserControlledCapacity userControlledCapacity = null;

        public BBuildingEntity(string name, KPrefabID kPrefabID)
        {
            this.name = name;
            this.nameString = kPrefabID.GetProperName();
            this.tags = kPrefabID.Tags;
            this.kPrefabID = new BKprefabID(kPrefabID);
        }
    }
}
