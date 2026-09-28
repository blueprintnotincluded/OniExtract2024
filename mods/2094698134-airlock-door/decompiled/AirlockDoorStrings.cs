using STRINGS;

namespace PeterHan.AirlockDoor;

public static class AirlockDoorStrings
{
	public static class BUILDING
	{
		public static class STATUSITEMS
		{
			public static class AIRLOCKSTOREDCHARGE
			{
				public static LocString NAME = LocString.op_Implicit("Charge Available: {0}/{1}");

				public static LocString TOOLTIP = LocString.op_Implicit("This Airlock has <b>{0}</b> of stored " + UI.PRE_KEYWORD + "Power" + UI.PST_KEYWORD + "\n\nIt consumes up to " + UI.FormatAsNegativeRate("{2}") + " per use");
			}
		}
	}

	public static class BUILDINGS
	{
		public static class PREFABS
		{
			public static class PAIRLOCKDOOR
			{
				public static LocString NAME = LocString.op_Implicit(UI.FormatAsLink("Airlock Door", "PAirlockDoor"));

				public static LocString DESC = LocString.op_Implicit("Sucking Duplicants that have nowhere to go into space through " + UI.FormatAsLink("Mechanized Airlocks", "PressureDoor") + " is poor taste. Airlock Doors now allow Duplicants safe passage without the loss of any of their mysterious fluids.");

				public static LocString EFFECT = LocString.op_Implicit("Blocks " + UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID") + " and " + UI.FormatAsLink("Gas", "ELEMENTS_GAS") + " flow, even while Duplicants are passing.\n\nWill not allow passage when no " + UI.FormatAsLink("Power", "POWER") + " is available.\n\n" + UI.FormatAsLink("Critters", "CRITTERS") + " can never pass through this door.");

				public static LocString LOGIC_OPEN = LocString.op_Implicit("Unlock/Lock");

				public static LocString LOGIC_OPEN_ACTIVE = LocString.op_Implicit(UI.FormatAsAutomationState("Green Signal", (AutomationState)0) + ": Unlock door");

				public static LocString LOGIC_OPEN_INACTIVE = LocString.op_Implicit(UI.FormatAsAutomationState("Red Signal", (AutomationState)1) + ": Lock door");
			}

			public static class PAIRLOCKDOORINSULATED
			{
				public static LocString NAME = LocString.op_Implicit(UI.FormatAsLink("Insulated Airlock Door", "PAirlockDoorInsulated"));

				public static LocString DESC = LocString.op_Implicit("Sucking Duplicants that have nowhere to go into space through " + UI.FormatAsLink("Insulated Doors", "InsulatedDoor") + " is poor taste. Insulated Airlock Doors now allow Duplicants safe passage without the loss of any of their mysterious fluids.");

				public static LocString EFFECT = LocString.op_Implicit("Blocks " + UI.FormatAsLink("Liquid", "ELEMENTS_LIQUID") + " and " + UI.FormatAsLink("Gas", "ELEMENTS_GAS") + " flow, even while Duplicants are passing.\n\nWill not allow passage when no " + UI.FormatAsLink("Power", "POWER") + " is available.\n\n" + UI.FormatAsLink("Critters", "CRITTERS") + " can never pass through this door.\n\n" + UI.FormatAsLink("Heat", "HEAT") + " flow through this door is greatly reduced.");
			}
		}
	}
}
