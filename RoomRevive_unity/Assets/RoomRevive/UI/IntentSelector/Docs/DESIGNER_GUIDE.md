# Intent Selector — Designer Guide

> Audience: designers, artists, anyone who wants to change how the Intent Selector looks or behaves **without writing code.**

## What it does

The Intent Selector is the floating world-space panel that lets the user pick a room mood — **Calm & Unwind**, **Host & Gather**, or **Fast & Focused**. Each option is a card. Picking a card triggers a room transformation (splat swap), changes the music, and toggles which UI elements are visible.

There can be any number of cards. Adding more is a designer-safe operation — no code needed.

---

## Two ways to edit it

### Path A — Edit it in Unity (Prefab Mode)

Best for: visual tweaks (fonts, colours, sprites, layout), inspector wiring (drag-and-drop side effects), reordering cards, adding/removing cards.

1. In the Project window, navigate to `Assets/RoomRevive/UI/IntentSelector/Prefabs/`.
2. Double-click **`IntentSelectorUI.prefab`** to open Prefab Mode.
3. To edit a single card, expand `ViewRoot → CardsContainer` and double-click `Card_00_calm`, `Card_01_host`, or `Card_02_fast` — each opens its own prefab (`Card_Calm.prefab`, `Card_Host.prefab`, `Card_Fast.prefab` under `Prefabs/Cards/`).
4. Make your change. `Ctrl/Cmd-S` to save.

Commit the changed `.prefab` files.

### Path B — Ask an AI assistant (Codex / Claude)

Best for: changes that are easier to describe than to click ("make every card subtitle pink", "add a fourth state for Focused Cooking with this gradient"), or when you want a coordinated change across multiple files.

How to ask, in order:

1. **Regenerate the snapshot** so the AI sees the current state, not the original defaults.
   - Inspector route: click **📸 Export Snapshot** on the `IntentSelectorController` component.
   - Menu route: `Tools → RoomRevive → Intent Selector → Export Snapshot`.
2. Open `Assets/RoomRevive/UI/IntentSelector/Snapshots/IntentSelectorUI.md` and paste its contents into the chat.
3. Describe what you want in plain language.
4. The assistant follows [`docs/AI_EDIT_PROTOCOL.md`](../../../../../docs/AI_EDIT_PROTOCOL.md): read snapshot → diff goal → minimal edit. It should preserve any prefab tweaks you made in Path A.
5. Save and commit.

---

## Common tasks

### Change a card's title or subtitle

**Path A:** open `Assets/RoomRevive/UI/IntentSelector/Data/IntentState_Calm.asset` (or Host/Fast) in the inspector. Edit `Display Name` or `Subtitle`. Save.

The card prefab also has the text baked in for visibility in Prefab Mode, but at runtime the ScriptableObject's value wins.

**Path B:** "Change the Calm state's subtitle to 'A moment of stillness.'"

---

### Change a card's image (the gradient/photo on the upper part of the card)

**Path A:** in `IntentState_Calm.asset`, drag a new `Sprite` into the `Card Image` slot.

**Path B:** "Use the sprite at `Assets/<…>/MyNewCalmImage.png` for the Calm card."

---

### Change card colours, fonts, or layout

**Path A:** open the card prefab (`Card_Calm.prefab` etc.) in Prefab Mode. Select the relevant text/image child. Change colour, font, font size, position, sprite. Save.

**Path B:** "On all three cards, set the title font size to 22 and the subtitle to italic." (Be specific — the AI will edit the prefab YAML minimally.)

---

### Add a fourth intent state (e.g. "Focused Cooking")

**Path A — fully manual:**

1. **Create the data asset.** Right-click in `Assets/RoomRevive/UI/IntentSelector/Data/` → `Create / RoomRevive / Intent Selector / Intent State`. Name it `IntentState_FocusedCooking.asset`. Fill in:
   - `Id`: `focused-cooking`
   - `Display Name`: `Focused Cooking`
   - `Subtitle`: whatever fits
   - `Card Image`: drag a sprite
   - `Atmosphere Music`: optional AudioClip
   - Visibility flags as needed.
2. **Create the card prefab.** Duplicate `Prefabs/Cards/Card_Host.prefab` → rename `Card_FocusedCooking.prefab`. Open it in Prefab Mode if you want to restyle.
3. **Wire data → prefab.** On `IntentState_FocusedCooking.asset`, drag `Card_FocusedCooking.prefab` into the `Card Prefab` slot.
4. **Add to the catalog.** Open `Data/IntentStateCatalog.asset`. Add the new state to the `States` list. Position determines order in the row.
5. **Place the card in the UI.** Open `IntentSelectorUI.prefab` → `CardsContainer`. Drag `Card_FocusedCooking.prefab` into the container as a child. On the parent `IntentSelectorView` component, add the new card to the `Card Views` list.
6. **Wire the scene action.** On `IntentSelectorUI.prefab`'s `IntentUnityEventRouter` component, add a binding:
   - `State`: drag `IntentState_FocusedCooking.asset`.
   - `On Selected (UnityEvent)`: drag in your `SplatManager` and pick the method to call (or any other scene action).

