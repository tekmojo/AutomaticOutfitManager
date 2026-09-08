# 0.4.1 deployment and upload stage — 2026-09-08

The maintainer authorized deployment and staging for a manual game-menu update. The game was closed throughout deployment and junction selection. No upload, publication, game launch or source-control operation was performed.

## Verified candidate and package

- RC branch: `codex/workshop-release-prep`; HEAD `76821a1607128ddec2b635c15d35081380128482`, with the accumulated tracked and untracked candidate changes preserved.
- A fresh build reproduced the previously validated DLL exactly: **B8C1A1DAED38FF2AAC4BEBDA27DDED08A7466BCF60F0B2B0977677C8C5F47894**.
- The [copy preflight](RELEASE-COPY-AUDIT-0.4.1.md) records 3,431 positive checks across 29 suites and 29 negative-control variants. No source changes required another contract run during staging.
- `package-workshop.ps1 -SkipBuild` created a new stage; no previous stage was replaced.
- Exact allowlist: **12 files, 1,031,768 bytes**. All staged files match RC source assets/runtime inputs, and the generated manifest matches every payload file. There are no source files, scripts, PDBs or development artifacts in the mod folder.
- About and DLL versions are **0.4.1**, package ID is `tekmojo.automaticoutfitmanager`, and `PublishedFileId.txt` is **3792731788**. The public item page was read and identified as **Automatic Outfit Manager**, creator **tekmojo**. Its existing identity was preserved.
- Preview and icons retain the validated dimensions and transparency. The 369,895-byte cover remains below the conservative 1,000,000-byte image limit. Gameplay screenshots were not replaced.

## Paths and installed state

Upload mod folder:

`C:/GitHub/AutomaticOutfitManager/work/workshop-staging/0.4.1-2026-09-08/AutomaticOutfitManager`

Manifest and copy-ready fields, outside the runtime folder:

- `C:/GitHub/AutomaticOutfitManager/work/workshop-staging/0.4.1-2026-09-08/AutomaticOutfitManager-0.4.1-SHA256.txt`
- `C:/GitHub/AutomaticOutfitManager/work/workshop-staging/0.4.1-2026-09-08/Workshop-description-0.4.1.txt`
- `C:/GitHub/AutomaticOutfitManager/work/workshop-staging/0.4.1-2026-09-08/Change-notes-0.4.1.txt`

The live repository DLL was deployed first and verified against the candidate. The installed junction at `F:/Steam/steamapps/common/RimWorld/Mods/AutomaticOutfitManager` was then changed from `C:/GitHub/AutomaticOutfitManager` to the upload mod folder above. **Every payload file was hashed through the installed path and matched the stage**, including About, published ID, preview and DLL.

Only the validated junction entry was replaced. Both repositories and their existing source changes remain intact. The live checkout's metadata was not synchronized: the uploader now reads the complete staged metadata instead. Routine DLL-deployment helpers must not run against this temporary staging junction.

## Maintainer upload session

1. Launch RimWorld manually. In the mod menu, confirm Automatic Outfit Manager shows **0.4.1**, the normal cover and the short gameplay description.
2. Update the existing Workshop item **3792731788**. The prepared description and full change note are separate copy fields; uploading the package is not evidence that both public text fields were updated.
3. After the upload session, close RimWorld before restoring the installed junction to the live repository. Verify its DLL still matches the release hash. Do not change a junction while the game is running.
4. Record the uploaded result and verify the public item and downloaded subscriber files separately. Steam's subscriber cache can lag behind a successful public update.

Focused gameplay checks in the [release checklist](../RELEASE-CHECKLIST.md), the new gallery, UI/load confirmation and subscriber-package verification remain pending. Staging is not recorded as a pass for them.

## Local recovery evidence

`C:/GitHub/AutomaticOutfitManager/work/release-prep/2026-09-08-0.4.1-staging/` contains the prior live DLL, preserved published ID, fresh build log, before-upload public page, package inventory, verified installed metadata and original junction target. The old live DLL hash was `09BB39D2D970EB399D46D247050A74F8CFE2AE455A19630C59FD8EEC7ED94905`.
