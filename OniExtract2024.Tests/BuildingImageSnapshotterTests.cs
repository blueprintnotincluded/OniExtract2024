using OniExtract2024.building;
using UnityEngine;
using Xunit;

namespace OniExtract2024.Tests
{
    public class ImageCropTests
    {
        private static Color[] MakePixels(int w, int h) => new Color[w * h]; // all transparent

        private static void SetPixel(Color[] px, int w, int x, int y, float alpha)
        {
            px[y * w + x] = new Color(1f, 1f, 1f, alpha);
        }

        [Fact]
        public void FindOpaqueBBox_FullyTransparent_ReturnsFalse()
        {
            var px = MakePixels(4, 4);
            bool found = ImageCrop.FindOpaqueBBox(px, 4, 4, out _, out _, out _, out _);
            Assert.False(found);
        }

        [Fact]
        public void FindOpaqueBBox_SingleOpaquePixel_TightBounds()
        {
            var px = MakePixels(5, 5);
            SetPixel(px, 5, 2, 3, 1f);

            bool found = ImageCrop.FindOpaqueBBox(px, 5, 5,
                out int minX, out int minY, out int maxX, out int maxY);

            Assert.True(found);
            Assert.Equal(2, minX);
            Assert.Equal(2, maxX);
            Assert.Equal(3, minY);
            Assert.Equal(3, maxY);
        }

        [Fact]
        public void FindOpaqueBBox_MultiplePixels_CorrectBounds()
        {
            var px = MakePixels(10, 10);
            SetPixel(px, 10, 1, 2, 1f);
            SetPixel(px, 10, 7, 8, 1f);

            ImageCrop.FindOpaqueBBox(px, 10, 10,
                out int minX, out int minY, out int maxX, out int maxY);

            Assert.Equal(1, minX);
            Assert.Equal(7, maxX);
            Assert.Equal(2, minY);
            Assert.Equal(8, maxY);
        }

        [Fact]
        public void FindOpaqueBBox_BelowAlphaThreshold_TreatedAsTransparent()
        {
            var px = MakePixels(4, 4);
            SetPixel(px, 4, 1, 1, 0.02f); // below 0.03 threshold
            SetPixel(px, 4, 3, 3, 1f);

            ImageCrop.FindOpaqueBBox(px, 4, 4,
                out int minX, out int minY, out int maxX, out int maxY);

            Assert.Equal(3, minX);
            Assert.Equal(3, minY);
        }

        [Fact]
        public void CropPixels_ExtractsCorrectRegion()
        {
            // 3x3 grid, pixel value encodes position as (x+1, y+1, 0, 1)
            int w = 3, h = 3;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color(x + 1, y + 1, 0f, 1f);

            // Crop the bottom-right 2x2: x=[1,2], y=[1,2]
            Color[] cropped = ImageCrop.CropPixels(px, w, 1, 1, 2, 2);

            Assert.Equal(4, cropped.Length);
            Assert.Equal(new Color(2, 2, 0, 1), cropped[0]); // oy=0,ox=0 -> x=1,y=1
            Assert.Equal(new Color(3, 2, 0, 1), cropped[1]); // oy=0,ox=1 -> x=2,y=1
            Assert.Equal(new Color(2, 3, 0, 1), cropped[2]); // oy=1,ox=0 -> x=1,y=2
            Assert.Equal(new Color(3, 3, 0, 1), cropped[3]); // oy=1,ox=1 -> x=2,y=2
        }
    }
}
