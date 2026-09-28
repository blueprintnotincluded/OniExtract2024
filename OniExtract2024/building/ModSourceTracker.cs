using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace OniExtract2024.building
{
    /// <summary>
    /// Records which mod (if any) registered each building, so the export can attribute
    /// modded buildings to their source mod (building.json per-entry `mod`/`modTitle` and
    /// the root `mods` roster — the website uses these to group modded buildings and to
    /// flag blueprints that require a mod).
    ///
    /// BuildingConfigManager.RegisterBuilding is the single funnel every IBuildingConfig —
    /// vanilla and modded — passes through; the config type's Assembly identifies the DLL,
    /// and KMod.Manager maps loaded assemblies back to mod labels. Vanilla configs live in
    /// Assembly-CSharp and resolve to no mod.
    /// </summary>
    public static class ModSourceTracker
    {
        // BuildingDef.PrefabID -> assembly that declared the registering IBuildingConfig.
        private static readonly Dictionary<string, Assembly> DefSource =
            new Dictionary<string, Assembly>();

        private static readonly Assembly GameAssembly = typeof(BuildingConfigManager).Assembly;

        // Assembly -> resolved mod label; null value = base game (no attribution).
        private static readonly Dictionary<Assembly, KeyValuePair<string, string>?> Resolved =
            new Dictionary<Assembly, KeyValuePair<string, string>?>();

        [HarmonyPatch(typeof(BuildingConfigManager), nameof(BuildingConfigManager.RegisterBuilding))]
        internal static class RegisterBuilding_Patch
        {
            // ___configTable: RegisterBuilding early-returns on a DLC mismatch without
            // registering the config, so only actually-registered defs land in DefSource.
            private static void Postfix(IBuildingConfig config,
                Dictionary<IBuildingConfig, BuildingDef> ___configTable)
            {
                if (config == null || ___configTable == null)
                    return;
                if (___configTable.TryGetValue(config, out BuildingDef def) && def != null)
                    DefSource[def.PrefabID] = config.GetType().Assembly;
            }
        }

        /// <summary>
        /// True (with the mod's workshop/local id and title) when the building was
        /// registered by a mod DLL; false for base-game buildings.
        /// </summary>
        public static bool TryGetMod(string prefabId, out string modId, out string modTitle)
        {
            modId = null;
            modTitle = null;
            if (prefabId == null || !DefSource.TryGetValue(prefabId, out Assembly asm))
                return false;
            if (!Resolved.TryGetValue(asm, out KeyValuePair<string, string>? label))
            {
                label = ResolveAssembly(asm);
                Resolved[asm] = label;
            }
            if (label == null)
                return false;
            modId = label.Value.Key;
            modTitle = label.Value.Value;
            return true;
        }

        private static KeyValuePair<string, string>? ResolveAssembly(Assembly asm)
        {
            if (asm == null || asm == GameAssembly)
                return null;
            string asmName = asm.GetName().Name;
            if (asmName != null && asmName.StartsWith("Assembly-CSharp"))
                return null;

            KMod.Manager manager = Global.Instance != null ? Global.Instance.modManager : null;
            if (manager != null)
            {
                foreach (KMod.Mod mod in manager.mods)
                {
                    ICollection<Assembly> dlls =
                        mod.loaded_mod_data != null ? mod.loaded_mod_data.dlls : null;
                    if (dlls != null && dlls.Contains(asm))
                        return new KeyValuePair<string, string>(mod.label.id, mod.label.title);
                }
            }

            // Non-game assembly we couldn't map to a mod (unexpected for buildings) —
            // keep the attribution under the assembly name rather than dropping it.
            return new KeyValuePair<string, string>(asmName, asmName);
        }
    }
}
