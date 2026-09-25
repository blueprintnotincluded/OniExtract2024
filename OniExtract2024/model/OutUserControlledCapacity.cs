namespace OniExtract2024
{
    // A user-adjustable storage capacity (the "capacity" slider in the side screen). The
    // blueprint `buildingData.IUserControlledCapacity.UserMaxCapacity` setting (and
    // `buildingData.StorageTile` on Storage Tiles) is a value in [minCapacity, maxCapacity].
    //
    // Read from whichever component on the prefab implements IUserControlledCapacity
    // (StorageLocker, Refrigerator, RationBox, FuelTank, OxidizerTank, CargoBayCluster, ...),
    // or from StorageTile.Def for Storage Tiles, whose implementation lives on a state-machine
    // instance that does not exist at export time. `source` names that component/def type.
    public class OutUserControlledCapacity
    {
        public float minCapacity;
        public float maxCapacity;
        // true → the slider steps in whole units (item counts) rather than fractional kg.
        public bool wholeValues;
        // Display unit for the slider ("kg" for mass; unit-count storages vary).
        public string units;
        public string source;
    }
}
