using HarmonyLib;

namespace High_Pressure_Applications.BuildingConfigs.DecompressionGasValve_Patches.DecompressionGasValve_TechPatch;

[HarmonyPatch(typeof(Db), "Initialize")]
internal class DecompressionGasValveTechMod
{
	private static void Postfix()
	{
		((ResourceSet<Tech>)(object)Db.Get().Techs).Get("HVAC").unlockedItemIDs.Add("DecompressionGasValve");
	}
}
