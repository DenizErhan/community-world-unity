// The map camera. "Design space" is the 720x1280 virtual screen of the prototype: one design
// pixel is S real pixels (S = the smaller of width/720 and height/1280, like Godot's
// canvas_items + expand stretch), so UI sizes and tap distances match the prototype on any phone.
using UnityEngine;

namespace CommunityWorld
{
    public class CameraRig
    {
        public const float ZoomMin = 0.3f;
        public const float ZoomMax = 1.6f;
        public const float StartZoom = 0.4f;

        public readonly Camera Cam;
        public Vec2 Position;
        public double Zoom = StartZoom;

        private readonly Geo geo;
        private readonly int gridW;
        private readonly int gridH;

        public CameraRig(Geo geo, int gridW, int gridH, Color clear)
        {
            this.geo = geo;
            this.gridW = gridW;
            this.gridH = gridH;
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Cam = go.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = clear;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 100f;
            Position = geo.TileCenter(gridW * 0.5 - 0.5, gridH * 0.5 - 0.5) + new Vec2(0, 60);
            Apply();
        }

        /// <summary>Real pixels per design pixel.</summary>
        public static float S { get { return Mathf.Max(0.0001f, Mathf.Min(Screen.width / 720f, Screen.height / 1280f)); } }

        /// <summary>Size of the visible area in design pixels (at least 720 x 1280).</summary>
        public static Vec2 Viewport { get { return new Vec2(Screen.width / S, Screen.height / S); } }

        public void Apply()
        {
            Cam.orthographicSize = (float)(Viewport.Y / (2.0 * Zoom)) / Gfx.Unit;
            Cam.transform.position = new Vector3((float)Position.X / Gfx.Unit, -(float)Position.Y / Gfx.Unit, -10f);
        }

        /// <summary>Real screen point (y up, as Unity gives it) -> design-space point on the 720x1280 screen (y down).</summary>
        public static Vec2 ScreenToDesign(Vector2 screen)
        {
            return new Vec2(screen.x / S, (Screen.height - screen.y) / S);
        }

        /// <summary>Screen (design px, y down) -> map position.</summary>
        public Vec2 ScreenToWorld(Vec2 d)
        {
            Vec2 vp = Viewport;
            return new Vec2(Position.X + (d.X - vp.X * 0.5) / Zoom, Position.Y + (d.Y - vp.Y * 0.5) / Zoom);
        }

        /// <summary>Map position -> screen (design px, y down).</summary>
        public Vec2 WorldToScreen(Vec2 w)
        {
            Vec2 vp = Viewport;
            return new Vec2(vp.X * 0.5 + (w.X - Position.X) * Zoom, vp.Y * 0.5 + (w.Y - Position.Y) * Zoom);
        }

        public Tile ScreenToTile(Vec2 d) { return geo.WorldToTile(ScreenToWorld(d)); }

        public Rect VisibleRect(double margin)
        {
            Vec2 vp = Viewport;
            double w = vp.X / Zoom;
            double h = vp.Y / Zoom;
            return new Rect((float)(Position.X - w * 0.5 - margin), (float)(Position.Y - h * 0.5 - margin), (float)(w + 2 * margin), (float)(h + 2 * margin));
        }

        public void Clamp()
        {
            double minX = geo.TileCenter(0, gridH - 1).X - geo.TW * 0.5;
            double maxX = geo.TileCenter(gridW - 1, 0).X + geo.TW * 0.5;
            double minY = -geo.TH * 0.5;
            double maxY = geo.TileCenter(gridW - 1, gridH - 1).Y + geo.TH * 0.5 + geo.SlabDepth;
            Position = new Vec2(System.Math.Max(minX, System.Math.Min(maxX, Position.X)), System.Math.Max(minY, System.Math.Min(maxY, Position.Y)));
        }

        public void SetZoom(double z)
        {
            Zoom = System.Math.Max(ZoomMin, System.Math.Min(ZoomMax, z));
            Clamp();
        }

        public void Pan(Vec2 designDelta)
        {
            Position = new Vec2(Position.X - designDelta.X / Zoom, Position.Y - designDelta.Y / Zoom);
            Clamp();
        }
    }
}
