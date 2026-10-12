# 0.4.6 RimWorld 1.7 compatibility package — October 12, 2026

**Prepared locally; publication pending.** The maintainer accepted current testing and requested proceeding with the compatibility patch. This is a metadata/loading maintenance update, preserving version 0.4.6 and the exact tested binary. No source-code rebuild, live deployment, junction retarget, game launch, upload, commit, tag or push was performed.

## Frozen package

- RC: `C:/GitHub/AutomaticOutfitManager/work/radiation-any-weapon`, branch `codex/workshop-release-prep`, base `b618146`. Existing dirty audit documentation was preserved and incorporated into the archived readiness where applicable.
- Stage: `C:/GitHub/AutomaticOutfitManager/work/workshop-staging-0.4.6-rimworld-1.7-20261012/AutomaticOutfitManager`.
- Files: **13**, total **1,079,086 bytes**.
- DLL SHA-256: `A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE`.
- About SHA-256: `C5111493E9AB7E110BA4A724C88CA39A3CA8DF112F2E5D9D05B0FF7A5283448B`.
- LoadFolders SHA-256: `76AFAAB12939FD0C100D2D030419C420AF17E2BCE31FB5EE5A198DB0F53592FB`.
- Per-file manifest SHA-256: `054C9D2895A7E3355E1B4A4A61B649E899F2181122CD4925ABF19EC24EF76943`.
- ZIP: `AutomaticOutfitManager-0.4.6-RimWorld-1.7.zip`, **633,058 bytes**, SHA-256 `C5D37AC27AD6654AD730E065D78681D03EA19FB9D02AD51B7D31BDD09E3715A2`. The adjacent `.zip.sha256` file identifies it. Both are local artifacts in the stage's parent directory.

Compared with the audited October 2 stage: eleven files are byte-identical, About.xml adds 1.7, and LoadFolders.xml is added. There is no gameplay, asset, Def or save-format change. Workshop ID remains **3792731788**, package ID `tekmojo.automaticoutfitmanager`. No dependency DLL is bundled.

## Verification

- The RC packaging script with `-SkipBuild` creates the stage. Every staged file matches its RC input.
- Parsed About/project/assembly/product versions remain 0.4.6; supported game versions are 1.6 and 1.7.
- Static folder resolution for each supported version selects the root and `1.6` once: one AOM DLL and four definition files, with no duplicate load roots. The mapping follows the installed game's ModUpdating documentation and the same version-node syntax used by Harmony. This is not a live packaged-load test.
- The ZIP contains exactly the stage's thirteen relative paths at its root; all extracted bytes match. No source, PDB, script, audit or extra dependency is packaged.
- XML, preview/icon constraints, source/XML and compiled-string encoding are checked. The encoding scanner reports 121 files and 1,598 compiled strings with no findings.
- All 120 checked Markdown files have valid local file links. Workshop BBCode is balanced; description is 5,872 characters and the separate note is 383 characters. Maintained-text encoding and whitespace checks pass. A negative package control with a duplicated 1.7 load root is rejected as intended.
- The [current installation inventory](RIMWORLD-1.7-CANDIDATE-2026-10-12.json) records the 1.7 layout and Harmony 1.7 path. Missing legacy Unity-named reference DLLs are inventory results, not runtime compatibility failures.

The [readiness record](NEXT-UPDATE-READINESS.md) states accepted gameplay and deferred cases. There is no new gameplay test or runtime build claim. The immutable original v0.4.6 tag and GitHub assets remain unchanged.

## Installation and publication handoff

The installed development junction still targets `C:/GitHub/AutomaticOutfitManager`; its metadata has not received this candidate. RC, live and installed DLL hashes match the tested binary. Do not launch an upload session until the existing junction is deliberately pointed at this audited stage with authorization, while RimWorld is closed, and About/Workshop ID/preview/DLL are compared through the installed path.

Update the existing public item through RimWorld's Mods menu. Keep its identity and visibility, use the [full description](../docs/workshop/DESCRIPTION.txt) and [new separate compatibility note](../docs/workshop/change-notes/0.4.6-rimworld-1.7.txt), and preserve the original October 2 note. Then verify public content and refreshed subscriber files independently. Further gameplay testing is deferred by the maintainer. Any GitHub distribution update needs its own requested operation without moving the original published tag or silently replacing assets.

## Authorized upload-session preparation — October 12, 22:43 UTC

The maintainer explicitly authorized switching the installed mod to the prepared package for a Mods-menu Workshop upload. RimWorld was confirmed closed. The verified junction `F:/Steam/steamapps/common/RimWorld/Mods/AutomaticOutfitManager` was retargeted from `C:/GitHub/AutomaticOutfitManager` to the stage recorded above. Only the junction changed; repository contents and saves were not moved or deleted.

Every one of the thirteen installed files matches the frozen manifest, including About.xml, PublishedFileId.txt, preview, LoadFolders.xml and the unchanged DLL. Parsed title, author, package ID, mod version 0.4.6, support versions 1.6/1.7 and Workshop ID 3792731788 match. Prior junction state and Workshop ID are backed up under `.codex-audit/compatibility-package-20261012/`; `installed-upload-verification.json` records the comparison. The earlier not-installed statement above describes the initial packaging stage and is superseded by this entry.

Manual Steam launch and the existing-item upload are now ready for the maintainer. No game launch, Workshop upload or GitHub operation was performed by the agent. Close RimWorld after upload before restoring the original junction. Public and refreshed-subscriber verification remain pending.

Pre-upload public-page verification also succeeded: existing item 3792731788 is titled Automatic Outfit Manager and lists creator tekmojo. Snapshot: .codex-audit/compatibility-package-20261012/workshop-before-upload.html. This confirms target identity, not publication of the new package.

## Upload and subscriber verification — October 12

The maintainer reports the upload completed. The public page shows the October 12 update and approximately 1.079 MB, and the refreshed Steam manifest is `6503806276562891881` with exact size 1,079,086 bytes. All thirteen subscriber paths and SHA-256 values match the audited stage. The latest public change note is still Steam's auto-generated upload text; the prepared description and note remain pending manual text edits. Edit that existing entry rather than adding a duplicate update.

RimWorld was confirmed closed. The temporary junction was restored to `C:/GitHub/AutomaticOutfitManager` and its original tested DLL hash verified. This restores the existing development checkout; it does not synchronize its source or metadata with the candidate. No package reupload is needed for Workshop text edits. Evidence: `subscriber-after-upload.json`, `workshop-after-upload.html`, `changelog-after-upload.html` and `junction-restored.json` under `.codex-audit/compatibility-package-20261012/`.

## Final public-copy verification

The full Workshop description and existing latest change-note entry `1791845186` now match the prepared copy. Steam-generated HTML contains the expected headings, bold text, quote, lists and links, without literal BBCode. Two automatic `[github.com]` external-link labels are excluded from the text comparison. This is HTML formatting verification, not a browser screenshot. See [closeout](RELEASE-CLOSEOUT-0.4.6-RIMWORLD-1.7.md). Earlier pending states above are historical.
