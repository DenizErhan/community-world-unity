// Rules tests that run without Unity (mono):
//   mcs -out:tests/core_tests.exe Assets/Scripts/Core/*.cs tests/CoreTests.cs && mono tests/core_tests.exe
// Ported from the Godot smoke test; exits with code 1 on any failure.
using System;
using System.Collections.Generic;
using System.IO;
using CommunityWorld;

public static class CoreTests
{
    static int failures;
    static int checks;

    static void Check(bool cond, string label)
    {
        checks++;
        if (cond) Console.WriteLine("  ok    - " + label);
        else { failures++; Console.WriteLine("  FAIL  - " + label); }
    }

    static bool Near(double a, double b, double eps = 0.01) { return Math.Abs(a - b) < eps; }

    static List<string> toasts = new List<string>();
    static List<string> fx = new List<string>();
    static int saves;

    static GameSession NewSession(out JObj tokens)
    {
        GameData data = GameData.Parse(File.ReadAllText("Assets/Resources/data/buildings.json"));
        tokens = MiniJson.ParseObject(File.ReadAllText("Assets/Resources/design/tokens.json"));
        GameSession s = new GameSession(new GameState(data), tokens);
        s.Toast += delegate (string m) { toasts.Add(m); };
        s.FxEvent += delegate (string k, Tile t) { fx.Add(k); };
        s.SaveRequested += delegate { saves++; };
        return s;
    }

    static void BuildAt(GameSession s, string id, Tile t)
    {
        s.BeginPlace(id, Tile.None);
        s.SetPending(t);
        s.ConfirmPlace();
    }

