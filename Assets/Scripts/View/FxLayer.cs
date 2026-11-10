// Light animation layer drawn above the world. Everything it draws is described in the design
// tokens under "motion" (loops per building, ambient life, one-shot events). It never takes input
// and never changes game state. It switches itself off when the browser/OS asks for reduced motion.
// Shapes are batched into one dynamic mesh (soft discs, lines, triangles) rebuilt at motion.fps.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace CommunityWorld
{
    public class FxLayer
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int CW_PrefersReducedMotion();
#endif

        private class Ev
        {
            public string Kind;
            public Vec2 Pos;
            public double T0;
            public double Dur;
            public double Seed;
            public string Text;
            public Color Col;
            public Text Label;
        }

        public bool Enabled = true;

        private readonly GameSession session;
        private readonly CameraRig cam;
        private readonly RectTransform canvas;
        private readonly JObj tokens;
        private readonly JObj m;
        private readonly string age;
        private readonly JObj pal;
        private readonly Geo geo;
        private readonly List<Ev> events = new List<Ev>();
        private readonly System.Random rng = new System.Random();
        private double acc;

        // mesh batch
        private readonly Mesh mesh;
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Color> cols = new List<Color>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> tris = new List<int>();
        private static readonly Vector2 Solid = new Vector2(0.5f, 0.5f);

        public FxLayer(GameSession session, CameraRig cam, RectTransform canvas, JObj tokens, string age)
        {
            this.session = session;
            this.cam = cam;
            this.canvas = canvas;
            this.tokens = tokens;
            this.age = age;
            geo = session.Geo;
            m = J.O(tokens, "motion");
            pal = J.O(J.O(J.O(tokens, "ages"), age), "palette");
            Enabled = J.Bool(m["enabled"], true);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (CW_PrefersReducedMotion() == 1) Enabled = false;
#endif
            mesh = new Mesh();
            mesh.MarkDynamic();
            GameObject go = new GameObject("Fx");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            Material mat = new Material(Gfx.SpriteMaterial);
            mat.mainTexture = Gfx.CircleTexture;
            mr.sharedMaterial = mat;
            mr.sortingOrder = 50000;
        }

        // ------------------------------------------------------------ one-shot events

        public void BurstDust(Tile t) { Add("dust", geo.TileCenter(t), J.N(J.O(J.O(m, "events"), "place"), "dur", 0.6), null, Color.white); }

        public void Sparkle(Tile t) { Add("sparkle", geo.TileCenter(t), J.N(J.O(J.O(m, "events"), "upgrade"), "dur", 1.1), null, Color.white); }

        public void FloatText(Vec2 pos, string text, Color col) { Add("text", pos, J.N(J.O(J.O(m, "events"), "collect"), "dur", 1.0), text, col); }

        public int EventCount { get { return events.Count; } }

        private static double Now { get { return Time.realtimeSinceStartupAsDouble; } }

        private void Add(string kind, Vec2 pos, double dur, string text, Color col)
        {
            if (!Enabled) return;
            Ev e = new Ev { Kind = kind, Pos = pos, T0 = Now, Dur = dur, Seed = rng.NextDouble() * 100.0, Text = text, Col = col };
            if (kind == "text")
            {
                GameObject go = new GameObject("FloatText", typeof(RectTransform));
                go.transform.SetParent(canvas, false);
                Text t = go.AddComponent<Text>();
                t.font = Gfx.Font;
                t.fontSize = (int)J.N(J.O(J.O(m, "events"), "collect"), "font", 22);
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                t.text = text;
                RectTransform rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(140, 40);
                Outline o = go.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.7f);
                o.effectDistance = new Vector2(2, -2);
                e.Label = t;
            }
            events.Add(e);
        }

        // ------------------------------------------------------------ helpers

        private static double Rnd(double x)
        {
            double v = Math.Sin(x * 12.9898) * 43758.5453;
            return v - Math.Floor(v);
        }

        private static double Phase(Tile t) { return Rnd(t.X * 127 + t.Y * 311); }
        private static double Out(double p) { return 1.0 - Math.Pow(1.0 - p, 3.0); }
        private static double Lerp(double a, double b, double t) { return a + (b - a) * t; }
        private static double Frac(double x) { return x - Math.Floor(x); }

        private Color Col(string role, double a = 1.0)
        {
            return Gfx.WithAlpha(Gfx.Hex(J.S(J.O(m, "colors"), role)), (float)a);
        }

        // ------------------------------------------------------------ per frame

        public void Update(float dt)
        {
            if (!Enabled)
            {
                ClearEvents();
                if (mesh.vertexCount > 0) mesh.Clear();
                return;
            }
            double now = Now;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                Ev e = events[i];
                if (now - e.T0 >= e.Dur)
                {
                    if (e.Label != null) UnityEngine.Object.Destroy(e.Label.gameObject);
                    events.RemoveAt(i);
                }
            }
            UpdateTexts(now);
            acc += dt;
            if (acc >= 1.0 / J.N(m, "fps", 30))
            {
                acc = 0;
                Redraw(now);
            }
        }

        private void ClearEvents()
        {
            foreach (Ev e in events) if (e.Label != null) UnityEngine.Object.Destroy(e.Label.gameObject);
            events.Clear();
        }

        private void UpdateTexts(double now)
        {
            double rise = J.N(J.O(J.O(m, "events"), "collect"), "rise", 46);
            foreach (Ev e in events)
            {
                if (e.Label == null) continue;
                double p = (now - e.T0) / e.Dur;
                Vec2 w = e.Pos + new Vec2(0, -rise * Out(p));
                Vec2 s = cam.WorldToScreen(w);
                e.Label.rectTransform.anchoredPosition = new Vector2((float)s.X, -(float)s.Y);
                e.Label.color = new Color(e.Col.r, e.Col.g, e.Col.b, (float)Math.Max(0, 1.0 - p * p));
            }
        }

        private void Redraw(double now)
        {
            verts.Clear(); cols.Clear(); uvs.Clear(); tris.Clear();
            GameState st = session.State;
            double z = cam.Zoom;
            double k = Math.Max(1.0, Math.Min(2.2, J.N(m, "min_screen_scale", 0.7) / z));   // keep effects visible when zoomed out
            Rect view = cam.VisibleRect(J.N(m, "view_margin", 160));
            JObj loopsById = J.O(m, "loops");
            foreach (KeyValuePair<Tile, Placed> kv in st.Buildings)
            {
                Tile t = kv.Key;
                if (t == session.MovingFrom) continue;
                List<object> loops = J.A(loopsById, kv.Value.Id);
                if (loops.Count == 0) continue;
                Vec2 c = geo.TileCenter(t);
                if (!view.Contains(new Vector2((float)c.X, (float)c.Y))) continue;
                double ph = Phase(t);
                foreach (object o in loops)
                {
                    JObj lp = J.Obj(o);
                    switch (J.S(lp, "type"))
                    {
                        case "smoke": Smoke(lp, c, now, ph, k); break;
                        case "sway": Sway(lp, c, now, ph, k); break;
                        case "shimmer": Shimmer(lp, c, now, ph, k); break;
                        case "dust": Dust(lp, c, now, ph, k); break;
                    }
                }
            }
            Butterflies(now, k, view);
            foreach (Ev e in events)
            {
                double p = (now - e.T0) / e.Dur;
                if (p < 0.0 || p >= 1.0) continue;
                if (e.Kind == "dust") EvDust(e, p, k);
                else if (e.Kind == "sparkle") EvSparkle(e, p, k);
            }
            mesh.Clear();
            if (verts.Count == 0) return;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        // ------------------------------------------------------------ primitives (design space)

        private void Ellipse(Vec2 c, double rx, double ry, Color col)
        {
            int i = verts.Count;
            verts.Add(Gfx.ToWorld(new Vec2(c.X - rx, c.Y - ry)));
            verts.Add(Gfx.ToWorld(new Vec2(c.X + rx, c.Y - ry)));
            verts.Add(Gfx.ToWorld(new Vec2(c.X + rx, c.Y + ry)));
            verts.Add(Gfx.ToWorld(new Vec2(c.X - rx, c.Y + ry)));
            uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(0, 0));
            for (int k = 0; k < 4; k++) cols.Add(col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        private void Circle(Vec2 c, double r, Color col) { Ellipse(c, r, r, col); }

        private void Line(Vec2 a, Vec2 b, double width, Color col)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) return;
            double nx = -dy / len * width * 0.5, ny = dx / len * width * 0.5;
            int i = verts.Count;
            verts.Add(Gfx.ToWorld(new Vec2(a.X + nx, a.Y + ny)));
            verts.Add(Gfx.ToWorld(new Vec2(b.X + nx, b.Y + ny)));
            verts.Add(Gfx.ToWorld(new Vec2(b.X - nx, b.Y - ny)));
            verts.Add(Gfx.ToWorld(new Vec2(a.X - nx, a.Y - ny)));
            for (int k = 0; k < 4; k++) { uvs.Add(Solid); cols.Add(col); }
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        private void Tri(Vec2 a, Vec2 b, Vec2 c, Color col)
        {
            int i = verts.Count;
            verts.Add(Gfx.ToWorld(a)); verts.Add(Gfx.ToWorld(b)); verts.Add(Gfx.ToWorld(c));
            for (int k = 0; k < 3; k++) { uvs.Add(Solid); cols.Add(col); }
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        // ------------------------------------------------------------ loops

        private void Smoke(JObj lp, Vec2 c, double now, double ph, double k)
        {
            object a = lp["anchor"];
            if (a is JObj) { JObj byAge = (JObj)a; a = byAge.Has(age) ? byAge[age] : byAge["stone"]; }
            List<object> an = J.Arr(a);
            if (an.Count < 2) return;
            Vec2 o = c + new Vec2(J.Num(an[0]), J.Num(an[1]));
            int n = (int)J.N(lp, "puffs", 3);
            double period = J.N(lp, "period", 3.6), drift = J.N(lp, "drift", 14), rise = J.N(lp, "rise", 36);
            double r0 = J.N(lp, "r0", 4), r1 = J.N(lp, "r1", 11), alpha = J.N(lp, "alpha", 0.75);
            for (int i = 0; i < n; i++)
            {
                double p = Frac(now / period + (double)i / n + ph);
                Vec2 pos = o + new Vec2(drift * p * k + Math.Sin(p * 5.0 + i * 2.0 + ph * 6.0) * 3.0 * k, -rise * p * k);
                double al = alpha * (1.0 - p) * Math.Min(1.0, p * 8.0);
                Circle(pos, Lerp(r0, r1, p) * k, Col("smoke", al));
            }
        }

        private void Sway(JObj lp, Vec2 c, double now, double ph, double k)
        {
            int g = (int)J.N(lp, "grid", 3);
            double span = J.N(lp, "span", 0.55);
            double f = J.N(J.O(J.O(tokens, "shapes"), "farm"), "f", 0.9);
            double hw = geo.TW * 0.5 * f, hh = geo.TH * 0.5 * f;
            Vec2 top = c + new Vec2(0, J.N(lp, "top", -10));
            Color crop = Gfx.Hex(J.S(pal, "crop"));
            Color stalk = Gfx.Darkened(crop, 0.3f);
            Color head = Gfx.Lightened(crop, 0.45f);
            List<object> hs = J.A(lp, "height");
            double h0 = hs.Count > 1 ? J.Num(hs[0]) : 10, h1 = hs.Count > 1 ? J.Num(hs[1]) : 15;
            double amp = J.N(lp, "amp", 3.2), speed = J.N(lp, "speed", 1.7), wave = J.N(lp, "wave", 0.9);
            for (int i = 0; i < g; i++)
            {
                for (int j = 0; j < g; j++)
                {
                    double u = Lerp(-span, span, (double)i / (g - 1));
                    double v = Lerp(-span, span, (double)j / (g - 1));
                    Vec2 b = top + new Vec2((u - v) * hw * 0.5, (u + v) * hh * 0.5);
                    double h = Lerp(h0, h1, Rnd(i * 3 + j + ph * 10.0)) * k;
                    double sw = Math.Sin(now * speed + (u + v) * wave * Math.PI + ph * 6.0) * amp * k;
                    Vec2 tip = b + new Vec2(sw, -h);
                    Line(b, tip, 2.0 * k, stalk);
                    Circle(tip, 2.6 * k, head);
                }
            }
        }

        private void Shimmer(JObj lp, Vec2 c, double now, double ph, double k)
        {
            List<object> ce = J.A(lp, "center"), ha = J.A(lp, "half");
            if (ce.Count < 2 || ha.Count < 2) return;
            Vec2 cen = c + new Vec2(J.Num(ce[0]), J.Num(ce[1]));
            double halfX = J.Num(ha[0]), halfY = J.Num(ha[1]);
            double rp = Frac(now / J.N(lp, "ripple_period", 4.4) + ph);
            // ripple: an ellipse (squashed to half height) drawn as a ring of short lines
            double rr = 3.0 + halfX * 0.5 * rp;
            Color rc = Col("glint", 0.5 * (1.0 - rp));
            const int seg = 20;
            for (int i = 0; i < seg; i++)
            {
                double a0 = i * 2 * Math.PI / seg, a1 = (i + 1) * 2 * Math.PI / seg;
                Line(cen + new Vec2(Math.Cos(a0) * rr, Math.Sin(a0) * rr * 0.5), cen + new Vec2(Math.Cos(a1) * rr, Math.Sin(a1) * rr * 0.5), 1.6 * Math.Min(k, 1.4), rc);
            }
            int glints = (int)J.N(lp, "glints", 3);
            double period = J.N(lp, "period", 2.6);
            for (int i = 0; i < glints; i++)
            {
                double cyc = now / (period * (0.8 + 0.4 * Rnd(i))) + ph + i * 0.37;
                double cycle = Math.Floor(cyc);
                double p = cyc - cycle;
                double u = (Rnd(cycle * 7.1 + i * 3.3) * 2.0 - 1.0) * 0.65;
                double v = (Rnd(cycle * 5.3 + i * 9.1) * 2.0 - 1.0) * 0.65;
                Vec2 pos = cen + new Vec2((u - v) * halfX * 0.5, (u + v) * halfY * 0.5);
                Ellipse(pos, 3.4 * k, 3.4 * k * 0.4, Col("glint", Math.Sin(p * Math.PI) * 0.85));
            }
        }

        private void Dust(JObj lp, Vec2 c, double now, double ph, double k)
        {
            List<object> a = J.A(lp, "anchor");
            if (a.Count < 2) return;
            double period = J.N(lp, "period", 4.8), spread = J.N(lp, "spread", 18);
            double r0 = J.N(lp, "r0", 4), r1 = J.N(lp, "r1", 14), alpha = J.N(lp, "alpha", 0.65);
            int puffs = (int)J.N(lp, "puffs", 2);
            for (int i = 0; i < puffs; i++)
            {
                double cyc = now / period + ph + i * 0.5;
                double cycle = Math.Floor(cyc);
                double p = (cyc - cycle) / 0.5;      // a puff lives for the first half of its cycle, then rests
                if (p >= 1.0) continue;
                double x = J.Num(a[0]) + (Rnd(cycle * 3.7 + i * 5.1) * 2.0 - 1.0) * spread;
                Vec2 pos = c + new Vec2(x, J.Num(a[1]) - 10.0 * p * k);
                Circle(pos, Lerp(r0, r1, p) * k, Col("dust", alpha * (1.0 - p)));
            }
        }

        private void Butterflies(double now, double k, Rect view)
        {
            JObj b = J.O(J.O(m, "ambient"), "butterflies");
            List<object> palette = J.A(J.O(m, "colors"), "butterfly");
            if (palette.Count == 0) return;
            int gw = session.Data.GridW, gh = session.Data.GridH;
            double cx = gw * 0.5 - 0.5, cy = gh * 0.5 - 0.5;
            double sp = J.N(b, "speed", 0.32), area = J.N(b, "area", 0.7), size = J.N(b, "size", 16);
            int count = (int)J.N(b, "count", 3);
            for (int i = 0; i < count; i++)
            {
                double s = i * 2.1;
                double gx = cx + Math.Sin(now * sp * (1.0 + 0.3 * i) + s) * gw * area * 0.5 + Math.Sin(now * sp * 2.3 + s * 1.7) * 1.2;
                double gy = cy + Math.Cos(now * sp * (0.8 + 0.25 * i) + s * 1.3) * gh * area * 0.5;
                Vec2 pos = geo.TileCenter(gx, gy) + new Vec2(0, -18.0 + Math.Sin(now * 3.0 + s) * 4.0);
                if (!view.Contains(new Vector2((float)pos.X, (float)pos.Y))) continue;
                double flap = 0.25 + 0.75 * Math.Abs(Math.Sin(now * 14.0 + s));
                Color col = Gfx.Hex(J.Str(palette[i % palette.Count]));
                double sc = size / 9.0 * k;
                Tri(pos, pos + new Vec2(-6.0 * flap, -5) * sc, pos + new Vec2(-5.0 * flap, 3) * sc, col);
                Tri(pos, pos + new Vec2(6.0 * flap, -5) * sc, pos + new Vec2(5.0 * flap, 3) * sc, col);
                Line(pos + new Vec2(0, -2) * sc, pos + new Vec2(0, 3) * sc, 1.4 * sc, new Color(0.2f, 0.18f, 0.16f, 1f));
            }
        }

        private void EvDust(Ev e, double p, double k)
        {
            JObj cfg = J.O(J.O(m, "events"), "place");
            int n = (int)J.N(cfg, "puffs", 8);
            List<object> rad = J.A(cfg, "radius");
            double rx = rad.Count > 1 ? J.Num(rad[0]) : 40, ry = rad.Count > 1 ? J.Num(rad[1]) : 20;
            for (int i = 0; i < n; i++)
            {
                double a = (double)i / n * Math.PI * 2 + e.Seed;
                Vec2 pos = e.Pos + new Vec2(Math.Cos(a) * rx, Math.Sin(a) * ry) * (0.35 + 0.65 * Out(p));
                Circle(pos, Lerp(4.0, 10.0, p) * k, Col("dust", 0.6 * (1.0 - p)));
            }
        }

        private void EvSparkle(Ev e, double p, double k)
        {
            JObj cfg = J.O(J.O(m, "events"), "upgrade");
            int stars = (int)J.N(cfg, "stars", 9);
            double rise = J.N(cfg, "rise", 56), size = J.N(cfg, "size", 9);
            for (int i = 0; i < stars; i++)
            {
                double a = Rnd(e.Seed + i * 1.7) * Math.PI * 2;
                double r = 14.0 + Rnd(e.Seed + i * 4.1) * 30.0;
                Vec2 pos = e.Pos + new Vec2(Math.Cos(a) * r, -26.0 - rise * Out(p) + Math.Sin(a) * r * 0.4);
                double s = (1.0 - p) * size * k;
                Color col = Col("sparkle", 1.0 - p);
                Line(pos - new Vec2(s, 0), pos + new Vec2(s, 0), 2.5, col);
                Line(pos - new Vec2(0, s), pos + new Vec2(0, s), 2.5, col);
                Circle(pos, s * 0.35, col);
            }
        }
    }
}
