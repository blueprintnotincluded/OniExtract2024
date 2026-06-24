using System.IO;
using Xunit;

namespace OniExtract2024.Core.Tests
{
    public class ExportPathsTests
    {
        [Fact]
        public void BuildExportPath_WithExpansion1_NoSuffix()
        {
            string expected = Path.Combine("C:/root", "export", "database");
            Assert.Equal(expected, ExportPaths.BuildExportPath("C:/root", "database", true));
        }

        [Fact]
        public void BuildExportPath_WithoutExpansion1_AddsSuffix()
        {
            string expected = Path.Combine("C:/root", "export", "database_base");
            Assert.Equal(expected, ExportPaths.BuildExportPath("C:/root", "database", false));
        }

        [Fact]
        public void BuildExportPath_PreservesSubDirName()
        {
            string result = ExportPaths.BuildExportPath("C:/root", "recipe", true);
            Assert.Contains("recipe", result);
            Assert.DoesNotContain("recipe_base", result);
        }
    }
}
