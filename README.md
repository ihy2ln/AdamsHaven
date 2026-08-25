# Adams Haven

Anime lane-tactics RPG with farming, town building, and gacha.

**Repo:** https://github.com/ihy2ln/AdamsHaven
**Unity Editor:** `S:\AI\Game Engine\Unity\UnityEditors\Editor\6000.5.7f1` (Unity 6)

## Layout

| Path | Purpose |
|---|---|
| `Unity/` | **Primary game project** (Unity 6000.5.7f1) |
| `Unity/Assets/Scripts/Farm/` | 16×16 top-down farm simulation, presentation, integration contracts, and saves |
| `Unity/Assets/Scripts/Data/` | Design-package data layer |
| `Unity/Assets/Art/` | Aesthetics/camera/map design doc + reference art |
| `sections/farm/` | Web prototype (reference / quick preview) |
| `android/` + `releases/` | Earlier WebView APK (sideload) |
| `AI.Game Commits/<section>/` | Per-section snapshots |

## Open Unity

```bat
S:\AI\Game\test\AI.Game\Unity\OpenUnity.bat
```

Play `Assets/Scenes/Farm.unity`.

## Farm foundation

Stardew × Rune Factory clearing, tools, seeds, water, fertilizer, real-time/battle-count growth, harvesting, inventory, and local saving on a **16×16 top-down** field. Town/dungeon crop legality and Market/Kitchen transfer boundaries are in place. See [`FARM-FOUNDATION.md`](FARM-FOUNDATION.md).

The contained farm workstream is tracked separately in [`FARM-MILESTONES.md`](FARM-MILESTONES.md). Farm milestones use `F0`–`F10`; battle milestones use the separate `M` series.

## Roadmap

1. ~~Web 4×4 farm + APK~~
2. ~~Unity project + farm scene~~
3. ~~Crop/tool/fertilizer/time-and-battle growth foundation~~
4. Shared battle/farm inventory and Market/Kitchen verbs
5. Larger overgrown and dungeon farm maps
6. Cross-save transport and Android integration
7. Combat/town integration — in progress on `feature/battle-slice`, see [`PROJECT-README.md`](PROJECT-README.md)
