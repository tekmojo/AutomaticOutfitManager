# 0.4.2 Workshop upload preparation — 2026-09-08

The maintainer authorized deployment and preparation for updating the existing mod through RimWorld's menu. The candidate runtime was deployed to the live checkout while RimWorld was closed, then the installed junction was temporarily selected onto the audited package for the upload session.

- RC, live and installed DLL SHA-256: `EB08621418BA99D2DF42ABCF345DF094E65D75D04927DF317BBF0B11B8C10C6C`.
- Package: **12 files, 1,035,376 bytes**. All staged paths and hashes match the authoritative RC inputs; all installed paths and hashes match the stage.
- Installed target: `C:/GitHub/AutomaticOutfitManager/work/releases/0.4.2-upload/AutomaticOutfitManager`.
- Parsed installed identity: Automatic Outfit Manager, version **0.4.2**, package `tekmojo.automaticoutfitmanager`, Workshop ID **3792731788**. Preview, icon, About, Defs, license/notices and DLL are verified through the installed link.
- Steam public API confirmed the existing title and creator tekmojo's Steam ID `76561197960452376`, public visibility, and the prior 0.4.1 content handle `2399755163012728719`. This is a pre-upload baseline, not 0.4.2 publication evidence.
- The original PublishedFileId is backed up. The source/package hash record covers 117 inputs; source remains uncommitted. Full Workshop description and separate change note were extracted from the reviewed draft into copy-ready text files outside the runtime package.

No game launch, upload, public/subscriber verification, GitHub release or source-control operation was performed. The user launches through Steam and checks the staged About card before updating. The [focused gameplay cases](../RELEASE-CHECKLIST.md) remain unverified; preparation does not turn them into passes.

After upload, verify the public description, full note, update time/content handle and package byte count. Then verify Steam's refreshed subscriber file set and hashes separately. When RimWorld is closed, restore the normal development junction and verify its deployed DLL. The restoration helper is prepared but not executed.

Local artifacts: `C:/GitHub/AutomaticOutfitManager/work/releases/0.4.2-upload/` contains the stage, manifest, public baseline, installed verification, PublishedFileId backup, copy-ready text, instructions and junction restoration helper. None of the sibling evidence/scripts/text files is inside the uploaded mod folder.
