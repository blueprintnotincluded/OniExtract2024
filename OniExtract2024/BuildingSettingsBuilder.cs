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
            // Prioritizable lands on the BuildingComplete prefab only through the config's
            // Prioritizable.AddRef(go) (DoPostConfigureComplete); BuildingLoader adds one to the
            // under-construction template, never to the complete building. Runtime AddRef calls
            // (e.g. Deconstructable while a deconstruct is queued) are transient and not what a
            // blueprint's buildingData.Prioritizable describes.
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
        // are read from StorageTile.Def instead.
        //
        // The getters are runtime code. Several (Refrigerator, RationBox, CargoBayCluster,
        // Bottler) implement MaxCapacity as `storage.capacityKg` through a [MyCmpReq] field that
        // is only bound in OnPrefabInit, so on the inactive BuildingComplete prefab they throw
        // NullReferenceException; StorageLocker/ObjectDispenser use GetComponent<Storage>() and
        // FuelTank/OxidizerTank plain fields, which work. Each property is therefore read on its
        // own, and a failed MaxCapacity falls back to the prefab's Storage.capacityKg -- the
        // value those implementations would have returned. Only when that is unavailable too is
        // the component skipped (one warning per building).
        public static OutUserControlledCapacity BuildCapacity(GameObject go)
        {
            foreach (Component comp in go.GetComponents<Component>())
            {
                var cap = comp as IUserControlledCapacity;
                if (cap == null) continue;

                float maxCapacity;
                bool haveMax = TryRead(() => cap.MaxCapacity, out maxCapacity);
                if (!haveMax)
                {
                    Storage storage = go.GetComponent<Storage>();
                    if (storage != null)
                    {
                        maxCapacity = storage.capacityKg;
                        haveMax = true;
                    }
                }
                if (!haveMax)
                {
                    Debug.LogWarning("OniExtract: IUserControlledCapacity.MaxCapacity unreadable on "
                        + go.name + " (" + comp.GetType().Name + "); skipped");
                    continue;
                }

                float minCapacity;
                if (!TryRead(() => cap.MinCapacity, out minCapacity)) minCapacity = 0f;
                bool wholeValues;
                if (!TryRead(() => cap.WholeValues, out wholeValues)) wholeValues = false;
                string units;
                if (!TryRead(() => LocStringToString(cap.CapacityUnits), out units)) units = null;

                return new OutUserControlledCapacity
                {
                    minCapacity = minCapacity,
                    maxCapacity = maxCapacity,
                    wholeValues = wholeValues,
                    units = units,
                    source = comp.GetType().Name,
                };
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

        // Evaluates a prefab-time getter that may dereference unbound runtime fields.
        private static bool TryRead<T>(Func<T> read, out T value)
        {
            try
            {
                value = read();
                return true;
            }
            catch (Exception)
            {
                value = default(T);
                return false;
            }
        }
    }
}
