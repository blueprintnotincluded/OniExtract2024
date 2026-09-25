using System;
using UnityEngine;

namespace OniExtract2024
{
    // Static facts behind the per-building settings a blueprint can carry in `buildingData`
    // (Blueprints Included / BlueprintsV2 handler keys): whether a building is prioritizable or
    // nameable, and the slider bounds for doors, valves, limit valves and capacity-controlled
    // storages. All read from the BuildingComplete prefab. Fields are omitted from the JSON
    // when the building lacks the component. See WEBSITE_ROCKET_MODULES.md ("Settings ranges").
    public static class BuildingSettingsBuilder
    {
        public static void Apply(BBuildingEntity b, GameObject go)
        {
            b.prioritizable = go.GetComponent<Prioritizable>() != null;
            b.userNameable = go.GetComponent<UserNameable>() != null;

            Door door = go.GetComponent<Door>();
            if (door != null) b.door = new OutDoor(door);

            ValveBase valveBase = go.GetComponent<ValveBase>();
            if (valveBase != null) b.valve = new OutValve(valveBase);

            LimitValve limitValve = go.GetComponent<LimitValve>();
            if (limitValve != null) b.limitValve = new OutLimitValve(limitValve);

            b.userControlledCapacity = BuildCapacity(go);
        }

        // First component implementing IUserControlledCapacity wins (StorageLocker,
        // Refrigerator, RationBox, FuelTank, OxidizerTank, CargoBayCluster, ...). Storage Tiles
        // implement it on a state-machine Instance that does not exist on the prefab, so they
        // are read from StorageTile.Def instead. Property getters can touch runtime state, so
        // each read is guarded: a failure skips that component rather than the export.
        public static OutUserControlledCapacity BuildCapacity(GameObject go)
        {
            foreach (Component comp in go.GetComponents<Component>())
            {
                var cap = comp as IUserControlledCapacity;
                if (cap == null) continue;
                try
                {
                    return new OutUserControlledCapacity
                    {
                        minCapacity = cap.MinCapacity,
                        maxCapacity = cap.MaxCapacity,
                        wholeValues = cap.WholeValues,
                        units = LocStringToString(cap.CapacityUnits),
                        source = comp.GetType().Name,
                    };
                }
                catch (Exception e)
                {
                    Debug.LogWarning("OniExtract: IUserControlledCapacity read failed on " + go.name
                        + " (" + comp.GetType().Name + "): " + e.Message);
                }
            }

            StorageTile.Def storageTile = go.GetDef<StorageTile.Def>();
            if (storageTile != null)
            {
                string units;
                try { units = LocStringToString(GameUtil.GetCurrentMassUnit()); }
                catch (Exception) { units = "kg"; }
                return new OutUserControlledCapacity
                {
                    minCapacity = 0f,
                    maxCapacity = storageTile.MaxCapacity,
                    wholeValues = false,
                    units = units,
                    source = "StorageTile.Def",
                };
            }
            return null;
        }

        private static string LocStringToString(LocString s)
        {
            return s == null ? null : s.ToString();
        }
    }
}
