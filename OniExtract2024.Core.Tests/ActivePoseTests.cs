using System.Collections.Generic;
using OniExtract2024.building;
using Xunit;

namespace OniExtract2024.Core.Tests
{
    // Extents are the game's own (KAnim.Anim.unScaledSize at 0.005 cells per kanim pixel), read
    // offline from the shipped kanims on 2026-10-18.
    public class ActivePoseTests
    {
        private static AnimCandidate A(string name, float halfW, float halfH) => new AnimCandidate(name, halfW, halfH);

        [Theory]
        [InlineData("working_loop", 110)]
        [InlineData("working_pre", 92)]
        [InlineData("on", 50)]
        [InlineData("light_on", 50)]
        [InlineData("idle", 20)]
        [InlineData("meter", 0)]
        [InlineData("off", -1)]
        [InlineData("ui", -1)]
        [InlineData("place", -1)]
        public void Score_RanksActiveStates(string name, int expected)
        {
            Assert.Equal(expected, ActivePose.Score(name));
        }

        [Theory]
        [InlineData("working_pre", true)]
        [InlineData("working_pst_open", true)]
        [InlineData("working_loop", false)]
        [InlineData("on", false)]
        public void IsTransition(string name, bool expected)
        {
            Assert.Equal(expected, ActivePose.IsTransition(name));
        }

        [Fact]
        public void SteamTurbine2_SteamPlumeLosesToTheBody()
        {
            // 5x3. working_loop's steam reaches 4.48 cells from the origin; on stays at 3.05.
            var anims = new List<AnimCandidate>
            {
                A("on", 2.59f, 3.05f), A("off", 2.59f, 3.05f),
                A("working_pre", 2.76f, 4.48f), A("working_loop", 2.76f, 4.48f), A("working_pst", 2.76f, 4.48f),
            };
            Assert.Equal("on", ActivePose.Choose(anims, 5, 3));
        }

        [Fact]
        public void NoEffects_MostActiveStateStillWins()
        {
            // WallToilet: every state has the body's extent.
            var anims = new List<AnimCandidate>
            {
                A("on", 1.65f, 2.97f), A("working_pre", 1.65f, 2.97f), A("working_loop", 1.65f, 2.97f),
                A("working_pst", 1.65f, 2.97f), A("flush", 1.74f, 2.97f),
            };
            Assert.Equal("working_loop", ActivePose.Choose(anims, 1, 3));
        }

        [Fact]
        public void SmallDifference_WithinTolerance_KeepsTheActiveState()
        {
            // 2x2: working_loop is 0.2 cells further from the footprint than on.
            var anims = new List<AnimCandidate> { A("on", 1.05f, 2.05f), A("working_loop", 1.15f, 2.15f) };
            Assert.Equal("working_loop", ActivePose.Choose(anims, 2, 2));
        }

        [Fact]
        public void VerticalWindTunnel_GarbageExtentIsLeftOut()
        {
            // The working anims carry the 536870-cell sentinel; on is the only sized one.
            var anims = new List<AnimCandidate>
            {
                A("on", 2.66f, 7.59f), A("working_pre", 536870.92f, 536870.92f),
                A("working_loop", 536870.92f, 536870.92f), A("off", 3.79f, 10.42f),
            };
            Assert.Equal("on", ActivePose.Choose(anims, 5, 6));
        }

        [Fact]
        public void OnlyGarbageExtents_FallsBackToTheScore()
        {
            var anims = new List<AnimCandidate> { A("on", float.NaN, 1f), A("working", float.NaN, float.NaN) };
            Assert.Equal("working", ActivePose.Choose(anims, 1, 1));
        }

        [Fact]
        public void PartialOverlay_IsNotTheClosest()
        {
            // OilChanger 3x3: light_on is a small overlay anim, far smaller than the footprint.
            var anims = new List<AnimCandidate>
            {
                A("working_loop", 1.86f, 3.49f), A("on", 1.63f, 3.15f), A("light_on", 0.20f, 0.44f),
            };
            Assert.Equal("on", ActivePose.Choose(anims, 3, 3));
        }

        [Fact]
        public void Transitions_OnlyWhenNoSteadyStateScores()
        {
            // A transition with a tighter extent still loses to a steady state.
            var anims = new List<AnimCandidate> { A("working_pre", 0.6f, 1.17f), A("working_loop", 0.81f, 1.60f) };
            Assert.Equal("working_loop", ActivePose.Choose(anims, 1, 1));

            var onlyTransitions = new List<AnimCandidate> { A("working_pre_1", 0.63f, 2.06f), A("working_pst_1", 0.63f, 3.0f) };
            Assert.Equal("working_pst_1", ActivePose.Choose(onlyTransitions, 1, 3));
        }

        [Fact]
        public void Ties_GoToNameOrder_WhateverOrderTheAnimsComeIn()
        {
            // LogicHammer: on_hit and on_miss score the same and are equally close.
            var a = new List<AnimCandidate> { A("on_miss", 1.19f, 1.58f), A("on_hit", 1.19f, 1.58f) };
            var b = new List<AnimCandidate> { A("on_hit", 1.19f, 1.58f), A("on_miss", 1.19f, 1.58f) };
            Assert.Equal("on_hit", ActivePose.Choose(a, 1, 1));
            Assert.Equal("on_hit", ActivePose.Choose(b, 1, 1));

            var unsized = new List<AnimCandidate> { A("working_b", float.NaN, 1f), A("working_a", float.NaN, 1f) };
            Assert.Equal("working_a", ActivePose.Choose(unsized, 1, 1));
        }

        [Fact]
        public void NothingScores_ReturnsNull()
        {
            var anims = new List<AnimCandidate> { A("off", 1f, 1f), A("meter", 0.2f, 0.8f) };
            Assert.Null(ActivePose.Choose(anims, 1, 1));
        }
    }
}
