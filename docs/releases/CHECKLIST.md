# 0.4.6 RimWorld 1.7 compatibility maintenance checklist

Workshop maintenance update completed October 12, 2026. Public description/change note and all thirteen subscriber files are verified. The maintainer requested proceeding and accepted current gameplay testing as sufficient. Remaining focused cases are deferred, not passed. The [original 0.4.6 release](history/RELEASE-HISTORY-0.4.6.md), published tag and assets remain historical evidence.

## Compatibility candidate

- [x] Preserve the exact tested 0.4.6 DLL; change no runtime source, save format or rule defaults.
- [x] Declare RimWorld 1.6 and 1.7 support in About metadata.
- [x] Map both versions to root assets plus the shared `1.6` assembly folder; package only one DLL.
- [x] Archive the completed checklist, readiness and published Workshop description.
- [x] Prepare player documentation, concise Workshop description and separate BBCode change note.
- [x] Record accepted gameplay scope and the maintainer's deferral decision.
- [x] Verify XML, encoding, links, assets and staged package hashes.

## Distribution

- [x] Authorize and prepare the installed junction for the audited package while RimWorld is closed; all thirteen installed files match the stage (October 12, 22:43 UTC).
- [x] Maintainer updates existing Workshop item 3792731788 through RimWorld's Mods menu.
- [x] Verify public metadata and Steam-generated HTML formatting/text for the description and separate change note.
- [x] Compare all thirteen refreshed subscriber files with the audited stage; manifest 6503806276562891881.
- [x] Restore the temporary junction to the live repository while RimWorld is closed and verify the DLL hash.
- [x] Commit/tag/push the compatibility content and publish [verified GitHub downloads](https://github.com/tekmojo/AutomaticOutfitManager/releases/tag/v0.4.6-rimworld-1.7). New tag `v0.4.6-rimworld-1.7` points to `0a558bb`; original `v0.4.6` tag/assets are preserved.

Additional gameplay tests and final packaged-load smoke are deferred by the maintainer's decision, not represented as passed. Static package validation is separate from gameplay. No new runtime build is needed for unchanged binary content. See [readiness](../../Tests/NEXT-UPDATE-READINESS.md), [package record](../../Tests/UPLOAD-PREP-0.4.6-RIMWORLD-1.7.md) and [Workshop fields](../workshop/README.md).

[Workshop closeout](../../Tests/RELEASE-CLOSEOUT-0.4.6-RIMWORLD-1.7.md) records publication, hashes and the restored development junction.
