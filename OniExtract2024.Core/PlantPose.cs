using System;
using System.Collections.Generic;

namespace OniExtract2024.building
{
    /// <summary>
    /// The anim a plant icon is posed in, and which frame of it: the mature, fully grown look
    /// of the plant standing in the world. Pure logic over anim names, so CI can test it;
    /// BuildingImageSnapshotter feeds it from the live controller and tries the plant's own
    /// StandardCropPlant anim set first, which names the grown anim outright.
    ///
    /// The rule: <c>idle_full</c>, else <c>grown</c>, else <c>idle</c>, else the highest-numbered
    /// other idle state (<c>idle3</c> over <c>idle2</c>, <c>idle_loop</c> over
    /// <c>oxygen_idle_loop</c> by name length), else the last frame of <c>grow</c>. Never a
    /// seed, empty, half-grown, wilted, dead, harvest, sleep, hidden or transition state, and
    /// never <c>ui</c> or <c>place</c>.
    /// </summary>
    public static class PlantPose
    {
        /// <summary>A chosen anim and whether its last frame, rather than its middle, is the pose.</summary>
        public struct Choice
        {
            public string Anim;
            public bool LastFrame;
        }

        // Name parts that mark a state which is not the grown plant at rest.
        private static readonly HashSet<string> ExcludedTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "empty", "seed", "half", "wilt", "wilted", "dead", "harvest", "harvestable", "place",
            "ui", "hidden", "sleep", "pre", "pst", "meter", "shake", "waning", "closed", "trap",
            "digesting", "vent", "consume", "awaiting", "expand", "misc", "eat", "branch",
        };

        /// <summary>
        /// How well a name fits the grown look: 400 for <c>idle_full</c>, 300 for <c>grown</c>,
        /// 200 for <c>idle</c>, 100 plus any trailing number for another idle state, 10 for
        /// <c>grow</c> (posed at its last frame), 0 for a name that is never posed.
        /// </summary>
        public static int Rank(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            string n = name.ToLowerInvariant();
            if (n == "idle_full") return 400;
            if (n == "grown") return 300;
            if (n == "idle") return 200;
            if (n == "grow") return 10;

            string[] tokens = n.Split('_');
            bool idle = false;
            foreach (string raw in tokens)
            {
                string t = raw.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (ExcludedTokens.Contains(t)) return 0;
                if (t == "idle") idle = true;
            }
            if (!idle) return 0;

            int number = 0;
            int end = n.Length;
            while (end > 0 && char.IsDigit(n[end - 1])) end--;
            if (end < n.Length) int.TryParse(n.Substring(end), out number);
            return 100 + number;
        }

        /// <summary>
        /// The anim to pose a plant in, out of the names its controller can play, or an empty
        /// choice (null <see cref="Choice.Anim"/>) when nothing ranks. Ties go to the shorter
        /// name, then ordinal order, never to the controller's dictionary order.
        /// </summary>
        public static Choice Choose(IEnumerable<string> animNames)
        {
            string pick = null;
            int pickRank = 0;
            foreach (string name in animNames)
            {
                int rank = Rank(name);
                if (rank <= 0) continue;
                if (rank > pickRank
                    || (rank == pickRank && (name.Length < pick.Length
                        || (name.Length == pick.Length && string.CompareOrdinal(name, pick) < 0))))
                {
                    pick = name;
                    pickRank = rank;
                }
            }
            return new Choice { Anim = pick, LastFrame = pick != null && Rank(pick) == 10 };
        }
    }
}
