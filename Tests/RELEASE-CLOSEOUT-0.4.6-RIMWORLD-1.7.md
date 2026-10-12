# 0.4.6 RimWorld 1.7 compatibility maintenance — Workshop closeout

Completed October 12, 2026. The maintainer updated the existing [Automatic Outfit Manager Workshop item](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788) through RimWorld's Mods menu and saved the prepared description and change note. The Workshop update is complete within the accepted testing scope. GitHub/source-control operations are separate and were not performed.

## Published content

- Author: tekmojo; Workshop ID: `3792731788`; package ID: `tekmojo.automaticoutfitmanager`.
- Mod version: **0.4.6**, with support declared for **RimWorld 1.6 and 1.7**.
- Single shared DLL, unchanged SHA-256: `A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE`.
- About.xml adds 1.7. LoadFolders.xml explicitly loads root assets and the shared `1.6` assembly folder for both game versions. Eleven other files remain byte-identical to the October 2 package.
- Public package size: **1,079,086 bytes**, displayed as approximately 1.079 MB. Steam subscriber manifest: **6503806276562891881**; update timestamp: **1791845186**.
- All **13 subscriber files** match the audited stage by relative path and SHA-256.

The public [description](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788) matches `docs/workshop/DESCRIPTION.txt`, apart from Steam's automatic external-link domain labels. The [latest change note](https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788), entry `1791845186`, matches `docs/workshop/change-notes/0.4.6-rimworld-1.7.txt`. Steam-generated HTML verifies heading/bold/quote/list/link structure and no literal BBCode; a new browser screenshot was not captured. The original October 2 note remains intact. Copy was edited without a second package upload.

## Preserved artifacts

- Stage: `C:/GitHub/AutomaticOutfitManager/work/workshop-staging-0.4.6-rimworld-1.7-20261012/AutomaticOutfitManager`.
- Manifest SHA-256: `054C9D2895A7E3355E1B4A4A61B649E899F2181122CD4925ABF19EC24EF76943`.
- Local ZIP: `AutomaticOutfitManager-0.4.6-RimWorld-1.7.zip`, 633,058 bytes; SHA-256 `C5D37AC27AD6654AD730E065D78681D03EA19FB9D02AD51B7D31BDD09E3715A2`. The ZIP is in the stage's parent directory with its checksum; its thirteen entries match the audited stage.
- Evidence directory: `C:/GitHub/AutomaticOutfitManager/.codex-audit/compatibility-package-20261012/`. Public HTML, text/format comparison, downloaded-file hashes, pre-upload junction state, installed verification and restored-junction evidence are preserved there.

## Local installation and source control

The temporary upload junction was restored with RimWorld closed to `C:/GitHub/AutomaticOutfitManager`; the DLL hash through the installed path matches the tested binary. This restores the previous development checkout and does not copy candidate metadata or source into that separately dirty checkout. The Steam subscriber copy contains the new compatibility metadata.

RC branch remains `codex/workshop-release-prep`, base `b618146`, with uncommitted release/audit work preserved. No commit, tag, push, merge, GitHub release or asset replacement was performed. The original published `v0.4.6` tag and GitHub downloads retain the October 2 package, and must not be described as containing this maintenance update.

## Accepted scope and deferred checks

The maintainer explicitly accepted the 1.7 unstable tests as sufficient and deferred further gameplay testing. Tested preparation/restoration, weapon and combined outfits, active-outfit reload and ordinary Dining/recreation evidence remains in [readiness](NEXT-UPDATE-READINESS.md) and the linked audits. Exact boundary timing, prior route-conflict causes, other save phases, selected-outfit Non-Work, optional integrations and broader compatibility checks remain unverified. No new gameplay fix, save-format change, performance claim or subscriber-only gameplay pass is asserted.

See [package preparation](UPLOAD-PREP-0.4.6-RIMWORLD-1.7.md), [release checklist](../docs/releases/CHECKLIST.md) and [original 0.4.6 closeout](RELEASE-CLOSEOUT-0.4.6.md).

## GitHub maintenance release preparation

The maintainer subsequently authorized the remaining GitHub distribution steps. Publish a separate `v0.4.6-rimworld-1.7` release from the compatibility-content commit, retaining mod/DLL version 0.4.6 and the original `v0.4.6` tag/assets. Include the verified thirteen-file ZIP and its checksum. Record the resulting release commit and remote asset digests in a later closeout commit; do not move either published tag.
