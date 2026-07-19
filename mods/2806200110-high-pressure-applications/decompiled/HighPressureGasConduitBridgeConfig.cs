using TUNING;
using UnityEngine;

namespace High_Pressure_Applications.BuildingConfigs;

public class HighPressureGasConduitBridgeConfig : IBuildingConfig
{
	public const string Id = "HighPressureGasConduitBridge";

	public const string DisplayName = "High Pressure Gas Conduit Bridge";

	public const string Description = "A reinforced gas pipe bridge capable of handling high pressure flow. Composite nature of the pipe prevents gas contents from significantly changing temperature in transit.";

	public static string Effect = "Runs one High Pressure Gas Pipe section over another without joining them.\n\nCan be run through wall and floor tile.";

	private const ConduitType CONDUIT_TYPE = (ConduitType)1;

	public override BuildingDef CreateBuildingDef()
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		string text = "HighPressureGasConduitBridge";
		int num = 3;
		int num2 = 1;
		string text2 = "pressure_gas_bridge_kanim";
		int num3 = 10;
		float num4 = 45f;
		float[] array = new float[2] { 10f, 5f };
		string[] array2 = new string[2]
		{
			((object)(SimHashes)(-899253461)/*cast due to constrained. prefix*/).ToString(),
			"Plastic"
		};
		float num5 = 1600f;
		BuildLocationRule val = (BuildLocationRule)8;
		EffectorValues nONE = NOISE_POLLUTION.NONE;
		BuildingDef val2 = BuildingTemplates.CreateBuildingDef(text, num, num2, text2, num3, num4, array, array2, num5, val, PENALTY.TIER1, nONE, 0.2f);
		val2.ObjectLayer = (ObjectLayer)15;
		val2.SceneLayer = (SceneLayer)4;
		val2.InputConduitType = (ConduitType)1;
		val2.OutputConduitType = (ConduitType)1;
		val2.Floodable = false;
		val2.Entombable = false;
		val2.Overheatable = false;
		val2.ViewMode = GasConduits.ID;
		val2.AudioCategory = "Metal";
		val2.AudioSize = "small";
		val2.BaseTimeUntilRepair = -1f;
		val2.PermittedRotations = (PermittedRotations)2;
		val2.UtilityInputOffset = new CellOffset(-1, 0);
		val2.UtilityOutputOffset = new CellOffset(1, 0);
		val2.ThermalConductivity = 1E-05f;
		GeneratedBuildings.RegisterWithOverlay(OverlayScreen.GasVentIDs, ((Def)val2).PrefabID);
		return val2;
	}

	public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		GeneratedBuildings.MakeBuildingAlwaysOperational(go);
		BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation), prefab_tag);
		ConduitBridge val = EntityTemplateExtensions.AddOrGet<ConduitBridge>(go);
		val.type = (ConduitType)1;
	}

	public override void DoPostConfigureComplete(GameObject go)
	{
		Object.DestroyImmediate((Object)(object)go.GetComponent<RequireInputs>());
		Object.DestroyImmediate((Object)(object)go.GetComponent<ConduitConsumer>());
		Object.DestroyImmediate((Object)(object)go.GetComponent<ConduitDispenser>());
	}
}
