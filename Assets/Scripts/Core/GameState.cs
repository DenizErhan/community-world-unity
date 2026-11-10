// The village rules: stock, production, levels, collecting, saving. No UnityEngine here.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CommunityWorld
{
    public class Placed
    {
        public string Id;
        public int Level = 1;
        public Dictionary<string, double> Bucket = new Dictionary<string, double>();   // output waiting to be collected
    }

    public class AgeReq
    {
        public string Id;
        public int Have;
        public int Need;
    }

    public class LoadInfo
    {
        public bool HasCam;
        public double CamX;
        public double CamY;
        public double CamZoom;
        public string OfflineMessage = "";
    }

    public class GameState
    {
        public readonly GameData Data;
        public readonly Dictionary<Tile, Placed> Buildings = new Dictionary<Tile, Placed>();
        public readonly Dictionary<string, double> Stock = new Dictionary<string, double>();
        public readonly Dictionary<string, double> Rates = new Dictionary<string, double>();
        public readonly Dictionary<string, double> Caps = new Dictionary<string, double>();

        public GameState(GameData data)
        {
            Data = data;
            Reset();
        }

        public void Reset()
        {
            Buildings.Clear();
            foreach (string r in Data.ResourceIds)
            {
                double s;
                Stock[r] = Data.Start.TryGetValue(r, out s) ? s : 0.0;
            }
            Recalc();
        }

        // ------------------------------------------------------------ names

        public string RName(string r) { return Loc.T("res." + r + ".name"); }
        public string BName(string id) { return Loc.T("building." + id + ".name"); }

        // ------------------------------------------------------------ levels (data-driven)

        public double Up(string id, string key) { return Data.Up(id, key); }
        public int Level(Tile t) { Placed p; return Buildings.TryGetValue(t, out p) ? p.Level : 1; }
        public int MaxLevel(string id) { return (int)Up(id, "max_level"); }
        public bool Has(Tile t) { return Buildings.ContainsKey(t); }
        public string IdAt(Tile t) { return Buildings[t].Id; }

        public double RateAt(string id, int lv, string r)
        {
            double b;
            if (!Data.Defs[id].Produces.TryGetValue(r, out b)) return 0.0;
            return b * Math.Pow(Up(id, "prod_growth"), lv - 1);
        }

        public double CapacityAt(string id, int lv)
        {
            return Data.Defs[id].Capacity * Math.Pow(Up(id, "capacity_growth"), lv - 1);
        }

        /// <summary>How much of one resource a building can hold before you collect it.</summary>
        public double BucketCapAt(string id, int lv, string r)
        {
            double b;
            if (!Data.Defs[id].Produces.TryGetValue(r, out b)) return 0.0;
            return b * Data.BucketSeconds * Math.Pow(Up(id, "bucket_growth"), lv - 1);
        }

        /// <summary>Cost to go from level lv to lv + 1.</summary>
        public Dictionary<string, double> UpgradeCost(string id, int lv)
        {
            Dictionary<string, double> o = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> kv in Data.Defs[id].Cost)
                o[kv.Key] = Math.Ceiling(kv.Value * Math.Pow(Up(id, "cost_growth"), lv));
            return o;
        }

        /// <summary>Everything spent on a building so far (build + upgrades).</summary>
        public Dictionary<string, double> Invested(string id, int lv)
        {
            Dictionary<string, double> o = new Dictionary<string, double>(Data.Defs[id].Cost);
            for (int l = 1; l < lv; l++)
            {
                foreach (KeyValuePair<string, double> kv in UpgradeCost(id, l))
                {
                    double cur;
                    o[kv.Key] = (o.TryGetValue(kv.Key, out cur) ? cur : 0.0) + kv.Value;
                }
            }
            return o;
        }

        /// <summary>Half of everything spent on the building, rounded down.</summary>
        public Dictionary<string, double> RefundOf(string id, int lv = 1)
        {
            Dictionary<string, double> o = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> kv in Invested(id, lv)) o[kv.Key] = Math.Floor(kv.Value * 0.5);
            return o;
        }

        public int BestLevel(string id)
        {
            int best = 0;
            foreach (Placed p in Buildings.Values) if (p.Id == id) best = Math.Max(best, p.Level);
            return best;
        }

        public bool HasBuilding(string id)
        {
            foreach (Placed p in Buildings.Values) if (p.Id == id) return true;
            return false;
        }

        /// <summary>Age-up needs certain buildings at certain levels.</summary>
        public List<AgeReq> AgeProgress()
        {
            List<AgeReq> o = new List<AgeReq>();
            foreach (KeyValuePair<string, int> kv in Data.AgeUpRequires)
                o.Add(new AgeReq { Id = kv.Key, Have = BestLevel(kv.Key), Need = kv.Value });
            return o;
        }

        public bool AgeReady()
        {
            if (Data.AgeUpRequires.Count == 0) return false;
            foreach (AgeReq e in AgeProgress()) if (e.Have < e.Need) return false;
            return true;
        }

        /// <summary>Empty = can upgrade (apart from cost). A building cannot pass the level of the building it needs.</summary>
        public string UpgradeBlocker(string id, int lv)
        {
            if (lv >= MaxLevel(id)) return Loc.T("toast.max_level", "name", BName(id));
            string req = Data.Defs[id].Requires;
            if (req != "" && BestLevel(req) < lv + 1)
                return Loc.T("toast.upgrade_needs", "name", BName(req), "n", lv + 1);
            return "";
        }

        public bool CanUpgrade(Tile t)
        {
            if (!Has(t)) return false;
            string id = IdAt(t);
            int lv = Level(t);
            return UpgradeBlocker(id, lv) == "" && Afford(UpgradeCost(id, lv));
        }

        // ------------------------------------------------------------ totals

        public void Recalc()
        {
            Rates.Clear();
            Caps.Clear();
            foreach (string r in Data.ResourceIds) { Rates[r] = 0.0; Caps[r] = Data.BaseCapacity; }
            foreach (Placed p in Buildings.Values)
            {
                foreach (string r in Data.Defs[p.Id].Produces.Keys) Rates[r] += RateAt(p.Id, p.Level, r);
                double add = CapacityAt(p.Id, p.Level);
                if (add > 0.0) foreach (string r in Data.ResourceIds) Caps[r] += add;
            }
            foreach (string r in Data.ResourceIds) Stock[r] = Math.Min(Stock[r], Caps[r]);
        }

        public bool Afford(Dictionary<string, double> cost)
        {
            foreach (KeyValuePair<string, double> kv in cost)
            {
                double have;
                if (!Stock.TryGetValue(kv.Key, out have) || have < kv.Value) return false;
            }
            return true;
        }

        public void Spend(Dictionary<string, double> cost)
        {
            foreach (KeyValuePair<string, double> kv in cost) Stock[kv.Key] -= kv.Value;
        }

        public string CostText(Dictionary<string, double> cost)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, double> kv in cost)
                parts.Add(Loc.T("ui.cost_part", "n", (int)kv.Value, "res", RName(kv.Key)));
            return string.Join(Loc.T("ui.cost_join"), parts.ToArray());
        }

        public string Details(string id, int lv = 1)
        {
            List<string> parts = new List<string>();
            foreach (string r in Data.Defs[id].Produces.Keys)
            {
                parts.Add(Loc.T("info.produces", "rate", RateAt(id, lv, r).ToString("0.0", CultureInfo.InvariantCulture), "res", RName(r)));
                parts.Add(Loc.T("info.holds", "n", (int)BucketCapAt(id, lv, r)));
            }
            double add = CapacityAt(id, lv);
            if (add > 0.0) parts.Add(Loc.T("info.storage", "n", (int)add));
            return string.Join(Loc.T("info.join"), parts.ToArray());
        }

        // ------------------------------------------------------------ production and collecting

        public double BucketTotal(Tile t)
        {
            Placed p;
            if (!Buildings.TryGetValue(t, out p)) return 0.0;
            double n = 0.0;
            foreach (double v in p.Bucket.Values) n += v;
            return n;
        }

        /// <summary>The resource a building mainly makes (what its badge shows).</summary>
        public string MainResource(string id)
        {
            foreach (string r in Data.Defs[id].Produces.Keys) return r;
            return Data.ResourceIds[0];
        }

        public void Produce(double dt)
        {
            foreach (Placed p in Buildings.Values)
            {
                Dictionary<string, double> prod = Data.Defs[p.Id].Produces;
                if (prod.Count == 0) continue;
                foreach (string r in new List<string>(prod.Keys))
                {
                    double cur;
                    p.Bucket.TryGetValue(r, out cur);
                    p.Bucket[r] = Math.Min(BucketCapAt(p.Id, p.Level, r), cur + RateAt(p.Id, p.Level, r) * dt);
                }
            }
        }

        /// <summary>Moves a building's output into village storage (as much as fits). Returns what was taken.</summary>
        public Dictionary<string, double> Collect(Tile t)
        {
            Dictionary<string, double> got = new Dictionary<string, double>();
            Placed p;
            if (!Buildings.TryGetValue(t, out p)) return got;
            foreach (string r in new List<string>(p.Bucket.Keys))
            {
                double have = p.Bucket[r];
                double take = Math.Min(have, Caps[r] - Stock[r]);
                if (take < have) take = Math.Floor(take);
                if (take > 0.0)
                {
                    Stock[r] += take;
                    p.Bucket[r] = have - take;
                    got[r] = take;
                }
            }
            return got;
        }

        public string GainsText(Dictionary<string, double> got)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, double> kv in got)
                if (kv.Value >= 1.0) parts.Add(Loc.T("toast.offline_part", "n", (int)kv.Value, "res", RName(kv.Key)));
            return string.Join(Loc.T("info.join"), parts.ToArray());
        }

        // ------------------------------------------------------------ building

        /// <summary>Empty = placeable, otherwise the reason to show the player.</summary>
        public string CheckPlace(Tile t, string id)
        {
            if (Buildings.ContainsKey(t)) return Loc.T("toast.occupied");
            BuildingDef d = Data.Defs[id];
            if (d.Requires != "" && !HasBuilding(d.Requires)) return Loc.T("toast.need_building", "name", BName(d.Requires));
            foreach (KeyValuePair<string, double> kv in d.Cost)
            {
                if (Stock[kv.Key] < kv.Value) return Loc.T("toast.not_enough", "res", RName(kv.Key));
            }
            return "";
        }

        public bool InGrid(Tile t) { return t.X >= 0 && t.Y >= 0 && t.X < Data.GridW && t.Y < Data.GridH; }

        /// <summary>Builds without checking (callers use CheckPlace first). Spends the cost.</summary>
        public void PlaceBuilding(Tile t, string id)
        {
            Spend(Data.Defs[id].Cost);
            Buildings[t] = new Placed { Id = id };
            Recalc();
        }

        public bool UpgradeBuilding(Tile t)
        {
            if (!Has(t)) return false;
            Placed p = Buildings[t];
            Spend(UpgradeCost(p.Id, p.Level));
            p.Level++;
            Recalc();
            return true;
        }

        /// <summary>Takes what fits into storage, refunds half of everything spent, removes the building.</summary>
        public Dictionary<string, double> RemoveBuilding(Tile t)
        {
            Placed p = Buildings[t];
            Collect(t);
            Dictionary<string, double> refund = RefundOf(p.Id, p.Level);
            foreach (KeyValuePair<string, double> kv in refund) Stock[kv.Key] += kv.Value;
            Buildings.Remove(t);
            Recalc();
            return refund;
        }

        public void MoveBuilding(Tile from, Tile to)
        {
            Placed p = Buildings[from];
            Buildings.Remove(from);
            Buildings[to] = p;
        }

        // ------------------------------------------------------------ save / load

        public string Serialize(double nowUnix, double camX, double camY, double camZoom)
        {
            JObj root = new JObj();
            root["v"] = 1.0;
            root["t"] = nowUnix;
            JObj stock = new JObj();
            foreach (string r in Data.ResourceIds) stock[r] = Stock[r];
            root["stock"] = stock;
            List<object> list = new List<object>();
            foreach (KeyValuePair<Tile, Placed> kv in Buildings)
            {
                JObj bucket = new JObj();
                foreach (KeyValuePair<string, double> b in kv.Value.Bucket) bucket[b.Key] = b.Value;
                list.Add(new List<object> { (double)kv.Key.X, (double)kv.Key.Y, kv.Value.Id, (double)kv.Value.Level, bucket });
            }
            root["b"] = list;
            root["cam"] = new List<object> { camX, camY, camZoom };
            return MiniJson.Serialize(root);
        }

        /// <summary>Returns null (and changes nothing) when the text is not a save.</summary>
        public LoadInfo Deserialize(string json, double nowUnix)
        {
            JObj data = MiniJson.ParseObject(json);
            if (data == null) return null;
            Buildings.Clear();
            foreach (object o in J.A(data, "b"))
            {
                List<object> e = J.Arr(o);
                if (e.Count < 3) continue;
                string id = J.Str(e[2]);
                if (!Data.Defs.ContainsKey(id)) continue;
                Tile t = new Tile((int)J.Num(e[0]), (int)J.Num(e[1]));
                Placed p = new Placed { Id = id };
                if (e.Count >= 4) p.Level = Math.Max(1, Math.Min((int)J.Num(e[3], 1), MaxLevel(id)));
                if (e.Count >= 5 && e[4] is JObj)
                {
                    JObj bk = (JObj)e[4];
                    foreach (string r in bk.Keys)
                        if (Data.ResourceIds.Contains(r)) p.Bucket[r] = Math.Min(J.Num(bk[r]), BucketCapAt(id, p.Level, r));
                }
                Buildings[t] = p;
            }
            JObj st = J.O(data, "stock");
            foreach (string r in Data.ResourceIds) if (st.Has(r)) Stock[r] = J.Num(st[r]);
            LoadInfo info = new LoadInfo();
            List<object> cp = J.A(data, "cam");
            if (cp.Count == 3)
            {
                info.HasCam = true;
                info.CamX = J.Num(cp[0]);
                info.CamY = J.Num(cp[1]);
                info.CamZoom = J.Num(cp[2], 0.4);
            }
            Recalc();
            // Offline accumulation: buildings keep producing into their own stores (up to their capacity).
            double dt = Math.Max(0.0, Math.Min(nowUnix - J.N(data, "t", 0), Data.MaxOfflineSeconds));
            if (dt > 30.0)
            {
                Dictionary<string, double> made = new Dictionary<string, double>();
                foreach (Placed p in Buildings.Values)
                {
                    foreach (string r in Data.Defs[p.Id].Produces.Keys)
                    {
                        double cur;
                        p.Bucket.TryGetValue(r, out cur);
                        double add = Math.Min(BucketCapAt(p.Id, p.Level, r) - cur, RateAt(p.Id, p.Level, r) * dt);
                        if (add > 0.0)
                        {
                            p.Bucket[r] = cur + add;
                            double m;
                            made.TryGetValue(r, out m);
                            made[r] = m + add;
                        }
                    }
                }
                string txt = GainsText(made);
                if (txt != "") info.OfflineMessage = Loc.T("toast.offline", "gains", txt);
            }
            return info;
        }
    }
}
