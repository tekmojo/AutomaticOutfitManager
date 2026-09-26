# 0.4.4 Workshop upload preparation — 2026-09-26

Prepared and installed for the maintainer's existing-item upload through RimWorld's Mods menu. Publication has not occurred. RimWorld was closed throughout build and installation and was not launched.

## Frozen package identity

- RC branch: `codex/workshop-release-prep`; base `61393889a6e85c10c99083283ca59c9c4517e479` plus the preserved uncommitted repair/nonhuman-routing changes.
- DLL SHA-256: `ED72D9E73C80344960E1090FE3E46BD20973FB4D5B7E733B500A013974784609`. A fresh build reproduces the exact DLL used in the normal-play and psychic-ritual recordings. No gameplay or interface-source changes were needed during this preparation.
- Stage: `C:\GitHub\AutomaticOutfitManager\work\workshop-staging-0.4.4-upload-20260926\AutomaticOutfitManager`.
- Installed junction: `F:\Steam\steamapps\common\RimWorld\Mods\AutomaticOutfitManager`; previous target `C:\GitHub\AutomaticOutfitManager`.
- Twelve package files, **1,064,050 bytes** total. Every installed file matches both stage and corresponding RC input.
- Manifest SHA-256: `2CF6CBAC09653329FE79BA2188BC09A13FB0082DFB3F901D8061AE3423E715D9`. Manifest is adjacent to the stage, outside the uploaded mod directory.
- Workshop item `3792731788`: public title Automatic Outfit Manager and creator tekmojo checked before preparation. ID preserved in source, backup, stage and installed path.

## Copy and preflight

- Corrected README/changelog mojibake without rewriting historical claims. Clarified that humanlike robots retain outfit requirements while permitted nonhuman robots/mechs/animals avoid outfit-only detours in both area kinds.
- Kept the About/loading description concise and feature-focused. The compact Workshop latest-update block and separate change note cover both repairs and routing.
- Reviewed tooltips, Def descriptions, versions and changed gameplay source; no new tooltip or runtime change was necessary. No debug-only runtime additions are part of this preparation.
- Six XML/project files parse; 58 local links in eight maintained documents passed before adding this record. Version 0.4.4 agrees across About, project, assembly and package expectations. Harmony and RimWorld 1.6 identities are retained; no translation directories, matching English-only copy.
- Encoding check: 115 source/XML/project files and 1,583 compiled user strings; zero findings. Maintained documentation and Workshop text also pass the encoding scan. Diff whitespace check passes.
- Preview 640×360, 369,895 bytes; transparent 64×64 About and main-button icons. Package includes only allowlisted runtime/XML/assets/legal files, with no source, scripts or PDBs.
- Previously passing 1,032 automated checks plus expected-failing old-routing control remain applicable to the unchanged DLL. No redundant gameplay suite rerun was needed for documentation-only edits.
- Updated session-audit and release-copy-audit skills with psychic-ritual evidence boundaries, sidearm-wait attribution, nonhuman wording and documentation encoding checks. Exact backups retained. Standard skill validator could not run because PyYAML is absent; preserved frontmatter and append-only changes were checked directly.

## Maintainer handoff

Launch RimWorld manually. In Mods, select Automatic Outfit Manager and verify the 0.4.4 About card, expected preview and concise description, then update the existing Workshop item. Use [the separate change note](../WORKSHOP-CHANGE-NOTE-0.4.4.txt); [the full description](../WORKSHOP-DESCRIPTION.txt) is a separate field. Do not replace the concise About text with the change note.

The live repository's About metadata still reflects its separate published checkout; the installed path deliberately points at the verified 0.4.4 stage during this upload session. After upload and game closure, restore the junction to the live repository through the release workflow. Preserve the stage for public/subscriber verification.

Controlled mech/animal routing modes, denied access/overlaps, contested repair and fresh save/reload remain pending, as recorded in [readiness](NEXT-UPDATE-READINESS.md). Normal-play and five-adult Void provocation completion/restoration have been observed. Background sidearm/idle, Arakis and other-mod reference concerns are not claimed fixed. Public update, subscriber refresh, downloaded-copy smoke and source-control release closeout remain separate unperformed steps.

Local evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/upload-prep-0.4.4` (before-copy backups, skill backups, encoding/static reports, public page and per-file installed hashes).
