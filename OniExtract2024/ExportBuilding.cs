using System;
using System.Collections.Generic;
using UnityEngine;
using OniExtract2024;
using System.Linq;

public class ExportBuilding : BaseExport
{
    public override string ExportFileName { get; set; } = "building";
    public List<BBuildingEntity> bBuildingDefList = new List<BBuildingEntity>();
    public List<BuildMenuCategory> buildMenuCategories = new List<BuildMenuCategory>();
    public Dictionary<string, List<KeyValuePair<string, string>>> buildingAndSubcategoryDataPairs = new Dictionary<string, List<KeyValuePair<string, string>>>();
    public List<Tag> roomConstraintTags= new List<Tag>();
    public Dictionary<string, string> requiredSkillPerkMap = new Dictionary<string, string>();

    public ExportBuilding()
    {
    }

    public void AddNewBuildingDef(BuildingDef buildingDef)
    {
        this.roomConstraintTags = RoomConstraints.ConstraintTags.AllTags;
    }

    public void AddNewBuildingEntity(BuildingDef buildingDef)
    {
        GameObject go = buildingDef.BuildingComplete;
        KPrefabID prefabID = go.GetComponent<KPrefabID>();
        BBuildingEntity bBuild = new BBuildingEntity(buildingDef.Tag.Name, prefabID);
        // Carry the measured ui_image placement (if any) from the durable sidecar so it
        // survives this main-menu rewrite of building.json. Keyed by Tag.Name == json `name`.
        if (OniExtract2024.building.UiImageRectStore.TryGet(buildingDef.Tag.Name, out var uiRect))
            bBuild.uiImageRect = uiRect;
        bBuild.widthInCells = buildingDef.WidthInCells;
        bBuild.heightInCells = buildingDef.HeightInCells;
        bBuild.materialCategory = buildingDef.MaterialCategory;
        bBuild.materialMass = buildingDef.Mass;
        bBuild.isFoundation = buildingDef.IsFoundation;
        bBuild.isKAnimTile = buildingDef.isKAnimTile;
        bBuild.isUtility = buildingDef.isUtility;
        bBuild.dragBuild = buildingDef.DragBuild;
        bBuild.buildLocationRule = (int)buildingDef.BuildLocationRule;
        bBuild.permittedRotations = (int)buildingDef.PermittedRotations;
        bBuild.sceneLayer = (int)buildingDef.SceneLayer;
        bBuild.objectLayer = (int)buildingDef.ObjectLayer;
        bBuild.viewMode = ViewModeToString(buildingDef.ViewMode);
        bBuild.defaultAnimState = buildingDef.DefaultAnimState;
        bBuild.uiSpriteName = buildingDef.UISprite != null ? buildingDef.UISprite.name : null;
        EnergyGenerator energyGenerator = go.GetComponent<EnergyGenerator>();
        if (energyGenerator != null)
        {
            bBuild.energyGenerator = new OutEnergyGenerator(energyGenerator);
            bBuild.powerOutputOffset = buildingDef.PowerOutputOffset;
        }
        else
        {
            bBuild.energyGenerator = null;
        }
        EnergyConsumer energyConsumer = go.GetComponent<EnergyConsumer>();
        if (energyConsumer != null)
        {
            var ec = new OutEnergyConsumer(energyConsumer);
            // BaseWattageRating is a runtime field set by the building's state machine;
            // at main-menu export it is always 0. Read the static value from BuildingDef.
            ec.baseWattageRating = buildingDef.EnergyConsumptionWhenActive;
            bBuild.energyConsumer = ec;
        }
        if (buildingDef.RequiresPowerInput)
        {
            bBuild.powerInputOffset = buildingDef.PowerInputOffset;
        }
        ConduitConsumer conduitConsumer = go.GetComponent<ConduitConsumer>();
        if (conduitConsumer != null)
        {
            bBuild.conduitConsumer = new OutConduitConsumer(conduitConsumer);
        }
        else
        {
            bBuild.conduitConsumer = null;
        }
        ConduitDispenser conduitDispenser = go.GetComponent<ConduitDispenser>();
        if (conduitDispenser != null)
        {
            bBuild.conduitDispenser = new OutConduitDispenser(conduitDispenser);
        }
        else
        {
            bBuild.conduitDispenser = null;
        }
        ElementConverter[] elementConverters = go.GetComponents<ElementConverter>();
        if (elementConverters != null && elementConverters.Length > 0)
        {
            foreach (var elementConverter in elementConverters)
            {
                bBuild.elementConverters.Add(new OutElementConverter(elementConverter));
            }
        }
        PlantablePlot plantablePlot = go.GetComponent<PlantablePlot>();
        if (plantablePlot != null)
        {
            bBuild.plantablePlot = new OutPlantablePlot(plantablePlot);
        }
        else
        {
            bBuild.plantablePlot = null;
        }
        ElementConsumer[] elementConsumers = go.GetComponents<ElementConsumer>();
        if (elementConsumers != null && elementConsumers.Length > 0)
        {
            foreach (var elementConsumer in elementConsumers)
            {
                bBuild.elementConsumers.Add(new OutElementConsumer(elementConsumer));
            }
        }
        PassiveElementConsumer[] passiveElementConsumers = go.GetComponents<PassiveElementConsumer>();
        if (passiveElementConsumers != null && passiveElementConsumers.Length > 0)
        {
            foreach (var passiveElementConsumer in passiveElementConsumers)
            {
                bBuild.passiveElementConsumers.Add(new OutPassiveElementConsumer(passiveElementConsumer));
            }
        }
        Storage storage = go.GetComponent<Storage>();
        if (storage != null)
        {
            bBuild.storage = new OutStorage(storage);
        }
        AttachableBuilding attachableBuilding = go.GetComponent<AttachableBuilding>();
        if (attachableBuilding != null)
        {
            bBuild.attachableBuilding = attachableBuilding;
        }
        BuildingAttachPoint buildingAttachPoint = go.GetComponent<BuildingAttachPoint>();
        if (buildingAttachPoint != null)
        {
            bBuild.buildingAttachPoint = buildingAttachPoint;
        }
        RocketModule rocketModule = go.GetComponent<RocketModule>();
        if (rocketModule != null)
        {
            bBuild.rocketModule = rocketModule;
        }
        ReorderableBuilding reorderableBuilding = go.GetComponent<ReorderableBuilding>();
        if (reorderableBuilding != null)
        {
            bBuild.reorderableBuilding = reorderableBuilding;
        }
        RocketEngineCluster rocketEngineCluster = go.GetComponent<RocketEngineCluster>();
        if (rocketEngineCluster != null)
        {
            bBuild.rocketEngineCluster = new OutRocketEngineCluster(rocketEngineCluster);
        }
        RocketModuleCluster rocketModuleCluster = go.GetComponent<RocketModuleCluster>();
        if (rocketModuleCluster != null)
        {
            bBuild.rocketModuleCluster = rocketModuleCluster;
        }
        RocketEngine rocketEngine = go.GetComponent<RocketEngine>();
        if (rocketEngine != null)
        {
            bBuild.rocketEngine = new OutRocketEngine(rocketEngine);
        }
        PassengerRocketModule passengerRocketModule = go.GetComponent<PassengerRocketModule>();
        if (passengerRocketModule != null)
        {
            bBuild.passengerRocketModule = passengerRocketModule;
        }
        CargoBay cargoBay = go.GetComponent<CargoBay>();
        if (cargoBay != null)
        {
            bBuild.cargoBay = new OutCargoBay(cargoBay);
        }
        CargoBayConduit cargoBayConduit = go.GetComponent<CargoBayConduit>();
        if (cargoBayConduit != null)
        {
            bBuild.cargoBayConduit = cargoBayConduit;
        }
        CargoBayCluster cargoBayCluster = go.GetComponent<CargoBayCluster>();
        if (cargoBayCluster != null)
        {
            bBuild.cargoBayCluster = new OutCargoBayCluster(cargoBayCluster);
        }
        TreeFilterable treeFilterable = go.GetComponent<TreeFilterable>();
        if (treeFilterable != null)
        {
            bBuild.treeFilterable = new OutTreeFilterable(treeFilterable);
        }
        Deconstructable deconstructable = go.GetComponent<Deconstructable>();
        if (deconstructable != null)
        {
            bBuild.deconstructable = deconstructable;
        }
        Demolishable demolishable = go.GetComponent<Demolishable>();
        if (demolishable != null)
        {
            bBuild.demolishable = demolishable;
        }   
        Workable[] workableComponents = go.GetComponents<Workable>();
        var derivedWorkables = workableComponents.Where(component => component.GetType() != typeof(Workable) && component.GetType().IsSubclassOf(typeof(Workable)));
        foreach (var workable in derivedWorkables)
        {
            if (workable != null && workable.requiredSkillPerk != null && workable.requiredSkillPerk != "")
            {
                this.requiredSkillPerkMap.Add(buildingDef.Tag.Name, workable.requiredSkillPerk);
            }
        }
        Battery battery = go.GetComponent<Battery>();
        if (battery != null)
        {
            bBuild.battery = new OutBattery(battery);
        }
        RoomTracker roomTracker = go.GetComponent<RoomTracker>();
        if (roomTracker != null)
        {
            bBuild.roomTracker = roomTracker;
        }
        RocketUsageRestriction.Def rocketUsage = go.GetDef<RocketUsageRestriction.Def>();
        if (rocketUsage != null)
        {
            bBuild.rocketUsageRestrictionDef = rocketUsage;
        }

        bBuild.utilities = BuildUtilityPorts(buildingDef, go);

        this.bBuildingDefList.Add(bBuild);
    }

