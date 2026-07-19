using HarmonyLib;
using SkyLib;

namespace Drains;

public class DrainPatch
{
	[HarmonyPatch("Initialize")]
	[HarmonyPatch(typeof(Db))]
	public static class Db_Initialize_Patch
	{
		public static void Prefix()
		{
			OniUtils.AddBuildingStrings("Drain", "Drain", "9 out of 10 plumbers recommend Drain® for its uncloggability. Our new Drain® companion product, Clog-Be-Gone™, will hit shelves soon. Now less likely to vaporize unexpectedly during normal operation!", DrainConfig.Effect);
		}

		public static void Postfix()
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			OniUtils.AddBuildingToBuildMenu(HashedString.op_Implicit("Plumbing"), "Drain");
			OniUtils.AddBuildingToTech("SanitationSciences", "Drain");
		}
	}
}
