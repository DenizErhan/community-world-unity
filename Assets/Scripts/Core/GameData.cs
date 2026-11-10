// Gameplay data read from data/buildings.json. Rules never hard-code content (GDD pillar 5).
using System.Collections.Generic;

namespace CommunityWorld
{
    public class BuildingDef
    {
        public string Id;
        public string Requires = "";
        public Dictionary<string, double> Cost = new Dictionary<string, double>();
        public Dictionary<string, double> Produces = new Dictionary<string, double>();
        public double Capacity;
        public JObj Upgrade = new JObj();   // per-building overrides of the global upgrade rules
    }

    public class GameData
    {
        public string Age = "stone";
        public int GridW = 14;
        public int GridH = 14;
        public double BaseCapacity = 50;
        public double MaxOfflineSeconds = 8 * 3600;
        public double BucketSeconds = 120;
        public JObj Upgrade = new JObj();
        public string AgeUpNext = "";
        public Dictionary<string, int> AgeUpRequires = new Dictionary<string, int>();
        public List<string> ResourceIds = new List<string>();
        public Dictionary<string, double> Start = new Dictionary<string, double>();
        public List<string> Order = new List<string>();
        public Dictionary<string, BuildingDef> Defs = new Dictionary<string, BuildingDef>();

        /// <summary>Returns null when the text is not valid game data.</summary>
        public static GameData Parse(string json)
        {
            JObj cfg = MiniJson.ParseObject(json);
            if (cfg == null) return null;
            GameData d = new GameData();
            d.Age = J.S(cfg, "age", "stone");
            JObj g = J.O(cfg, "grid");
            d.GridW = (int)J.N(g, "w", 14);
            d.GridH = (int)J.N(g, "h", 14);
            d.BaseCapacity = J.N(cfg, "base_capacity", 50);
            d.MaxOfflineSeconds = J.N(cfg, "max_offline_hours", 8) * 3600.0;
            d.BucketSeconds = J.N(cfg, "bucket_seconds", 120);
            d.Upgrade = J.O(cfg, "upgrade");
            JObj ageUp = J.O(cfg, "age_up");
            d.AgeUpNext = J.S(ageUp, "next");
            JObj req = J.O(ageUp, "requires");
            foreach (string k in req.Keys) d.AgeUpRequires[k] = (int)J.Num(req[k]);
            JObj res = J.O(cfg, "resources");
            foreach (string k in res.Keys) d.ResourceIds.Add(k);
            d.Start = Amounts(J.O(cfg, "start"));
            foreach (object o in J.A(cfg, "buildings"))
            {
                JObj b = J.Obj(o);
                BuildingDef def = new BuildingDef();
                def.Id = J.S(b, "id");
                def.Requires = J.S(b, "requires");
                def.Cost = Amounts(J.O(b, "cost"));
                def.Produces = Amounts(J.O(b, "produces"));
                def.Capacity = J.N(b, "capacity");
                def.Upgrade = J.O(b, "upgrade");
                if (def.Id == "") continue;
                d.Defs[def.Id] = def;
                d.Order.Add(def.Id);
            }
            if (d.Order.Count == 0 || d.ResourceIds.Count == 0) return null;
            return d;
        }

        private static Dictionary<string, double> Amounts(JObj o)
        {
            Dictionary<string, double> m = new Dictionary<string, double>();
            foreach (string k in o.Keys) m[k] = J.Num(o[k]);
            return m;
        }

        /// <summary>A building's own "upgrade" block overrides the global defaults.</summary>
        public double Up(string id, string key)
        {
            BuildingDef def = Defs[id];
            if (def.Upgrade.Has(key)) return J.Num(def.Upgrade[key], 1.0);
            return J.N(Upgrade, key, 1.0);
        }
    }
}
