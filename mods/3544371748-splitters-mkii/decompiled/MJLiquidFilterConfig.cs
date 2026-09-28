using System.Collections.Generic;
using STRINGS;
using TUNING;
using UnityEngine;

namespace MJSplittersMKII.Filters;

public class MJLiquidFilterConfig : IBuildingConfig
{
	public static string ID = "MJSplitterMKIILiquid";

	public static string DisplayName = LocString.op_Implicit(MJSplittersMKIIStrings.UI.LIQUID_DISPLAY_NAME);

	public static string Description = LocString.op_Implicit(MJSplittersMKIIStrings.UI.DESCRIPTION);

	public static string Effect = LocString.op_Implicit(MJSplittersMKIIStrings.UI.EFFECT);

	protected ConduitType ConduitType = (ConduitType)2;

	protected List<Tag> TagList = new List<Tag>(STORAGEFILTERS.LIQUIDS);

	protected HashSet<Tag> OverlayTags = OverlayScreen.LiquidVentIDs;

	protected ConduitPortInfo SecondaryPort = new ConduitPortInfo((ConduitType)2, new CellOffset(0, 0));

	protected HashedString ViewMode = LiquidConduits.ID;

	public override BuildingDef CreateBuildingDef()
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		string iD = ID;
		string text = ID.ToLowerInvariant() + "_kanim";
		float[] tIER = CONSTRUCTION_MASS_KG.TIER3;
		string[] rEFINED_METALS = MATERIALS.REFINED_METALS;
		EffectorValues tIER2 = NOISY.TIER1;
		BuildingDef val = BuildingTemplates.CreateBuildingDef(iD, 3, 1, text, 30, 10f, tIER, rEFINED_METALS, 1600f, (BuildLocationRule)0, PENALTY.TIER0, tIER2, 0.2f);
		val.RequiresPowerInput = true;
		val.EnergyConsumptionWhenActive = 240f;
		val.SelfHeatKilowattsWhenActive = 6f;
		val.ExhaustKilowattsWhenActive = 0f;
		val.InputConduitType = ConduitType;
		val.OutputConduitType = ConduitType;
		val.Floodable = false;
		val.ViewMode = ViewMode;
		val.AudioCategory = "Metal";
		val.UtilityInputOffset = new CellOffset(-1, 0);
		val.UtilityOutputOffset = new CellOffset(1, 0);
		val.PermittedRotations = (PermittedRotations)2;
		GeneratedBuildings.RegisterWithOverlay(OverlayTags, ID);
		val.AddSearchTerms(LocString.op_Implicit(SEARCH_TERMS.FILTER));
		return val;
	}

	private void AttachPort(GameObject go)
	{
		go.AddComponent<ConduitSecondaryOutput>().portInfo = SecondaryPort;
	}

	public override void DoPostConfigurePreview(BuildingDef def, GameObject go)
	{
		((IBuildingConfig)this).DoPostConfigurePreview(def, go);
		AttachPort(go);
	}

	public override void DoPostConfigureUnderConstruction(GameObject go)
	{
		((IBuildingConfig)this).DoPostConfigureUnderConstruction(go);
		AttachPort(go);
	}

	public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		go.GetComponent<KPrefabID>().AddTag(ConstraintTags.IndustrialMachinery, false);
		EntityTemplateExtensions.AddOrGet<MJFilter>(go).portInfo = SecondaryPort;
	}

	public override void DoPostConfigureComplete(GameObject go)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		EntityTemplateExtensions.AddOrGetDef<Def>(go).showWorkingStatus = true;
		go.GetComponent<KPrefabID>().AddTag(GameTags.OverlayInFrontOfConduits, false);
		Storage val = EntityTemplateExtensions.AddOrGet<Storage>(go);
		val.capacityKg = 0f;
		val.showInUI = true;
		val.showDescriptor = true;
		val.storageFilters = TagList;
		val.allowItemRemoval = false;
		val.onlyTransferFromLowerPriority = false;
		val.allowSettingOnlyFetchMarkedItems = false;
		val.allowClearable = false;
		val.fetchCategory = (FetchCategory)0;
		val.showInUI = true;
		val.allowUIItemRemoval = false;
		EntityTemplateExtensions.AddOrGet<TreeFilterable>(go);
	}
}
