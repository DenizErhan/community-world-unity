// The map: slab, ground tiles, buildings, the placement ghost and the highlight outline.
// Everything is a SpriteRenderer/mesh sorted back to front by diagonal (gx + gy), like the
// prototype's painter's algorithm.
using System.Collections.Generic;
using UnityEngine;

namespace CommunityWorld
{
    public class WorldView
    {
        private const int SlabOrder = -10;
        private const int OutlineOrder = 100000;

        private readonly GameSession session;
        private readonly JObj tokens;
        private readonly string age;
        private readonly Transform root;
        private readonly Dictionary<Tile, SpriteRenderer> buildingSprites = new Dictionary<Tile, SpriteRenderer>();
        private readonly Dictionary<string, Sprite> buildingArt = new Dictionary<string, Sprite>();
        private SpriteRenderer ghost;
        private LineRenderer outlineMain;
        private LineRenderer outlineFrom;
        private readonly float ghostAlpha;
        private readonly Color hoverOk, hoverBad, hoverNeutral, hoverDim;

        public WorldView(GameSession session, JObj tokens, string age)
        {
            this.session = session;
            this.tokens = tokens;
            this.age = age;
            root = new GameObject("World").transform;
            JObj world = J.O(J.O(tokens, "ui"), "world");
            ghostAlpha = (float)J.N(world, "ghost_alpha", 0.55);
            hoverOk = Gfx.Hex(J.S(world, "hover_ok"));
            hoverBad = Gfx.Hex(J.S(world, "hover_bad"));
            hoverNeutral = Gfx.Hex(J.S(world, "hover_neutral"));
            hoverDim = Gfx.Hex(J.S(world, "hover_dim"));
            foreach (string id in session.Data.Order) buildingArt[id] = Gfx.Art("buildings/" + id + "_" + age);
            BuildGround();
            BuildSlab();
            ghost = NewSprite("Ghost");
            outlineMain = NewOutline("Outline");
            outlineFrom = NewOutline("OutlineFrom");
        }

        private static int Diag(Tile t) { return (t.X + t.Y) * 4; }

        private SpriteRenderer NewSprite(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.AddComponent<SpriteRenderer>();
        }

        private LineRenderer NewOutline(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Gfx.SpriteMaterial;
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 4;
            lr.numCornerVertices = 2;
            lr.sortingOrder = OutlineOrder;
            lr.widthMultiplier = (float)J.N(J.O(J.O(tokens, "ui"), "world"), "highlight_width", 5) / Gfx.Unit;
            lr.enabled = false;
            return lr;
        }

        private void BuildGround()
        {
            Sprite[] tex = { Gfx.Art("ground/ground_" + age + "_a"), Gfx.Art("ground/ground_" + age + "_b") };
            for (int gx = 0; gx < session.Data.GridW; gx++)
            {
                for (int gy = 0; gy < session.Data.GridH; gy++)
                {
                    Tile t = new Tile(gx, gy);
                    SpriteRenderer sr = NewSprite("Ground " + gx + "," + gy);
                    sr.sprite = tex[(gx + gy) % 2];
                    sr.sortingOrder = Diag(t);
                    sr.transform.position = Gfx.ToWorld(session.Geo.TileCenter(t));
                }
            }
        }

        /// <summary>The two visible sides of the island's base (left and right faces).</summary>
        private void BuildSlab()
        {
            Geo geo = session.Geo;
            int gw = session.Data.GridW;
            int gh = session.Data.GridH;
            Vec2 dv = new Vec2(0, geo.SlabDepth);
            Vec2 left = geo.TileCenter(0, gh - 1) + new Vec2(-geo.TW * 0.5, 0);
            Vec2 bottom = geo.TileCenter(gw - 1, gh - 1) + new Vec2(0, geo.TH * 0.5);
            Vec2 right = geo.TileCenter(gw - 1, 0) + new Vec2(geo.TW * 0.5, 0);
            JObj pal = J.O(J.O(J.O(tokens, "ages"), age), "palette");
            Color cl = Gfx.Hex(J.S(pal, "slab_left"));
            Color cr = Gfx.Hex(J.S(pal, "slab_right"));

            List<Vector3> v = new List<Vector3>();
            List<Color> c = new List<Color>();
            List<int> tri = new List<int>();
            AddQuad(v, c, tri, left, bottom, bottom + dv, left + dv, cl);
            AddQuad(v, c, tri, bottom, right, right + dv, bottom + dv, cr);
            Mesh mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            GameObject go = new GameObject("Slab");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Gfx.SpriteMaterial;
            mr.sortingOrder = SlabOrder;
        }

