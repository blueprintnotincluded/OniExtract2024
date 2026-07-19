using System.Collections.Generic;
using UnityEngine;

namespace OniExtract2024
{
    // Collects every area-of-effect a building projects onto surrounding cells, reading
    // only components that exist on the BuildingComplete prefab (all are configured in
    // ConfigureBuildingTemplate/DoPostConfigureComplete, so they are prefab-readable —
    // see GAME_INTERNALS.md "Building configuration lifecycle").
    //
    // Sources, in emit order:
    //   Light2D                  -> kind "light"          (lamps, sun lamp, mercury light, ...)
    //   ElementConsumer          -> kind "elementIntake"  (deodorizer, pumps, liquid-cooled fan, ...)
    //   RangeVisualizer          -> kind "operationRange" (robo-miner, sweeper, space heater, ...)
    //   ScannerNetworkVisualizer -> kind "skyScan"        (space scanner)
    //   SkyVisibilityVisualizer  -> kind "skyScan"        (telescopes)
    //   RadiationEmitter         -> kind "radiation"      (research reactor, radbolt spawner, ...)
    //
    // Geometry semantics are replicated from the game's own cell math (DiscreteShadowCaster
    // for light, the sim's orthogonal spread for consumers, RangeVisualizerEffect for rects)
    // with occlusion removed: cells[] is the nominal, unobstructed area. AREA_OF_EFFECT.md
    // documents each shape with worked examples.
    public static class AreaOfEffectBuilder
    {
        // Safety cap: params always describe the shape; drop the cell list if some modded
        // building produces an absurd area (keeps building.json bounded).
        internal const int MaxCells = 1024;

        public static List<OutAreaOfEffect> Build(GameObject go)
        {
            var list = new List<OutAreaOfEffect>();
            foreach (Light2D light in go.GetComponents<Light2D>())
                AddLight(list, light);
            // GetComponents<ElementConsumer> also returns PassiveElementConsumer subclasses.
            foreach (ElementConsumer consumer in go.GetComponents<ElementConsumer>())
                AddElementIntake(list, consumer);
            foreach (RangeVisualizer vis in go.GetComponents<RangeVisualizer>())
                AddOperationRange(list, vis);
            foreach (ScannerNetworkVisualizer vis in go.GetComponents<ScannerNetworkVisualizer>())
                AddSkyScan(list, "ScannerNetworkVisualizer", vis.OriginOffset, vis.RangeMin, vis.RangeMax, 0);
            foreach (SkyVisibilityVisualizer vis in go.GetComponents<SkyVisibilityVisualizer>())
                AddSkyScan(list, "SkyVisibilityVisualizer", vis.OriginOffset, vis.RangeMin, vis.RangeMax, vis.ScanVerticalStep);
            foreach (RadiationEmitter emitter in go.GetComponents<RadiationEmitter>())
                AddRadiation(list, emitter);
            return list;
        }

        private static void AddLight(List<OutAreaOfEffect> list, Light2D light)
        {
            if (light == null || light.Lux <= 0 || light.Range <= 0f)
                return;
            int range = (int)light.Range; // Light2D truncates Range for its cell extents
            BVector2 origin = PosToCellOffset(light.Offset.x, light.Offset.y);
            var aoe = new OutAreaOfEffect
            {
                kind = "light",
                source = "Light2D",
                origin = origin,
                blockedBySolids = true,
                range = light.Range,
                lux = light.Lux,
                falloffRate = light.FalloffRate,
                lightColor = new BColor(light.Color),
            };
            switch (light.shape)
            {
                case LightShape.Circle:
                    aoe.shape = "circle";
                    aoe.cells = Translate(CircleCells(range), origin);
                    break;
                case LightShape.Cone:
                    aoe.shape = "cone";
                    aoe.cells = Translate(ConeCells(range), origin);
                    break;
                case LightShape.Quad:
                    aoe.shape = "quad";
                    aoe.width = light.Width;
                    aoe.direction = light.LightDirection.ToString();
                    aoe.cells = Translate(QuadCells(light.Width, range, light.LightDirection), origin);
                    break;
                default:
                    // PLib et al. register custom LightShape values; export params only.
                    aoe.shape = light.shape.ToString();
                    break;
            }
            list.Add(CapCells(aoe));
        }

