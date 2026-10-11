using System.IO;
using Xunit;

namespace OniExtract2024.Core.Tests
{
    public class ExportPruningTests
    {
        private static string Png(string name) => Path.Combine("C:/export/ui_image", name + ".png");

        [Fact]
        public void Stale_ReturnsOnlyFilesWhoseNameIsNotKept()
        {
            var files = new[] { Png("Tile"), Png("PressureGasPump"), Png("input") };
            var stale = ExportPruning.Stale(files, new[] { "Tile", "input" });
            Assert.Equal(new[] { Png("PressureGasPump") }, stale);
        }

        [Fact]
        public void Stale_IgnoresCaseSoACurrentFileIsNeverDeleted()
        {
            var stale = ExportPruning.Stale(new[] { Png("steamturbine") }, new[] { "SteamTurbine" });
            Assert.Empty(stale);
        }

        [Fact]
        public void Stale_ComparesNamesWithoutTheExtension()
        {
            var stale = ExportPruning.Stale(new[] { Png("Tile") }, new[] { "Tile.png" });
            Assert.Single(stale);
        }

        [Fact]
        public void Stale_WorksForDirectories()
        {
            string dir = Path.Combine("C:/export/connection_sprites", "HighPressureGasConduit");
            var stale = ExportPruning.Stale(new[] { dir }, new[] { "Tile" });
            Assert.Equal(new[] { dir }, stale);
        }

        [Fact]
        public void Stale_WithNothingKept_ReturnsEverything()
        {
            var files = new[] { Png("A"), Png("B") };
            Assert.Equal(files, ExportPruning.Stale(files, new string[0]));
        }
    }
}
