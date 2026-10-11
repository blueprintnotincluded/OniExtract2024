using System;
using System.Collections.Generic;
using System.IO;

namespace OniExtract2024
{
    /// <summary>
    /// Decides what an export run leaves behind. The export folder is written in place, so
    /// anything a previous run produced for a prefab that is no longer loaded (a mod since
    /// disabled, a building the game removed) would otherwise stay on disk and travel to the
    /// website as if it were current. Each run prunes its output to the prefabs it saw.
    /// Pure logic, so CI can test it without a game install.
    /// </summary>
    public static class ExportPruning
    {
        /// <summary>
        /// The entries of <paramref name="paths"/> (files or directories) whose name without
        /// extension is not in <paramref name="keep"/>. Names are compared ignoring case: the
        /// export lives on a case-insensitive file system, where a keep name differing from
        /// the file only by case refers to the same file, and deleting it would discard
        /// output that is current.
        /// </summary>
        public static List<string> Stale(IEnumerable<string> paths, IEnumerable<string> keep)
        {
            var keepSet = new HashSet<string>(keep, StringComparer.OrdinalIgnoreCase);
            var stale = new List<string>();
            foreach (string path in paths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (!keepSet.Contains(name))
                    stale.Add(path);
            }
            return stale;
        }
    }
}