That's it. Hit play — the new card shows up in the row and triggers your wired action when selected.

**Path B:** "Add a new intent state called Focused Cooking. Reuse the host card visual for now. When selected, it should call `SplatManager.SetHostKitchenNewCabinetA()`." — the AI will create the SO, duplicate the prefab, update the catalog, and add the router binding.

---

### Make a card trigger a different scene action

**Path A:** open `IntentSelectorUI.prefab` → select the root → find the `IntentUnityEventRouter` component → find the binding for the state you want → drag your target object (e.g. `SplatManager`) into the `On Selected` UnityEvent slot → pick the method from the dropdown.

This is the *most common* designer change. No code, no AI, just inspector drag-and-drop.

**Path B:** "When the Host state is selected, also call `MyLightingController.SetWarmMode()`."

---

### Reorder the cards

**Path A:** open `Data/IntentStateCatalog.asset`. Drag entries in the `States` list to reorder. Then in `IntentSelectorUI.prefab`'s `CardsContainer`, drag the corresponding card GameObjects into the same order. Also reorder them in `IntentSelectorView.cardViews` to match.

**Path B:** "Reorder cards to Fast / Calm / Host."

---

### Change keyboard shortcuts

**Path A:** on `IntentSelectorController`, find the `Keyboard Debug` section. Change `Previous Key`, `Next Key`, `Confirm Key` to whatever `KeyCode` you want.

**Path B:** "Make the Esc key confirm selection instead of Enter."

---

## What you should NOT change without a developer

- **Anything inside `Scripts/Runtime/` or `Scripts/Editor/`.** That's code.
- **The `IntentSelectorController.View` reference**, or any wiring between Controller / View / Routers — these are infrastructure. The 🔗 Sync button restores them if they break.
- **Component names in the prefab hierarchy** (`ViewRoot`, `HeaderPill`, `CardsContainer`, `CardTemplate`, `Visual`, `ImageArea`, `LabelArea`, `IconContent`, `TitleText`, `SubtitleText`, `StateOverlay`). The editor binder finds children by these names — renaming breaks Sync.
- **The Meta SDK Ray surface** (`ISDK_RayInteractionSurface` and its children). Touching this can break hand-tracking ray input.

When in doubt — paste the snapshot into an AI chat and describe what you want. The AI will tell you whether it's designer-safe or needs a developer.

---

## Files reference

| File | What it is | Safe to edit by hand? |
|---|---|---|
| `Prefabs/IntentSelectorUI.prefab` | The main panel | ✓ in Prefab Mode |
| `Prefabs/Cards/Card_*.prefab` | Per-state card visuals | ✓ in Prefab Mode |
| `Data/IntentStateCatalog.asset` | Ordered list of states | ✓ in inspector |
| `Data/IntentState_*.asset` | Per-state content (title, subtitle, image, music) | ✓ in inspector |
| `Data/IntentSelectorTheme.asset` | Scale + overlay animation settings | ✓ in inspector |
| `GeneratedSprites/*.png` | Baked rounded mask + gradients | Regenerate via 🔗 Sync, don't hand-paint over |
| `Snapshots/IntentSelectorUI.md` | Auto-generated state dump | Regenerate via 📸 Snapshot, don't hand-edit |
| `Scripts/Runtime/*.cs` | Behaviour code | Ask a developer / AI |
| `Scripts/Editor/*.cs` | Editor tooling | Ask a developer / AI |

---

## When something breaks

- The most likely cause is a missing reference: a slot in the inspector says `None (X)` where it shouldn't.
- Click **🔗 Sync (non-destructive)** on the `IntentSelectorController`. It re-wires missing references without overwriting your edits.
- If a card stopped showing up: check `IntentSelectorView.cardViews` — the list should have one entry per card, none of them `None`.
- If a state stopped triggering its action: check `IntentUnityEventRouter.bindings` — find the row for that state, verify the `On Selected` UnityEvent is wired.
- **Only use ⚠ Rebuild** if a developer or AI explicitly tells you to. It regenerates the prefab from scratch and **wipes manual edits.**

---

## Recipe: paste this into an AI chat for a new task

```
I want to change something in the Intent Selector.

Read these first:
1. Assets/RoomRevive/UI/IntentSelector/Snapshots/IntentSelectorUI.md (current state)
2. Assets/RoomRevive/UI/IntentSelector/Docs/DESIGNER_GUIDE.md (this file)
3. docs/AI_EDIT_PROTOCOL.md (read-first, edit-minimally rule)

What I want to do:
<describe in plain words>

Default to Sync over Rebuild. Preserve any manual prefab edits.
```
