using System;
using System.IO;
using OniExtract2024.utils;
using UnityEngine;
using Bits = Rendering.BlockTileRenderer.Bits;

namespace OniExtract2024.connection
{
    /// <summary>
    /// Extracts the 16 four-directional connection-state sprites for isKAnimTile
    /// buildings (Tile, MeshTile, GlassTile, ...).
    ///
    /// Unlike utilities, kanim tiles are NOT drawn from per-connection kanim
    /// animations. They are drawn by <c>Rendering.BlockTileRenderer</c>, which for
    /// each cell selects the <b>first</b> <c>BuildingDef.BlockTileAtlas</c> item whose
    /// required/forbidden connection-bit pattern matches the neighbour bitmask and
    /// draws a single cell-sized quad from its <c>uvBox</c> (see
    /// <c>BlockTileRenderer.RenderInfo.Rebuild</c> - it <c>break</c>s on the first
    /// match, it does not overlay). Each tile type has its own atlas (tiles_glass,
    /// tiles_metal, ...), so the extracted sprites are per-building distinct.
    ///
    /// Crucially we reproduce the game's per-cell geometry rather than cropping the
    /// raw atlas rect, because that geometry is what makes tiles tile seamlessly
    /// (see <c>BlockTileRenderer.AddVertexInfo</c>):
    ///   - a CONNECTED side trims the UV inward by 1/32 and keeps the quad edge flush
    ///     at the cell boundary (so neighbours meet with no seam);
    ///   - a DISCONNECTED side overhangs the cell by 0.25 and shows the rounded border.
    /// Every state is resampled into the same cell-anchored frame (a 1.5x1.5-cell
    /// canvas with the cell centred), so the website can place each sprite on its grid
    /// cell and adjacent tiles join edge-to-edge exactly as in game.
    ///
    /// NOTE: the decor "tops" layer (DecorBlockTileInfo - top-surface highlights and
    /// corner embellishments drawn by BlockTileRenderer.DecorRenderInfo) is
    /// deliberately NOT reproduced. Its placement depends on the full 8-neighbour
    /// (incl. diagonal) state, which the website's 4-bit/16-state model can't encode,
    /// so it would mismatch at junctions. Left as a future enhancement; a worked
    /// implementation exists in git history (the decor-rasteriser commit) if revisited.
    /// </summary>
    public static class TileConnectionExtractor
    {
        // BlockTileRenderer.RenderInfo: trimUVSize = 1/32 (atlas UV) on both axes.
        private const float UvTrim = 1f / 32f;
        // BlockTileRenderer.AddVertexInfo: a disconnected side overhangs the cell by
        // world_trim_size (0.25 cell).
        private const float Overhang = 0.25f;
        // Output frame spans the cell plus the overhang margin on every side.
        private const float FrameCells = 1f + 2f * Overhang; // 1.5

        // Website bitmask -> game Bits (orthogonal only; diagonals stay 0).
        // Website encoding: left=1, right=2, up=4, down=8.
        private static Bits ToConnectionBits(int mask)
        {
            Bits bits = (Bits)0;
            if ((mask & 1) != 0) bits |= Bits.Left;
            if ((mask & 2) != 0) bits |= Bits.Right;
            if ((mask & 4) != 0) bits |= Bits.Up;
            if ((mask & 8) != 0) bits |= Bits.Down;
            return bits;
        }

