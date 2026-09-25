using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OniExtract2024
{
    // Door component. Present on every door-type building (Door, ManualPressureDoor,
    // PressureDoor, BunkerDoor, plus modded doors that reuse the component). The blueprint
    // `buildingData.Door.requestedState` setting (Auto / Opened / Locked) applies to these.
    public class OutDoor
    {
        // Door.DoorType enum name: "Pressure" | "ManualPressure" | "Internal" | "Sealed".
        [JsonConverter(typeof(StringEnumConverter))]
        public Door.DoorType doorType;
        // Whether the in-game side screen offers the open/auto/lock controls.
        public bool hasComplexUserControls;
        // Whether the door may be driven by automation / auto-open logic.
        public bool allowAutoControl;

        public OutDoor(Door door)
        {
            this.doorType = door.doorType;
            this.hasComplexUserControls = door.hasComplexUserControls;
            this.allowAutoControl = door.allowAutoControl;
        }
    }
}
