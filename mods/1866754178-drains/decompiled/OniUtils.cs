using System.Collections.Generic;
using STRINGS;
using TUNING;
using UnityEngine;

namespace SkyLib;

public static class OniUtils
{
	public static void AddBuildingToTech(string tech, string buildingid)
	{
		((ResourceSet<Tech>)(object)Db.Get().Techs).Get(tech).unlockedItemIDs.Add(buildingid);
	}

	public static void AddBuildingToBuildMenu(HashedString category, string buildingid, string addAfterId = null)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		int num = BUILDINGS.PLANORDER.FindIndex((PlanInfo x) => x.category == category);
		if (num == -1)
		{
			Logger.LogLine($"Could not find building category '{category}'");
			return;
		}
		IList<string> data = BUILDINGS.PLANORDER[num].data;
		if (data == null)
		{
			Logger.LogLine($"Could not find planorder with the given index for '{category}'");
			return;
		}
		if (addAfterId == null)
		{
			data.Add(buildingid);
			return;
		}
		int num2 = data.IndexOf(addAfterId);
		if (num2 == -1)
		{
			Logger.LogLine("Could not find the building '" + addAfterId + "' to add '" + buildingid + "' after.");
		}
		else
		{
			data.Insert(num2 + 1, buildingid);
		}
	}

	public static void AddBuildingStrings(string id, string name, string desc, string effect)
	{
		string text = id.ToUpperInvariant();
		Strings.Add(new string[2]
		{
			"STRINGS.BUILDINGS.PREFABS." + text + ".NAME",
			UI.FormatAsLink(name, id)
		});
		Strings.Add(new string[2]
		{
			"STRINGS.BUILDINGS.PREFABS." + text + ".DESC",
			desc
		});
		Strings.Add(new string[2]
		{
			"STRINGS.BUILDINGS.PREFABS." + text + ".EFFECT",
			effect
		});
	}

	public static void AddStatusItem(string status_id, string stringtype, string statusitem, string category = "MISC")
	{
		category = category.ToUpperInvariant();
		Strings.Add(new string[2]
		{
			"STRINGS." + category + ".STATUSITEMS." + status_id.ToUpperInvariant() + "." + stringtype.ToUpperInvariant(),
			statusitem
		});
	}

	public static void AddDiseaseName(string disease_id, string name)
	{
		Strings.Add(new string[2]
		{
			"STRINGS.DUPLICANTS.DISEASES." + disease_id.ToUpperInvariant() + ".NAME",
			name
		});
	}

	public static bool IsCellExposedToSpace(int cell)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Invalid comparison between Unknown and I4
		if ((int)Game.Instance.world.zoneRenderData.GetSubWorldZoneType(cell) == 7)
		{
			return (Object)(object)((ObjectLayerIndexer)(ref Grid.Objects))[cell, 2] == (Object)null;
		}
		return false;
	}
}
