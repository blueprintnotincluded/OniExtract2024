namespace OniExtract2024
{
    // RocketModuleCluster.performanceStats (Spaced Out rocketry). Set per module in
    // BuildingTemplates.ExtendBuildingToRocketModuleCluster from TUNING.ROCKETRY constants.
    //   burden                  — module weight the engine must lift (ROCKETRY.BURDEN.*).
    //   enginePower             — lift the engine provides (ROCKETRY.ENGINE_POWER.*); 0 for
    //                             non-engine modules.
    //   fuelKilogramPerDistance — engine fuel cost per cluster tile; 0 for non-engines.
    // Clustercraft.Speed = sum(enginePower) / sum(burden) over the stack; fuel per hex = sum(fuelKilogramPerDistance).
    public class OutRocketModulePerformance
    {
        public float burden;
        public float enginePower;
        public float fuelKilogramPerDistance;

        public OutRocketModulePerformance(RocketModulePerformance stats)
        {
            this.burden = stats.Burden;
            this.enginePower = stats.EnginePower;
            this.fuelKilogramPerDistance = stats.FuelKilogramPerDistance;
        }
    }
}
