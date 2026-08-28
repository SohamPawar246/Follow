# Follow — Credits & Disclosure

Submission for **TXG Nagaland Game Jam 2026** · Theme: *Where Nature Leads*

> Jam rules require every third-party asset and every use of AI tooling to be declared
> in the Technical README. **Add a line here the moment you import anything.**
> Reconstructing this at the deadline is how teams get disqualified.

---

## Engine

| | |
|---|---|
| Engine | Unity 6.3 (6000.3.10f1) |
| Pipeline | Universal Render Pipeline 17.3.0 |
| Input | Unity Input System 1.18.0 |
| Text | TextMeshPro (com.unity.ugui) |

---

## Third-party assets

| Asset | Author | License | Attribution required | Used for |
|---|---|---|---|---|
| Stylized Nature MegaKit | Quaternius | CC0 | No | Forest environment |
| Ultimate Animated Animals | Quaternius | CC0 | No | Survey subjects, the dog |
| Nature Kit | Kenney | CC0 | No | Camp props, terrain dressing |
| UI Pack / UI Pack Adventure | Kenney | CC0 | No | Interface |
| KayKit Adventurers | Kay Lousberg | CC0 | No | Player character |
| _(bird models)_ | Poly Pizza — **per-model** | **CHECK EACH** | Some CC-BY | Birds |
| _(audio)_ | Kenney / OpenGameArt / Freesound | CC0 | No | Ambience, SFX, music |

**Poly Pizza warning:** models re-hosted from the old Google Poly archive are often
CC-BY, not CC0. Record the model name, author and licence for each one you actually use.

### Sources
- https://quaternius.com/packs/stylizednaturemegakit.html
- https://quaternius.com/packs/ultimateanimatedanimals.html
- https://kenney.nl/assets/nature-kit
- https://kenney.nl/assets/ui-pack
- https://kenney.nl/assets/ui-pack-adventure
- https://kaylousberg.itch.io/kaykit-adventurers
- https://poly.pizza/
- https://opengameart.org/ · https://freesound.org/

---

## AI tool usage

| Tool | Used for | Where it appears |
|---|---|---|
| Google Veo 3 | Studio logo intro video | Plays on launch, before the main menu |
| Google Veo 3 | Story panel imagery | The opening story sequence |
| ChatGPT (image model) | Story panel imagery | The opening story sequence |

**Generated content in this build:** the studio logo intro and the story panels are
AI generated. Everything else — 3D models, textures, audio — is either third-party CC0
work listed above or synthesised procedurally in code at runtime.

No AI-generated 3D models are used. No AI-generated audio is used; the ambience, music,
whistle and the dog's fallback voice are written as code that generates AudioClips at
load, which is procedural synthesis rather than a generative model.

These credits are also shown in game, on the **Credits** card reachable from the main
menu. **If you add anything here, add it there too** — see `CreditsPanel.Left()` and `Right()`.

---

## Species reference

Field-guide entries reference real species of Nagaland and the Eastern Himalaya
(Great Hornbill, Blyth's Tragopan, Hoolock Gibbon, Mithun, Clouded Leopard,
Rhododendron arboreum, Vanda coerulea and others). Natural-history details are
drawn from general public reference material; no text is reproduced verbatim.

---

## Build checklist

- [ ] Build under **250 MB** (all textures capped at 1024, compressed)
- [ ] Windows `.exe` **or** WebGL build, playable without login
- [ ] Presentation (max 10 slides) or video (max 2 min)
- [ ] Technical README: engine, install steps, hardware requirements
- [ ] This file complete and accurate
