using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Coordinates and cover match src/world/Layout.ts in the browser game.
    public static class PrototypeLayout
    {
        public const float MapSize = 720f;
        public static readonly Vector2 BlueBase = new Vector2(-311f, -302f);
        public static readonly Vector2 RedBase = new Vector2(311f, 302f);
        public static readonly Vector2 BlueTankDisplay = new Vector2(-45f, -101f);
        public static readonly Vector2 RedTankDisplay = new Vector2(45f, 101f);

        public readonly struct Objective
        {
            public readonly string Id;
            public readonly string Name;
            public readonly Vector2 Position;

            public Objective(string id, string name, float x, float z)
            {
                Id = id;
                Name = name;
                Position = new Vector2(x, z);
            }
        }

        public readonly struct Block
        {
            public readonly Vector2 Position;
            public readonly float Width;
            public readonly float Depth;
            public readonly float Height;
            public readonly string Kind;

            public Block(float x, float z, float width, float depth, float height, string kind)
            {
                Position = new Vector2(x, z);
                Width = width;
                Depth = depth;
                Height = height;
                Kind = kind;
            }
        }

        public static readonly Objective[] Objectives =
        {
            new Objective("A", "Industrial Complex", -201f, -143f),
            new Objective("B", "Central Village", -20f, -93f),
            new Objective("C", "Railway Station", 17f, 81f),
            new Objective("D", "Military Base", 199f, 157f),
            new Objective("E", "Communications Facility", -189f, 185f)
        };

        public static readonly IReadOnlyList<Block> Blocks = BuildBlocks();

        public static float HeightAt(float x, float z)
        {
            return Hill(x, z, -270f, 175f, 110f, 17f)
                 + Hill(x, z, 245f, -210f, 120f, 16f)
                 + Hill(x, z, 285f, 265f, 105f, 8f);
        }

        private static float Hill(float x, float z, float hx, float hz, float radius, float height)
        {
            float dx = x - hx;
            float dz = z - hz;
            return height * Mathf.Exp(-(dx * dx + dz * dz) / (radius * radius));
        }

        public static bool Collides(Vector2 point, float radius)
        {
            if (Mathf.Abs(point.x) > MapSize / 2f - radius ||
                Mathf.Abs(point.y) > MapSize / 2f - radius)
                return true;

            foreach (Block block in Blocks)
            {
                float x = Mathf.Clamp(point.x, block.Position.x - block.Width / 2f,
                    block.Position.x + block.Width / 2f);
                float z = Mathf.Clamp(point.y, block.Position.y - block.Depth / 2f,
                    block.Position.y + block.Depth / 2f);
                float dx = point.x - x;
                float dz = point.y - z;
                if (dx * dx + dz * dz < radius * radius) return true;
            }
            return false;
        }

        public static bool LineBlocked(Vector2 from, Vector2 to, float padding = 0f)
        {
            float dx = to.x - from.x;
            float dz = to.y - from.y;
            foreach (Block block in Blocks)
            {
                float minX = block.Position.x - block.Width / 2f - padding;
                float maxX = block.Position.x + block.Width / 2f + padding;
                float minZ = block.Position.y - block.Depth / 2f - padding;
                float maxZ = block.Position.y + block.Depth / 2f + padding;
                float enter = 0f;
                float exit = 1f;

                if (!ClipAxis(from.x, dx, minX, maxX, ref enter, ref exit) ||
                    !ClipAxis(from.y, dz, minZ, maxZ, ref enter, ref exit))
                    continue;
                if (enter <= exit && exit > 0.02f && enter < 0.98f) return true;
            }
            return false;
        }

        private static bool ClipAxis(float origin, float delta, float min, float max,
            ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < 0.00000001f) return origin >= min && origin <= max;
            float a = (min - origin) / delta;
            float b = (max - origin) / delta;
            if (a > b) (a, b) = (b, a);
            enter = Mathf.Max(enter, a);
            exit = Mathf.Min(exit, b);
            return enter <= exit;
        }

        private static IReadOnlyList<Block> BuildBlocks()
        {
            var blocks = new List<Block>();
            void Add(float x, float z, float w, float d, float h, string kind) =>
                blocks.Add(new Block(x, z, w, d, h, kind));

            // Visual-only T90s are solid scenery; route AI around their footprints.
            Add(BlueTankDisplay.x, BlueTankDisplay.y, 7f, 10f, 3.3f, "tank-preview");
            Add(RedTankDisplay.x, RedTankDisplay.y, 7f, 10f, 3.3f, "tank-preview");

            Add(-269, -170, 37, 30, 15, "factory");
            Add(-250, -102, 48, 25, 10, "factory");
            Add(-163, -191, 22, 50, 16, "factory");
            for (int i = 0; i < 6; i++) Add(-292 + i * 18, -215, 13, 7, 4, "container");
            foreach (Vector2 p in new[] { new Vector2(-83,-142), new Vector2(13,-150),
                         new Vector2(65,-121), new Vector2(-100,-55), new Vector2(50,-38) })
                Add(p.x, p.y, 22, 20, 9, "house");
            for (int i = 0; i < 4; i++)
            {
                Add(95 + i * 28, 53, 18, 9, 6, "railcar");
                Add(-104 + i * 28, 130, 18, 9, 6, "railcar");
            }
            Add(82, 127, 29, 22, 11, "factory");
            Add(247, 207, 43, 19, 9, "barracks");
            Add(254, 114, 38, 20, 9, "barracks");
            Add(151, 209, 28, 22, 10, "barracks");
            Add(-246, 229, 29, 22, 9, "factory");
            Add(-130, 232, 34, 20, 8, "barracks");
            foreach (Vector2 p in new[] { new Vector2(-29,189), new Vector2(96,-201) })
                foreach (int x in new[] { -13, 13 })
                    foreach (int z in new[] { -10, 10 })
                        Add(p.x + x, p.y + z, 2.8f, 2.8f, 7, "shedpost");

            foreach (Vector2 p in new[] { new Vector2(-7,-16), new Vector2(-40,146),
                         new Vector2(116,-160) })
            {
                float d = p.x == -7 ? 24f : p.x == -40 ? 23f : 32f;
                float w = p.x == 116 ? 32f : 29f;
                float h = p.x == -7 ? 7.6f : p.x == -40 ? 7.3f : 7f;
                Add(p.x, p.y - d / 2f, w, 2.3f, h, "wall");
                Add(p.x - w / 2f, p.y, 2.3f, d, h, "wall");
                Add(p.x + w / 2f, p.y, 2.3f, d, h, "wall");
            }

            foreach (Vector2 p in new[] {
                new Vector2(-193,-212),new Vector2(-160,-106),new Vector2(-132,-159),
                new Vector2(-45,-170),new Vector2(34,-70),new Vector2(8,-192),
                new Vector2(-68,3),new Vector2(41,21),new Vector2(-65,68),
                new Vector2(55,166),new Vector2(113,181),new Vector2(170,100),
                new Vector2(229,95),new Vector2(204,222),new Vector2(-225,118),
                new Vector2(-143,126),new Vector2(-232,277),new Vector2(-101,209),
                new Vector2(132,-119),new Vector2(-4,240),new Vector2(108,12),
                new Vector2(-285,-30),new Vector2(275,-46),new Vector2(260,-147) })
                Add(p.x, p.y, 10, 5, 3, "crate");

            // Low cover just outside the B/C capture circles gives both teams a
            // reachable position to fight from without sealing the objectives.
            foreach (Vector2 p in new[] {
                new Vector2(-48,-91), new Vector2(10,-102), new Vector2(-18,-125),
                new Vector2(-14,75), new Vector2(44,88), new Vector2(12,47),
                new Vector2(58,100) })
                Add(p.x, p.y, 7, 4, 2.8f, "crate");
            return blocks;
        }
    }
}
