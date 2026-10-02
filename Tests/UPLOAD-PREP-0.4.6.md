# 0.4.6 Workshop upload preparation — 2026-10-02

Prepared and installed for the maintainer's update through RimWorld's Mods menu. No game launch, upload, commit, tag, push or GitHub release was performed.

## Frozen package and deployment

- RC branch: `codex/workshop-release-prep`, base `e6bb309ff541dd82b2409ae4b62954aef9fe9339`. All accumulated tracked and untracked work is preserved.
- DLL SHA-256: `A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE`.
- Stage: `C:/GitHub/AutomaticOutfitManager/work/workshop-staging-0.4.6-upload-20261002/AutomaticOutfitManager`.
- Manifest SHA-256: `A0C2ACFFC433BCCB481A9058922A11CCD12015759DC848417B2E58D522FA5C6D`.
- Twelve allowlisted files, **1,078,898 bytes**. Every staged file matches its RC input, and every installed path, size and hash matches the stage. No source, scripts, PDBs, screenshots or audit files enter the package.
- Installed junction: `F:/Steam/steamapps/common/RimWorld/Mods/AutomaticOutfitManager`, temporarily targeting the stage above. Previous target: `C:/GitHub/AutomaticOutfitManager`.
- Live DLL was deployed and verified before changing the junction. Live source and metadata were not synchronized. RimWorld was closed throughout and was not launched.
- Workshop ID `3792731788` matches the preserved backup, RC, stage and installed copy. The public page was checked for Automatic Outfit Manager, creator tekmojo and Harmony dependency. Existing identity and visibility are preserved; no public content has been changed.

The installed About card and DLL both report 0.4.6. Package ID remains `tekmojo.automaticoutfitmanager`; About text stays concise and feature-focused. The full Workshop description and separate change note are distinct upload fields.

## Changes completed for preparation

- Bumped About, project, assembly and package expectations from 0.4.5 to 0.4.6.
- Aligned the player guide, changelog, design, readiness, release checklist and indexes with saved-gear locality, ordinary personal Wear adoption, retained-item controls and the top-toolbar Saved outfits button.
- Clarified locker storage/floor preference in the tooltip and ordinary clothing updates in the saved-outfit viewer.
- Corrected downed restoration text and detail so missing-item or Non-Work return wording does not override the actual suspension. This changes status display only, including the Non-Work and inspect paths; no job, ownership or restoration decisions changed during release preparation.
- Archived 0.4.5 release/readiness/full Workshop description before preparing new draft copy. Previous published notes and tags remain unchanged.
- Prepared the compact Latest update block and separate Steam BBCode note, preserving the established heading/list format. Full description: 5,883 characters. Kept the existing preview/icons; the separately prepared Anomaly gallery image is optional and predates the new toolbar placement.
- Updated three skill resources with focused lessons about missing-reference evidence, successful ordinary Wear, locker-local storage and type-wide Forget versus exact-item Release. Exact originals are backed up; previous bytes, frontmatter and local reference links are preserved. The standard skill validator requires unavailable PyYAML, so basic structural/preservation checks were used without installing dependencies.

## Verification

Build passed. Seven focused suites passed **1,082 checks**:

| Suite | Checks |
| --- | ---: |
| Personal Wear native probe | 23 |
| Saved locker native probe | 46 |
| Non-Work contracts | 322 |
| Restoration planning/status | 50 |
| Storage contracts | 296 |
| Managed gear tracking | 33 |
| Saved gear recovery | 312 |

Four negative controls produced the expected failure: the Non-Work runner's previous inactive cleanup decision, the pre-fix personal Wear DLL, previous remote-locker selection and the pre-fix pass-through storage DLL. These are bounded fixture/native API checks, not a running-game smoke.

Encoding scan passed for 121 source/XML/project files and 1,598 compiled strings. Six XML/project files parse. Maintained Markdown/Workshop text and local links pass. Steam BBCode tags/list formatting pass. Preview is 640×360, 369,895 bytes; both icons are 64×64 with transparent backgrounds. Whitespace diff check passes. Package assembly product version and parsed installed metadata are verified.

Gameplay evidence carries forward from `74C5192A1AB2B70A3F3886A82261D877BE7CA16A22E7D8869EDA70EBF8A2B6D8`: [full session audit](FULL-SESSION-AUDIT-2026-10-02.md). The changes after that recording are version and display text only. The new downed label and 0.4.6 About card still need visual confirmation in game.

## Maintainer handoff

Launch RimWorld manually and select Automatic Outfit Manager in Mods. Confirm version **0.4.6**, expected preview and concise description before updating existing item **3792731788**. Use [full description](../docs/workshop/DESCRIPTION.txt) and [separate change note](../docs/workshop/change-notes/0.4.6.txt) in their respective fields.

After uploading, close RimWorld before restoring the junction to the live repository. Preserve the stage for public/subscriber comparison. Do not run the ordinary DLL-deploy helper while the junction targets this stage. Public upload, rendered copy, subscriber refresh/gameplay and GitHub closeout remain pending.

The [readiness record](NEXT-UPDATE-READINESS.md) retains the recommended preserved-autosave load check, native missing-pawn relationship warning, earlier Hospitality arrival continuation and inherited manual limits. This update does not claim to fix those findings. Deployment does not certify save/reload.

Local evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/upload-prep-0.4.6/` contains the previous live DLL, Workshop ID backup, public-page snapshot, individual test logs, encoding/static reports, skill backups and installed per-file hashes.
