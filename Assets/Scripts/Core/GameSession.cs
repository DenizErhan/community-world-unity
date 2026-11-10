// Everything a player can do, independent of how it is drawn: selecting a building, the
// action menu, placing/moving with a ghost, collecting, upgrading, removing.
// The Unity view listens to the events below and draws the result.
using System;
using System.Collections.Generic;

namespace CommunityWorld
{
    public class GameSession
    {
        public readonly GameState State;
        public readonly Geo Geo;
        public readonly JObj Tokens;

        // selection
        public Tile SelTile = Tile.None;
        public double RemoveArmed;

        // placement: a ghost for a NEW building or one being MOVED
        public string PendingId = "";
        public Tile PendingTile = Tile.None;
        public Tile MovingFrom = Tile.None;
        public string IntroText = "";

        // events (all optional)
        public event Action<string> Toast;
        public event Action<string, bool> HoldInfo;      // text, warning; stays until replaced
        public event Action<string, Tile> FxEvent;       // "dust" | "sparkle"
        public event Action<Tile, string, string> FloatText;   // tile, "+5", resource id
        public event Action Changed;                      // anything the view should redraw
        public event Action SaveRequested;

        private readonly double armSeconds;
        private readonly double badgeRadius;

        public GameSession(GameState state, JObj tokens)
        {
            State = state;
            Tokens = tokens;
            Geo = new Geo(tokens);
            armSeconds = J.N(J.O(J.O(tokens, "ui"), "actionbar"), "arm_seconds", 3);
            badgeRadius = Geo.BadgeRadius;
        }

        public bool Placing { get { return PendingId != ""; } }
        public bool Moving { get { return !MovingFrom.IsNone; } }
        public bool HasSelection { get { return State.Has(SelTile); } }
        public GameData Data { get { return State.Data; } }

        private void RaiseToast(string s) { if (Toast != null) Toast(s); }
        private void RaiseChanged() { if (Changed != null) Changed(); }
        private void RaiseSave() { if (SaveRequested != null) SaveRequested(); }
        private void RaiseFx(string kind, Tile t) { if (FxEvent != null) FxEvent(kind, t); }

        /// <summary>First launch: start with the first building on the ghost and a welcome hint.</summary>
        public void StartIntro()
        {
            if (State.Buildings.Count > 0) return;
            BeginPlace(Data.Order[0], Tile.None);
            IntroText = Loc.T("toast.welcome");
            RefreshPlaceUi();
        }

        public void Tick(double dt)
        {
            State.Produce(dt);
            if (RemoveArmed > 0.0)
            {
                RemoveArmed -= dt;
                if (RemoveArmed <= 0.0) { RemoveArmed = 0.0; RaiseChanged(); }
            }
            if (!HasSelection && !SelTile.IsNone) Deselect();
        }

        // ------------------------------------------------------------ tapping

        public void Tap(Tile t)
        {
            if (Placing)
            {
                if (State.InGrid(t)) SetPending(t);     // a tap only moves the ghost; the tick builds
            }
            else if (State.InGrid(t) && State.Has(t)) Select(t);   // a tap only selects; the menu offers the actions
            else Deselect();
            RaiseChanged();
        }

        public void Select(Tile t)
        {
            if (t != SelTile) DisarmRemove();
            SelTile = t;
            string id = State.IdAt(t);
            int lv = State.Level(t);
            RaiseToast(Loc.T("info.selected", "name", State.BName(id), "lv", lv, "max", State.MaxLevel(id), "details", State.Details(id, lv)));
            RaiseChanged();
        }

        public void Deselect()
        {
            SelTile = Tile.None;
            DisarmRemove();
            RaiseChanged();
        }

        public void DisarmRemove() { RemoveArmed = 0.0; }

        // ------------------------------------------------------------ action menu

        public void OnInfo()
        {
            if (!HasSelection) return;
            string id = State.IdAt(SelTile);
            int lv = State.Level(SelTile);
            if (lv >= State.MaxLevel(id)) RaiseToast(Loc.T("toast.max_level", "name", State.BName(id)));
            else RaiseToast(Loc.T("info.next_level", "lv", lv + 1, "details", State.Details(id, lv + 1), "cost", State.CostText(State.UpgradeCost(id, lv))));
        }

        public void OnMove()
        {
            if (!HasSelection) return;
            Tile t = SelTile;
            Deselect();
            BeginPlace(State.IdAt(t), t);
        }