        private static void AddElementIntake(List<OutAreaOfEffect> list, ElementConsumer consumer)
        {
            if (consumer == null)
                return;
            BVector2 origin = PosToCellOffset(consumer.sampleCellOffset.x, consumer.sampleCellOffset.y);
            string element = consumer.configuration == ElementConsumer.Configuration.Element
                ? consumer.elementToConsume.ToString()
                : consumer.configuration.ToString(); // AllGas / AllLiquid
            list.Add(CapCells(new OutAreaOfEffect
            {
                kind = "elementIntake",
                source = consumer is PassiveElementConsumer ? "PassiveElementConsumer" : "ElementConsumer",
                shape = "diamond",
                origin = origin,
                blockedBySolids = true,
                radius = consumer.consumptionRadius,
                element = element,
                consumptionRate = consumer.consumptionRate,
                cells = Translate(DiamondCells(consumer.consumptionRadius - 1), origin),
            }));
        }

        private static void AddOperationRange(List<OutAreaOfEffect> list, RangeVisualizer vis)
        {
            if (vis == null)
                return;
            // A default (0,0)-(0,0) rect means the range is driven at runtime (WaterTrap's
            // trail grows down through water) — nothing static to export.
            if (vis.RangeMin.x == 0 && vis.RangeMin.y == 0 && vis.RangeMax.x == 0 && vis.RangeMax.y == 0)
                return;
            var cells = new List<BVector2>();
            for (int y = vis.RangeMin.y; y <= vis.RangeMax.y; y++)
                for (int x = vis.RangeMin.x; x <= vis.RangeMax.x; x++)
                    cells.Add(new BVector2(vis.OriginOffset.x + x, vis.OriginOffset.y + y));
            list.Add(CapCells(new OutAreaOfEffect
            {
                kind = "operationRange",
                source = "RangeVisualizer",
                shape = "rect",
                origin = new BVector2(vis.OriginOffset.x, vis.OriginOffset.y),
                blockedBySolids = vis.TestLineOfSight,
                rectMin = new BVector2(vis.RangeMin.x, vis.RangeMin.y),
                rectMax = new BVector2(vis.RangeMax.x, vis.RangeMax.y),
                cells = cells,
            }));
        }

        private static void AddSkyScan(List<OutAreaOfEffect> list, string source, Vector2I originOffset, int rangeMin, int rangeMax, int verticalStep)
        {
            list.Add(new OutAreaOfEffect
            {
                kind = "skyScan",
                source = source,
                shape = "skyColumns",
                origin = new BVector2(originOffset.x, originOffset.y),
                blockedBySolids = true,
                scanMinX = rangeMin,
                scanMaxX = rangeMax,
                verticalStep = verticalStep,
                // cells omitted: the scanned columns extend to the top of the world,
                // which depends on the map the building is placed in.
            });
        }

        private static void AddRadiation(List<OutAreaOfEffect> list, RadiationEmitter emitter)
        {
            if (emitter == null)
                return;
            bool partialArc = emitter.emitAngle < 360f;
            list.Add(new OutAreaOfEffect
            {
                kind = "radiation",
                source = "RadiationEmitter",
                shape = partialArc ? "ellipseArc" : "ellipse",
                origin = PosToCellOffset(emitter.emissionOffset.x, emitter.emissionOffset.y),
                blockedBySolids = true, // the sim attenuates radiation through solids by mass
                radiusX = emitter.emitRadiusX,
                radiusY = emitter.emitRadiusY,
                rads = emitter.emitRads,
                arcAngle = partialArc ? (float?)emitter.emitAngle : null,
                arcDirection = partialArc ? (float?)emitter.emitDirection : null,
                emitType = emitter.emitType.ToString(),
                radiusScalesWithRads = emitter.radiusProportionalToRads ? (bool?)true : null,
                // cells omitted: derive from (dx/radiusX)^2 + (dy/radiusY)^2 <= 1 — the
                // Research Reactor's 25x25 ellipse alone would add ~2000 entries.
            });
        }

        // Building prefabs sit at the bottom-center of their origin cell (CellToPosCBC),
        // so a float emission offset lands in cell (floor(0.5+x), floor(y)) — the same math
        // Grid.PosToCell applies to transform.position + offset at runtime.
        internal static BVector2 PosToCellOffset(float x, float y)
        {
            return new BVector2(Mathf.FloorToInt(0.5f + x), Mathf.FloorToInt(y));
        }