    public static int Main()
    {
        Loc.Init(File.ReadAllText("Assets/Resources/locales/en.json"), "en", null);
        JObj tokens;
        GameSession s = NewSession(out tokens);
        GameState st = s.State;

        Console.WriteLine("JSON");
        object parsed = MiniJson.Parse("{\"a\": [1, 2.5, \"x\\n\\u0041\"], \"b\": {\"c\": true, \"d\": null}}");
        JObj po = parsed as JObj;
        Check(po != null && J.A(po, "a").Count == 3 && J.Num(J.A(po, "a")[1]) == 2.5, "parses arrays and numbers");
        Check(J.Str(J.A(po, "a")[2]) == "x\nA", "parses string escapes");
        Check(MiniJson.Parse("{\"a\": ") == null, "broken JSON gives null, not a crash");
        string round = MiniJson.Serialize(MiniJson.Parse("{\"k\":[1,2.5,{\"z\":\"q\"}],\"f\":false}"));
        Check(round == "{\"k\":[1,2.5,{\"z\":\"q\"}],\"f\":false}", "serialize keeps order and numbers");

        Console.WriteLine("Coordinates");
        bool rt = true;
        for (int gx = 0; gx < st.Data.GridW; gx++)
            for (int gy = 0; gy < st.Data.GridH; gy++)
            {
                Vec2 c = s.Geo.TileCenter(gx, gy);
                if (s.Geo.WorldToTile(c) != new Tile(gx, gy)) rt = false;
                if (s.Geo.WorldToTile(c + new Vec2(12, 5)) != new Tile(gx, gy)) rt = false;
            }
        Check(rt, "tile center <-> world conversion is consistent on every tile");

        Console.WriteLine("Placement ghost");
        s.DefaultGhostTile = new Tile(7, 7);
        s.BeginPlace("hut", Tile.None);
        Check(s.PendingId == "hut", "choosing a building puts a ghost on the grid");
        Check(st.InGrid(s.PendingTile) && !st.Has(s.PendingTile), "the ghost starts on a free tile");
        Check(st.Buildings.Count == 0, "nothing is built before the tick");
        s.Tap(new Tile(6, 6));
        Check(s.PendingTile == new Tile(6, 6) && st.Buildings.Count == 0, "tapping a tile only moves the ghost");
        s.Nudge(1, 0);
        Check(s.PendingTile == new Tile(7, 6), "an arrow moves the ghost one tile");
        s.SetPending(new Tile(-5, 99));
        Check(s.PendingTile == new Tile(0, st.Data.GridH - 1), "the ghost stays inside the grid");
        s.SetPending(new Tile(6, 6));
        Vec2 centre = s.Geo.TileCenter(6, 6);
        Check(s.OnGhost(centre, 0.4), "a press on the ghost grabs it");
        Check(!s.OnGhost(centre + new Vec2(900, 900), 0.4), "a press far from the ghost pans the camera instead");
        s.CancelPlace();
        Check(s.PendingId == "" && st.Buildings.Count == 0, "the cross cancels without building");
        Check(st.Stock["stone"] == 30.0, "cancelling spends nothing");

        Console.WriteLine("Rules");
        s.BeginPlace("farm", Tile.None);
        Check(s.PendingError() != "" && !s.ConfirmEnabled, "farm cannot be confirmed before a hut (tick is disabled)");
        s.ConfirmPlace();
        Check(st.Buildings.Count == 0, "confirming an invalid ghost builds nothing");
        s.CancelPlace();
        BuildAt(s, "hut", new Tile(6, 6));
        Check(st.Has(new Tile(6, 6)), "hut built with the tick");
        Check(Near(st.Stock["stone"], 20.0, 0.5), "hut cost 10 stone");
        Check(s.PendingId == "", "the ghost is gone after building");
        s.BeginPlace("hut", Tile.None);
        s.SetPending(new Tile(6, 6));
        Check(s.PendingError() != "", "cannot place on an occupied tile");
        s.CancelPlace();
        BuildAt(s, "quarry", new Tile(7, 6));
        Check(st.Has(new Tile(7, 6)), "quarry built");
        s.BeginPlace("farm", Tile.None);
        s.SetPending(new Tile(8, 6));
        Check(s.PendingError() != "", "farm refused when stone is short");
        s.ConfirmPlace();
        Check(!st.Has(new Tile(8, 6)), "confirming without enough stone builds nothing");
        s.CancelPlace();

        Console.WriteLine("Production and storage");
        double before = st.Stock["stone"];
        s.Tick(10.0);
        Check(st.Stock["stone"] == before, "production does not go straight into storage");
        Check(Near(st.Buildings[new Tile(7, 6)].Bucket["stone"], 5.0), "quarry holds +5 stone after 10 s");
        s.CollectAndReport(new List<Tile> { new Tile(7, 6) });
        Check(Near(st.Stock["stone"] - before, 5.0, 0.5), "collecting moves +5 stone into storage");
        Check(st.BucketTotal(new Tile(7, 6)) < 0.01, "the building is empty after collecting");
        s.Tick(100000.0);
        double capB = st.BucketCapAt("quarry", 1, "stone");
        Check(Near(st.Buildings[new Tile(7, 6)].Bucket["stone"], capB) && capB == 60.0, "a building stops producing when its store is full (120 s of output)");
        st.Stock["stone"] = 40.0;
        s.CollectAll();
        Check(st.Stock["stone"] <= st.Caps["stone"] + 0.001, "storage never exceeds the cap");
        Check(st.Stock["stone"] >= st.Caps["stone"] - 1.0 && st.BucketTotal(new Tile(7, 6)) > 0.0, "what does not fit stays in the building");
        Check(st.Caps["stone"] == 75.0, "hut raised storage 50 -> 75");

        Console.WriteLine("Tapping a building");
        Vec2 hutC = s.Geo.TileCenter(6, 6);
        foreach (double z in new double[] { 0.4, 1.0, 1.6 })
        {
            string tag = "zoom " + z + ": ";
            Check(s.PickTile(hutC + new Vec2(0, -30), z) == new Tile(6, 6), tag + "a tap on the hut's body picks the hut, not a neighbour");
            Check(s.PickTile(s.Geo.TileCenter(7, 6) + new Vec2(0, -20), z) == new Tile(7, 6), tag + "a tap on the quarry's body picks the quarry");
            Check(s.PickTile(hutC + new Vec2(0, -400), z) != new Tile(6, 6), tag + "a tap far above the roof does not pick the hut");
        }
        Check(s.Geo.WorldToTile(hutC + new Vec2(0, -45)) != new Tile(6, 6), "(the roof is drawn over a different ground tile)");
        Check(s.PickTile(hutC + new Vec2(0, -45), 0.4) == new Tile(6, 6), "a tap on the roof picks the hut even though the ground tile under it is another");
        Check(s.PickTile(hutC + new Vec2(500, 0), 0.4) == s.Geo.WorldToTile(hutC + new Vec2(500, 0)), "a tap on open ground picks that ground tile");

        Console.WriteLine("Moving a building");
        double stoneBeforeMove = st.Stock["stone"];
        s.Tap(new Tile(7, 6));
        Check(s.PendingId == "" && s.SelTile == new Tile(7, 6), "tapping a building only selects it");
        Check(st.Has(new Tile(7, 6)), "selecting does not move or change anything");
        s.Tap(new Tile(12, 12));
        Check(s.SelTile.IsNone, "tapping open ground deselects");
        s.Tap(new Tile(7, 6));
        s.OnInfo();
        Check(s.SelTile == new Tile(7, 6), "info keeps the selection");
        s.OnMove();
        Check(s.PendingId == "quarry" && s.MovingFrom == new Tile(7, 6) && s.SelTile.IsNone, "Move picks the building up and hides the menu");
        Check(st.Has(new Tile(7, 6)), "the building stays where it was until the tick");
        s.SetPending(new Tile(6, 6));
        Check(s.PendingError() != "", "cannot move onto an occupied tile");
        s.SetPending(new Tile(9, 9));
        s.ConfirmPlace();
        Check(st.Has(new Tile(9, 9)) && !st.Has(new Tile(7, 6)), "tick moves the building");
        Check(st.Stock["stone"] == stoneBeforeMove, "moving is free");
        Check(Near(st.Rates["stone"], 0.5, 0.001), "production is unchanged by moving");
        s.Tap(new Tile(9, 9));
        s.OnMove();
        s.SetPending(new Tile(3, 3));
        s.CancelPlace();
        Check(st.Has(new Tile(9, 9)) && !st.Has(new Tile(3, 3)), "cross puts the building back");

        Console.WriteLine("Save / load");
        string json = st.Serialize(1000.0, 10, 20, 0.5);
        GameState st2 = new GameState(st.Data);
        LoadInfo li = st2.Deserialize(json, 1000.0);
        Check(li != null && st2.Buildings.Count == 2, "2 buildings restored from save");
        Check(st2.Has(new Tile(9, 9)), "moved building position is saved");
        Check(li != null && li.HasCam && li.CamZoom == 0.5, "camera is saved");
        Check(st.Data.Defs.Count == 4 && st2.Deserialize("not json", 1000.0) == null, "a broken save is ignored");

        Console.WriteLine("Offline production");
        GameState st3 = new GameState(st.Data);
        string old = st.Serialize(1000.0, 0, 0, 1);
        st.Buildings[new Tile(9, 9)].Bucket["stone"] = 0.0;
        old = st.Serialize(1000.0, 0, 0, 1);
        LoadInfo off = st3.Deserialize(old, 1000.0 + 90.0);
        Check(off != null && off.OfflineMessage != "", "coming back after 90 s reports what the buildings made");
        Check(Near(st3.Buildings[new Tile(9, 9)].Bucket["stone"], 45.0, 0.5), "offline output fills the building (0.5/s for 90 s)");
        GameState st4 = new GameState(st.Data);
        LoadInfo off2 = st4.Deserialize(old, 1000.0 + 10.0);
        Check(off2 != null && off2.OfflineMessage == "", "a short break reports nothing");
        GameState st5 = new GameState(st.Data);
        st5.Deserialize(old, 1000.0 + 10 * 3600.0);
        Check(Near(st5.Buildings[new Tile(9, 9)].Bucket["stone"], 60.0, 0.5), "offline output is capped by the building's store");

        Console.WriteLine("Demolish");
        s.Tap(new Tile(9, 9));
        s.OnRemove();
        Check(st.Has(new Tile(9, 9)) && s.RemoveArmed > 0.0, "first Remove tap only arms the button");
        s.Tap(new Tile(6, 6));
        Check(s.RemoveArmed == 0.0, "selecting another building disarms Remove");
        s.Tap(new Tile(9, 9));
        s.OnRemove();
        Check(st.RefundOf("quarry")["stone"] == 6.0, "refund is half the cost, e.g. 6 of the quarry's 12 stone");
        s.OnRemove();
        Check(!st.Has(new Tile(9, 9)) && s.SelTile.IsNone, "second Remove tap demolishes the quarry");
        s.Tap(new Tile(6, 6));
        s.OnRemove();
        s.Tick(10.0);
        Check(s.RemoveArmed == 0.0, "the armed Remove expires after a few seconds");

        Console.WriteLine("Upgrades");
        st.Buildings[new Tile(3, 3)] = new Placed { Id = "hut" };
        st.Buildings[new Tile(4, 3)] = new Placed { Id = "farm" };
        st.Recalc();
        st.Stock["stone"] = 70.0;
        s.Tap(new Tile(4, 3));
        s.OnUpgrade();
        Check(st.Level(new Tile(4, 3)) == 1, "a building cannot pass the level of the hut it needs");
        Check(st.UpgradeCost("hut", 1)["stone"] == 18.0, "hut level 2 costs 18 stone (10 x 1.8)");
        s.Tap(new Tile(3, 3));
        s.OnUpgrade();
        Check(st.Level(new Tile(3, 3)) == 2 && Near(st.Stock["stone"], 52.0), "upgrading the hut costs 18 stone and raises its level");
        Check(st.Caps["stone"] == 50.0 + 25.0 + 50.0 || st.Caps["stone"] == 50.0 + 25.0 + 50.0, "hut level 2 doubles its storage bonus (25 -> 50)");
        s.Tap(new Tile(4, 3));
        s.OnUpgrade();
        Check(st.Level(new Tile(4, 3)) == 2, "farm can now reach level 2");
        Check(Near(st.RateAt("farm", 2, "food"), 0.6, 0.001) && Near(st.Rates["food"], 0.6, 0.001), "farm level 2 produces 1.5x");
        Check(st.BucketCapAt("farm", 2, "food") > st.BucketCapAt("farm", 1, "food"), "a higher level holds more output");
        st.Stock["stone"] = 70.0;
        s.Tap(new Tile(3, 3));
        s.OnUpgrade();
        st.Stock["stone"] = 70.0;
        s.OnUpgrade();
        Check(st.Level(new Tile(3, 3)) == 3, "hut stops at the maximum level");
        st.Stock["stone"] = 0.0;
        s.Tap(new Tile(4, 3));
        s.OnUpgrade();
        Check(st.Level(new Tile(4, 3)) == 2, "upgrade needs the resources");
        st.Stock["stone"] = 70.0;
        s.OnUpgrade();
        Check(st.Level(new Tile(4, 3)) == 3, "farm reaches level 3");
        Check(st.RefundOf("farm", 3)["stone"] == Math.Floor((15.0 + 27.0 + 49.0) * 0.5), "removing refunds half of everything invested, upgrades included");
        string saved = st.Serialize(2000.0, 0, 0, 1);
        GameState st6 = new GameState(st.Data);
        st6.Deserialize(saved, 2000.0);
        Check(st6.Level(new Tile(3, 3)) == 3 && st6.Level(new Tile(4, 3)) == 3, "levels are saved and restored");
        Check(!st.AgeReady(), "age-up is not ready until the required levels are reached");
        Check(st.AgeProgress().Count == 4, "age-up lists its four requirements");
        Check(fx.Contains("sparkle") && fx.Contains("dust"), "upgrades and building raise effects");
        Check(saves > 0, "actions ask for a save");

        Console.WriteLine("Design system");
        JObj ages = J.O(tokens, "ages");
        Check(ages.Has(st.Data.Age), "tokens loaded and the current age has a palette");
        JObj loops = J.O(J.O(tokens, "motion"), "loops");
        foreach (string id in st.Data.Order) Check(loops.Has(id) && J.A(loops, id).Count > 0, id + " has an animation loop in the design tokens");

        Console.WriteLine("Localization");
        Check(Loc.T("toast.built", "name", "Farm") == "Farm built.", "placeholders are substituted");
        Check(Loc.T("no.such.key") == "no.such.key", "missing keys return the key (visible gap, no crash)");
        bool strings = true;
        foreach (string r in st.Data.ResourceIds) if (!Loc.Has("res." + r + ".name")) strings = false;
        foreach (string id in st.Data.Order) { if (!Loc.Has("building." + id + ".name") || !Loc.Has("building." + id + ".short")) strings = false; }
        Check(strings, "every resource and building has English strings");

        s.Reset();
        Check(st.Buildings.Count == 0, "reset cleared the village");
        Check(st.Stock["stone"] == 30.0, "reset restored the starting stock");
        s.StartIntro();
        Check(s.PendingId == "hut" && s.IntroText != "", "a fresh village starts with the hut ghost and a welcome hint");

        Console.WriteLine("Result: " + (failures == 0 ? "PASS" : failures + " FAILED") + " (" + checks + " checks)");
        return failures == 0 ? 0 : 1;
    }
}