    // Maps a building's overlay HashedString (BuildingDef.ViewMode) to the game-native
    // overlay name string. Keyed on the canonical OverlayModes.*.ID fields; HashedString
    // equality is by (case-insensitive) hash, so this matches regardless of the original
    // string casing the building config used. Returns null for buildings with no special
    // overlay (e.g. ViewMode == HashedString.Invalid / 0x0).
    private static readonly Dictionary<HashedString, string> ViewModeNames =
        new Dictionary<HashedString, string>
        {
            { OverlayModes.Power.ID,          "Power" },
            { OverlayModes.GasConduits.ID,    "GasConduit" },
            { OverlayModes.LiquidConduits.ID, "LiquidConduit" },
            { OverlayModes.SolidConveyor.ID,  "SolidConveyor" },
            { OverlayModes.Logic.ID,          "Logic" },
            { OverlayModes.Oxygen.ID,         "Oxygen" },
            { OverlayModes.Decor.ID,          "Decor" },
            { OverlayModes.Light.ID,          "Light" },
            { OverlayModes.Temperature.ID,    "Temperature" },
            { OverlayModes.Rooms.ID,          "Rooms" },
            { OverlayModes.Radiation.ID,      "Radiation" },
            { OverlayModes.Disease.ID,        "Disease" },
            { OverlayModes.Crop.ID,           "Crop" },
        };

