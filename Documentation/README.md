# Documentation

Project documentation. Two locations:

- `Documentation/` (this folder, project-root) — design specs,
  asset inventories, traceability records. Lives outside `Assets/`
  because it's source-of-truth for humans, not Unity-loaded
  content.
- `Assets/Documentation/` — active reference docs that benefit
  from being inside Unity (so menu items, inspector hints, or
  in-editor links can reference them).

## Active references (this folder)

| Document | Purpose |
|---|---|
| `Player_Animator_Design_2026-04-26.md` | Player Animator + scripts design spec. Original M1 + M2/M3 addendum. |
| `Player_Asset_Inventory_2026-04-26.md` | Clip GUIDs, bone paths, prefab defaults. |

## Historical / archived (`Documentation/Archive/`)

| Document | Captured |
|---|---|
| `Player_M1_Acceptance_2026-04-26.md` | M1 acceptance checklist, signed off. |
| `V1_CLEANUP_AUDIT.md` | V1 retirement audit (2026-04-25). |

## See also

- [`CLAUDE.md`](../CLAUDE.md) — project canon. Living document,
  updated each session.
- [`Assets/Documentation/README.md`](../Assets/Documentation/README.md)
  — Unity-side documentation index.
