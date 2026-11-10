# Community World (Unity)

A collaborative village-building game: mobile, isometric 2.5D, 50-player servers where everyone builds their own plot and a shared village, and the village advances through the ages together. This repo is the **Unity port** of the Godot prototype (same rules, data, art and design tokens): isometric ground, building placement, collecting, upgrades, light animations. Single player for now.

Unity 2022.3 LTS, WebGL for phone testing, Android APK later (ads and in-app purchases).

## How it is built

The game creates itself from code at start-up (`Assets/Scripts/View/Bootstrap.cs`), so the scene is empty and there are no prefabs to break.

| Folder | What |
|---|---|
| `Assets/Scripts/Core/` | Game rules. **No UnityEngine** in here, so they are tested with plain mono in CI. `GameState` (production, levels, collect, save), `GameSession` (selection, action menu, placement ghost), `Geometry`, `Loc`, `MiniJson` |
| `Assets/Scripts/View/` | Everything Unity: `GameController` (input, save, frame loop), `WorldView` (tiles, buildings, ghost, outline), `HudView` (uGUI built from tokens), `MarksView` (collect badges), `FxLayer` (animations), `CameraRig` |
| `Assets/Resources/data/buildings.json` | Gameplay data only (resources, costs, production, upgrades, age-up) |
| `Assets/Resources/design/tokens.json` | Design system source of truth (palettes, shading, shapes, UI sizes, motion) |
| `Assets/Resources/locales/en.json` | All player-facing text, by key |
| `Assets/Resources/Art/` | Generated PNGs (do not edit by hand) |
| `art/` | Generated SVG sources of that art |
| `tools/gen_assets.py` | Generates `art/*.svg` and the PNGs from the tokens (`--check` fails CI when the SVGs are stale) |
| `tests/` | Rules tests (`CoreTests.cs`) and Unity API stubs used to compile-check the view code without Unity |
| `Assets/Editor/` | `ArtImporter` (sprite settings from tokens), `BuildScript` (WebGL / Android builds) |

UI sizes are in "design pixels" of a 720x1280 screen (scaled like the prototype's `canvas_items` + `expand`), so every number in the tokens means the same thing as before.

## Controls

| Action | Phone | Desktop |
|---|---|---|
| Place a building | Pick it at the bottom. A ghost appears: drag it, tap a tile or use the four arrows, then the green tick (the red cross cancels) | Same; `Enter` confirms, `Esc` cancels |
| Select a building | Tap it: a menu appears under it (Info, Move, Upgrade, Remove) | Same |
| Collect | Tap the round badge over a building, or **Collect** (top bar) for all | Same |
| Upgrade | Select, tap Upgrade (max level 3). A building cannot pass the level of the Hut | Same |
| Remove | Select, tap the bin twice (50% of everything spent comes back) | Same |
| Pan / zoom | Drag / two fingers | Drag / mouse wheel |

## Run the rules tests locally

```
sudo apt-get install -y mono-mcs mono-runtime      # or any mono install
mcs -out:tests/core_tests.exe Assets/Scripts/Core/*.cs tests/CoreTests.cs && mono tests/core_tests.exe
```

## Play it on a phone: Unity license for CI

CI always runs the rules tests and a compile check. The **WebGL build and Pages deploy** need a Unity Personal license in GitHub secrets (without them that job is skipped, not failed):

1. In this repo: **Actions** -> add a one-off workflow with `game-ci/unity-request-activation-file@v2` (see https://game.ci/docs/github/activation), run it, download the `.alf` artifact.
2. Open https://license.unity3d.com/manual, upload the `.alf`, download the `.ulf`. (Unity no longer officially supports manual activation of Personal licenses; if only "Pro or Plus" is offered, the page's hidden `option-personal` element can be unhidden in the browser dev tools.)
3. **Settings -> Secrets and variables -> Actions**, add `UNITY_LICENSE` (the full text of the `.ulf`), `UNITY_EMAIL` and `UNITY_PASSWORD` (your Unity login).
4. **Settings -> Pages -> Source: GitHub Actions**, then push to `main`. The first build takes 10-20 minutes (later ones are faster thanks to the Library cache).
5. Open `https://<user>.github.io/community-world-unity/` on the phone.

No license, or it fails? Open the project in Unity Hub (2022.3.62f1 or any 2022.3), then **Community World -> Build WebGL** (or Android) from the menu.

## Changing the look

Edit `Assets/Resources/design/tokens.json`, run `python3 tools/gen_assets.py` (needs `rsvg-convert`: `apt install librsvg2-bin`), commit the regenerated `art/` and PNGs.

## Known limits

- Never run inside Unity yet: written without an editor available, checked by compiling against API stubs. Expect a first round of small fixes after the first real build.
- Only the Stone Age is playable; Iron and Middle Age art exists.
- No server, no ads, no purchases yet.
- Language picker and a Turkish file are not built (add `locales/<code>.json` and extend `Loc`).
