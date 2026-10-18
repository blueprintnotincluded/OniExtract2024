using UnityEngine;

namespace OniExtract2024.building
{
    /// <summary>
    /// Pure pixel-level crop helpers. Extracted from BuildingImageSnapshotter so
    /// unit tests can reach them without triggering a load of KMonoBehaviour or any
    /// other Assembly-CSharp-firstpass type (which the .NET 4.8 test host rejects
    /// due to default interface method implementations Unity uses in those DLLs).
    /// </summary>
    internal static class ImageCrop
    {
        /// <summary>
        /// Finds the tightest bounding box of pixels whose alpha exceeds the threshold.
        /// Returns false when the whole image is transparent.
        /// </summary>
        internal static bool FindOpaqueBBox(Color[] px, int w, int h,
            out int minX, out int minY, out int maxX, out int maxY,
            float alphaThreshold = 0.03f)
        {
            minX = w; maxX = -1; minY = h; maxY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a > alphaThreshold)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            return maxX >= minX && maxY >= minY;
        }

        internal static Color[] CropPixels(Color[] px, int srcW, int minX, int minY, int cw, int ch)
        {
            Color[] outPx = new Color[cw * ch];
            for (int oy = 0; oy < ch; oy++)
                for (int ox = 0; ox < cw; ox++)
                    outPx[oy * cw + ox] = px[(minY + oy) * srcW + (minX + ox)];
            return outPx;
        }

        // The same two helpers over Color32, for the building render: a quarter of the memory
        // of Color[] on canvases that now reach several megapixels. The threshold is the same
        // rule in bytes: alpha / 255 > 0.03 exactly when alpha >= 8.
        internal const byte AlphaThreshold32 = 7;

        internal static bool FindOpaqueBBox(Color32[] px, int w, int h,
            out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = w; maxX = -1; minY = h; maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x].a > AlphaThreshold32)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            return maxX >= minX && maxY >= minY;
        }

        internal static Color32[] CropPixels(Color32[] px, int srcW, int minX, int minY, int cw, int ch)
        {
            Color32[] outPx = new Color32[cw * ch];
            for (int oy = 0; oy < ch; oy++)
                System.Array.Copy(px, (minY + oy) * srcW + minX, outPx, oy * cw, cw);
            return outPx;
        }
    }
}
