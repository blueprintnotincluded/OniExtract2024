namespace OniExtract2024
{
    // Connection port types used by buildings. Serialised as the enum name string.
    // The indicator sprite for each type is exported to ui_image/ using the names:
    //   input / output             -> gas, liquid, solid conduit ports
    //   electrical_disconnected    -> power ports
    //   logicInput / logicOutput   -> logic ports
    //   logicResetUpdate           -> logic reset/update ports
    //   logic_ribbon_all_in / out  -> logic ribbon ports
    public enum ConnectionType
    {
        PowerInput,
        PowerOutput,
        GasInput,
        GasOutput,
        LiquidInput,
        LiquidOutput,
        SolidInput,
        SolidOutput,
        LogicInput,
        LogicOutput,
        LogicRibbonInput,
        LogicRibbonOutput,
        LogicReset,
    }

    // One entry per utility port on a building.
    // offset is a cell offset from the building's bottom-left corner, pre-rotation.
    public class OutUtilityPort
    {
        public BVector2 offset;
        public ConnectionType type;
        public bool isSecondary;

        public OutUtilityPort(CellOffset offset, ConnectionType type, bool isSecondary)
        {
            this.offset = new BVector2(offset);
            this.type = type;
            this.isSecondary = isSecondary;
        }
    }
}
