// The screen UI, built in code from the design tokens (uGUI): top bar with resource counters,
// bottom bar with the building palette or the placement controls, the info line, and the action
// menu that floats under a selected building. Canvas units equal design pixels.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CommunityWorld
{
    public class HudView
    {
        public const float TopHeight = 104f;
        public const float BottomHeight = 196f;

        public readonly Canvas Canvas;
        public readonly RectTransform CanvasRect;
        public RectTransform ActionMenu;
        public Vector2 ActionMenuSize;

        private readonly GameSession session;
        private readonly JObj tokens;
        private readonly JObj ui;
        private readonly Dictionary<string, Text> resLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, UiButton> paletteButtons = new Dictionary<string, UiButton>();
        private GameObject paletteRow;
        private GameObject placeRow;
        private UiButton confirmBtn;
        private UiButton upgradeBtn;
        private UiButton removeBtn;
        private UiButton resetBtn;
        private Text resetText;
        private Text toast;
        private CanvasGroup toastGroup;
        private float toastTimer = -1f;
        private bool toastHeld;
        private float resetArmed;
        private readonly float toastHold, toastFade;

        public Action<string> OnPalette;
        public Action OnConfirm, OnCancel, OnCollectAll, OnReset, OnInfo, OnMove, OnUpgrade, OnRemove;
        public Action<int, int> OnNudge;

        public class UiButton
        {
            public GameObject Go;
            public Button Button;
            public Image Background;
            public Text Label;
            public Image Icon;
            public Color Base;
            public Color Tint = Color.white;

            public void Apply()
            {
                Background.color = Gfx.Multiply(Base, Tint);
                if (Icon != null) Icon.color = new Color(Tint.r, Tint.g, Tint.b, Icon.color.a);
            }

            public void SetBase(Color c) { Base = c; Apply(); }
            public void SetTint(Color c) { Tint = c; Apply(); }
        }

        public HudView(GameSession session, JObj tokens)
        {
            this.session = session;
            this.tokens = tokens;
            ui = J.O(tokens, "ui");
            JObj motion = J.O(tokens, "motion");
            toastHold = (float)J.N(motion, "toast_hold", 2.2);
            toastFade = (float)J.N(motion, "toast_fade", 0.6);

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            GameObject cgo = new GameObject("HUD", typeof(RectTransform));
            Canvas = cgo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            cgo.AddComponent<GraphicRaycaster>();
            CanvasRect = cgo.GetComponent<RectTransform>();

            BuildTopBar();
            BuildBottomBar();
            BuildActionMenu();
            Refresh();
        }

        // ------------------------------------------------------------ tokens

        private Color UCol(string name) { return Gfx.Hex(J.S(J.O(ui, "colors"), name)); }
        private int FontSize(string name, int def) { return (int)J.N(J.O(ui, "font_size"), name, def); }
        private int Gap { get { return (int)J.N(J.O(ui, "spacing"), "gap", 8); } }
        private int Margin { get { return (int)J.N(J.O(ui, "spacing"), "panel_margin", 12); } }

        // ------------------------------------------------------------ building blocks

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private RectTransform Panel(string name, Transform parent, Color col)
        {
            RectTransform rt = NewRect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = col;
            return rt;
        }

        private Text NewText(string name, Transform parent, int size, Color col, TextAnchor anchor)
        {
            RectTransform rt = NewRect(name, parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = Gfx.Font;
            t.fontSize = size;
            t.color = col;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.raycastTarget = false;
            return t;
        }

        private static void Fit(Text t, int min, int max)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = min;
            t.resizeTextMaxSize = max;
        }

        private static LayoutElement Elem(GameObject go, float minW = -1, float prefW = -1, float flexW = -1, float minH = -1, float prefH = -1)
        {
            LayoutElement le = go.AddComponent<LayoutElement>();
            if (minW >= 0) le.minWidth = minW;
            if (prefW >= 0) le.preferredWidth = prefW;
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (minH >= 0) le.minHeight = minH;
            if (prefH >= 0) le.preferredHeight = prefH;
            return le;
        }

        private UiButton NewButton(string name, Transform parent, Color col)
        {
            RectTransform rt = NewRect(name, parent);
            UiButton b = new UiButton();
            b.Go = rt.gameObject;
            b.Background = rt.gameObject.AddComponent<Image>();
            b.Background.sprite = Gfx.RoundedRect((int)J.N(J.O(ui, "button"), "radius", 12));
            b.Background.type = Image.Type.Sliced;
            b.Button = rt.gameObject.AddComponent<Button>();
            b.Button.targetGraphic = b.Background;
            ColorBlock cb = b.Button.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            b.Button.colors = cb;
            b.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            b.Base = col;
            b.Apply();
            return b;
        }

        private UiButton TextButton(string name, Transform parent, string text, int size, Color col)
        {
            UiButton b = NewButton(name, parent, col);
            b.Label = NewText("Label", b.Go.transform, size, UCol("text"), TextAnchor.MiddleCenter);
            Stretch(b.Label.rectTransform);
            b.Label.text = text;
            Fit(b.Label, 12, size);
            return b;
        }

        /// <summary>Icon button. side &gt; 0 = a square of that size, otherwise it fills its row.</summary>
        private UiButton IconButton(string name, Transform parent, string icon, string colorKey, float side)
        {
            UiButton b = NewButton(name, parent, UCol(colorKey));
            float disp = (float)J.N(J.O(ui, "icon"), "display", 56);
            RectTransform ir = NewRect("Icon", b.Go.transform);
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = new Vector2(disp, disp);
            b.Icon = ir.gameObject.AddComponent<Image>();
            b.Icon.sprite = Gfx.Art("ui/icon_" + icon);
            b.Icon.raycastTarget = false;
            if (side > 0) Elem(b.Go, side, side, 0, side, side);
            else Elem(b.Go, 0, 0, 1, (float)J.N(J.O(ui, "button"), "min_height", 104), (float)J.N(J.O(ui, "button"), "min_height", 104));
            return b;
        }

        // ------------------------------------------------------------ top bar

        private void BuildTopBar()
        {
            RectTransform top = Panel("TopBar", CanvasRect, UCol("panel_top"));
            top.anchorMin = new Vector2(0, 1);
            top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.sizeDelta = new Vector2(0, TopHeight);
            top.anchoredPosition = Vector2.zero;
            HorizontalLayoutGroup row = top.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(Margin, Margin, Margin, Margin);
            row.spacing = Gap;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            foreach (string r in session.Data.ResourceIds)
            {
                Text l = NewText("Res " + r, top, FontSize("label", 24), UCol("resource_" + r), TextAnchor.MiddleCenter);
                Fit(l, 14, FontSize("label", 24));
                Elem(l.gameObject, 0, 0, 1);
                resLabels[r] = l;
            }
            int small = FontSize("small", 20);
            UiButton collect = TextButton("Collect", top, Loc.T("ui.collect_all"), small, UCol("button_normal"));
            Elem(collect.Go, 120, 120, 0);
            collect.Button.onClick.AddListener(delegate { if (OnCollectAll != null) OnCollectAll(); });
            resetBtn = TextButton("Reset", top, Loc.T("ui.reset"), small, UCol("button_normal"));
            resetText = resetBtn.Label;
            Elem(resetBtn.Go, 104, 104, 0);
            resetBtn.Button.onClick.AddListener(OnResetClicked);
        }

        private void OnResetClicked()
        {
            if (resetArmed > 0f)
            {
                resetArmed = 0f;
                resetText.text = Loc.T("ui.reset");
                if (OnReset != null) OnReset();
                Toast(Loc.T("toast.reset_done"));
            }
            else
            {
                resetArmed = 3f;
                resetText.text = Loc.T("ui.reset_confirm");
            }
        }

        // ------------------------------------------------------------ bottom bar

        private void BuildBottomBar()
        {
            RectTransform bottom = Panel("BottomBar", CanvasRect, UCol("panel_bottom"));
            bottom.anchorMin = new Vector2(0, 0);
            bottom.anchorMax = new Vector2(1, 0);
            bottom.pivot = new Vector2(0.5f, 0);
            bottom.sizeDelta = new Vector2(0, BottomHeight);
            bottom.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup col = bottom.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(Margin, Margin, Margin, Margin);
            col.spacing = Gap;
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;

            toast = NewText("Toast", bottom, 22, UCol("text"), TextAnchor.MiddleCenter);
            Fit(toast, 14, 22);
            Elem(toast.gameObject, -1, -1, 1, 60, 60);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;

            int minH = (int)J.N(J.O(ui, "button"), "min_height", 104);
            paletteRow = NewRow("Palette", bottom, minH);
            foreach (string id in session.Data.Order)
            {
                BuildingDef d = session.Data.Defs[id];
                string id2 = id;
                UiButton b = TextButton("Build " + id, paletteRow.transform, Loc.T("building." + id + ".short") + "\n" + session.State.CostText(d.Cost), FontSize("button", 22), UCol("button_normal"));
                Elem(b.Go, 0, 0, 1, minH, minH);
                b.Button.onClick.AddListener(delegate { if (OnPalette != null) OnPalette(id2); });
                paletteButtons[id] = b;
            }

            placeRow = NewRow("Place", bottom, minH);
            float stretch = (float)J.N(J.O(ui, "placement"), "confirm_stretch", 1.4);
            UiButton cancel = IconButton("Cancel", placeRow.transform, "cancel", "button_cancel", 0);
            cancel.Go.GetComponent<LayoutElement>().flexibleWidth = stretch;
            cancel.Button.onClick.AddListener(delegate { if (OnCancel != null) OnCancel(); });
            object[][] arrows =
            {
                new object[] { "arrow_nw", -1, 0 },
                new object[] { "arrow_sw", 0, 1 },
                new object[] { "arrow_ne", 0, -1 },
                new object[] { "arrow_se", 1, 0 },
            };
            foreach (object[] a in arrows)
            {
                int dx = (int)a[1];
                int dy = (int)a[2];
                UiButton ab = IconButton("Nudge " + a[0], placeRow.transform, (string)a[0], "button_normal", 0);
                ab.Button.onClick.AddListener(delegate { if (OnNudge != null) OnNudge(dx, dy); });
            }
            confirmBtn = IconButton("Confirm", placeRow.transform, "confirm", "button_confirm", 0);
            confirmBtn.Go.GetComponent<LayoutElement>().flexibleWidth = stretch;
            confirmBtn.Button.onClick.AddListener(delegate { if (OnConfirm != null) OnConfirm(); });
            placeRow.SetActive(false);
        }

        private GameObject NewRow(string name, Transform parent, float height)
        {
            RectTransform rt = NewRect(name, parent);
            HorizontalLayoutGroup h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = Gap;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            Elem(rt.gameObject, -1, -1, 1, height, height);
            return rt.gameObject;
        }

        // ------------------------------------------------------------ action menu

        private void BuildActionMenu()
        {
            JObj ab = J.O(ui, "actionbar");
            int side = (int)J.N(ab, "button", 84);
            int gap = (int)J.N(ab, "gap", 6);
            ActionMenu = Panel("ActionMenu", CanvasRect, UCol("panel_bottom"));
            ActionMenu.anchorMin = ActionMenu.anchorMax = new Vector2(0, 1);
            ActionMenu.pivot = new Vector2(0, 1);
            ActionMenuSize = new Vector2(Margin * 2 + side * 4 + gap * 3, Margin * 2 + side);
            ActionMenu.sizeDelta = ActionMenuSize;
            HorizontalLayoutGroup h = ActionMenu.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(Margin, Margin, Margin, Margin);
            h.spacing = gap;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            UiButton info = IconButton("Info", ActionMenu, "info", "button_normal", side);
            info.Button.onClick.AddListener(delegate { if (OnInfo != null) OnInfo(); });
            UiButton move = IconButton("Move", ActionMenu, "move", "button_normal", side);
            move.Button.onClick.AddListener(delegate { if (OnMove != null) OnMove(); });
            upgradeBtn = IconButton("Upgrade", ActionMenu, "upgrade", "button_normal", side);
            upgradeBtn.Button.onClick.AddListener(delegate { if (OnUpgrade != null) OnUpgrade(); });
            removeBtn = IconButton("Remove", ActionMenu, "remove", "button_normal", side);
            removeBtn.Button.onClick.AddListener(delegate { if (OnRemove != null) OnRemove(); });
            ActionMenu.gameObject.SetActive(false);
        }

        /// <summary>Puts the menu at a screen position (design px, y down, top-left of the menu).</summary>
        public void PlaceMenu(Vec2 topLeft)
        {
            ActionMenu.anchoredPosition = new Vector2((float)topLeft.X, -(float)topLeft.Y);
        }

        // ------------------------------------------------------------ info line

        /// <summary>A message that fades out after a moment.</summary>
        public void Toast(string msg)
        {
            toast.text = msg;
            toast.color = UCol("text");
            toastGroup.alpha = 1f;
            toastHeld = false;
            toastTimer = 0f;
        }

        /// <summary>A message that stays until replaced (used while a ghost is out).</summary>
        public void Hold(string msg, bool warn)
        {
            toast.text = msg;
            toast.color = warn ? UCol("text_warn") : UCol("text");
            toastGroup.alpha = 1f;
            toastHeld = true;
            toastTimer = -1f;
        }

        // ------------------------------------------------------------ refresh

        /// <summary>Everything that follows the game state: labels, button states, which bar shows.</summary>
        public void Refresh()
        {
            GameState st = session.State;
            foreach (string r in session.Data.ResourceIds)
            {
                double rate = st.Rates[r];
                string rateTxt = rate > 0.0 ? Loc.T("ui.rate", "rate", rate.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)) : Loc.T("ui.no_rate");
                resLabels[r].text = Loc.T("ui.res_line", "name", st.RName(r), "cur", (int)st.Stock[r], "cap", (int)st.Caps[r]) + "\n" + rateTxt;
            }
            Color tintBad = UCol("button_unaffordable_tint");
            foreach (string id in session.Data.Order)
            {
                BuildingDef d = session.Data.Defs[id];
                UiButton b = paletteButtons[id];
                bool locked = d.Requires != "" && !st.HasBuilding(d.Requires);
                b.Button.interactable = !locked;
                b.SetTint(st.Afford(d.Cost) && !locked ? Color.white : tintBad);
            }
            paletteRow.SetActive(!session.Placing);
            placeRow.SetActive(session.Placing);
            confirmBtn.Button.interactable = session.ConfirmEnabled;
            confirmBtn.Icon.color = new Color(1, 1, 1, session.ConfirmEnabled ? 1f : (float)J.N(J.O(ui, "icon"), "disabled_alpha", 0.35));
            bool menu = session.HasSelection && !session.Placing;
            if (ActionMenu.gameObject.activeSelf != menu) ActionMenu.gameObject.SetActive(menu);
            if (menu)
            {
                upgradeBtn.SetTint(st.CanUpgrade(session.SelTile) ? Color.white : tintBad);
                removeBtn.SetBase(session.RemoveArmed > 0.0 ? UCol("button_cancel") : UCol("button_normal"));
            }
        }

        public void Update(float dt)
        {
            if (!toastHeld && toastTimer >= 0f)
            {
                toastTimer += dt;
                if (toastTimer > toastHold)
                {
                    float p = (toastTimer - toastHold) / Mathf.Max(0.01f, toastFade);
                    toastGroup.alpha = Mathf.Clamp01(1f - p);
                    if (p >= 1f) toastTimer = -1f;
                }
            }
            if (resetArmed > 0f)
            {
                resetArmed -= dt;
                if (resetArmed <= 0f) { resetArmed = 0f; resetText.text = Loc.T("ui.reset"); }
            }
        }
    }
}