        /// <returns>number of PNG files written for this building.</returns>
        public static int Export(BuildingDef def)
        {
            TextureAtlas atlas = def.BlockTileAtlas;
            if (atlas == null || atlas.items == null || atlas.items.Length == 0 || atlas.texture == null)
                return 0;

            // Parse required/forbidden connection bits from each atlas item name.
            // Mirrors Rendering.BlockTileRenderer.RenderInfo's ctor: the name ends in
            // "<required:8><forbidden:8>" preceded by a separator, then a 4+8 suffix.
            int forbiddenStart = atlas.items[0].name.Length - 4 - 8;
            int requiredStart = forbiddenStart - 1 - 8;
            if (requiredStart < 0)
            {
                Debug.LogWarning("OniExtract: unexpected atlas item name for " + def.PrefabID + " (" + atlas.items[0].name + "); skipped tile.");
                return 0;
            }

            int count = atlas.items.Length;
            Bits[] required = new Bits[count];
            Bits[] forbidden = new Bits[count];
            try
            {
                for (int k = 0; k < count; k++)
                {
                    string name = atlas.items[k].name;
                    required[k] = (Bits)Convert.ToInt32(name.Substring(requiredStart, 8), 2);
                    forbidden[k] = (Bits)Convert.ToInt32(name.Substring(forbiddenStart, 8), 2);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("OniExtract: failed to parse atlas bits for " + def.PrefabID + ": " + e.Message);
                return 0;
            }

            Texture2D readable = AnimTool.GetReadableCopy(atlas.texture);
            if (readable == null)
                return 0;
            readable.wrapMode = TextureWrapMode.Clamp; // avoid bilinear bleed at item edges

            // Pixels-per-cell derived from native atlas resolution: an item's full
            // (untrimmed) uvBox maps to the fully-disconnected 1.5-cell quad, so the
            // cell is two-thirds of the item's pixel width. Keeps output near native
            // res (no upscaling) and consistent across all 16 states.
            Vector4 box0 = atlas.items[0].uvBox;
            float fullPxW = Mathf.Abs(box0.z - box0.x) * readable.width;
            int cellPx = Mathf.Max(1, Mathf.RoundToInt(fullPxW / FrameCells));
            int frame = Mathf.RoundToInt(FrameCells * cellPx);

            string dir = ExportConnectionSprites.OutputDir(def.PrefabID);
            Directory.CreateDirectory(dir);

            int written = 0;
            for (int mask = 0; mask <= 15; mask++)
            {
                Bits bits = ToConnectionBits(mask);
                Texture2D composite = ComposeState(readable, atlas, required, forbidden, bits, cellPx, frame);
                if (composite == null)
                    continue;
                File.WriteAllBytes(Path.Combine(dir, mask + ".png"), composite.EncodeToPNG());
                UnityEngine.Object.Destroy(composite);
                written++;
            }

            UnityEngine.Object.Destroy(readable);

            if (written == 0)
                Debug.LogWarning("OniExtract: tile " + def.PrefabID + " matched no atlas items (atlas=" +
                    (atlas.texture != null ? atlas.texture.name : "?") + ", items=" + count +
                    ", firstName=" + atlas.items[0].name + ").");
            else
                Debug.Log("OniExtract: tile " + def.PrefabID + " -> " + written + " sprites (" + frame + "px).");
            return written;
        }

        // Resamples the matched atlas item into a cell-anchored frame, applying the
        // game's connected-edge trim and disconnected-edge overhang so states share a
        // consistent cell position and tile seamlessly.
        private static Texture2D ComposeState(Texture2D atlasTex, TextureAtlas atlas, Bits[] required, Bits[] forbidden,
            Bits bits, int cellPx, int frame)
        {
            // Base layer: the game draws exactly ONE item per cell - the first whose
            // pattern matches (RenderInfo.Rebuild breaks on first match).
            int match = -1;
            for (int k = 0; k < atlas.items.Length; k++)
            {
                bool requiredMet = (required[k] & bits) == required[k];
                bool forbiddenHit = (forbidden[k] & bits) != 0;
                if (requiredMet && !forbiddenHit)
                {
                    match = k;
                    break;
                }
            }
            if (match < 0)
                return null;

            // Reproduce BlockTileRenderer.AddVertexInfo: world quad <-> (trimmed) uv quad.
            //   world corner A (wx0,wy0) <-> uv corner A (iAx,iAy)
            //   world corner B (wx1,wy1) <-> uv corner B (iBx,iBy)
            // Cell occupies world [0,1]; the frame covers world [-0.25, 1.25].
            Vector4 uvBox = atlas.items[match].uvBox;
            float wx0 = 0f, wy0 = 0f, wx1 = 1f, wy1 = 1f;
            float iAx = uvBox.x, iAy = uvBox.w;
            float iBx = uvBox.z, iBy = uvBox.y;
            if ((bits & Bits.Left) == 0) wx0 -= Overhang; else iAx += UvTrim;
            if ((bits & Bits.Right) == 0) wx1 += Overhang; else iBx -= UvTrim;
            if ((bits & Bits.Up) == 0) wy1 += Overhang; else iBy -= UvTrim;
            if ((bits & Bits.Down) == 0) wy0 -= Overhang; else iAy += UvTrim;

            float dwx = wx1 - wx0, dwy = wy1 - wy0;
            if (dwx <= 0f || dwy <= 0f)
                return null;

            Color[] outPx = new Color[frame * frame];
            float invCell = 1f / cellPx;
            for (int oy = 0; oy < frame; oy++)
            {
                // Frame origin is world -0.25; rows are bottom-up to match SetPixels.
                float wy = -Overhang + (oy + 0.5f) * invCell;
                bool yIn = wy >= wy0 && wy <= wy1;
                float vfrac = yIn ? (wy - wy0) / dwy : 0f;
                for (int ox = 0; ox < frame; ox++)
                {
                    float wx = -Overhang + (ox + 0.5f) * invCell;
                    if (!yIn || wx < wx0 || wx > wx1)
                        continue; // outside the cell quad -> transparent
                    float ufrac = (wx - wx0) / dwx;
                    float u = iAx + ufrac * (iBx - iAx);
                    float v = iAy + vfrac * (iBy - iAy);
                    outPx[oy * frame + ox] = atlasTex.GetPixelBilinear(u, v);
                }
            }

            Texture2D outTex = new Texture2D(frame, frame);
            outTex.SetPixels(outPx);
            outTex.Apply();
            return outTex;
        }
    }
}
