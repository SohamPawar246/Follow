# Follow

A cozy top-down survey of an endless forest, and the young Shiba Inu who finds what you
cannot.

Submission for the **TXG Nagaland Game Jam 2026** · Theme: *Where Nature Leads*

---

## The game

You are surveying a forest with a camera and a field journal. Most of what lives here is
invisible to you — it is behind a thicket, or it heard you coming. The dog works ahead,
picks up a scent, stops over it and barks until you arrive. That bark is the only thing
in the game that tells you where to look.

Photographing something is a short rhythm minigame in the air above it: press the arrows
in the order shown before the bar runs out. A clean run is a field-guide plate; a fumbled
one is a smudge you can keep or throw away.

Underneath that is a day. You need firewood for the fire, water from any pond, and food
from what the dog brings back or what you can fish out yourself. When the light goes the
survey stops — it is too dark to work — and the only things left are the fire and the
tent. Where the dog chooses to lie down when you sleep is the only place the game ever
tells you how much she trusts you.

There is no ending. There is a journal that fills up.

---

## Controls

| Key | Does |
|---|---|
| **W A S D** | Walk |
| **F** | Photograph whatever is in front of you |
| **Arrow keys** *or* **W A S D** | Answer the shot sequence |
| **E** | Whatever you are standing at — build or feed the fire, cast a line, sleep |
| **Q** | Whistle for the dog *(or click the paw badge, top right)* |
| **G** | Share your food with her |
| **R** | Eat a ration |
| **J** *or* **Tab** | Open the field journal |
| **Esc** | Pause and options |

Water is free: stand at any pond and the bar refills itself.

---

## Running it

**Unity 6000.3.10f1** (Unity 6.3). Open the project and press play from
`Assets/Follow/Scenes/Boot.unity` — that is the first scene in the build and it sets up
the persistent systems before handing over to the menu.

Playing from `Game.unity` directly also works and skips straight to the forest, which is
what you want while iterating on gameplay.

### Requirements

Anything that runs URP. It is a low-poly forest with a streaming radius of a few hundred
metres; there is no baked lighting and no large textures.

- 64-bit Windows, macOS or Linux
- A GPU supporting Shader Model 4.5
- ~1 GB disk for the project, well under 250 MB built

### Building

`File → Build Profiles`, Windows target. The four scenes must stay in this order:

```
Boot  →  MainMenu  →  Story  →  Game
```

`Boot` plays the studio logo, so it has to be index 0.

---

## The studio logo

`LogoIntro` looks for a `VideoClip` in `Assets/Follow/Resources/` — preferring one named
`StudioLogo`, and otherwise taking whatever video is in there. Drop a file in and it
plays on launch, letterboxed to its own aspect and skippable with any key. With no video
present it falls back to a drawn title card, so the boot sequence cannot break.

Set `Boot.showLogo = false` to skip it while working on something else.

---

## Editor tooling

Almost none of this project is authored by hand in the inspector. The scenes are built by
menu commands, which means a broken scene is always one command away from being correct
again — and it is why changing a number in code sometimes *isn't* enough (see the warning
below).

| Menu command | Does |
|---|---|
| `Follow/Build Everything` | Rebuilds **all four scenes** from scratch. Destructive. |
| `Follow/Build The World` | Bakes the scatter prefabs and rewires `Game.unity`. The usual one. |
| `Follow/Dress The Menu` | Plants the forest vignette behind the main menu. |
| `Follow/Build The Player` | Rebuilds the player rig from the KayKit model. |
| `Follow/Spawn The Dog` | Rebuilds the dog rig and its animator. |
| `Follow/Reseed Species` | Regenerates the twelve species assets and their controllers. |
| `Follow/Bind Cozy UI Art` | Rebinds the UI sprite atlas and sound bank. |
| `Follow/Fix Asset Imports` | Re-imports the third-party kits with correct settings. |

### A warning about serialized fields

Public fields on components are **serialized into the scene**. Changing a default in C#
does nothing to a component that already exists in `Game.unity` — the saved value wins,
silently. This has bitten this project more than once: the dog's bark interval sat at
`2.2` in the scene for two rounds of "fixes" while the source said otherwise.

Tuning therefore lives in explicit methods that *write* the values —
`WorldBuilder.TuneDayCycle()` and `TuneDog()` — which run as part of
`Follow/Build The World`. **If you change a tuning number in code, run that command**, or
set it on the component directly.

---

## How the world works

**It has no edge.** The forest is a set of pure functions of world position in
`WorldComposer` — height, moisture, canopy density, trail openness — plus landmarks
hashed per 50 m grid cell. Nothing is stored, so nothing can run out. `WorldStreamer`
builds and discards chunks around you as you walk.

**The dog is not scripted.** `DogBrain` is a small state machine — range, scent, point,
fetch, deliver, lead, rest — and every transition is weighted by bond. Nothing about her
is commanded except the whistle, and even that she can answer half-heartedly.

**Audio is synthesised at load.** The asset packs contain UI clicks and RPG foley, no
ambience and no music. The wind, birdsong, crickets, the music-box phrases, the whistle
and the dog's fallback voice are all generated into `AudioClip`s at runtime — cheaper to
make than to store, and it lets the day bed and the night bed be genuinely different
rather than the same file at two volumes.

**Photographs are real renders.** The album stores an actual capture of the actual scene
from a second camera, so a shot at dusk is dark because it was dusk.

### Layout

```
Assets/Follow/
  Scripts/
    Core/        state, scene flow, settings, the boot sequence and logo
    Data/        species assets and the library that queries them
    Dog/         brain, body, voice, scent points
    Game/        photography, fishing, survival, sleep, day cycle, sound
    UI/          HUD, journal, menus, the shot sequence, theme
    World/       composer, streamer, campfire, pickups, wildlife, flora
    Diagnostics/ editor-only probes that drive the real systems and report
  Editor/        the build commands above
  Scenes/        Boot, MainMenu, Story, Game
```

`Diagnostics/` is worth knowing about: those probes play the game rather than unit-test
it — they walk the clock into the small hours, starve the player, whistle the dog and
count her barks, then print what happened. Drop one on an empty GameObject and press
play.

---

## Known gaps

Honest list, roughly by how much they matter.

- **No save/load.** The album — including the texture of every photograph — the day
  count and the bond live in memory only. Quitting loses the run. Settings and the
  tutorial flag are the only things that persist.
- **The mithun renders near-black.** The Quaternius bull has no usable material.
- **Hill Bamboo is mapped to a purple-leaved plant** (`Plant_7_Big`). It scales and
  lights correctly, it is just the wrong plant.
- **The story scene has no art** — a `[ artwork panel ]` placeholder sits behind the
  narration.
- **No gamepad support** in gameplay. Only the logo skip reads one.

---

## Credits

Third-party assets, licences and AI tool disclosure are in **[CREDITS.md](CREDITS.md)**,
which is the file the jam actually requires. Keep it current.
