using System.IO;

namespace OniExtract2024
{
    /// <summary>
    /// Pure path helpers for the export layout. Lives in OniExtract2024.Core (no Unity/ONI
    /// dependency) so it can be unit-tested on a CI runner without a game install. BaseExport
    /// delegates to this so the in-game export and the tests agree on one implementation.
    /// </summary>
    public static class ExportPaths
    {
        /// <summary>
        /// The database/asset directory for an export: <c>{rootFolder}/export/{dirName}</c>,
        /// with a <c>_base</c> suffix when the Spaced Out (Expansion 1) DLC is inactive so a
        /// vanilla export does not overwrite a Spaced Out one.
        /// </summary>
        public static string BuildExportPath(string rootFolder, string dirName, bool isExpansion1Active)
        {
            string suffix = isExpansion1Active ? "" : "_base";
            return Path.Combine(rootFolder, "export", dirName + suffix);
        }
    }
}
