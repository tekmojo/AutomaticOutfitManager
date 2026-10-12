# 0.4.6 RimWorld 1.7 compatibility maintenance — Workshop closeout

Completed October 12, 2026. The maintainer updated the existing [Automatic Outfit Manager Workshop item](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788) through RimWorld's Mods menu and saved the prepared description and change note. The Workshop update is complete within the accepted testing scope. The matching [GitHub maintenance release](https://github.com/tekmojo/AutomaticOutfitManager/releases/tag/v0.4.6-rimworld-1.7) is now published and verified; the separate source-control/publication phase is recorded below.

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

The compatibility release content is committed as `0a558bb1c425d156869e217a276c3b125f2a9551` on `codex/workshop-release-prep` and `main`, fast-forwarded from `b618146`. Tag `v0.4.6-rimworld-1.7` remains fixed on that release-content commit; later documentation closeout does not move it. Original tag `v0.4.6` remains on `5058262117e70ac0ef318321596b6940b3002747`, and its October 2 assets retain their prior digests.

## Accepted scope and deferred checks

The maintainer explicitly accepted the 1.7 unstable tests as sufficient and deferred further gameplay testing. Tested preparation/restoration, weapon and combined outfits, active-outfit reload and ordinary Dining/recreation evidence remains in [readiness](NEXT-UPDATE-READINESS.md) and the linked audits. Exact boundary timing, prior route-conflict causes, other save phases, selected-outfit Non-Work, optional integrations and broader compatibility checks remain unverified. No new gameplay fix, save-format change, performance claim or subscriber-only gameplay pass is asserted.

See [package preparation](UPLOAD-PREP-0.4.6-RIMWORLD-1.7.md), [release checklist](../docs/releases/CHECKLIST.md) and [original 0.4.6 closeout](RELEASE-CLOSEOUT-0.4.6.md).

## GitHub maintenance release preparation

The maintainer subsequently authorized the remaining GitHub distribution steps. Publish a separate `v0.4.6-rimworld-1.7` release from the compatibility-content commit, retaining mod/DLL version 0.4.6 and the original `v0.4.6` tag/assets. Include the verified thirteen-file ZIP and its checksum. Record the resulting release commit and remote asset digests in a later closeout commit; do not move either published tag.

## Verified GitHub publication — October 12, 23:02 UTC

Published release: [v0.4.6-rimworld-1.7](https://github.com/tekmojo/AutomaticOutfitManager/releases/tag/v0.4.6-rimworld-1.7), release-content commit `0a558bb1c425d156869e217a276c3b125f2a9551`. The release is public, non-draft and non-prerelease. The game branch used for testing remains 1.7 unstable; the GitHub release status does not claim that RimWorld 1.7 itself is stable.

GitHub confirms both uploaded assets:

| Asset | Bytes | GitHub SHA-256 |
| --- | ---: | --- |
| AutomaticOutfitManager-0.4.6-RimWorld-1.7.zip | 633,058 | `c5d37ac27ad6654ad730e065d78681d03ea19fb9d02ad51b7d31bdd09e3715a2` |
| AutomaticOutfitManager-0.4.6-RimWorld-1.7.zip.sha256 | 113 | `e5afe1f243f21472d5d9a7494e69cfba47ad074292e4e85c9510c1ab4ef59cf3` |

Both remote digests and sizes match the local files. The ZIP contains the exact thirteen-file audited stage. Original v0.4.6 ZIP and checksum digests remain `2b90cb1f3926394a9c9a2e900d4b3c128a4103a477e08cac1af0387d320cf60c` and `6da9b211b641eb8df7cbfc03e9ed147277f99f577dfbefdb1ca259b789fb2c67` respectively. No existing asset was replaced.

Publication evidence is in `github-draft-verified.json` and `github-published.json` under the evidence directory above. The later documentation-only closeout commit is recorded in `github-closeout-state.json` there; both branches advance to that commit without moving the maintenance tag. The separately dirty live checkout remains untouched.
