using System;
using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
using TUNING;
using UnityEngine;

namespace PeterHan.AirlockDoor;

public sealed class AirlockDoorConfig : IBuildingConfig
{
	public const string ID = "PAirlockDoor";

	internal static float ENERGY_CAPACITY = 10000f;

	internal static float ENERGY_PER_USE = 1500f;

	internal static PBuilding AirlockDoorTemplate;

	internal static PBuilding CreateBuilding()
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		PBuilding obj = new PBuilding("PAirlockDoor", LocString.op_Implicit(AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.NAME))
		{
			AddAfter = "PressureDoor",
			Animation = "airlock_door_kanim",
			Category = HashedString.op_Implicit("Base"),
			ConstructionTime = 60f,
			Decor = PENALTY.TIER1,
			Description = null,
			EffectText = null,
			Entombs = false,
			Floods = false,
			Height = 2,
			HP = 30,
			LogicIO = { Port.InputPort(AirlockDoor.OPEN_CLOSE_PORT_ID, CellOffset.none, LocString.op_Implicit(AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.LOGIC_OPEN), LocString.op_Implicit(AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.LOGIC_OPEN_ACTIVE), LocString.op_Implicit(AirlockDoorStrings.BUILDINGS.PREFABS.PAIRLOCKDOOR.LOGIC_OPEN_INACTIVE), false, false) },
			Ingredients = 
			{
				new BuildIngredient("RefinedMetal", 4)
			},
			Placement = (BuildLocationRule)6,
			PowerInput = new PowerRequirement(120f, new CellOffset(0, 0)),
			RotateMode = (PermittedRotations)0,
			SceneLayer = (SceneLayer)16,
			SubCategory = "doors",
			Tech = "ImprovedGasPiping",
			Width = 3
		};
		AirlockDoorTemplate = obj;
		return obj;
	}

	public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		((IBuildingConfig)this).ConfigureBuildingTemplate(go, prefab_tag);
		AirlockDoorTemplate?.ConfigureBuildingTemplate(go);
	}

	public override BuildingDef CreateBuildingDef()
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		LocString.CreateLocStringKeys(typeof(AirlockDoorStrings.BUILDING), "STRINGS.");
		LocString.CreateLocStringKeys(typeof(AirlockDoorStrings.BUILDINGS), "STRINGS.");
		if (AirlockDoorTemplate == null)
		{
			throw new ArgumentNullException("AirlockDoorTemplate");
		}
		BuildingDef obj = AirlockDoorTemplate.CreateDef();
		obj.ForegroundLayer = (SceneLayer)30;
		obj.IsFoundation = true;
		obj.PreventIdleTraversalPastBuilding = true;
		obj.ThermalConductivity = 0.2f;
		obj.TileLayer = PGameUtils.GetObjectLayer("FoundationTile", (ObjectLayer)9);
		return obj;
	}

	public override void DoPostConfigureUnderConstruction(GameObject go)
	{
		AirlockDoorTemplate?.CreateLogicPorts(go);
	}

	public override void DoPostConfigurePreview(BuildingDef def, GameObject go)
	{
		AirlockDoorTemplate?.CreateLogicPorts(go);
	}

	public override void DoPostConfigureComplete(GameObject go)
	{
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		AirlockDoorTemplate?.DoPostConfigureComplete(go);
		AirlockDoorTemplate?.CreateLogicPorts(go);
		AirlockDoor airlockDoor = EntityTemplateExtensions.AddOrGet<AirlockDoor>(go);
		airlockDoor.EnergyCapacity = ENERGY_CAPACITY;
		airlockDoor.EnergyPerUse = ENERGY_PER_USE;
		SimCellOccupier obj = EntityTemplateExtensions.AddOrGet<SimCellOccupier>(go);
		obj.doReplaceElement = true;
		obj.notifyOnMelt = true;
		EntityTemplateExtensions.AddOrGet<TileTemperature>(go);
		EntityTemplateExtensions.AddOrGet<AccessControl>(go).controlEnabled = true;
		EntityTemplateExtensions.AddOrGet<KBoxCollider2D>(go);
		EntityTemplateExtensions.AddOrGet<BuildingHP>(go).destroyOnDamaged = true;
		Prioritizable.AddRef(go);
		EntityTemplateExtensions.AddOrGet<CopyBuildingSettings>(go).copyGroupTag = GameTags.Door;
		EntityTemplateExtensions.AddOrGet<Workable>(go).workTime = 3f;
		BuildingEnabledButton val = default(BuildingEnabledButton);
		if (go.TryGetComponent<BuildingEnabledButton>(ref val))
		{
			Object.DestroyImmediate((Object)(object)val);
		}
		KBatchedAnimController val2 = default(KBatchedAnimController);
		if (go.TryGetComponent<KBatchedAnimController>(ref val2))
		{
			((KAnimControllerBase)val2).initialAnim = "closed";
		}
	}
}
