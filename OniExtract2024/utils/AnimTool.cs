// The following code snippet is from the GPL-2.0 project Sgt_Imalas-Oni-Mods (https://github.com/Sgt-Imalas/Sgt_Imalas-Oni-Mods).
// Original author: Sgt_Imalas
// Modification date: 2024-05-22

using System.Collections.Generic;
using System.IO;
using OniExtract2024.building;
using UnityEngine;

namespace OniExtract2024.utils
{
    public class AnimTool
    {
        static Dictionary<Texture2D, Texture2D> CacheRealTexture = new Dictionary<Texture2D, Texture2D>();
        static Dictionary<Sprite, Texture2D> CacheTexture = new Dictionary<Sprite, Texture2D>();

        public static Texture2D GetReadableCopy(Texture2D source)
        {
            if (CacheRealTexture.ContainsKey(source))
                return CacheRealTexture[source];

            if (source == null || source.width == 0 || source.height == 0) return null;

            RenderTexture renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height);


            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            CacheRealTexture[source] = readableText;
            return readableText;
        }

        static Texture2D GetSingleSpriteFromTexture(Sprite sprite, Color tint = default)
        {
            if (sprite == null || sprite.rect == null || sprite.rect.width <= 0 || sprite.rect.height <= 0)
                return null;

            bool useTint = tint != default;

            if (useTint || !CacheTexture.ContainsKey(sprite))
            {
                var output = new Texture2D(Mathf.RoundToInt(sprite.textureRect.width), Mathf.RoundToInt(sprite.textureRect.height));
                var r = sprite.textureRect;
                if (r.width == 0 || r.height == 0)
                    return null;

                var readableTexture = GetReadableCopy(sprite.texture);

                if (readableTexture == null)
                    return null;

                var pixels = readableTexture.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), Mathf.RoundToInt(r.width), Mathf.RoundToInt(r.height));
                if (useTint)
                {
                    var tintedPixels = new Color[pixels.Length];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        tintedPixels[i] = pixels[i] * tint;
                    }
                    output.SetPixels(tintedPixels);
                }
                else
                {
                    output.SetPixels(pixels);
                }
                output.Apply();
                output.name = sprite.texture.name + " " + sprite.name;

                if (useTint)
                    return output;

                CacheTexture.Add(sprite, output);
            }
            return CacheTexture[sprite];
        }

        // Returns a new texture cropped to the opaque bounding box, or null when the
        // input is already tight (or fully transparent) and should be written as-is.
        // Never mutates the input — GetSingleSpriteFromTexture caches untinted textures.
        static Texture2D TrimToOpaqueBBox(Texture2D tex)
        {
            Color[] px = tex.GetPixels();
            if (!ImageCrop.FindOpaqueBBox(px, tex.width, tex.height,
                    out int minX, out int minY, out int maxX, out int maxY))
                return null;

            int cw = maxX - minX + 1;
            int ch = maxY - minY + 1;
            if (cw == tex.width && ch == tex.height)
                return null;

            var result = new Texture2D(cw, ch);
            result.SetPixels(ImageCrop.CropPixels(px, tex.width, minX, minY, cw, ch));
            result.Apply();
            return result;
        }

        public static void WriteUISpriteToFile(Sprite sprite, string folder, string UIName, Color tint = default)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            string fileName = Path.Combine(folder, UIName + ".png");

            //Debug.Log(fileName);
            var tex = GetSingleSpriteFromTexture(sprite, tint);

            if (tex == null)
                return;

            // Trim transparent padding baked into the atlas sprite rect. Buildings the
            // hi-res snapshot pass can't render (tiles — no KBatchedAnimController) keep
            // this atlas icon as their final ui_image, and the website stretches the whole
            // PNG to the footprint, so any padding shows up as blank border in-editor.
            // Same opaque-bbox rule as BuildingImageSnapshotter.TrimToOpaqueBBox.
            var trimmed = TrimToOpaqueBBox(tex);

            var imageBytes = (trimmed != null ? trimmed : tex).EncodeToPNG();
            File.WriteAllBytes(fileName, imageBytes);

            if (trimmed != null)
                Object.Destroy(trimmed);
        }
    }
}
