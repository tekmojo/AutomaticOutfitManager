# 0.4.5 Workshop upload preparation — 2026-09-27

Prepared and installed for the maintainer's existing-item update through RimWorld's Mods menu. No upload, game launch, commit, tag, push or GitHub publication occurred.

## Frozen package

- RC branch `codex/workshop-release-prep`, base `9ff91a34fa047f582e5c9a9f12f0c51c4dfab2ae`, with all intentional tracked/untracked work preserved.
- DLL SHA-256: `975BBCDB92F13BB758CA80B27C353103871D78A038CAD28B6396E8C4038D14CF`.
- Stage: `C:/GitHub/AutomaticOutfitManager/work/workshop-staging-0.4.5-upload-20260927/AutomaticOutfitManager`.
- Manifest SHA-256: `9818513200BC42A07190E1EAD0BCA7306C8759DAE1FAAC4196240B73608B092D`.
- Twelve allowlisted files, **1,069,682 bytes**. All staged files match their RC inputs; all installed paths, sizes and hashes match the stage. No source, scripts, PDBs, audit or work files enter the package.
- Installed junction: `F:/Steam/steamapps/common/RimWorld/Mods/AutomaticOutfitManager`, now targeting the stage above. Previous target: `C:/GitHub/AutomaticOutfitManager`.
- The live DLL was updated and verified before retargeting. Live source and metadata were not synchronized. RimWorld was closed throughout; it was not launched.
- Existing Workshop ID `3792731788` preserved in RC, backup, stage and installed files. Public page title Automatic Outfit Manager, creator tekmojo and Harmony dependency were checked before installation.

## Copy, skills and validation

- Version 0.4.5 agrees across About, project, assembly and packaging expectations. The version bump is the only runtime source change made during release preparation; gameplay logic is the same as tested DLL `35E9D0097762005F37709C6EE41827CD1C09F58E56613880E91138B19E50D8D4`.
- Prepared the four-fix changelog, compact Workshop update block, separate upload note, player-guide borrowed/retained explanation, technical design, readiness and release checklist. Archived 0.4.4 release/readiness/Workshop copy before replacement and updated indexes.
- Reviewed existing tooltips, inspect labels, action/storage descriptions and Def text against source. No tooltip or gameplay edits were necessary. About text remains concise and feature-focused. Existing preview/icons retained; screenshot guide explicitly labels the older gallery as legacy.
- Build passed. Six suites passed **592 checks**: floor 42, installation 24, gear tracking 33, preparation 134, boundary 47, saved-gear recovery 312. Three old-decision negative controls reproduce the intended failures.
- Encoding scan: 119 source/XML/project files, 1,590 compiled user strings, zero findings. Maintained Markdown/Workshop text also passes. Six XML/project files parse. Local documentation links pass; current full Workshop description is 5,399 characters. No translations, matching English-only copy.
- Preview 640x360 and 369,895 bytes; About/main-button icons both 64x64 with transparent backgrounds. Whitespace diff check passes.
- Updated the AOM session-audit skill and debug-fix invariants with sampled buffer evidence, borrowed-ID lifetime, original blueprint/native-scanner continuity and dropped-installation count constraints. Exact before-edit skill bytes are backed up. Append-only preservation, frontmatter and links were checked. The standard skill validator could not run because its PyYAML dependency is unavailable; no dependencies were installed.

## Maintainer upload

Launch RimWorld manually and select Automatic Outfit Manager in Mods. Confirm version 0.4.5, expected preview and concise description before updating the existing item. Use [full description](../docs/workshop/DESCRIPTION.txt) and [separate change note](../docs/workshop/change-notes/0.4.5.txt) in their respective fields.

After upload, close RimWorld before restoring the junction to the live repository. Preserve this stage for public/subscriber verification. Do not run the ordinary DLL-deploy helper while the installed junction targets this upload stage.

## Remaining evidence

The new version label still needs its normal in-game load/About smoke. The latest pre-version-bump floor recording proves all three prepared floor jobs reach native RemoveFloor and visible construction progresses. The small-map regression proves healthy gear returns and meal continuations. Fresh save/reload during floor preparation and the exact dropped-installation interruption remain untested in game. Public upload, subscriber refresh, subscriber-only gameplay and source-control/GitHub closeout are not complete; see [readiness](NEXT-UPDATE-READINESS.md).

Local evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/upload-prep-0.4.5` contains the public-page snapshot, preserved Workshop ID/previous live DLL, skill backups, encoding/static reports and per-file staged/installed hashes.
