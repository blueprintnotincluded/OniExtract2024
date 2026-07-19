using HarmonyLib;

namespace High_Pressure_Applications.BuildingConfigs.DecompressionLiquidValve_Patches.DecompressionLiquidValve_TechPatch;

[HarmonyPatch(typeof(Db), "Initialize")]
internal class DecompressionLiquidValveTechMod
{
	private static void Postfix()
	{
		((ResourceSet<Tech>)(object)Db.Get().Techs).Get("LiquidTemperature").unlockedItemIDs.Add("DecompressionLiquidValve");
	}
}
