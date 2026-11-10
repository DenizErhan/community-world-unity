// Collect badges and level marks: UI elements that follow their building on screen at a
// constant size (like the prototype), so they stay readable at any zoom.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CommunityWorld
{
    public class MarksView
    {
        private class Mark
        {
            public RectTransform Badge;
            public Image Ring;
            public Text Count;
            public Text Level;
        }

        private readonly GameSession session;
        private readonly CameraRig cam;
        private readonly RectTransform canvas;
        private readonly JObj tokens;
        private readonly Dictionary<Tile, Mark> marks = new Dictionary<Tile, Mark>();
        private readonly Sprite ringThin, ringThick;
        private readonly float radius;
        private readonly int fontSize, levelFont;
        private readonly Color panel, textCol;

        public MarksView(GameSession session, CameraRig cam, RectTransform canvas, JObj tokens)
        {
            this.session = session;
            this.cam = cam;
            this.canvas = canvas;
            this.tokens = tokens;
            JObj ui = J.O(tokens, "ui");
            JObj bd = J.O(ui, "badge");
            radius = (float)J.N(bd, "radius", 26);
            fontSize = (int)J.N(bd, "font", 22);
            levelFont = (int)J.N(bd, "level_font", 18);
            float ring = (float)J.N(bd, "ring", 4);
            const int px = 128;
            float perDesign = px / (radius * 2f);
            ringThin = Gfx.Ring(px, ring * perDesign);
            ringThick = Gfx.Ring(px, ring * 1.6f * perDesign);
            panel = Gfx.Hex(J.S(J.O(ui, "colors"), "panel_bottom"));
            textCol = Gfx.Hex(J.S(J.O(ui, "colors"), "text"));
        }

        private Color ResColor(string r) { return Gfx.Hex(J.S(J.O(J.O(tokens, "ui"), "colors"), "resource_" + r)); }

        private Text NewText(string name, Transform parent, int size, TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = Gfx.Font;
            t.fontSize = size;
            t.color = textCol;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private Mark Create(Tile t)
        {
            Mark m = new Mark();
            GameObject b = new GameObject("Badge", typeof(RectTransform));
            m.Badge = b.GetComponent<RectTransform>();
            m.Badge.SetParent(canvas, false);
            m.Badge.anchorMin = m.Badge.anchorMax = new Vector2(0, 1);
            m.Badge.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            Image bg = b.AddComponent<Image>();
            bg.sprite = Gfx.CircleSprite;
            bg.color = panel;
            bg.raycastTarget = false;
            GameObject r = new GameObject("Ring", typeof(RectTransform));
            r.transform.SetParent(m.Badge, false);
            RectTransform rr = r.GetComponent<RectTransform>();
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            m.Ring = r.AddComponent<Image>();
            m.Ring.raycastTarget = false;
            m.Count = NewText("Count", m.Badge, fontSize, TextAnchor.MiddleCenter);
            RectTransform cr = m.Count.rectTransform;
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
            m.Level = NewText("Level", canvas, levelFont, TextAnchor.MiddleLeft);
            m.Level.rectTransform.anchorMin = m.Level.rectTransform.anchorMax = new Vector2(0, 1);
            m.Level.rectTransform.pivot = new Vector2(0, 0.5f);
            m.Level.rectTransform.sizeDelta = new Vector2(120, levelFont + 8);
            Outline o = m.Level.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.8f);
            o.effectDistance = new Vector2(2, -2);
            return m;
        }

        private static Vector2 Canvas(Vec2 screenDown) { return new Vector2((float)screenDown.X, -(float)screenDown.Y); }

        public void Update()
        {
            GameState st = session.State;
            List<Tile> gone = null;
            foreach (KeyValuePair<Tile, Mark> kv in marks)
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
                    Object.Destroy(marks[t].Badge.gameObject);
                    Object.Destroy(marks[t].Level.gameObject);
                    marks.Remove(t);
                }
            }
            foreach (KeyValuePair<Tile, Placed> kv in st.Buildings)
            {
                Tile t = kv.Key;
                Mark m;
                if (!marks.TryGetValue(t, out m)) { m = Create(t); marks[t] = m; }
                bool hidden = t == session.MovingFrom;

                bool showLevel = !hidden && kv.Value.Level >= 2;
                if (m.Level.gameObject.activeSelf != showLevel) m.Level.gameObject.SetActive(showLevel);
                if (showLevel)
                {
                    m.Level.text = Loc.T("ui.level_mark", "n", kv.Value.Level);
                    Vec2 p = cam.WorldToScreen(session.Geo.TileCenter(t) + new Vec2(session.Geo.TW * 0.30, 6));
                    m.Level.rectTransform.anchoredPosition = Canvas(p) + new Vector2(0, levelFont * 0.35f);
                }

                double total = st.BucketTotal(t);
                bool showBadge = !hidden && total >= 1.0;
                if (m.Badge.gameObject.activeSelf != showBadge) m.Badge.gameObject.SetActive(showBadge);
                if (showBadge)
                {
                    string res = st.MainResource(kv.Value.Id);
                    bool full = total >= st.BucketCapAt(kv.Value.Id, kv.Value.Level, res) - 0.01;
                    m.Ring.sprite = full ? ringThick : ringThin;
                    m.Ring.color = ResColor(res);
                    m.Count.text = ((int)total).ToString();
                    m.Badge.anchoredPosition = Canvas(cam.WorldToScreen(session.BadgeCentre(t)));
                }
            }
        }
    }
}
