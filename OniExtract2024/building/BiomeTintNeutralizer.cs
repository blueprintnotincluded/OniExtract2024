// Neutralises the per-biome zone tint so building renders come out in true colour
// instead of darkened/colour-shifted by whatever biome a building happens to sit in.
//
// ONI tints world contents (including building kanims) by multiplying against a
// per-zone colour sampled from a texture that SubworldZoneRenderData builds from its
// zoneColours array. Forcing every entry to white makes that multiply the identity,
// so kanims render at their authored colour. This is the same trick Sgt_Imalas's
// AnimExportTool uses for clean icon export:
// https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods/blob/master/AnimExportTool/Patches.cs#L332-L349
//
// Why global (not gated around our renders): the zone texture is generated once at
// world init, not per frame, so it must already be white by the time the exporter or
// inspector renders. A render-time toggle wouldn't help. The only side effect is that
// the live biome overlay also shows white while this dev/export mod is enabled, which
// is acceptable. We white ALL zones (Sgt_Imalas skipped index 7 / space) because the
// temp building can spawn in any biome — whiting everything guarantees no tint
// regardless of the spawn cell.

using HarmonyLib;
using UnityEngine;

namespace OniExtract2024.building
{
    [HarmonyPatch(typeof(SubworldZoneRenderData), "GenerateTexture")]
    public static class SubworldZoneRenderData_GenerateTexture_Patch
    {
        // zoneColours is a public Color32[] (NOT Color[] — getting that wrong throws an
        // InvalidCastException inside SubworldZoneRenderData.OnSpawn and breaks world render).
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        public static void Prefix(SubworldZoneRenderData __instance)
        {
            var colours = __instance.zoneColours;
            if (colours == null) return;
            for (int i = 0; i < colours.Length; i++)
                colours[i] = White;
        }
    }
}