        // LightShape.Circle per DiscreteShadowCaster: every cell within Euclidean
        // distance <= range of the origin (squared-distance test, boundary inclusive).
        internal static List<BVector2> CircleCells(int range)
        {
            var cells = new List<BVector2>();
            int rangeSq = range * range;
            for (int dy = -range; dy <= range; dy++)
                for (int dx = -range; dx <= range; dx++)
                    if (dx * dx + dy * dy <= rangeSq)
                        cells.Add(new BVector2(dx, dy));
            return cells;
        }

        // LightShape.Cone per DiscreteShadowCaster (the two southern octants): the origin
        // cell plus, for each row d below it, cells within 45 degrees (|dx| <= d) that are
        // also within Euclidean range.
        internal static List<BVector2> ConeCells(int range)
        {
            var cells = new List<BVector2> { new BVector2(0, 0) };
            int rangeSq = range * range;
            for (int d = 1; d <= range; d++)
                for (int dx = -d; dx <= d; dx++)
                    if (dx * dx + d * d <= rangeSq)
                        cells.Add(new BVector2(dx, -d));
            return cells;
        }

        // LightShape.Quad per DiscreteShadowCaster.ScanQuad: a strip `width` cells across
        // (asymmetric for even widths: offsets -(width/2-1) .. width/2), extruded `range`
        // cells along `direction` starting at the origin row; plus the origin cell itself.
        internal static List<BVector2> QuadCells(int width, int range, DiscreteShadowCaster.Direction direction)
        {
            var cells = new List<BVector2>();
            var seen = new HashSet<int>();
            AddUnique(cells, seen, 0, 0); // GetVisibleCells always includes the emitting cell
            if (width <= 0 || range <= 0)
                return cells;
            int dirX = 0, dirY = 0, orthoX = 0, orthoY = 0;
            switch (direction)
            {
                case DiscreteShadowCaster.Direction.North: dirY = 1; orthoX = 1; break;
                case DiscreteShadowCaster.Direction.South: dirY = -1; orthoX = 1; break;
                case DiscreteShadowCaster.Direction.East: dirX = 1; orthoY = 1; break;
                case DiscreteShadowCaster.Direction.West: dirX = -1; orthoY = 1; break;
            }
            int start = (width % 2 == 0) ? (width / 2 - 1) : (width - 1) / 2;
            for (int i = 0; i < width; i++)
            {
                int sx = (i - start) * orthoX, sy = (i - start) * orthoY;
                for (int t = 0; t < range; t++)
                    AddUnique(cells, seen, sx + dirX * t, sy + dirY * t);
            }
            return cells;
        }

        // ElementConsumer reach: the sim spreads orthogonally from the sample cell up to
        // consumptionRadius-1 steps (confirmed against Show Building Ranges' BFS), which in
        // open space is the Manhattan diamond |dx|+|dy| <= reach.
        internal static List<BVector2> DiamondCells(int reach)
        {
            if (reach < 0)
                reach = 0;
            var cells = new List<BVector2>();
            for (int dy = -reach; dy <= reach; dy++)
            {
                int span = reach - (dy < 0 ? -dy : dy);
                for (int dx = -span; dx <= span; dx++)
                    cells.Add(new BVector2(dx, dy));
            }
            return cells;
        }

        internal static List<BVector2> Translate(List<BVector2> cells, BVector2 origin)
        {
            if (origin.x == 0 && origin.y == 0)
                return cells;
            var moved = new List<BVector2>(cells.Count);
            foreach (BVector2 c in cells)
                moved.Add(new BVector2(c.x + origin.x, c.y + origin.y));
            return moved;
        }

        private static void AddUnique(List<BVector2> cells, HashSet<int> seen, int x, int y)
        {
            int key = (x << 16) ^ (y & 0xFFFF);
            if (seen.Add(key))
                cells.Add(new BVector2(x, y));
        }

        private static OutAreaOfEffect CapCells(OutAreaOfEffect aoe)
        {
            if (aoe.cells != null && aoe.cells.Count > MaxCells)
                aoe.cells = null;
            return aoe;
        }
    }
}
