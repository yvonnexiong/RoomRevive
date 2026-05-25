# Designer Guide Template

This is the template every new subsystem ships with. Place the filled-in copy at:

```
Assets/RoomRevive/<…>/<Feature>/Docs/DESIGNER_GUIDE.md
```

Replace anything in `<angle brackets>`. Keep it scannable — designers skim, they don't read.

For the first real example, see [`Assets/RoomRevive/UI/IntentSelector/Docs/DESIGNER_GUIDE.md`](../RoomRevive_unity/Assets/RoomRevive/UI/IntentSelector/Docs/DESIGNER_GUIDE.md).

---

```markdown
# <Feature Name> — Designer Guide

> Audience: designers, artists, anyone who wants to change how this feature looks or behaves **without writing code.**

## What this feature does

<One paragraph. No jargon. What does the user see / experience?>

## Two ways to edit it

You can change this feature in two ways. Both are valid — pick whichever feels faster for the task.

### Path A — Edit it directly in Unity

For visual tweaks (fonts, colours, layout, sprites), inspector wiring (`UnityEvent` slots), and adding/removing instances of variant prefabs.

1. Open `<MainPrefab.prefab>` in **Prefab Mode** (double-click in the Project window).
2. Make your change.
3. `Ctrl/Cmd-S` to save.

The change is persistent. Commit the `.prefab` file when you're happy.

### Path B — Ask an AI assistant (Codex / Claude)

For changes that are described more easily in words than in clicks: "make every card's subtitle 2pt larger", "add a fourth state called X that triggers Y", "swap the host card to use this new gradient I made".

How to ask:
1. Paste the snapshot file content (see below) into the chat.
2. Describe what you want.
3. The assistant proposes minimal edits.
4. Review, save, commit.

The snapshot for this feature lives at:
```
Assets/RoomRevive/<…>/<Feature>/Snapshots/<Feature>.md
```

Regenerate it whenever you've changed things in Prefab Mode — use the **📸 Export Snapshot** button on `<MainComponent>` in the inspector, or `Tools / RoomRevive / <Feature> / Export Snapshot`.

## Common tasks

### <Task 1 — e.g. Change a card's title>

<Path A steps + Path B prompt example.>

### <Task 2>

…

### <Task 3>

…

## What you should NOT change without a developer

- <Anything that requires script changes.>
- <Anything that would break references in scenes.>
- <Anything where the consequences aren't obvious.>

If in doubt — ask. Or paste the snapshot into an AI chat and describe what you want; it'll tell you whether it's a designer-safe change or needs code.

## Files reference

| File | What it is | Safe to edit by hand? |
|---|---|---|
| `Prefabs/<MainPrefab>.prefab` | The visual hierarchy | ✓ in Prefab Mode |
| `Prefabs/<…>/*.prefab` | Per-variant prefabs | ✓ in Prefab Mode |
| `Data/*.asset` | ScriptableObject data | ✓ in inspector |
| `Snapshots/<Feature>.md` | Auto-generated state dump | Regenerate, don't hand-edit |
| `Scripts/Runtime/*.cs` | Behaviour code | Ask a developer |
| `Scripts/Editor/*.cs` | Editor tooling | Ask a developer |

## When something breaks

- The most likely cause is a missing reference: a slot in the inspector says `None (X)` where it shouldn't.
- Click the **🔗 Sync (non-destructive)** button on `<MainComponent>` first — it re-wires missing references without overwriting your edits.
- Only use **⚠ Rebuild** if a developer or AI explicitly tells you to. It regenerates the prefab from scratch and wipes manual edits.

```
