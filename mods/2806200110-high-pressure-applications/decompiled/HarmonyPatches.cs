using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using High_Pressure_Applications.BuildingConfigs;
using High_Pressure_Applications.Components;
using UnityEngine;

namespace High_Pressure_Applications;

internal static class HarmonyPatches
{
	public class ConduitFlowPatches
	{
		[HarmonyPatch(typeof(ConduitFlow), "AddElement")]
		internal static class Patch_ConduitFlow_AddElement
		{
			internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				CodeInstruction getCellInstruction = new CodeInstruction(OpCodes.Ldarg_1, (object)null);
				foreach (CodeInstruction code in instructions)
				{
					foreach (CodeInstruction item in Integration.AddIntegrationIfNeeded(code, getCellInstruction))
					{
						yield return item;
					}
				}
			}
		}

		[HarmonyPatch(typeof(ConduitFlow), "UpdateConduit")]
		internal static class Patch_ConduitFlow_UpdateConduit
		{
			internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				CodeInstruction getCellInstruction = new CodeInstruction(OpCodes.Ldloc_S, (object)13);
				foreach (CodeInstruction code in instructions)
				{
					foreach (CodeInstruction item in Integration.AddIntegrationIfNeeded(code, getCellInstruction, isUpdateConduit: true))
					{
						yield return item;
					}
				}
			}
		}

		[HarmonyPatch(typeof(ConduitFlow), "IsConduitFull")]
		internal static class Patch_ConduitFlow_IsConduitFull
		{
			internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				CodeInstruction getCellInstruction = new CodeInstruction(OpCodes.Ldarg_1, (object)null);
				foreach (CodeInstruction code in instructions)
				{
					foreach (CodeInstruction item in Integration.AddIntegrationIfNeeded(code, getCellInstruction))
					{
						yield return item;
					}
				}
			}
		}

		[HarmonyPatch(typeof(ConduitFlow), "OnDeserialized")]
		internal static class Patch_ConduitFlow_OnDeserialized
		{
			internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				MethodInfo patch = AccessTools.Method(typeof(Patch_ConduitFlow_OnDeserialized), "ReplaceMaxMass", (Type[])null, (Type[])null);
				foreach (CodeInstruction original in instructions)
				{
					if (original.opcode == OpCodes.Ldfld && original.operand as FieldInfo == maxMass)
					{
						yield return original;
						yield return new CodeInstruction(OpCodes.Call, (object)patch);
					}
					else
					{
						yield return original;
					}
				}
			}

			internal static float ReplaceMaxMass(float original)
			{
				return float.PositiveInfinity;
			}
		}
	}

	[HarmonyPatch(typeof(GeneratedBuildings))]
	[HarmonyPatch("LoadGeneratedBuildings")]
	public static class HighPressure_GeneratedBuildings_LoadGeneratedBuildings
	{
		public static void Prefix()
		{
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0231: Unknown result type (might be due to invalid IL or missing references)
			string text = "STRINGS.BUILDINGS.PREFABS." + "HighPressureGasConduit".ToUpper();
			Strings.Add(new string[2]
			{
				text + ".NAME",
				"High Pressure Gas Conduit"
			});
			Strings.Add(new string[2]
			{
				text + ".DESC",
				"A reinforced gas pipe capable of handling high pressure flow. Composite nature of the pipe prevents gas contents from significantly changing temperature in transit."
			});
			Strings.Add(new string[2]
			{
				text + ".EFFECT",
				HighPressureGasConduitConfig.Effect
			});
			ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("HVAC"), "HighPressureGasConduit");
			text = "STRINGS.BUILDINGS.PREFABS." + "HighPressureGasConduitBridge".ToUpper();
			Strings.Add(new string[2]
			{
				text + ".NAME",
				"High Pressure Gas Conduit Bridge"
			});
			Strings.Add(new string[2]
			{
				text + ".DESC",
				"A reinforced gas pipe bridge capable of handling high pressure flow. Composite nature of the pipe prevents gas contents from significantly changing temperature in transit."
			});
			Strings.Add(new string[2]
			{
				text + ".EFFECT",
				HighPressureGasConduitBridgeConfig.Effect
			});
			ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("HVAC"), "HighPressureGasConduitBridge");
			text = "STRINGS.BUILDINGS.PREFABS." + "HighPressureLiquidConduit".ToUpper();
			Strings.Add(new string[2]
			{
				text + ".NAME",
				"High Pressure Liquid Pipe"
			});
			Strings.Add(new string[2]
			{
				text + ".DESC",
				"A reinforced liquid pipe capable of handling high pressure flow. Composite nature of the pipe prevents liquid contents from significantly changing temperature in transit."
			});
			Strings.Add(new string[2]
			{
				text + ".EFFECT",
				HighPressureLiquidConduitConfig.Effect
			});
			ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("Plumbing"), "HighPressureLiquidConduit");
			text = "STRINGS.BUILDINGS.PREFABS." + "HighPressureLiquidConduitBridge".ToUpper();
			Strings.Add(new string[2]
			{
				text + ".NAME",
				"High Pressure Liquid Conduit Bridge"
			});
			Strings.Add(new string[2]
			{
				text + ".DESC",
				"A reinforced liquid pipe bridge capable of handling high pressure flow. Composite nature of the pipe prevents liquid contents from significantly changing temperature in transit."
			});
			Strings.Add(new string[2]
			{
				text + ".EFFECT",
				HighPressureLiquidConduitBridgeConfig.Effect
			});
			ModUtil.AddBuildingToPlanScreen(HashedString.op_Implicit("Plumbing"), "HighPressureLiquidConduitBridge");
		}
	}

	[HarmonyPatch(typeof(Db))]
	[HarmonyPatch("Initialize")]
	public static class HighPressure_Db_Initialize
	{
		private static void Postfix()
		{
			((ResourceSet<Tech>)(object)Db.Get().Techs).Get("HVAC").unlockedItemIDs.Add("HighPressureGasConduit");
			((ResourceSet<Tech>)(object)Db.Get().Techs).Get("HVAC").unlockedItemIDs.Add("HighPressureGasConduitBridge");
			((ResourceSet<Tech>)(object)Db.Get().Techs).Get("LiquidTemperature").unlockedItemIDs.Add("HighPressureLiquidConduit");
			((ResourceSet<Tech>)(object)Db.Get().Techs).Get("LiquidTemperature").unlockedItemIDs.Add("HighPressureLiquidConduitBridge");
		}
	}

	[HarmonyPatch(typeof(ConduitBridge), "OnPrefabInit")]
	internal static class Patch_ConduitBridge_OnPrefabInit
	{
		internal static void Postfix(ConduitBridge __instance)
		{
			EntityTemplateExtensions.AddOrGet<Pressurized>(((Component)__instance).gameObject);
		}
	}

	[HarmonyPatch(typeof(Conduit), "OnPrefabInit")]
	internal static class Patch_Conduit_OnPrefabInit
	{
		internal static void Postfix(Conduit __instance)
		{
			EntityTemplateExtensions.AddOrGet<Pressurized>(((Component)__instance).gameObject);
		}
	}

	[HarmonyPatch(typeof(Game), "Update")]
	internal static class Patch_Game_Update
	{
		internal static void Postfix()
		{
			List<Integration.QueueDamage> queueDamages = Integration.queueDamages;
			if (queueDamages.Count <= 0)
			{
				return;
			}
			foreach (Integration.QueueDamage item in queueDamages)
			{
				Integration.DoPressureDamage(item.Receiver);
			}
			queueDamages.Clear();
		}
	}

	[HarmonyPatch(typeof(Game), "OnLoadLevel")]
	internal static class Patch_Game_OnLoad
	{
		internal static void Postfix()
		{
			Integration.ClearStaticInfo();
		}
	}

	[HarmonyPatch(typeof(ValveBase), "ConduitUpdate")]
	internal static class Patch_ValveBase_ConduitUpdate
	{
		private static FieldInfo valveBaseOutputCell;

		internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo conduitGetContents = AccessTools.Method(typeof(Conduit), "GetContents", (Type[])null, (Type[])null);
			MethodInfo overPressurePatch = AccessTools.Method(typeof(Patch_ValveBase_ConduitUpdate), "OperationalValveOverPressure", (Type[])null, (Type[])null);
			valveBaseOutputCell = AccessTools.Field(typeof(ValveBase), "outputCell");
			foreach (CodeInstruction original in instructions)
			{
				if (original.opcode == OpCodes.Call && original.operand as MethodInfo == conduitGetContents)
				{
					yield return original;
					yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
					yield return new CodeInstruction(OpCodes.Ldloc_0, (object)null);
					yield return new CodeInstruction(OpCodes.Call, (object)overPressurePatch);
				}
				else
				{
					yield return original;
				}
			}
		}

		private static ConduitContents OperationalValveOverPressure(ConduitContents contents, ValveBase valveBase, ConduitFlow flowManager)
		{
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_0086: Unknown result type (might be due to invalid IL or missing references)
			//IL_008a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			OperationalValve val;
			if (Object.op_Implicit((Object)(object)(val = (OperationalValve)(object)((valveBase is OperationalValve) ? valveBase : null))) && ((ValveBase)val).CurrentFlow > 0f)
			{
				int cell = (int)valveBaseOutputCell.GetValue(valveBase);
				GameObject obj;
				float maxCapacityWithObject = Integration.GetMaxCapacityWithObject(cell, valveBase.conduitType, out obj);
				float mass = ((ConduitContents)(ref contents)).mass;
				if (mass > maxCapacityWithObject * 2f && Random.Range(0f, 1f) < 0.33f)
				{
					Integration.DoPressureDamage(obj);
				}
			}
			return contents;
		}
	}

	[HarmonyPatch(typeof(ConduitBridge), "ConduitUpdate")]
	internal static class Patch_ConduitBridge_ConduitUpdate
	{
		private static FieldInfo bridgeOutputCell;

		internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo flowManagerGetContents = AccessTools.Method(typeof(ConduitFlow), "GetContents", (Type[])null, (Type[])null);
			MethodInfo setMaxFlowPatch = AccessTools.Method(typeof(Patch_ConduitBridge_ConduitUpdate), "SetMaxFlow", (Type[])null, (Type[])null);
			bridgeOutputCell = AccessTools.Field(typeof(ConduitBridge), "outputCell");
			foreach (CodeInstruction original in instructions)
			{
				if (original.opcode == OpCodes.Callvirt && original.operand as MethodInfo == flowManagerGetContents)
				{
					yield return original;
					yield return new CodeInstruction(OpCodes.Ldarg_0, (object)null);
					yield return new CodeInstruction(OpCodes.Ldloc_0, (object)null);
					yield return new CodeInstruction(OpCodes.Call, (object)setMaxFlowPatch);
				}
				else
				{
					yield return original;
				}
			}
		}

		private static ConduitContents SetMaxFlow(ConduitContents contents, ConduitBridge bridge, ConduitFlow manager)
		{
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_005b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0115: Unknown result type (might be due to invalid IL or missing references)
			//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_0111: Unknown result type (might be due to invalid IL or missing references)
			if (((Component)bridge).GetComponent<BuildingHP>().HitPoints == 0)
			{
				((ConduitContents)(ref contents)).RemoveMass(((ConduitContents)(ref contents)).mass);
				return contents;
			}
			int cell = (int)bridgeOutputCell.GetValue(bridge);
			GameObject obj;
			float maxCapacityWithObject = Integration.GetMaxCapacityWithObject(cell, bridge.type, out obj);
			if ((Object)(object)obj == (Object)null)
			{
				return contents;
			}
			float maxCapacity = Pressurized.GetMaxCapacity(((Component)bridge).GetComponent<Pressurized>());
			if (((ConduitContents)(ref contents)).mass > maxCapacity)
			{
				if ((double)((ConduitContents)(ref contents)).mass > (double)maxCapacity * 1.1)
				{
					Integration.DoPressureDamage(((Component)bridge).gameObject);
				}
				float mass = ((ConduitContents)(ref contents)).mass;
				float num = ((ConduitContents)(ref contents)).RemoveMass(mass - maxCapacity);
				float num2 = num / mass;
				contents.diseaseCount = (int)((float)contents.diseaseCount * num2);
			}
			if (((ConduitContents)(ref contents)).mass > maxCapacityWithObject * 2f && Random.Range(0f, 1f) < 0.33f)
			{
				Integration.DoPressureDamage(obj);
			}
			return contents;
		}
	}

	private static readonly FieldInfo maxMass = AccessTools.Field(typeof(ConduitFlow), "MaxMass");
}
