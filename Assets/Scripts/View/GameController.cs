// Glue: loads data, builds the session and the views, wires them together, runs the frame loop,
// handles touch/mouse input and saving. The game rules live in Core/ (no UnityEngine there).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CommunityWorld
{
    public class GameController : MonoBehaviour
    {
        private const double TapSlop = 14.0;      // design px a finger may move and still count as a tap
        private const double PanSpeed = 0.8;      // drag pans the camera at 80% of finger speed (feels calmer)
        private const string SaveKey = "community_world_save";

        private GameSession session;
        private JObj tokens;
        private CameraRig rig;
        private WorldView world;
        private HudView hud;
        private MarksView marks;
        private FxLayer fx;
        private bool ready;

        private float hudTimer, saveTimer;

        // input state
        private bool dragging, uiPress, ghostGrab, ghostDragged;
        private double dragMoved, pinchLast;
        private Vec2 lastPos;
        private Tile grabOffset;
        private Vector2 lastMouse;
        private readonly List<RaycastResult> raycasts = new List<RaycastResult>();

        private void Start()
        {
            Application.targetFrameRate = 60;
            Input.simulateMouseWithTouches = false;
            Input.multiTouchEnabled = true;

            TextAsset dataText = Resources.Load<TextAsset>("data/buildings");
            TextAsset tokenText = Resources.Load<TextAsset>("design/tokens");
            TextAsset english = Resources.Load<TextAsset>("locales/en");
            if (dataText == null || tokenText == null || english == null)
            {
                Debug.LogError("Community World: game data is missing from Resources.");
                return;
            }
            Loc.Init(english.text, "en", null);
            GameData data = GameData.Parse(dataText.text);
            tokens = MiniJson.ParseObject(tokenText.text);
            if (data == null || tokens == null)
            {
                Debug.LogError("Community World: game data could not be read.");
                return;
            }
            if (!J.O(tokens, "ages").Has(data.Age))
            {
                Debug.LogError("Community World: unknown age in buildings.json: " + data.Age);
                return;
            }

            session = new GameSession(new GameState(data), tokens);
            Color clear = Gfx.Hex(J.S(J.O(J.O(tokens, "ui"), "world"), "clear"));
            rig = new CameraRig(session.Geo, data.GridW, data.GridH, clear);
            world = new WorldView(session, tokens, data.Age);
            hud = new HudView(session, tokens);
            marks = new MarksView(session, rig, hud.CanvasRect, tokens);
            fx = new FxLayer(session, rig, hud.CanvasRect, tokens, data.Age);
            Wire();

            Load();
            session.State.Recalc();
            if (session.State.Buildings.Count == 0)
            {
                session.DefaultGhostTile = session.Geo.WorldToTile(rig.Position);
                session.StartIntro();
                EnsureGhostVisible();
            }
            OnChanged();
            ready = true;
        }

        private void Wire()
        {
            session.Toast += hud.Toast;
            session.HoldInfo += hud.Hold;
            session.FxEvent += delegate (string kind, Tile t)
            {
                if (kind == "dust") fx.BurstDust(t);
                else fx.Sparkle(t);
            };
            session.FloatText += delegate (Tile t, string text, string res)
            {
                fx.FloatText(session.BadgeCentre(t), text, Gfx.Hex(J.S(J.O(J.O(tokens, "ui"), "colors"), "resource_" + res)));
            };
            session.Changed += OnChanged;
            session.SaveRequested += Save;

            hud.OnPalette = delegate (string id)
            {
                session.DefaultGhostTile = session.Geo.WorldToTile(rig.Position);
                session.BeginPlace(id, Tile.None);
                EnsureGhostVisible();
            };
            hud.OnConfirm = session.ConfirmPlace;
            hud.OnCancel = session.CancelPlace;
            hud.OnNudge = delegate (int dx, int dy) { session.Nudge(dx, dy); EnsureGhostVisible(); };
            hud.OnCollectAll = session.CollectAll;
            hud.OnReset = session.Reset;
            hud.OnInfo = session.OnInfo;
            hud.OnMove = delegate { session.OnMove(); EnsureGhostVisible(); };
            hud.OnUpgrade = session.OnUpgrade;
            hud.OnRemove = session.OnRemove;
        }

        private void OnChanged()
        {
            if (world == null) return;
            world.Refresh();
            hud.Refresh();
            PositionMenu();
        }

        // ------------------------------------------------------------ frame loop

        private void Update()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            session.Tick(dt);
            HandleInput();
            hud.Update(dt);
            fx.Update(dt);
            marks.Update();
            hudTimer += dt;
            if (hudTimer >= 0.2f)
            {
                hudTimer = 0f;
                hud.Refresh();
            }
            saveTimer += dt;
            if (saveTimer >= 5f)
            {
                saveTimer = 0f;
                Save();
            }
            PositionMenu();
            rig.Apply();
        }

        // ------------------------------------------------------------ camera helpers

        /// <summary>Keeps the menu under the selected building (above it when the bottom bar would cover it).</summary>
        private void PositionMenu()
        {
            if (!ready && hud == null) return;
            if (!session.HasSelection || session.Placing) return;
            JObj ab = J.O(J.O(tokens, "ui"), "actionbar");
            double margin = J.N(ab, "margin", 6), offset = J.N(ab, "offset", 10);
            Vec2 c = rig.WorldToScreen(session.Geo.TileCenter(session.SelTile));
            Vec2 vp = CameraRig.Viewport;
            Vector2 size = hud.ActionMenuSize;
            double x = c.X - size.x * 0.5;
            double y = c.Y + session.Geo.TH * 0.5 * rig.Zoom + offset;
            double bottomY = vp.Y - HudView.BottomHeight;
            if (y + size.y > bottomY - margin) y = c.Y - session.Geo.PickHeight * rig.Zoom - size.y - offset;
            double topY = HudView.TopHeight;
            y = Math.Max(topY + margin, Math.Min(bottomY - size.y - margin, y));
            x = Math.Max(margin, Math.Min(vp.X - size.x - margin, x));
            hud.PlaceMenu(new Vec2(x, y));
        }

        /// <summary>Keep the ghost between the two HUD bars; recenter the camera on it if it is out of view.</summary>
        private void EnsureGhostVisible()
        {
            if (!session.Placing) return;
            double margin = J.N(J.O(J.O(tokens, "ui"), "placement"), "view_margin", 90);
            Vec2 vp = CameraRig.Viewport;
            double topH = HudView.TopHeight, botH = HudView.BottomHeight;
            Vec2 tc = session.Geo.TileCenter(session.PendingTile);
            Vec2 sp = rig.WorldToScreen(tc);
            if (sp.X >= margin && sp.X <= vp.X - margin && sp.Y >= topH + margin && sp.Y <= vp.Y - botH - margin * 0.5) return;
            double freeCy = (topH + vp.Y - botH) * 0.5;
            rig.Position = new Vec2(tc.X, tc.Y - (freeCy - vp.Y * 0.5) / rig.Zoom);
            rig.Clamp();
        }

        // ------------------------------------------------------------ input

        private enum Phase { Down, Move, Up }

        private void HandleInput()
        {
            if (Input.touchCount >= 2)
            {
                Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                double d = Vector2.Distance(a.position, b.position);
                if (pinchLast > 0.0 && d > 0.0) rig.SetZoom(rig.Zoom * d / pinchLast);
                pinchLast = d;
                dragging = false;
                uiPress = false;
                return;
            }
            pinchLast = 0.0;
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) Pointer(Phase.Down, t.position);
                else if (t.phase == TouchPhase.Moved) Pointer(Phase.Move, t.position);
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) Pointer(Phase.Up, t.position);
            }
            else
            {
                float wheel = Input.mouseScrollDelta.y;
                if (wheel > 0.01f) rig.SetZoom(rig.Zoom * 1.12);
                else if (wheel < -0.01f) rig.SetZoom(rig.Zoom / 1.12);
                Vector2 mp = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) Pointer(Phase.Down, mp);
                else if (Input.GetMouseButtonUp(0)) Pointer(Phase.Up, mp);
                else if (Input.GetMouseButton(0) && mp != lastMouse) Pointer(Phase.Move, mp);
                lastMouse = mp;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) session.ConfirmPlace();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (session.Placing) session.CancelPlace();
                else session.Deselect();
            }
        }

        private bool OverUi(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            PointerEventData ped = new PointerEventData(EventSystem.current);
            ped.position = screenPos;
            raycasts.Clear();
            EventSystem.current.RaycastAll(ped, raycasts);
            return raycasts.Count > 0;
        }

        private void Pointer(Phase ph, Vector2 screenPos)
        {
            Vec2 d = CameraRig.ScreenToDesign(screenPos);
            switch (ph)
            {
                case Phase.Down:
                    if (OverUi(screenPos)) { uiPress = true; return; }
                    uiPress = false;
                    dragging = true;
                    dragMoved = 0.0;
                    ghostGrab = false;
                    ghostDragged = false;
                    lastPos = d;
                    if (session.Placing)
                    {
                        Vec2 w = rig.ScreenToWorld(d);
                        if (session.OnGhost(w, rig.Zoom))
                        {
                            ghostGrab = true;
                            Tile g = session.Geo.WorldToTile(w);
                            grabOffset = new Tile(session.PendingTile.X - g.X, session.PendingTile.Y - g.Y);
                        }
                    }
                    break;

                case Phase.Move:
                    if (uiPress || !dragging) return;
                    Vec2 rel = d - lastPos;
                    lastPos = d;
                    dragMoved += Math.Sqrt(rel.X * rel.X + rel.Y * rel.Y);
                    if (ghostGrab)
                    {
                        if (dragMoved >= J.N(J.O(J.O(tokens, "ui"), "placement"), "drag_slop", 6))
                        {
                            ghostDragged = true;
                            session.SetPending(rig.ScreenToTile(d) + grabOffset);
                        }
                    }
                    else if (dragMoved >= TapSlop) rig.Pan(rel * PanSpeed);
                    break;

                case Phase.Up:
                    if (uiPress) { uiPress = false; return; }
                    if (dragging && dragMoved < TapSlop && !ghostDragged)
                    {
                        Vec2 w = rig.ScreenToWorld(d);
                        Tile bt = session.Placing ? Tile.None : session.BadgeAt(w, rig.Zoom);
                        if (!bt.IsNone) session.CollectAndReport(new List<Tile> { bt });
                        else session.Tap(session.PickTile(w, rig.Zoom));
                    }
                    dragging = false;
                    ghostGrab = false;
                    break;
            }
        }

        // ------------------------------------------------------------ save / load

        private static double UnixNow()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private void Save()
        {
            if (!ready && rig == null) return;
            string json = session.State.Serialize(UnixNow(), rig.Position.X, rig.Position.Y, rig.Zoom);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (json == "") return;
            LoadInfo info = session.State.Deserialize(json, UnixNow());
            if (info == null) return;
            if (info.HasCam)
            {
                rig.Position = new Vec2(info.CamX, info.CamY);
                rig.SetZoom(info.CamZoom);
            }
            if (info.OfflineMessage != "") hud.Toast(info.OfflineMessage);
        }

        private void OnApplicationPause(bool paused) { if (paused && ready) Save(); }
        private void OnApplicationFocus(bool focus) { if (!focus && ready) Save(); }
        private void OnApplicationQuit() { if (ready) Save(); }
    }
}
