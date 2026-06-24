using System.Collections.Generic;
using UnityEngine;

namespace OniExtract2024.building
{
    /// <summary>
    /// Single source of truth for "can this building be spawned and rendered outside its
    /// intended context?" Used by both the pose inspector (to build its chooser list) and
    /// ExportBuildingImages (to gate the export sweep). Both spawn one building at a time at
    /// a single in-world cell, so the only things filtered here are buildings that crash or
    /// corrupt game state in *any* cell — not placement artifacts.
    /// </summary>
    internal static class BuildingSpawnFilter
    {
        // Deprecated buildings as a class crash the sweep: spawning one without full game
        // context corrupts state. Skip Deprecated by default and opt back in only buildings
        // vetted to spawn cleanly that the website still displays.
        private static readonly HashSet<string> DeprecatedAllowlist = new HashSet<string>
        {
            "SteamTurbine", // old 5x4 steam turbine; site still shows it, low-res today
        };

        // Buildings whose problematic state machines are registered at spawn time via
        // StateMachineController rather than as KMonoBehaviour components on the prefab.
        // HasSMDef now catches RocketUsageRestriction reliably, so this is a safety net.
        private static readonly HashSet<string> SpawnTimeSMBlocklist = new HashSet<string>
        {
            "ArcadeMachine",
            "HotTub",
            "Juicer",
        };

        // True if the building can be spawned off in a normal world cell and rendered.
        internal static bool IsRenderable(BuildingDef def)
        {
            if (def == null || def.BuildingComplete == null || !def.ShowInBuildMenu)
                return false;

            var kpid = def.BuildingComplete.GetComponent<KPrefabID>();

            if (def.Deprecated)
            {
                if (kpid == null || !DeprecatedAllowlist.Contains(kpid.PrefabTag.Name))
                    return false;
            }

            if (kpid != null && SpawnTimeSMBlocklist.Contains(kpid.PrefabTag.Name))
                return false;

            // Use GetComponentInChildren throughout: some components live on child objects
            // of the BuildingComplete prefab, not the root.
            if (def.BuildingComplete.GetComponentInChildren<KBatchedAnimController>(true) == null)
                return false;

            // Rocket-module buildings and interior fittings all require full rocket/cluster
            // context that doesn't exist when spawned into a normal world cell — these crash
            // in any cell, not just off-world ones.
            // RocketModuleCluster: rocket engine/cargo/etc. modules.
            // WireUtilitySemiVirtualNetworkLink: interior power/gas/liquid plugs — NOT
            //   subsumed by RocketModuleCluster.
            // LaunchPad: OnSpawn NPEs → corrupts BuildALaunchPad achievement per-frame.
            // RocketControlStation: GetRocket() NPEs in state machine.
            // InOrbitRequired: spawn + state monitor NPEs outside orbit.
            // LogicClusterLocationSensor: logic-tick subscription NPEs without valid world.
            if (def.BuildingComplete.GetComponentInChildren<RocketModuleCluster>(true) != null
                || def.BuildingComplete.GetComponentInChildren<WireUtilitySemiVirtualNetworkLink>(true) != null
                || def.BuildingComplete.GetComponentInChildren<LaunchPad>(true) != null
                || def.BuildingComplete.GetComponentInChildren<RocketUsageRestriction>(true) != null
                || def.BuildingComplete.GetComponentInChildren<RocketControlStation>(true) != null
                || def.BuildingComplete.GetComponentInChildren<InOrbitRequired>(true) != null
                || def.BuildingComplete.GetComponentInChildren<LogicClusterLocationSensor>(true) != null)
                return false;

            // RocketUsageRestriction can also be registered as a GameStateMachine via
            // StateMachineController rather than as a direct component — GetComponentInChildren
            // misses it in that case. Detect it through the controller's def list.
            if (HasSMDef<RocketUsageRestriction.Def>(def.BuildingComplete))
                return false;

            return true;
        }

        // Returns true if the building's StateMachineController has a def of type T
        // registered. Some state machines (e.g. RocketUsageRestriction) are GameStateMachines
        // whose Def is stored in the controller — GetComponentInChildren can't find them
        // because they are not KMonoBehaviour components on the prefab.
        //
        // The defs are added at config time via go.AddOrGetDef<T>(), which lives in the
        // controller's cmpdef.defs list (NOT the private `stateMachines` list — that holds
        // runtime StateMachine.Instance objects created on spawn, which is empty on a
        // prefab). StateMachineController exposes a public GetDef<T>() over cmpdef.defs, so
        // we use the game's own API rather than reflecting into private fields.
        internal static bool HasSMDef<T>(GameObject go) where T : StateMachine.BaseDef
        {
            var smc = go.GetComponent<StateMachineController>();
            return smc != null && smc.GetDef<T>() != null;
        }
    }
}
