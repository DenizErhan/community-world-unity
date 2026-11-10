// Plain 2D math for the isometric grid, in "design space": the same numbers as the design
// tokens (tile 128x64, y grows DOWN). The Unity view converts to world units at the edge.
using System;

namespace CommunityWorld
{
    public struct Vec2
    {
        public double X;
        public double Y;
        public Vec2(double x, double y) { X = x; Y = y; }
        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Y + b.Y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Y - b.Y); }
        public static Vec2 operator *(Vec2 a, double k) { return new Vec2(a.X * k, a.Y * k); }
        public double DistanceTo(Vec2 o) { double dx = X - o.X, dy = Y - o.Y; return Math.Sqrt(dx * dx + dy * dy); }
        public override string ToString() { return "(" + X + ", " + Y + ")"; }
    }

    public struct Tile : IEquatable<Tile>
    {
        public readonly int X;
        public readonly int Y;
        public Tile(int x, int y) { X = x; Y = y; }

        /// <summary>"No tile" marker, used for nothing selected / not moving.</summary>
        public static readonly Tile None = new Tile(-1, -1);
        public bool IsNone { get { return X == -1 && Y == -1; } }

        public bool Equals(Tile o) { return X == o.X && Y == o.Y; }
        public override bool Equals(object o) { return o is Tile && Equals((Tile)o); }
        public override int GetHashCode() { return X * 73856093 ^ Y * 19349663; }
        public static bool operator ==(Tile a, Tile b) { return a.X == b.X && a.Y == b.Y; }
        public static bool operator !=(Tile a, Tile b) { return !(a == b); }
        public static Tile operator +(Tile a, Tile b) { return new Tile(a.X + b.X, a.Y + b.Y); }
        public override string ToString() { return "(" + X + ", " + Y + ")"; }
    }

    public class Geo
    {
        public readonly double TW;
        public readonly double TH;
        public readonly double SlabDepth;
        public readonly double SpriteAnchorY;
        public readonly double PickHeight;
        public readonly double PickMinPx;
        public readonly double GrabPx;
        public readonly double BadgeLift;
        public readonly double BadgeRadius;

        public Geo(JObj tokens)
        {
            JObj g = J.O(tokens, "grid");
            TW = J.N(g, "tile_w", 128);
            TH = J.N(g, "tile_h", 64);
            SlabDepth = J.N(g, "slab_depth", 26);
            System.Collections.Generic.List<object> anchor = J.A(g, "sprite_anchor");
            SpriteAnchorY = anchor.Count > 1 ? J.Num(anchor[1], 88) : 88;
            JObj ui = J.O(tokens, "ui");
            JObj pl = J.O(ui, "placement");
            PickHeight = J.N(pl, "pick_height", 70);
            PickMinPx = J.N(pl, "pick_min_px", 44);
            GrabPx = J.N(pl, "grab_px", 110);
            JObj bd = J.O(ui, "badge");
            BadgeLift = J.N(bd, "lift", 100);
            BadgeRadius = J.N(bd, "radius", 26);
        }

        public Vec2 TileCenter(double gx, double gy)
        {
            return new Vec2((gx - gy) * TW * 0.5, (gx + gy) * TH * 0.5);
        }

        public Vec2 TileCenter(Tile t) { return TileCenter(t.X, t.Y); }

        public Tile WorldToTile(Vec2 w)
        {
            double u = (w.X / (TW * 0.5) + w.Y / (TH * 0.5)) * 0.5;
            double v = (w.Y / (TH * 0.5) - w.X / (TW * 0.5)) * 0.5;
            return new Tile(Round(u), Round(v));
        }

        /// <summary>Rounds half away from zero (matches the Godot prototype).</summary>
        public static int Round(double x) { return (int)Math.Round(x, MidpointRounding.AwayFromZero); }
    }
}
