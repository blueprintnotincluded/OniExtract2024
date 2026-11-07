using OniExtract2024.building;
using Xunit;

namespace OniExtract2024.Core.Tests
{
    // Anim names are the game's own, read offline from the shipped plant kanims on 2026-11-06.
    public class PlantPoseTests
    {
        [Theory]
        [InlineData("idle_full", 400)]
        [InlineData("grown", 300)]
        [InlineData("idle", 200)]
        [InlineData("idle_open", 100)]
        [InlineData("idle_loop", 100)]
        [InlineData("idle3", 103)]
        [InlineData("idle_2000", 199)]
        [InlineData("grow", 10)]
        [InlineData("idle_empty", 0)]
        [InlineData("idle_half", 0)]
        [InlineData("idle_full_closed", 0)]
        [InlineData("grow_seed", 0)]
        [InlineData("grow_pst", 0)]
        [InlineData("wilt1", 0)]
        [InlineData("harvestable_loop", 0)]
        [InlineData("ui", 0)]
        [InlineData("place", 0)]
        [InlineData("wilt_idle", 0)]
        [InlineData("branch_a_idle_full", 0)]
        public void Rank_PrefersTheGrownLook(string name, int expected)
        {
            Assert.Equal(expected, PlantPose.Rank(name));
        }

        [Fact]
        public void Crop_IdleFullWins()
        {
            // Mealwood: the fruiting state loops idle_full.
            var c = PlantPose.Choose(new[] { "idle_empty", "grow", "grow_harvest", "grow_pst", "idle_full", "harvest", "wilt1", "wilt2", "wilt3", "place", "ui", "waning" });
            Assert.Equal("idle_full", c.Anim);
            Assert.False(c.LastFrame);
        }

        [Fact]
        public void BlueGrass_FullBeatsHalf()
        {
            var c = PlantPose.Choose(new[] { "idle_empty", "grow", "idle_half", "idle_full", "harvest", "ui" });
            Assert.Equal("idle_full", c.Anim);
        }

        [Fact]
        public void DecorPlant_IdleIsTheGrownLook()
        {
            // Bluff Briar: grow_seed once, then idle forever.
            var c = PlantPose.Choose(new[] { "grow_seed", "idle", "wilt1", "place", "ui", "misc" });
            Assert.Equal("idle", c.Anim);
        }

        [Fact]
        public void Coral_PlainIdleLoopBeatsTheEffectOne()
        {
            // Flue Coral (OxyCoral): idle_loop and oxygen_idle_loop tie on rank; the shorter name wins.
            var c = PlantPose.Choose(new[] { "oxygen_idle_loop", "idle_loop", "wilt", "grow", "grow_pst", "place", "ui" });
            Assert.Equal("idle_loop", c.Anim);
        }

        [Fact]
        public void CritterTrap_OpenIdle()
        {
            var c = PlantPose.Choose(new[] { "idle_open", "trap", "digesting_loop", "vent_pre", "vent_loop", "vent_pst", "open_harvest", "harvestable_loop", "harvest", "open_trap", "wilt1", "place", "ui" });
            Assert.Equal("idle_open", c.Anim);
        }

        [Fact]
        public void BonbonTree_GrownTrunk()
        {
            var c = PlantPose.Choose(new[] { "idle_empty", "grow", "grow_pst", "grown", "harvest", "wilt1", "branch_b_idle", "grow_fill", "harvest_ready", "place", "ui" });
            Assert.Equal("grown", c.Anim);
        }

        [Fact]
        public void NumberedStages_HighestWins()
        {
            var c = PlantPose.Choose(new[] { "idle1", "idle2", "idle10", "grow" });
            Assert.Equal("idle10", c.Anim);
        }

        [Fact]
        public void NumberedStages_NeverOutrankTheGrownTiers()
        {
            Assert.Equal("idle_full", PlantPose.Choose(new[] { "idle_2000", "idle_full" }).Anim);
            Assert.Equal("grown", PlantPose.Choose(new[] { "idle_2000", "grown" }).Anim);
            Assert.Equal("idle", PlantPose.Choose(new[] { "idle_2000", "idle" }).Anim);
        }

        [Fact]
        public void OnlyGrow_LastFrame()
        {
            var c = PlantPose.Choose(new[] { "grow", "grow_pst", "wilt", "place", "ui" });
            Assert.Equal("grow", c.Anim);
            Assert.True(c.LastFrame);
        }

        [Fact]
        public void NothingRanks_NoChoice()
        {
            var c = PlantPose.Choose(new[] { "place", "ui", "wilt1" });
            Assert.Null(c.Anim);
            Assert.False(c.LastFrame);
        }

        [Fact]
        public void Ties_DoNotDependOnOrder()
        {
            Assert.Equal("idle_b", PlantPose.Choose(new[] { "idle_c", "idle_b" }).Anim);
            Assert.Equal("idle_b", PlantPose.Choose(new[] { "idle_b", "idle_c" }).Anim);
        }
    }
}
