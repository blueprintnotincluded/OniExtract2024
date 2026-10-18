using System;
using System.Collections.Generic;

namespace OniExtract2024.building
{
    /// <summary>
    /// One anim a building's controller can play, with its half-extent in cells about the anim
    /// origin: how far its art reaches over every frame, the measure the game stores as
    /// KAnim.Anim.unScaledSize (BuildingImageSnapshotter measures it from the frames). An extent
    /// that <see cref="RenderCanvas.IsUsableHalfExtent"/> rejects marks an anim whose size is
    /// unknown.
    /// </summary>
    public struct AnimCandidate
    {
        public string name;
        public float halfW;
        public float halfH;

        public AnimCandidate(string name, float halfW, float halfH)
        {
            this.name = name;
            this.halfW = halfW;
            this.halfH = halfH;
        }
    }

    /// <summary>
    /// Picks the anim a building icon is posed in. Pure logic over anim names and authored
    /// extents, so CI can test it; BuildingImageSnapshotter.ChooseActiveAnim feeds it from the
    /// live controller.
    /// </summary>
    public static class ActivePose
    {
        // Substrings that mark an animation as a non-active state — never snapshot these.
        // Includes construction/placement/teardown anims ("place", "construct", "install",
        // "demo") since those are never the working pose we want for an icon. "ui" renders at
        // atlas scale and silently shrinks every output; never remove it.
        private static readonly string[] InactiveMarkers =
        {
            "off", "broken", "error", "dead", "disabled", "outofnetwork",
            "no_power", "nopower", "unpowered", "closed", "empty", "ui",
            "place", "construct", "install", "demo",
        };

        /// <summary>
        /// How far, in cells summed over both axes, an anim's extent may be from the
        /// footprint's beyond the closest candidate's and still win on score. Small enough that
        /// steam, sparks and light cones lose to the body; large enough that a pump arm or a
        /// slightly larger working frame does not.
        /// </summary>
        public const float ToleranceCells = 0.25f;

        /// <summary>
        /// How "active" an anim name looks: 100 for working states, 50 for powered-on, 20 for
        /// idle, +10 for a loop, -8 for a transition; 0 for a name that says nothing and -1 for
        /// an inactive state. Only anims scoring above 0 are posed.
        /// </summary>
        public static int Score(string name)
        {
            string n = name.ToLowerInvariant();
            foreach (string bad in InactiveMarkers)
                if (n.Contains(bad))
                    return -1;

            int score;
            if (n.Contains("working") || n.Contains("generating") || n.Contains("dispensing")
                || n.Contains("channeling") || n.Contains("emitting") || n.Contains("charged")
                || n.Contains("running") || n.Contains("deployed"))
                score = 100;
            else if (n == "on" || n.StartsWith("on_") || n.EndsWith("_on") || n.Contains("powered"))
                score = 50;
            else if (n.Contains("idle"))
                score = 20;
            else
                return 0;

            if (n.Contains("loop")) score += 10;
            if (n.EndsWith("_pre") || n.EndsWith("_pst")) score -= 8;
            return score;
        }

        /// <summary>
        /// True for a transition into or out of a state (<c>working_pre</c>,
        /// <c>working_pst_open</c>): halfway through one the building is half-way between two
        /// looks.
        /// </summary>
        public static bool IsTransition(string name)
        {
            string n = name.ToLowerInvariant();
            return n.EndsWith("_pre") || n.EndsWith("_pst") || n.Contains("_pre_") || n.Contains("_pst_");
        }

        /// <summary>
        /// The anim to pose a <paramref name="cellW"/> x <paramref name="cellH"/> building in,
        /// or null when no anim scores above 0.
        ///
        /// Among the anims that score, the one whose extent is closest to the
        /// footprint's wins, unless a higher-scoring one is within <see cref="ToleranceCells"/>
        /// of it. Effects (steam, wind, light cones, a stray swoosh frame) are drawn outside the
        /// body and only in some states, so the state without them is the closest; with no
        /// effects, every state has the body's extent and the most active one wins as before.
        /// Steady states are preferred over transitions, and anims with an unusable extent are
        /// left out, unless nothing else scores. The footprint's extent is measured the way the
        /// game measures an anim's: half its width either side of the anim origin, which sits at
        /// the bottom centre, and its full height.
        /// </summary>
        public static string Choose(IList<AnimCandidate> anims, int cellW, int cellH)
        {
            var scored = new List<AnimCandidate>();
            foreach (var a in anims)
                if (!string.IsNullOrEmpty(a.name) && Score(a.name) > 0)
                    scored.Add(a);
            if (scored.Count == 0)
                return null;

            var sized = scored.FindAll(a => RenderCanvas.IsUsableHalfExtent(a.halfW, a.halfH));
            if (sized.Count == 0)
                return MostActive(scored);
            if (sized.Exists(a => !IsTransition(a.name)))
                sized = sized.FindAll(a => !IsTransition(a.name));

            float footW = cellW / 2f, footH = cellH;
            Func<AnimCandidate, float> cost = a => Math.Abs(a.halfW - footW) + Math.Abs(a.halfH - footH);
            float best = float.MaxValue;
            foreach (var a in sized)
                best = Math.Min(best, cost(a));
            return MostActive(sized.FindAll(a => cost(a) <= best + ToleranceCells));
        }

        // The highest-scoring name, ties going to the first in ordinal name order. Not the
        // order the anims came in: that is the controller's dictionary order, which shifts
        // when Klei adds an anim to the file, and would flip the pose with it.
        private static string MostActive(List<AnimCandidate> anims)
        {
            string pick = null;
            int pickScore = int.MinValue;
            foreach (var a in anims)
            {
                int s = Score(a.name);
                if (s > pickScore || (s == pickScore && string.CompareOrdinal(a.name, pick) < 0))
                {
                    pick = a.name;
                    pickScore = s;
                }
            }
            return pick;
        }
    }
}
