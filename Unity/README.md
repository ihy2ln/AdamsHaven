# AI.Game — Unity (6000.5.7f1)

Unity project for the full game. Editor path: `S:\AI\Unity\UnityEditors\Editor\6000.5.7f1\Editor\Unity.exe`.

## Open

```bat
"S:\AI\Unity\UnityEditors\Editor\6000.5.7f1\Editor\Unity.exe" -projectPath "S:\AI\Game\AI.Game\Unity"
```

Or double-click / Hub → open `AI.Game\Unity`.

## Farm starter (Play Mode)

1. Open scene `Assets/Scenes/Farm.unity` (auto-created on first import; menu **AI.Game → Farm → Create Starter Scene** if missing).
2. Press **Play**.
3. `FarmBootstrap` builds the 16×16 top-down authored clearing at runtime.
4. Press **B** or **ENTER BATTLE** to travel to `Battle.unity`; the Battle camp's
   **Leave dungeon** route returns to the farm.

### Controls

| Input | Action |
|---|---|
| WASD / Arrows | Move |
| E / Space / Enter | Clear obstacle |
| Click tile | Step / clear |
| On-screen pad | Mobile-friendly move + CLR |

Obstacles require farm level (weed 1 → boulder 4). Clearing grants XP.

## Layout

```
Assets/Scripts/Data/     Design-package ScriptableObject definitions
Assets/Scripts/Farm/     Runtime farm map / visuals / HUD
Assets/Scripts/Editor/   Scene builder + auto-setup
Assets/Art/Refs/         Brown Dust 2 reference stills
Assets/Scenes/Farm.unity Playable farm scene
```

## Android (Unity)

Android Build Support is present on this editor install. After opening the project:

1. File → Build Settings → Android → Switch Platform  
2. Player Settings → package `com.aigame.farm`, Landscape  
3. Build APK

Or batchmode (once Android SDK/NDK paths are set in Preferences):

```bat
Unity.exe -batchmode -nographics -projectPath "S:\AI\Game\AI.Game\Unity" -buildTarget Android -executeMethod ... -quit
```

The earlier WebView APK under `releases/` remains available for quick sideload; Unity APK is the target production path.
