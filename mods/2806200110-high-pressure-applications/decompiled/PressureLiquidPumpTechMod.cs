using HarmonyLib;

namespace High_Pressure_Applications.BuildingConfigs.PressureLiquidPump_Patches.PressureLiquidPump_TechPatch;

[HarmonyPatch(typeof(Db), "Initialize")]
internal class PressureLiquidPumpTechMod
{
	private static void Postfix()
	{
		((ResourceSet<Tech>)(object)Db.Get().Techs).Get("ValveMiniaturization").unlockedItemIDs.Add("PressureLiquidPump");
	}
}
