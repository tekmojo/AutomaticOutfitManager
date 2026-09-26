# 0.4.3 maintenance deployment — 2026-09-25

The maintainer requested deployment so the existing Workshop item can be updated through RimWorld's Mods menu. RimWorld was closed throughout deployment and upload-source verification. Version remains 0.4.3.

## Verified state

- Candidate DLL deployed to the live repository with the deployment helper; candidate/live/installed hashes matched before changing the junction.
- Package created with `package-workshop.ps1 -SkipBuild` from the previously built and checked candidate. New output root: `work/workshop-staging-0.4.3-maintenance-20260925` under the RC worktree.
- Stage contains exactly 12 runtime files totaling **1,052,784 bytes**. Every staged file matches its RC source. XML, version, preview/icon and full compiled-string checks pass. No source, scripts, PDBs, audit logs or other development files are in the upload package.
- Existing public item confirmed as **Automatic Outfit Manager** by **tekmojo**, ID **3792731788**, via the public page. PublishedFileId is preserved in the stage.
- Installed `F:/Steam/steamapps/common/RimWorld/Mods/AutomaticOutfitManager` is now a junction to `C:/GitHub/AutomaticOutfitManager/work/radiation-any-weapon/work/workshop-staging-0.4.3-maintenance-20260925/AutomaticOutfitManager`.
- All 12 installed file hashes match the stage. The installed About card data has title Automatic Outfit Manager, package ID tekmojo.automaticoutfitmanager, version 0.4.3 and the restored concise description.
- Candidate/live/staged/installed DLL SHA-256: `4CA32704045B015AB50A6FA20AFA236A72E717E90DDF812F785830CB1A0E9B32`.

The first retarget attempt stopped at its read-only target check because PowerShell returned Target as a scalar string rather than an array. It made no changes. After correcting that check, the verified junction alone was replaced nonrecursively; neither repository was moved or deleted. Final installation verification passed at 11:03:44 America/Denver.

## User handoff

Launch manually through Steam. Verify the concise About card and normal-scale Task Buffer minus/plus and selected-outfit tooltip, then use the Mods menu to update the existing item. The prepared maintenance note is in [Workshop copy](../docs/workshop/README.md); a paste-ready text file is saved at `C:/GitHub/AutomaticOutfitManager/.codex-audit/maintenance-0.4.3-2026-09-25/Workshop-maintenance-note.txt`.

No game launch, in-game visual smoke, upload, public metadata edit, subscriber refresh, commit, tag or GitHub asset update is claimed. Keep the active stage frozen. After upload, verify public and subscriber results separately, close RimWorld, then restore the installed junction to `C:/GitHub/AutomaticOutfitManager` and verify the deployed DLL through that path.

Evidence folder: `C:/GitHub/AutomaticOutfitManager/.codex-audit/maintenance-0.4.3-2026-09-25`. It contains the stage manifest, before/after junction record, all-file upload-source verification and pre-upload Workshop page. The package hash manifest is outside the staged mod folder.

## Post-upload verification — September 25, 2026

The maintainer uploaded through the Mods menu and updated the separate change note. The public [Workshop item](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788) and [change note](https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788) were verified at approximately 11:16 America/Denver. Steam records upload timestamp 1790356266 (17:11:06 UTC / 11:11:06 America/Denver), manifest 2204596392603111389, and 1,052,784 bytes. Version remains 0.4.3.

All 12 subscriber-cache files match the frozen stage by relative path, size and SHA-256. The downloaded DLL hash is 4CA32704045B015AB50A6FA20AFA236A72E717E90DDF812F785830CB1A0E9B32. Evidence: subscriber-after-upload.json and Workshop-after-upload.html / Workshop-changelog-after-upload.html in the deployment evidence folder.

The screenshot confirmed the concise About description, title, preview and version. No additional Task Buffer/tooltip or downloaded-copy gameplay smoke is claimed. With RimWorld confirmed closed, the temporary installed junction was restored to C:/GitHub/AutomaticOutfitManager at 11:17 and its DLL hash reverified. Evidence: junction-restored.json. The stage remains frozen. No commit, tag or GitHub asset changes were made.
