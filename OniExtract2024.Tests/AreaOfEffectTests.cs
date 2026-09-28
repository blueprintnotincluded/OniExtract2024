using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OniExtract2024.Tests
{
    // Validates the pure AoE geometry against the game's cell math (DiscreteShadowCaster
    // for light shapes, the sim's orthogonal spread for consumers) and the serialization
    // contract of OutAreaOfEffect under the real export settings.
    public class AreaOfEffectTests
    {
        private static HashSet<(int, int)> AsSet(List<BVector2> cells) =>
            new HashSet<(int, int)>(cells.Select(c => ((int)c.x, (int)c.y)));

        // ── Light circle ────────────────────────────────────────────────────────

        [Fact]
        public void CircleCells_IsEuclideanDisc_BoundaryInclusive()
        {
            var cells = AsSet(AreaOfEffectBuilder.CircleCells(4)); // Floor Lamp range
            Assert.Contains((0, 0), cells);
            Assert.Contains((0, 4), cells);   // straight up, on the boundary
            Assert.Contains((4, 0), cells);
            Assert.Contains((2, 3), cells);   // 4+9=13 <= 16
            Assert.DoesNotContain((3, 3), cells); // 18 > 16
            Assert.DoesNotContain((0, 5), cells);
            // brute-force count: all |dx|,|dy| <= 4 with dx^2+dy^2 <= 16
            int expected = 0;
            for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                    if (dx * dx + dy * dy <= 16) expected++;
            Assert.Equal(expected, cells.Count);
        }

        // ── Light cone ──────────────────────────────────────────────────────────

        [Fact]
        public void ConeCells_CeilingLight_WidensAt45Degrees_ClippedToRange()
        {
            var cells = AsSet(AreaOfEffectBuilder.ConeCells(8)); // Ceiling Light range
            Assert.Contains((0, 0), cells);    // emitting cell always lit
            Assert.Contains((0, -8), cells);   // full range straight down
            Assert.Contains((-1, -1), cells);  // 45-degree edge
            Assert.Contains((3, -7), cells);   // 9+49=58 <= 64
            Assert.DoesNotContain((4, -7), cells); // 16+49=65 > 64
            Assert.DoesNotContain((2, -1), cells); // outside the 45-degree cone
            Assert.DoesNotContain((0, 1), cells);  // nothing above the lamp
            Assert.DoesNotContain((1, -8), cells); // 1+64 > 64
        }

        // ── Light quad ──────────────────────────────────────────────────────────

        [Fact]
        public void QuadCells_MercuryCeilingLight_StripTimesRange()
        {
            // Mercury Ceiling Light: width 3, range 8, direction South.
            var cells = AsSet(AreaOfEffectBuilder.QuadCells(3, 8, DiscreteShadowCaster.Direction.South));
            // 3 columns x 8 rows (t = 0..7), origin included in the strip
            Assert.Equal(24, cells.Count);
            Assert.Contains((0, 0), cells);
            Assert.Contains((-1, 0), cells);
            Assert.Contains((1, -7), cells);
            Assert.DoesNotContain((0, -8), cells); // marches range cells starting AT the origin row
            Assert.DoesNotContain((2, 0), cells);
        }

        [Fact]
        public void QuadCells_EvenWidth_IsAsymmetricPerScanQuad()
        {
            // ScanQuad: even width w spans -(w/2-1) .. w/2 across the travel axis.
            var cells = AsSet(AreaOfEffectBuilder.QuadCells(2, 1, DiscreteShadowCaster.Direction.South));
            Assert.Contains((0, 0), cells);
            Assert.Contains((1, 0), cells);
            Assert.DoesNotContain((-1, 0), cells);
        }

        // ── Element intake diamond ──────────────────────────────────────────────

        [Fact]
        public void DiamondCells_Deodorizer_ReachesTwoCellsManhattan()
        {
            // Deodorizer (AirFilter): consumptionRadius 3 -> reach = 2 orthogonal steps.
            var cells = AsSet(AreaOfEffectBuilder.DiamondCells(2));
            Assert.Equal(13, cells.Count); // 1 + 4 + 8
            Assert.Contains((0, 2), cells);
            Assert.Contains((1, 1), cells);
            Assert.DoesNotContain((2, 1), cells);
        }

        [Fact]
        public void DiamondCells_RadiusOne_IsJustTheSampleCell()
        {
            // Pumps et al with consumptionRadius 1 only touch the sample cell.
            var cells = AreaOfEffectBuilder.DiamondCells(0);
            Assert.Single(cells);
            Assert.Equal(0, (int)cells[0].x);
            Assert.Equal(0, (int)cells[0].y);
        }

        // ── Origin cell math ────────────────────────────────────────────────────

        [Fact]
        public void PosToCellOffset_MatchesGridPosToCellForBuildingPivot()
        {
            // Buildings sit at the bottom-center of their origin cell, so
            // cell = (floor(0.5+x), floor(y)).
            var ceilingLight = AreaOfEffectBuilder.PosToCellOffset(0.05f, 0.65f);
            Assert.Equal((0f, 0f), (ceilingLight.x, ceilingLight.y));
            var floorLamp = AreaOfEffectBuilder.PosToCellOffset(0.05f, 1.5f);
            Assert.Equal((0f, 1f), (floorLamp.x, floorLamp.y));
            var saltPlant = AreaOfEffectBuilder.PosToCellOffset(0f, -1f);
            Assert.Equal((0f, -1f), (saltPlant.x, saltPlant.y));
        }

        // ── Serialization contract ──────────────────────────────────────────────

        [Fact]
        public void Cells_SerializeAsCompactIntPairs_WithExportSettings()
        {
            var aoe = new OutAreaOfEffect
            {
                kind = "elementIntake",
                source = "ElementConsumer",
                shape = "diamond",
                origin = new BVector2(0, 0),
                blockedBySolids = true,
                cells = new List<BVector2> { new BVector2(0, 0), new BVector2(1, -1) },
            };
            var json = JsonConvert.SerializeObject(aoe, BaseExport.BuildSerializerSettings());
            Assert.Contains("[[0,0],[1,-1]]", json);
        }

        [Fact]
        public void OptionalFields_OmittedWhenNull_WithExportSettings()
        {
            var aoe = new OutAreaOfEffect
            {
                kind = "skyScan",
                source = "ScannerNetworkVisualizer",
                shape = "skyColumns",
                origin = new BVector2(0, 0),
                blockedBySolids = true,
                scanMinX = -15,
                scanMaxX = 15,
            };
            var j = JObject.Parse(JsonConvert.SerializeObject(aoe, BaseExport.BuildSerializerSettings()));
            Assert.Null(j["cells"]);   // no null-cells noise for param-only shapes
            Assert.Null(j["lux"]);
            Assert.Null(j["rectMin"]);
            Assert.Equal(-15, (int)j["scanMinX"]);
            Assert.Equal("skyColumns", (string)j["shape"]);
        }
    }
}
