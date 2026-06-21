using System;
using System.Collections.Generic;
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
    /// draws a single quad from its <c>uvBox</c> (see
    /// <c>BlockTileRenderer.RenderInfo.Rebuild</c> - it <c>break</c>s on the first
    /// match, it does not overlay). Each tile type has its own atlas (tiles_glass,
    /// tiles_metal, ...), so the extracted sprites are per-building distinct.
    ///
    /// On top of that base layer the game draws a <b>decor</b> mesh
    /// (<c>BlockTileRenderer.DecorRenderInfo</c>) sampled from
    /// <c>BuildingDef.DecorBlockTileInfo</c> - the "tops" highlights (grass/snow/metal
    /// edge trim). Each decor variant carries its own triangle mesh
    /// (<c>TextureAtlas.Item.vertices/uvs/indices</c>) rather than an axis-aligned
    /// crop, so we reproduce it by rasterising those triangles in cell-local world
    /// space, mapped onto the base sprite via the matched item's own uv-&gt;world
    /// correspondence.
    ///
    /// All of this is reproduced offscreen with no camera and no placed building -
    /// only the (loaded) texture atlases.
    /// </summary>
    public static class TileConnectionExtractor
    {
        // BlockTileRenderer.RenderInfo: trimUVSize = 1/32 on both axes.
        private const float UvTrim = 1f / 32f;
        // BlockTileRenderer.AddVertexInfo: a disconnected side overhangs the cell by
        // world_trim_size (0.25 cell).
        private const float Overhang = 0.25f;

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

            // Optional decor "tops" layer. PostProcess() resolves each variant's
            // atlasItem mesh (BuildingDef already calls it at load, but it is
            // idempotent so we call it defensively).
            BlockTileDecorInfo decorInfo = def.DecorBlockTileInfo;
            Texture2D decorReadable = null;
            if (decorInfo != null && decorInfo.atlas != null && decorInfo.atlas.texture != null && decorInfo.decor != null)
            {
                decorInfo.PostProcess();
                decorReadable = AnimTool.GetReadableCopy(decorInfo.atlas.texture);
            }

            string dir = ExportConnectionSprites.OutputDir(def.PrefabID);
            Directory.CreateDirectory(dir);

            int written = 0;
            for (int mask = 0; mask <= 15; mask++)
            {
                Bits bits = ToConnectionBits(mask);
                Texture2D composite = ComposeState(readable, atlas, required, forbidden, bits, decorInfo, decorReadable);
                if (composite == null)
                    continue;
                File.WriteAllBytes(Path.Combine(dir, mask + ".png"), composite.EncodeToPNG());
                UnityEngine.Object.Destroy(composite);
                written++;
            }

            if (decorReadable != null)
                UnityEngine.Object.Destroy(decorReadable);
            UnityEngine.Object.Destroy(readable);

            if (written == 0)
                Debug.LogWarning("OniExtract: tile " + def.PrefabID + " matched no atlas items (atlas=" +
                    (atlas.texture != null ? atlas.texture.name : "?") + ", items=" + count +
                    ", firstName=" + atlas.items[0].name + ").");
            else
                Debug.Log("OniExtract: tile " + def.PrefabID + " -> " + written + " sprites" +
                    (decorReadable != null ? " (with decor)" : "") + ".");
            return written;
        }

        private static Texture2D ComposeState(Texture2D atlasTex, TextureAtlas atlas, Bits[] required, Bits[] forbidden, Bits bits,
            BlockTileDecorInfo decorInfo, Texture2D decorTex)
        {
            // Base layer: the game draws exactly ONE item per cell - the first whose
            // pattern matches (BlockTileRenderer.RenderInfo.Rebuild breaks on first
            // match), so we do the same rather than overlaying every match.
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

            TextureAtlas.Item item = atlas.items[match];

            // uvBox corners are paired (x,w) <-> (z,y) by BlockTileRenderer, so the
            // V extents may be stored in either order. Normalise with min/max.
            Vector4 uv = item.uvBox;
            float uMin = Mathf.Min(uv.x, uv.z);
            float uMax = Mathf.Max(uv.x, uv.z);
            float vMin = Mathf.Min(uv.y, uv.w);
            float vMax = Mathf.Max(uv.y, uv.w);
            int px = Mathf.RoundToInt(uMin * atlasTex.width);
            int py = Mathf.RoundToInt(vMin * atlasTex.height);
            int pw = Mathf.RoundToInt((uMax - uMin) * atlasTex.width);
            int ph = Mathf.RoundToInt((vMax - vMin) * atlasTex.height);
            if (pw <= 0 || ph <= 0)
                return null;
            px = Mathf.Clamp(px, 0, atlasTex.width - 1);
            py = Mathf.Clamp(py, 0, atlasTex.height - 1);
            pw = Mathf.Clamp(pw, 1, atlasTex.width - px);
            ph = Mathf.Clamp(ph, 1, atlasTex.height - py);

            Color[] canvas = atlasTex.GetPixels(px, py, pw, ph);
            int cw = pw, ch = ph;

            if (decorInfo != null && decorTex != null)
                RasterizeDecor(canvas, cw, ch, item, uMin, uMax, vMin, vMax, bits, decorInfo, decorTex);

            Texture2D outTex = new Texture2D(cw, ch);
            outTex.SetPixels(canvas);
            outTex.Apply();
            return outTex;
        }

        // Overlays the decor "tops" mesh onto the already-cropped base canvas.
        // Reproduces BlockTileRenderer.DecorRenderInfo for a single cell at origin.
        private static void RasterizeDecor(Color[] canvas, int cw, int ch, TextureAtlas.Item baseItem,
            float uMin, float uMax, float vMin, float vMax, Bits bits, BlockTileDecorInfo decorInfo, Texture2D decorTex)
        {
            // Recover the base quad's uv<->world correspondence exactly as
            // BlockTileRenderer.AddVertexInfo builds it, so decor world coordinates
            // (which DecorRenderInfo emits in the same cell space) map onto the crop.
            //   world corner A (wx0,wy0) <-> uv corner A (iAx,iAy)
            //   world corner B (wx1,wy1) <-> uv corner B (iBx,iBy)
            float wx0 = 0f, wy0 = 0f, wx1 = 1f, wy1 = 1f;
            float iAx = baseItem.uvBox.x, iAy = baseItem.uvBox.w;
            float iBx = baseItem.uvBox.z, iBy = baseItem.uvBox.y;
            if ((bits & Bits.Left) == 0) wx0 -= Overhang; else iAx += UvTrim;
            if ((bits & Bits.Right) == 0) wx1 += Overhang; else iBx -= UvTrim;
            if ((bits & Bits.Up) == 0) wy1 += Overhang; else iBy -= UvTrim;
            if ((bits & Bits.Down) == 0) wy0 -= Overhang; else iAy += UvTrim;

            // Guard degenerate denominators (would only happen on a malformed atlas).
            float duvx = iBx - iAx, duvy = iBy - iAy;
            if (Mathf.Abs(duvx) < 1e-9f || Mathf.Abs(duvy) < 1e-9f) return;
            float spanU = uMax - uMin, spanV = vMax - vMin;
            if (spanU < 1e-9f || spanV < 1e-9f) return;

            // world (wx,wy) -> canvas pixel (col, row; row 0 = bottom, matching GetPixels).
            // wx -> u: u = iAx + (wx-wx0)*(iBx-iAx)/(wx1-wx0); then col = (u-uMin)/spanU * cw.
            float wxScale = (iBx - iAx) / (wx1 - wx0);
            float wyScale = (iBy - iAy) / (wy1 - wy0);

            // Collect matching decors, drawn in ascending sortOrder (later = on top),
            // mirroring DecorRenderInfo.Rebuild's triangle sort.
            var matches = new List<int>();
            for (int i = 0; i < decorInfo.decor.Length; i++)
            {
                BlockTileDecorInfo.Decor decor = decorInfo.decor[i];
                if (decor.variants == null || decor.variants.Length == 0) continue;
                bool requiredMet = (bits & decor.requiredConnections) == decor.requiredConnections;
                bool forbiddenHit = (bits & decor.forbiddenConnections) != 0;
                if (requiredMet && !forbiddenHit) matches.Add(i);
            }
            matches.Sort((a, b) => decorInfo.decor[a].sortOrder.CompareTo(decorInfo.decor[b].sortOrder));

            foreach (int i in matches)
            {
                BlockTileDecorInfo.Decor decor = decorInfo.decor[i];

                // Variant selection mirrors DecorRenderInfo.AddDecor at cell (0,0):
                // a per-cell simplex sample gates appearance and picks the variant.
                float n = PerlinSimplexNoise.noise((float)(i + 0 + (int)bits) * 92.41f,
                                                   (float)(i + 0 + (int)bits) * 87.16f);
                if (n < decor.probabilityCutoff) continue;
                int vi = (int)((float)(decor.variants.Length - 1) * n);
                vi = Mathf.Clamp(vi, 0, decor.variants.Length - 1);

                BlockTileDecorInfo.ImageInfo variant = decor.variants[vi];
                TextureAtlas.Item mesh = variant.atlasItem;
                if (mesh.vertices == null || mesh.uvs == null || mesh.indices == null) continue;

                Vector3 offset = variant.offset; // cell origin is (0,0)
                for (int t = 0; t + 2 < mesh.indices.Length; t += 3)
                {
                    int a = mesh.indices[t], b = mesh.indices[t + 1], c = mesh.indices[t + 2];
                    Vector2 p0 = WorldToCanvas(mesh.vertices[a] + offset, wx0, wy0, iAx, iAy, wxScale, wyScale, uMin, vMin, spanU, spanV, cw, ch);
                    Vector2 p1 = WorldToCanvas(mesh.vertices[b] + offset, wx0, wy0, iAx, iAy, wxScale, wyScale, uMin, vMin, spanU, spanV, cw, ch);
                    Vector2 p2 = WorldToCanvas(mesh.vertices[c] + offset, wx0, wy0, iAx, iAy, wxScale, wyScale, uMin, vMin, spanU, spanV, cw, ch);
                    RasterTriangle(canvas, cw, ch, decorTex, p0, p1, p2, mesh.uvs[a], mesh.uvs[b], mesh.uvs[c]);
                }
            }
        }

        private static Vector2 WorldToCanvas(Vector3 world, float wx0, float wy0, float iAx, float iAy,
            float wxScale, float wyScale, float uMin, float vMin, float spanU, float spanV, int cw, int ch)
        {
            float u = iAx + (world.x - wx0) * wxScale;
            float v = iAy + (world.y - wy0) * wyScale;
            return new Vector2((u - uMin) / spanU * cw, (v - vMin) / spanV * ch);
        }

        // Barycentric triangle fill with bilinear decor-atlas sampling and source-over
        // alpha compositing onto the base canvas.
        private static void RasterTriangle(Color[] canvas, int cw, int ch, Texture2D decorTex,
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 t0, Vector2 t1, Vector2 t2)
        {
            float area = Edge(p0, p1, p2);
            if (Mathf.Abs(area) < 1e-6f) return;
            float invArea = 1f / area;

            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))), 0, cw - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x))), 0, cw - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))), 0, ch - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y))), 0, ch - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float l0 = Edge(p1, p2, p) * invArea;
                    float l1 = Edge(p2, p0, p) * invArea;
                    float l2 = Edge(p0, p1, p) * invArea;
                    if (l0 < -1e-4f || l1 < -1e-4f || l2 < -1e-4f)
                        continue;

                    Vector2 uv = l0 * t0 + l1 * t1 + l2 * t2;
                    Color s = decorTex.GetPixelBilinear(uv.x, uv.y);
                    if (s.a <= 0f)
                        continue;

                    int idx = y * cw + x;
                    Color d = canvas[idx];
                    float sa = s.a;
                    float outA = sa + d.a * (1f - sa);
                    Color outC = new Color(
                        s.r * sa + d.r * d.a * (1f - sa),
                        s.g * sa + d.g * d.a * (1f - sa),
                        s.b * sa + d.b * d.a * (1f - sa),
                        outA);
                    if (outA > 0f)
                    {
                        outC.r /= outA;
                        outC.g /= outA;
                        outC.b /= outA;
                    }
                    canvas[idx] = outC;
                }
            }
        }

        private static float Edge(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }
    }
}
