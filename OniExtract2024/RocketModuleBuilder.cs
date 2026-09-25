using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OniExtract2024
{
    // Rocketry data for building.json: which defs are rocket modules, where a module can carry
    // another module (attachPoints), what a module must sit on (attachableTo /
    // attachablePosition), the per-module lift/burden stats and the module-select constraints
    // the game enforces. Everything here is read off BuildingDef / the BuildingComplete prefab
    // at main-menu time; see WEBSITE_ROCKET_MODULES.md for the website-side interpretation.
    //
    // The pure helpers (AttachPointsFrom / LaunchPadAttachPoints / ConditionNames /
    // OrderModuleMenu) take plain game structs and lists, so the test project can exercise
    // them without a running game.
    public static class RocketModuleBuilder
    {
        // Fallback for LaunchPad.baseModulePosition when reflection fails; matches the field
        // initialiser in LaunchPad (`private CellOffset baseModulePosition = new CellOffset(0, 2)`).
        public static readonly CellOffset DefaultLaunchPadBasePosition = new CellOffset(0, 2);

        public static void Apply(BBuildingEntity b, BuildingDef def, GameObject go)
        {
            // RocketModuleCluster derives from RocketModule, so this covers both the Spaced Out
            // cluster modules and the base-game (non-cluster) rocket parts.
            b.isRocketModule = go.GetComponent<RocketModule>() != null;

            // The slot this building plugs into. BuildingLoader adds an AttachableBuilding with
            // attachableToTag = def.AttachmentSlotTag whenever the tag is set, so the def field
            // is the authoritative source and is what BuildingDef.IsValidPlaceLocation checks.
            if (def.AttachmentSlotTag.IsValid)
            {
                b.attachableTo = def.AttachmentSlotTag.Name;
                b.attachablePosition = new BVector2(def.attachablePosition);
            }

            var attachPoints = new List<OutAttachPoint>();
            BuildingAttachPoint bap = go.GetComponent<BuildingAttachPoint>();
            if (bap != null && bap.points != null)
                attachPoints.AddRange(AttachPointsFrom(bap.points));
            LaunchPad pad = go.GetComponent<LaunchPad>();
            if (pad != null)
                attachPoints.AddRange(LaunchPadAttachPoints(ReadLaunchPadBasePosition(pad)));
            b.attachPoints = attachPoints.Count > 0 ? attachPoints : null;

            RocketModuleCluster cluster = go.GetComponent<RocketModuleCluster>();
            if (cluster != null && cluster.performanceStats != null)
                b.rocketModulePerformance = new OutRocketModulePerformance(cluster.performanceStats);

            ReorderableBuilding reorderable = go.GetComponent<ReorderableBuilding>();
            if (reorderable != null && reorderable.buildConditions != null && reorderable.buildConditions.Count > 0)
                b.moduleBuildConditions = ConditionNames(reorderable.buildConditions);
        }

        // BuildingAttachPoint.HardPoint[] -> attachPoints entries. attachedBuilding is runtime
        // state (always null on a prefab) and is dropped.
        public static List<OutAttachPoint> AttachPointsFrom(IEnumerable<BuildingAttachPoint.HardPoint> points)
        {
            var list = new List<OutAttachPoint>();
            foreach (var p in points)
                list.Add(new OutAttachPoint(p.position, p.attachableType.Name));
            return list;
        }

        // The LaunchPad has no BuildingAttachPoint; LaunchPad.AddBaseModule places the rocket's
        // bottom module at pad-origin + baseModulePosition. Expose that as a Rocket hardpoint so
        // "module on pad" and "module on module" share one rule on the website.
        public static List<OutAttachPoint> LaunchPadAttachPoints(CellOffset baseModulePosition)
        {
            return new List<OutAttachPoint> { new OutAttachPoint(baseModulePosition, "Rocket") };
        }

        // Type names of the module's SelectModuleCondition list (TopOnly, EngineOnBottom,
        // LimitOneEngine, LimitOneCommandModule, RocketHeightLimit, ...). Order preserved,
        // duplicates dropped.
        public static List<string> ConditionNames(IEnumerable<SelectModuleCondition> conditions)
        {
            var names = new List<string>();
            foreach (var c in conditions)
            {
                if (c == null) continue;
                string n = c.GetType().Name;
                if (!names.Contains(n)) names.Add(n);
            }
            return names;
        }

        // The game's module-select order (SelectModuleSideScreen.moduleButtonSortOrder) filtered
        // to the modules that actually exist in this export, followed by any remaining cluster
        // modules (modded ones the game's list does not know about) in ordinal name order.
        public static List<string> OrderModuleMenu(IEnumerable<string> gameOrder, IEnumerable<string> available)
        {
            var remaining = new HashSet<string>(available);
            var result = new List<string>();
            foreach (string id in gameOrder)
            {
                if (remaining.Remove(id)) result.Add(id);
            }
            result.AddRange(remaining.OrderBy(s => s, StringComparer.Ordinal));
            return result;
        }

        // LaunchPad.baseModulePosition is private; read it by reflection and fall back to the
        // known initialiser if the field ever moves.
        public static CellOffset ReadLaunchPadBasePosition(LaunchPad pad)
        {
            try
            {
                FieldInfo f = typeof(LaunchPad).GetField("baseModulePosition",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null && f.FieldType == typeof(CellOffset))
                    return (CellOffset)f.GetValue(pad);
                Debug.LogWarning("OniExtract: LaunchPad.baseModulePosition not found; using (0,2)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: could not read LaunchPad.baseModulePosition: " + e.Message);
            }
            return DefaultLaunchPadBasePosition;
        }
    }
}