        public void OnUpgrade()
        {
            if (!HasSelection) return;
            string id = State.IdAt(SelTile);
            int lv = State.Level(SelTile);
            string blocked = State.UpgradeBlocker(id, lv);
            if (blocked != "") { RaiseToast(blocked); return; }
            Dictionary<string, double> cost = State.UpgradeCost(id, lv);
            if (!State.Afford(cost)) { RaiseToast(Loc.T("toast.upgrade_cost", "cost", State.CostText(cost))); return; }
            State.UpgradeBuilding(SelTile);
            RaiseSave();
            RaiseFx("sparkle", SelTile);
            RaiseToast(Loc.T("toast.upgraded", "name", State.BName(id), "lv", lv + 1));
            RaiseChanged();
        }

        public void OnRemove()
        {
            if (!HasSelection) return;
            if (RemoveArmed > 0.0)
            {
                Tile t = SelTile;
                Deselect();
                RemoveAt(t);
                return;
            }
            RemoveArmed = armSeconds;
            string id = State.IdAt(SelTile);
            RaiseToast(Loc.T("toast.confirm_remove", "name", State.BName(id), "refund", State.CostText(State.RefundOf(id, State.Level(SelTile)))));
            RaiseChanged();
        }

        public void RemoveAt(Tile t)
        {
            if (!State.Has(t)) return;
            string id = State.IdAt(t);
            Dictionary<string, double> refund = State.RemoveBuilding(t);
            RaiseFx("dust", t);
            RaiseSave();
            RaiseToast(Loc.T("toast.removed", "name", State.BName(id), "refund", State.CostText(refund)));
            RaiseChanged();
        }

        // ------------------------------------------------------------ collecting

        public void CollectAndReport(List<Tile> tiles)
        {
            Dictionary<string, double> total = new Dictionary<string, double>();
            double waiting = 0.0;
            foreach (Tile t in tiles)
            {
                Dictionary<string, double> got = State.Collect(t);
                foreach (KeyValuePair<string, double> kv in got)
                {
                    double cur;
                    total.TryGetValue(kv.Key, out cur);
                    total[kv.Key] = cur + kv.Value;
                    if (kv.Value >= 1.0 && FloatText != null) FloatText(t, "+" + (int)kv.Value, kv.Key);
                }
                waiting += State.BucketTotal(t);
            }
            string txt = State.GainsText(total);
            if (txt != "") { RaiseToast(Loc.T("toast.collected", "gains", txt)); RaiseSave(); }
            else if (waiting >= 1.0) RaiseToast(Loc.T("toast.storage_full"));
            else RaiseToast(Loc.T("toast.nothing_to_collect"));
            RaiseChanged();
        }

        public void CollectAll() { CollectAndReport(new List<Tile>(State.Buildings.Keys)); }

        // ------------------------------------------------------------ placement / moving

        public void BeginPlace(string id, Tile from)
        {
            Deselect();
            PendingId = id;
            MovingFrom = from;
            IntroText = "";
            PendingTile = !from.IsNone ? from : NearestFree(DefaultGhostTile);
            RefreshPlaceUi();
            RaiseChanged();
        }

        /// <summary>Where a new ghost starts: the view sets this to the tile at the screen centre.</summary>
        public Tile DefaultGhostTile = new Tile(6, 6);

        public void EndPlace()
        {
            PendingId = "";
            MovingFrom = Tile.None;
            IntroText = "";
            RaiseChanged();
        }

        public Tile NearestFree(Tile c)
        {
            c = new Tile(Math.Max(0, Math.Min(c.X, Data.GridW - 1)), Math.Max(0, Math.Min(c.Y, Data.GridH - 1)));
            if (!State.Has(c)) return c;
            Tile best = c;
            int bestD = int.MaxValue;
            for (int gx = 0; gx < Data.GridW; gx++)
            {
                for (int gy = 0; gy < Data.GridH; gy++)
                {
                    Tile t = new Tile(gx, gy);
                    if (State.Has(t)) continue;
                    int d = Math.Abs(t.X - c.X) + Math.Abs(t.Y - c.Y);
                    if (d < bestD) { bestD = d; best = t; }
                }
            }
            return best;
        }

        public void SetPending(Tile t)
        {
            if (!Placing) return;
            t = new Tile(Math.Max(0, Math.Min(t.X, Data.GridW - 1)), Math.Max(0, Math.Min(t.Y, Data.GridH - 1)));
            if (t != PendingTile)
            {
                PendingTile = t;
                IntroText = "";
                RefreshPlaceUi();
                RaiseChanged();
            }
        }

        public void Nudge(int dx, int dy) { SetPending(PendingTile + new Tile(dx, dy)); }

