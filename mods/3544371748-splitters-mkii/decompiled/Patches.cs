using System.Collections.Generic;
using Database;
using HarmonyLib;
using MJSplittersMKII.Filters;
using STRINGS;

namespace MJSplittersMKII.Patch;

public static class Patches
{
	[HarmonyPatch(typeof(GeneratedBuildings))]
	[HarmonyPatch("LoadGeneratedBuildings")]
	public static class GeneratedBuildings_LoadGeneratedBuildings_Patch
	{
		public static void Prefix()
		{
			Strings.Add(new string[2]
			{
				"STRINGS.BUILDINGS.PREFABS." + MJLiquidFilterConfig.ID.ToUpperInvariant() + ".NAME",
				UI.FormatAsLink(MJLiquidFilterConfig.DisplayName, MJLiquidFilterConfig.ID)
			});
			Strings.Add(new string[2]
			{
				"STRINGS.BUILDINGS.PREFABS." + MJLiquidFilterConfig.ID.ToUpperInvariant() + ".DESC",
				MJLiquidFilterConfig.Description
			});
			Strings.Add(new string[2]
			{
				"STRINGS.BUILDINGS.PREFABS." + MJLiquidFilterConfig.ID.ToUpperInvariant() + ".EFFECT",
				MJLiquidFilterConfig.Effect
			});
			Strings.Add(new string[2]
			{
				"STRINGS.BUILDINGS.PREFABS." + MJSolidFilterConfig.ID.ToUpperInvariant() + ".NAME",
				UI.FormatAsLink(MJSolidFilterConfig.DisplayName, MJSolidFilterConfig.ID)
			});
			Strings.Add(new string[2]
			{
				"STRINGS.BUILDINGS.PREFABS." + MJSolidFilterConfig.ID.ToUpperInvariant() + ".DESC",
				MJSolidFilterConfig.Description
			});
			Strings.Add(new string[2]
			{
				"STRINGS.BUILDINGS.PREFABS." + MJSolidFilterConfig.ID.ToUpperInvariant() + ".EFFECT",
				MJSolidFilterConfig.Effect
			});
			Strings.Add(new string[2]
			{
				"STRINGS.BUILDINGS.PREFABS." + MJGasFilterConfig.ID.ToUpperInvariant() + ".NAME",
				UI.FormatAsLink(MJGasFilterConfig.DisplayName, MJGasFilterConfig.ID)
			});
			Strings.Add(new string[2]
			{
				"STRINGS.BUILDINGS.PREFABS." + MJGasFilterConfig.ID.ToUpperInvariant() + ".DESC",
				MJGasFilterConfig.Description
			});
			Strings.Add(new string[2]
			{
				"STRINGS.BUILDINGS.PREFABS." + MJGasFilterConfig.ID.ToUpperInvariant() + ".EFFECT",
				MJGasFilterConfig.Effect
			});
		}
	}

	[HarmonyPatch(typeof(Db))]
	[HarmonyPatch("Initialize")]
	public static class Db_Initialize_Patch
	{
		public static void Postfix()
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0050: Unknown result type (might be due to invalid IL or missing references)
			ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("Plumbing"), MJLiquidFilterConfig.ID);
			AddBuildingToTechnology("AdvancedFiltration", MJLiquidFilterConfig.ID);
			ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("Conveyance"), MJSolidFilterConfig.ID);
			AddBuildingToTechnology("SolidManagement", MJSolidFilterConfig.ID);
			ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("HVAC"), MJGasFilterConfig.ID);
			AddBuildingToTechnology("AdvancedFiltration", MJGasFilterConfig.ID);
		}

		public static void AddBuildingToTechnology(string tech, string buildingId)
		{
			Traverse obj = Traverse.Create(typeof(Techs));
			object obj2;
			if (obj == null)
			{
				obj2 = null;
			}
			else
			{
				Traverse obj3 = obj.Field("TECH_GROUPING");
				obj2 = ((obj3 != null) ? obj3.GetValue<Dictionary<string, string[]>>() : null);
			}
			Dictionary<string, string[]> dictionary = (Dictionary<string, string[]>)obj2;
			if (dictionary != null)
			{
				if (dictionary.ContainsKey(tech))
				{
					List<string> list = new List<string>(dictionary[tech]) { buildingId };
					dictionary[tech] = list.ToArray();
				}
				else
				{
					Debug.LogWarning((object)("Could not find '" + tech + "' tech in TECH_GROUPING."));
				}
				return;
			}
			Tech val = ((ResourceSet<Tech>)(object)Db.Get().Techs).TryGet(tech);
			if (val != null)
			{
				Traverse obj4 = Traverse.Create((object)val);
				if (obj4 != null)
				{
					Traverse obj5 = obj4.Field("unlockedItemIDs");
					if (obj5 != null)
					{
						obj5.GetValue<List<string>>()?.Add(buildingId);
					}
				}
			}
			else
			{
				Debug.LogWarning((object)("Could not find '" + tech + "' tech."));
			}
		}
	}
}
