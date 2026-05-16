# RoomRevive

RoomRevive is an XR prototype that transforms a user's real kitchen into different lifestyle experiences.

Instead of showing different kitchens, it shows different ways of living in the same kitchen.

Core idea:
"This isn’t just your room—this is how you want to live."

## Intents
- Calm & Unwind
- Host & Gather
- Fast & Focused

## How it works
1. Load or reconstruct room
2. Select intent
3. Apply transformation
4. Interact with hotspots
5. Explore products

See /docs for full design + technical details.

## Structure

```
RoomRevive/
├── RoomRevive_unity/   # Unity project
├── Resources/          # Supporting assets and resources
└── docs/               # Project documentation
```

---

## Version-control & AI-editing setup

The project is configured so that prefabs, scenes, and ScriptableObjects can be diffed, merged, and (carefully) edited by both humans and AI agents.

### Editor settings (already configured)

- **Asset serialization: Force Text** — set in `RoomRevive_unity/ProjectSettings/EditorSettings.asset` (`m_SerializationMode: 2`). All `.prefab`, `.unity`, and `.asset` files are written as YAML.
- **Visible meta files** — every asset has a sibling `.meta` checked into git. (In modern Unity this is the default and the legacy `m_ExternalVersionControl` field has been removed.)
- **Line endings** — repo-wide settings are inherited from `.gitattributes` / `.gitignore`; do not change them per-script.

If you ever clone this repo on a new machine, run `git config core.autocrlf input` on Windows / `input` on macOS/Linux to avoid spurious line-ending diffs.

### UnityYAMLMerge (Smart Merge for `.prefab` / `.unity` / `.asset`)

Unity ships a YAML-aware three-way merge tool at:

- **Windows:** `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\Tools\UnityYAMLMerge.exe`
- **macOS:** `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Tools/UnityYAMLMerge`
- **Linux:** `~/Unity/Hub/Editor/<version>/Editor/Data/Tools/UnityYAMLMerge`

Configure git to use it for Unity YAML files (run once per machine):

```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver \
  '"<path-to-UnityYAMLMerge>" merge -p --force --fallback none %O %B %A %A'
```

Then in this repo, ensure `.gitattributes` routes Unity files to that driver:

```
*.prefab    merge=unityyamlmerge eol=lf
*.unity     merge=unityyamlmerge eol=lf
*.asset     merge=unityyamlmerge eol=lf
*.controller merge=unityyamlmerge eol=lf
*.mat        merge=unityyamlmerge eol=lf
*.anim       merge=unityyamlmerge eol=lf
```

Verify with `git check-attr merge -- Assets/SomeScene.unity` — should print `merge: unityyamlmerge`.

### Rules for editing Unity-serialized YAML (humans and AI agents)

1. **Do not hand-rewrite large prefab YAML.** Every component, GameObject, and asset has a `fileID` + `guid` that other objects reference; one typo orphans a reference and the prefab loads with Missing Script / `None` fields.
2. **Prefer these editing surfaces, in order:**
   1. ScriptableObject `.asset` fields (small, well-structured YAML — safe to edit by hand).
   2. JSON config files / UXML / USS.
   3. C# editor generation scripts (`AssetDatabase`, `PrefabUtility`, `TextureImporter`). See `Assets/RoomRevive/UI/IntentSelector/Scripts/Editor/` for examples — these run via `Tools/RoomRevive/...` menus and via inspector buttons.
   4. Hand edits to `.prefab` / `.unity` YAML — last resort, kept minimal.
3. **Keep prefab YAML changes minimal** when they're unavoidable. Patch one field at a time, preserve line ordering, do not reorder components, do not regenerate fileIDs.
4. **Always validate by opening the prefab in Unity** after a YAML edit. A clean reopen with no Missing Script warnings and intact references is the only proof the patch is good.
5. **Generated assets** (rounded sprite masks, gradient PNGs, prefab structure) live under `Assets/RoomRevive/UI/IntentSelector/`. Re-run the corresponding editor menu rather than editing the generated files by hand.