        private static void AddQuad(List<Vector3> v, List<Color> c, List<int> tri, Vec2 a, Vec2 b, Vec2 d, Vec2 e, Color col)
        {
            int i = v.Count;
            v.Add(Gfx.ToWorld(a)); v.Add(Gfx.ToWorld(b)); v.Add(Gfx.ToWorld(d)); v.Add(Gfx.ToWorld(e));
            for (int k = 0; k < 4; k++) c.Add(col);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        /// <summary>Syncs sprites with the village (call after any change).</summary>
        public void Refresh()
        {
            GameState st = session.State;
            List<Tile> gone = null;
            foreach (KeyValuePair<Tile, SpriteRenderer> kv in buildingSprites)
            {
                if (!st.Has(kv.Key))
                {
                    if (gone == null) gone = new List<Tile>();
                    gone.Add(kv.Key);
                }
            }
            if (gone != null)
            {
                foreach (Tile t in gone)
                {
                    Object.Destroy(buildingSprites[t].gameObject);
                    buildingSprites.Remove(t);
                }
            }
            foreach (KeyValuePair<Tile, Placed> kv in st.Buildings)
            {
                SpriteRenderer sr;
                if (!buildingSprites.TryGetValue(kv.Key, out sr))
                {
                    sr = NewSprite("Building " + kv.Key.X + "," + kv.Key.Y);
                    sr.sortingOrder = Diag(kv.Key) + 1;
                    sr.transform.position = Gfx.ToWorld(session.Geo.TileCenter(kv.Key));
                    buildingSprites[kv.Key] = sr;
                }
                if (sr.sprite != buildingArt[kv.Value.Id]) sr.sprite = buildingArt[kv.Value.Id];
                sr.enabled = kv.Key != session.MovingFrom;   // the building being moved is shown as the ghost
            }

            // ghost
            if (session.Placing)
            {
                ghost.enabled = true;
                ghost.sprite = buildingArt[session.PendingId];
                ghost.color = new Color(1, 1, 1, ghostAlpha);
                ghost.sortingOrder = Diag(session.PendingTile) + 2;
                ghost.transform.position = Gfx.ToWorld(session.Geo.TileCenter(session.PendingTile));
            }
            else ghost.enabled = false;

            // outlines
            outlineFrom.enabled = false;
            outlineMain.enabled = false;
            if (session.Placing)
            {
                if (session.Moving && session.MovingFrom != session.PendingTile)
                    ShowOutline(outlineFrom, session.MovingFrom, hoverDim);
                ShowOutline(outlineMain, session.PendingTile, session.PendingError() == "" ? hoverOk : hoverBad);
            }
            else if (session.HasSelection)
            {
                ShowOutline(outlineMain, session.SelTile, hoverNeutral);
            }
        }

        private void ShowOutline(LineRenderer lr, Tile t, Color col)
        {
            Geo geo = session.Geo;
            Vec2 c = geo.TileCenter(t);
            double hw = geo.TW * 0.5;
            double hh = geo.TH * 0.5;
            lr.SetPosition(0, Gfx.ToWorld(c + new Vec2(0, -hh)));
            lr.SetPosition(1, Gfx.ToWorld(c + new Vec2(hw, 0)));
            lr.SetPosition(2, Gfx.ToWorld(c + new Vec2(0, hh)));
            lr.SetPosition(3, Gfx.ToWorld(c + new Vec2(-hw, 0)));
            lr.startColor = col;
            lr.endColor = col;
            lr.enabled = true;
        }
    }
}
