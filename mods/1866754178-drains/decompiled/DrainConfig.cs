using PeterHan.PLib.Options;
using TUNING;
using UnityEngine;

namespace Drains;

internal class DrainConfig : IBuildingConfig
{
	public const string Id = "Drain";

	public const string DisplayName = "Drain";

	public const string Description = "9 out of 10 plumbers recommend Drain® for its uncloggability. Our new Drain® companion product, Clog-Be-Gone™, will hit shelves soon. Now less likely to vaporize unexpectedly during normal operation!";

	public static string Effect = "Slowly drains liquids into a pipe.";

	public static float[] MASS = CONSTRUCTION_MASS_KG.TIER2;

	public override BuildingDef CreateBuildingDef()
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		BuildingDef val = BuildingTemplates.CreateBuildingDef("Drain", 1, 1, SingletonOptions<DrainOptions>.Instance.UseSolidDrain ? "solidDrain_kanim" : "drain_kanim", 100, 30f, MASS, MATERIALS.ALL_METALS, 1600f, (BuildLocationRule)6, PENALTY.TIER0, NOISE_POLLUTION.NONE, 0.2f);
		BuildingTemplates.CreateFoundationTileDef(val);
		val.UseStructureTemperature = false;
		val.Floodable = false;
		val.Entombable = false;
		val.Overheatable = false;
		val.UseStructureTemperature = false;
		val.AudioCategory = "Metal";
		val.AudioSize = "small";
		val.SceneLayer = (SceneLayer)30;
		val.IsFoundation = true;
		val.EnergyConsumptionWhenActive = 0f;
		val.ExhaustKilowattsWhenActive = 0f;
		val.SelfHeatKilowattsWhenActive = 0f;
		val.UtilityOutputOffset = new CellOffset(0, 0);
		val.OutputConduitType = (ConduitType)2;
		val.ViewMode = LiquidConduits.ID;
		val.PermittedRotations = (PermittedRotations)0;
		val.ObjectLayer = (ObjectLayer)1;
		val.AudioSize = "small";
		if (SingletonOptions<DrainOptions>.Instance.UseSolidDrain)
		{
			val.isSolidTile = true;
		}
		return val;
	}

	public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		GeneratedBuildings.MakeBuildingAlwaysOperational(go);
		BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation), prefab_tag);
		SimCellOccupier val = EntityTemplateExtensions.AddOrGet<SimCellOccupier>(go);
		if (SingletonOptions<DrainOptions>.Instance.UseSolidDrain)
		{
			val.notifyOnMelt = true;
			val.doReplaceElement = true;
		}
		else
		{
			val.doReplaceElement = false;
		}
		EntityTemplateExtensions.AddOrGet<TileTemperature>(go);
		EntityTemplateExtensions.AddOrGet<BuildingHP>(go).destroyOnDamaged = true;
		EntityTemplateExtensions.AddOrGet<Drain>(go);
	}

	public override void DoPostConfigureComplete(GameObject go)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		BuildingTemplates.DoPostConfigure(go);
		GeneratedBuildings.RemoveLoopingSounds(go);
		go.GetComponent<KPrefabID>().AddTag(GameTags.FloorTiles, false);
		EntityTemplateExtensions.AddOrGet<Storage>(go).capacityKg = 1f;
		ElementConsumer val = EntityTemplateExtensions.AddOrGet<ElementConsumer>(go);
		val.configuration = (Configuration)1;
		val.consumptionRate = SingletonOptions<DrainOptions>.Instance.FlowRate;
		val.storeOnConsume = true;
		val.showInStatusPanel = false;
		val.consumptionRadius = 1;
		ConduitDispenser obj = EntityTemplateExtensions.AddOrGet<ConduitDispenser>(go);
		obj.conduitType = (ConduitType)2;
		obj.alwaysDispense = true;
		obj.elementFilter = null;
		((KAnimControllerBase)go.GetComponent<KBatchedAnimController>()).initialAnim = "built";
		if (SingletonOptions<DrainOptions>.Instance.UseSolidDrain)
		{
			val.sampleCellOffset = new Vector3(0f, 1f);
		}
	}
}