    private static string ViewModeToString(HashedString viewMode)
    {
        return ViewModeNames.TryGetValue(viewMode, out string name) ? name : null;
    }

    // Returns one OutUtilityPort per connection port on this building.
    private static List<OutUtilityPort> BuildUtilityPorts(BuildingDef def, GameObject go)
    {
        var ports = new List<OutUtilityPort>();

        // ── Conduit ports (gas / liquid) ──────────────────────────────────────
        //
        // Primary consumer/dispenser use BuildingDef.UtilityInputOffset / UtilityOutputOffset.
        // Secondary ones (useSecondaryInput/Output = true) call TryGetSecondaryOffset which
        // scans components via reflection for GetSecondaryConduitOffset(ConduitType) —
        // falls back to (0,0) if not found; isSecondary is still set correctly.
        foreach (ConduitConsumer c in go.GetComponents<ConduitConsumer>())
        {
            CellOffset off = c.useSecondaryInput
                ? TryGetSecondaryOffset(go, c.conduitType)
                : def.UtilityInputOffset;
            ports.Add(new OutUtilityPort(off, ConduitTypeToInput(c.conduitType), c.useSecondaryInput));
        }
        foreach (ConduitDispenser d in go.GetComponents<ConduitDispenser>())
        {
            CellOffset off = d.useSecondaryOutput
                ? TryGetSecondaryOffset(go, d.conduitType)
                : def.UtilityOutputOffset;
            ports.Add(new OutUtilityPort(off, ConduitTypeToOutput(d.conduitType), d.useSecondaryOutput));
        }

        // ── Conduit ports (solid / conveyor belt) ─────────────────────────────
        // Solid conduit uses SolidConduitConsumer/SolidConduitDispenser, which are
        // separate component types from ConduitConsumer/ConduitDispenser.
        foreach (SolidConduitConsumer c in go.GetComponents<SolidConduitConsumer>())
        {
            CellOffset off = c.useSecondaryInput
                ? TryGetSecondaryOffset(go, ConduitType.Solid)
                : def.UtilityInputOffset;
            ports.Add(new OutUtilityPort(off, ConnectionType.SolidInput, c.useSecondaryInput));
        }
        foreach (SolidConduitDispenser d in go.GetComponents<SolidConduitDispenser>())
        {
            CellOffset off = d.useSecondaryOutput
                ? TryGetSecondaryOffset(go, ConduitType.Solid)
                : def.UtilityOutputOffset;
            ports.Add(new OutUtilityPort(off, ConnectionType.SolidOutput, d.useSecondaryOutput));
        }

        // ── Conduit primary ports: BuildingDef fallback ───────────────────────────────────
        // Buildings like GasFilter/LiquidFilter/SolidFilter manage conduit flow via ElementFilter
        // rather than ConduitConsumer/ConduitDispenser, so the component loops above capture no
        // conduit ports for them.  If BuildingDef declares a conduit type but no matching port
        // was added above, fall back to the BuildingDef input/output offsets.
        if (def.InputConduitType == ConduitType.Gas    && !ports.Any(p => p.type == ConnectionType.GasInput))
            ports.Add(new OutUtilityPort(def.UtilityInputOffset, ConnectionType.GasInput, false));
        if (def.InputConduitType == ConduitType.Liquid && !ports.Any(p => p.type == ConnectionType.LiquidInput))
            ports.Add(new OutUtilityPort(def.UtilityInputOffset, ConnectionType.LiquidInput, false));
        if (def.InputConduitType == ConduitType.Solid  && !ports.Any(p => p.type == ConnectionType.SolidInput))
            ports.Add(new OutUtilityPort(def.UtilityInputOffset, ConnectionType.SolidInput, false));
        if (def.OutputConduitType == ConduitType.Gas    && !ports.Any(p => p.type == ConnectionType.GasOutput))
            ports.Add(new OutUtilityPort(def.UtilityOutputOffset, ConnectionType.GasOutput, false));
        if (def.OutputConduitType == ConduitType.Liquid && !ports.Any(p => p.type == ConnectionType.LiquidOutput))
            ports.Add(new OutUtilityPort(def.UtilityOutputOffset, ConnectionType.LiquidOutput, false));
        if (def.OutputConduitType == ConduitType.Solid  && !ports.Any(p => p.type == ConnectionType.SolidOutput))
            ports.Add(new OutUtilityPort(def.UtilityOutputOffset, ConnectionType.SolidOutput, false));

        // ── Secondary conduit outputs via ISecondaryOutput ──────────────────────────────
        // ElementFilter (GasFilter / LiquidFilter / SolidFilter) implements ISecondaryOutput and
        // registers a filtered-element port at portInfo.offset (distinct from UtilityOutputOffset).
        // Detected via reflection to avoid a hard reference to the interface.
        foreach (Component comp in go.GetComponents<Component>())
        {
            if (comp == null) continue;
            var hasMeth = comp.GetType().GetMethod(
                "HasSecondaryConduitType",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(ConduitType) }, null);
            var getMeth = comp.GetType().GetMethod(
                "GetSecondaryConduitOffset",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(ConduitType) }, null);
            if (hasMeth == null || getMeth == null) continue;
            foreach (ConduitType ct in new[] { ConduitType.Gas, ConduitType.Liquid, ConduitType.Solid })
            {
                bool has = false;
                try { has = (bool)hasMeth.Invoke(comp, new object[] { ct }); } catch { continue; }
                if (!has) continue;
                ConnectionType outType = ct == ConduitType.Gas    ? ConnectionType.GasOutput :
                                         ct == ConduitType.Liquid ? ConnectionType.LiquidOutput :
                                                                    ConnectionType.SolidOutput;
                // Skip if the ConduitDispenser loop already added a secondary port of this type.
                if (ports.Any(p => p.type == outType && p.isSecondary)) continue;
                CellOffset off = new CellOffset(0, 0);
                try { off = (CellOffset)getMeth.Invoke(comp, new object[] { ct }); } catch { continue; }
                ports.Add(new OutUtilityPort(off, outType, true));
            }
        }

        // ── Power ports ────────────────────────────────────────────────────────
        if (def.EnergyConsumptionWhenActive > 0f)
            ports.Add(new OutUtilityPort(def.PowerInputOffset, ConnectionType.PowerInput, false));
        // EnergyGenerator = wired generators; Battery = rechargeable storage that also outputs
        if (go.GetComponent<EnergyGenerator>() != null || go.GetComponent<Battery>() != null)
            ports.Add(new OutUtilityPort(def.PowerOutputOffset, ConnectionType.PowerOutput, false));

        // ── Logic ports (sensors and standard buildings) ───────────────────────
        // BuildingDef.LogicInputPorts/LogicOutputPorts are set during CreateBuildingDef()
        // for sensors and most buildings that connect to the logic network.
        if (def.LogicInputPorts != null)
            foreach (var p in def.LogicInputPorts)
                ports.Add(new OutUtilityPort(p.cellOffset, LogicSpriteToType(p.spriteType, true), false));
        if (def.LogicOutputPorts != null)
            foreach (var p in def.LogicOutputPorts)
                ports.Add(new OutUtilityPort(p.cellOffset, LogicSpriteToType(p.spriteType, false), false));

        // ── Logic ports (gates: AND/OR/XOR/NOT/BUFFER/FILTER/MUX/DEMUX) ────────
        // Logic gates do NOT set BuildingDef.LogicInputPorts/LogicOutputPorts.
        // Instead they add a LogicGateBase component in DoPostConfigureComplete and
        // store port offsets directly in its inputPortOffsets/outputPortOffsets/
        // controlPortOffsets arrays — these are set at prefab config time, not spawn time.
        LogicGateBase logicGate = go.GetComponent<LogicGateBase>();
        if (logicGate != null)
        {
            if (logicGate.inputPortOffsets != null)
                foreach (var off in logicGate.inputPortOffsets)
                    ports.Add(new OutUtilityPort(off, ConnectionType.LogicInput, false));
            if (logicGate.outputPortOffsets != null)
                foreach (var off in logicGate.outputPortOffsets)
                    ports.Add(new OutUtilityPort(off, ConnectionType.LogicOutput, false));
            // control ports (MUX/DEMUX selector bits) act as logic inputs
            if (logicGate.controlPortOffsets != null)
                foreach (var off in logicGate.controlPortOffsets)
                    ports.Add(new OutUtilityPort(off, ConnectionType.LogicInput, false));
        }

        return ports;
    }

    private static ConnectionType ConduitTypeToInput(ConduitType ct)
    {
        return ct == ConduitType.Liquid ? ConnectionType.LiquidInput : ConnectionType.GasInput;
    }

    private static ConnectionType ConduitTypeToOutput(ConduitType ct)
    {
        return ct == ConduitType.Liquid ? ConnectionType.LiquidOutput : ConnectionType.GasOutput;
    }

    // Maps LogicPortSpriteType (matched by .ToString() so it is resilient to int value
    // changes across ONI updates) to ConnectionType.  Defaults to the port direction.
    private static ConnectionType LogicSpriteToType(LogicPortSpriteType spriteType, bool isInput)
    {
        string s = spriteType.ToString();
        if (s.IndexOf("ribbon", StringComparison.OrdinalIgnoreCase) >= 0)
            return s.IndexOf("out", StringComparison.OrdinalIgnoreCase) >= 0
                ? ConnectionType.LogicRibbonOutput
                : ConnectionType.LogicRibbonInput;
        if (s.IndexOf("reset", StringComparison.OrdinalIgnoreCase) >= 0 ||
            s.IndexOf("update", StringComparison.OrdinalIgnoreCase) >= 0)
            return ConnectionType.LogicReset;
        return isInput ? ConnectionType.LogicInput : ConnectionType.LogicOutput;
    }

    // Scans every component on the GameObject for a public GetSecondaryConduitOffset(ConduitType)
    // method (avoids a hard reference to ISecondaryInput/ISecondaryOutput, which may not be
    // public interfaces in all ONI builds).  Falls back to (0,0) — isSecondary is still set.
    private static CellOffset TryGetSecondaryOffset(GameObject go, ConduitType conduitType)
    {
        foreach (Component comp in go.GetComponents<Component>())
        {
            if (comp == null) continue;
            var m = comp.GetType().GetMethod(
                "GetSecondaryConduitOffset",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(ConduitType) },
                null);
            if (m == null) continue;
            try { return (CellOffset)m.Invoke(comp, new object[] { conduitType }); }
            catch { }
        }
        return new CellOffset(0, 0);
    }

    public void ExportBuildMenu()
    {
        foreach (var planOrder in TUNING.BUILDINGS.PLANORDER)
        {
            string icon_name = PlanScreen.IconNameMap[planOrder.category];
            string categoryName = HashCache.Get().Get(planOrder.category);
            this.buildMenuCategories.Add(new BuildMenuCategory()
            {
                category = planOrder.category.HashValue,
                categoryName = categoryName,
                categoryIcon = icon_name
            });
            this.buildingAndSubcategoryDataPairs[categoryName] = planOrder.buildingAndSubcategoryData;
        }
    }
}
