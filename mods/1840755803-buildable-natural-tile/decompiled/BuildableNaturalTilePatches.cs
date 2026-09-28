using System;
using System.Collections.Generic;
using CaiLib.Utils;
using CoolLib;
using Database;
using HarmonyLib;
using TUNING;
using UnityEngine;

namespace BuildableNaturalTile;

public static class BuildableNaturalTilePatches
{
	[HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
	public static class GeneratedBuildings_LoadGeneratedBuildings_Patch
	{
		public static void Prefix()
		{
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			StringUtils.AddBuildingStrings("NaturalTile", "Natural Tile", "Fill that hole you dug out back in with any solid element.", NaturalTileConfig.Effect);
			int num = BUILDINGS.PLANORDER.FindIndex((PlanInfo x) => x.category == HashedString.op_Implicit("Base"));
			if (num != -1)
			{
				((ICollection<string>)BUILDINGS.PLANORDER[num].data).Add("NaturalTile");
			}
		}
	}

	[HarmonyPatch(typeof(Techs), "Init")]
	public static class Database_Techs_Init_Patch
	{
		public static void Postfix(ref Techs __instance)
		{
			__instance.TryGetTechForTechItem("RationBox").unlockedItemIDs.Add("NaturalTile");
		}
	}

	[HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
	public static class BuildingComplete_OnSpawn_Patch
	{
		private static readonly CellOffset[] _displacementOffsets = (CellOffset[])(object)new CellOffset[8]
		{
			new CellOffset(0, 1),
			new CellOffset(0, -1),
			new CellOffset(1, 0),
			new CellOffset(-1, 0),
			new CellOffset(1, 1),
			new CellOffset(1, -1),
			new CellOffset(-1, 1),
			new CellOffset(-1, -1)
		};

		public static void Postfix(BuildingComplete __instance)
		{
			//IL_0037: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_0074: Unknown result type (might be due to invalid IL or missing references)
			//IL_0094: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cc: Expected O, but got Unknown
			//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_012e: Unknown result type (might be due to invalid IL or missing references)
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0181: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
			//IL_0156: Unknown result type (might be due to invalid IL or missing references)
			//IL_0162: Unknown result type (might be due to invalid IL or missing references)
			//IL_0167: Unknown result type (might be due to invalid IL or missing references)
			//IL_016b: Unknown result type (might be due to invalid IL or missing references)
			GameObject gameObject = ((Component)__instance).gameObject;
			if (!(((Object)__instance).name == "NaturalTileComplete"))
			{
				return;
			}
			Log.LogDebug("__instance.name: " + ((Object)__instance).name);
			Vector3 position = gameObject.transform.position;
			PrimaryElement component = gameObject.GetComponent<PrimaryElement>();
			float temperature = component.Temperature;
			float blockMass = Settings.BlockMass;
			int num = Grid.PosToCell(position);
			Log.LogDebug($"{component}");
			Log.LogDebug($"pos: {position}, temperature: {temperature}, cell: {num}");
			SimMessages.ReplaceAndDisplaceElement(num, component.ElementID, (CellElementEvent)null, blockMass, temperature, byte.MaxValue, 0, -1);
			int num2 = num;
			foreach (Pickupable pickupable in Components.Pickupables)
			{
				Pickupable val = pickupable;
				if (Grid.PosToCell((KMonoBehaviour)(object)val) != num2)
				{
					continue;
				}
				Log.LogDebug($"pickupable: {val}");
				for (int i = 0; i < _displacementOffsets.Length; i++)
				{
					int num3 = Grid.OffsetCell(num, _displacementOffsets[i]);
					if (Grid.IsValidCell(num3) && !((BuildFlagsSolidIndexer)(ref Grid.Solid))[num3])
					{
						Vector3 val2 = Grid.CellToPosCBC(num3, (SceneLayer)25);
						KCollider2D component2 = ((Component)val).GetComponent<KCollider2D>();
						if ((Object)(object)component2 != (Object)null)
						{
							ref float y = ref val2.y;
							float num4 = y;
							float y2 = TransformExtensions.GetPosition(((KMonoBehaviour)val).transform).y;
							Bounds bounds = component2.bounds;
							y = num4 + (y2 - ((Bounds)(ref bounds)).min.y);
						}
						TransformExtensions.SetPosition(((KMonoBehaviour)val).transform, val2);
						num = num3;
						Traverse.Create((object)val).Method("RemoveFaller", new Type[0], (object[])null).GetValue();
						Traverse.Create((object)val).Method("AddFaller", new Type[1] { typeof(Vector2) }, (object[])null).GetValue(new object[1] { Vector2.zero });
						break;
					}
				}
			}
			TracesExtesions.DeleteObject(gameObject);
		}
	}

	public static Config Settings = Config.Load();

	public static void OnLoad()
	{
		Log.LogInit("BNT");
	}
}