        /// <summary>Empty = the ghost may be confirmed where it is, otherwise the reason.</summary>
        public string PendingError()
        {
            if (!Placing) return "";
            if (Moving)
            {
                if (PendingTile != MovingFrom && State.Has(PendingTile)) return Loc.T("toast.occupied");
                return "";
            }
            return State.CheckPlace(PendingTile, PendingId);
        }

        public bool ConfirmEnabled { get { return Placing && PendingError() == ""; } }

        public void ConfirmPlace()
        {
            if (!Placing) return;
            string err = PendingError();
            if (err != "") { Hold(err, true); return; }
            string id = PendingId;
            Tile t = PendingTile;
            if (Moving)
            {
                Tile from = MovingFrom;
                EndPlace();
                if (t != from)
                {
                    State.MoveBuilding(from, t);
                    RaiseFx("dust", t);
                    RaiseSave();
                    RaiseToast(Loc.T("toast.moved", "name", State.BName(id)));
                }
            }
            else
            {
                EndPlace();
                Place(t, id);
            }
        }

        public void CancelPlace() { if (Placing) EndPlace(); }

        public void Place(Tile t, string id)
        {
            string err = State.CheckPlace(t, id);
            if (err != "") { RaiseToast(err); return; }
            State.PlaceBuilding(t, id);
            RaiseFx("dust", t);
            RaiseSave();
            RaiseToast(Loc.T("toast.built", "name", State.BName(id)));
            RaiseChanged();
        }

        /// <summary>Keeps the info line in step with the ghost: why it cannot be placed, or what it is.</summary>
        public void RefreshPlaceUi()
        {
            if (!Placing) return;
            string err = PendingError();
            if (err != "") Hold(err, true);
            else if (IntroText != "") Hold(IntroText, false);
            else if (Moving) Hold(Loc.T("place.move", "name", State.BName(PendingId), "details", State.Details(PendingId)), false);
            else Hold(Loc.T("place.new", "name", State.BName(PendingId)), false);
        }

        private void Hold(string msg, bool warn) { if (HoldInfo != null) HoldInfo(msg, warn); }

        public void Reset()
        {
            EndPlace();
            State.Reset();
            Deselect();
            RaiseSave();
            RaiseChanged();
        }

        // ------------------------------------------------------------ picking (design space)

        /// <summary>
        /// The tile a tap means. With a ghost out, that is the ground tile under the finger. Otherwise a tap on a
        /// building's visible body counts even when the body stands over the tile behind it. When hit boxes overlap
        /// (neighbours, zoomed far out) the building whose body is nearest the finger wins.
        /// </summary>
        public Tile PickTile(Vec2 w, double zoom)
        {
            Tile ground = Geo.WorldToTile(w);
            if (Placing) return ground;
            double rise = Math.Max(Geo.PickHeight, Geo.PickMinPx / zoom);
            double halfW = Math.Max(Geo.TW * 0.4, Geo.PickMinPx * 0.6 / zoom);
            Vec2 body = new Vec2(0, -Geo.PickHeight * 0.4);
            Tile best = ground;
            double bestD = double.MaxValue;
            foreach (Tile t in State.Buildings.Keys)
            {
                Vec2 c = Geo.TileCenter(t);
                bool inside = Math.Abs(w.X - c.X) <= halfW && w.Y <= c.Y + Geo.TH * 0.25 && w.Y >= c.Y - rise;
                if (inside || t == ground)
                {
                    double d = w.DistanceTo(c + body);
                    if (d < bestD) { best = t; bestD = d; }
                }
            }
            return best;
        }

        /// <summary>True when a press at world point w grabs the ghost (screen distance, so it works at any zoom).</summary>
        public bool OnGhost(Vec2 w, double zoom)
        {
            double grab = Math.Max(Geo.GrabPx, Geo.TW * 0.7 * zoom);
            Vec2 centre = Geo.TileCenter(PendingTile) + new Vec2(0, -Geo.SpriteAnchorY * 0.4);
            return w.DistanceTo(centre) * zoom <= grab;
        }

        public Vec2 BadgeCentre(Tile t) { return Geo.TileCenter(t) + new Vec2(0, -Geo.BadgeLift); }

        /// <summary>The building whose collect badge is under the pointer, or Tile.None.</summary>
        public Tile BadgeAt(Vec2 w, double zoom)
        {
            double rad = badgeRadius + 6.0;
            Tile best = Tile.None;
            double bestD = double.MaxValue;
            foreach (Tile t in State.Buildings.Keys)
            {
                if (State.BucketTotal(t) < 1.0 || t == MovingFrom) continue;
                double d = w.DistanceTo(BadgeCentre(t)) * zoom;
                if (d <= rad && d < bestD) { best = t; bestD = d; }
            }
            return best;
        }
    }
}
