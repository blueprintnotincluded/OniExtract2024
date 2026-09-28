namespace OniExtract2024
{
    // One hardpoint another building can attach to. `offset` follows the utilities[].offset
    // convention: a cell offset from this building's origin cell, pre-rotation. `tag` is the
    // attachable type the hardpoint accepts (GameTags name, e.g. "Rocket"); a building whose
    // `attachableTo` equals this tag may sit with its `attachablePosition` cell on this cell.
    //
    // Sources:
    //   - BuildingAttachPoint.points on the prefab (rocket modules: engines, tanks, cargo bays,
    //     habitats... every module that can carry another module above it).
    //   - LaunchPad.baseModulePosition — the pad has no BuildingAttachPoint component; the game
    //     places a rocket's bottom module at pad-origin + baseModulePosition
    //     (LaunchPad.AddBaseModule). Emitted here in the same shape so the website can treat
    //     "module on pad" and "module on module" with one rule. See WEBSITE_ROCKET_MODULES.md.
    public class OutAttachPoint
    {
        public BVector2 offset;
        public string tag;

        public OutAttachPoint(CellOffset offset, string tag)
        {
            this.offset = new BVector2(offset);
            this.tag = tag;
        }
    }
}
