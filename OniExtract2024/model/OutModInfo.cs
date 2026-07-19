using System.Collections.Generic;

namespace OniExtract2024
{
    // One enabled mod that contributed buildings to this export. `id` matches the
    // per-building `mod` field (Steam workshop id, or local-mod folder name for
    // distribution_platform=Local). Serialized as the root `mods` roster of building.json.
    public class OutModInfo
    {
        public string id;
        public string title;
        public List<string> buildings = new List<string>();
    }
}
