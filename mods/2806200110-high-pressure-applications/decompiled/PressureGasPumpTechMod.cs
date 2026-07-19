using HarmonyLib;

namespace High_Pressure_Applications.BuildingConfigs.PressureGasPump_Patches.PressureGasPump_TechPatch;

[HarmonyPatch(typeof(Db), "Initialize")]
internal class PressureGasPumpTechMod
{
	private static void Postfix()
	{
		((ResourceSet<Tech>)(object)Db.Get().Techs).Get("ValveMiniaturization").unlockedItemIDs.Add("PressureGasPump");
	}
}
