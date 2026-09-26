# Final 0.4.3 bug-fix closeout — September 25

The maintainer uploaded through RimWorld's Mods menu, saved the separate change note and streamlined description, then requested the remaining closeout. Version remains **0.4.3** (assembly/file **0.4.3.0**).

## Workshop and installed runtime

- [Workshop item 3792731788](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788): Automatic Outfit Manager by tekmojo, creator 76561197960452376, public visibility. Harmony remains listed.
- Public update timestamp: **2026-09-26 00:48:28 UTC**, **2026-09-25 18:48:28 America/Denver**. [Separate change note](https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788) is verified.
- The public full description matches [the streamlined copy](../WORKSHOP-DESCRIPTION.txt) after newline normalization. The cumulative Latest update block is included; About/loading copy is unchanged.
- Subscriber manifest and public content handle: **4781208902276209270**. Steam's downloaded twelve-file set matches the frozen stage by paths, bytes and hashes: **1,061,488 bytes** total.
- RC/live/stage/subscriber/restored installed DLL: `85AD264B6CF071229DEFE128B6342D460776C37E7D5A6ED6CAFC9C85BB861D04`.
- With RimWorld closed, the installed upload junction was restored to `C:/GitHub/AutomaticOutfitManager`. Both targets were preserved. The live source checkout remains intentionally dirty and separate from release source.

## Matching GitHub package

The ZIP was created directly from the audited Workshop stage. All twelve decompressed entries match the stage and Steam subscriber files. Upload and remote digest verification are pending at this source-content checkpoint.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| AutomaticOutfitManager-0.4.3-final-bugfix-20260925.zip | 623,706 | `B3A189E2A71BD4E6B7E2DB3FB44BA1AC11C80772FD8A1159FF8F898B37F5984F` |
| AutomaticOutfitManager-0.4.3-final-bugfix-20260925.zip.sha256 | 121 | `D24A0FD64ADC69AF008E4043FF8D6470710DABE3C19C10392327B618F06B1973` |

Use the existing [GitHub 0.4.3 release](https://github.com/tekmojo/AutomaticOutfitManager/releases/tag/v0.4.3). Preserve earlier dated/undated assets and the original annotated tag object `c36f8fb5d84318d106baa6b511092b058b639bd7`, which points to original release commit `442cbd428cdbe07a7f7a195e65a3104f498190f7`. Automatic source archives for that tag remain the original release; this patch's source commit will be identified separately.

## Validation and limits

All 107 production source files match the preflight fingerprints; no new gameplay change was made during closeout. Carry forward the documented 888 checks and two negative controls. A fresh encoding check scans 113 source/XML/project files and 1,581 compiled strings with no findings. XML, versions, package contents and current documentation links are checked before commit.

[Latest gameplay evidence](FINAL-BUGFIX-SESSIONS-2026-09-25.md) confirms permitted child delivery, ordinary no-outfit work, intentional toggle-driven exit and saved-outfit restoration. Held-off exterior delivery, the earlier exact cooler route, queued/blocked gameplay cases, fresh save/reload and subscriber-only gameplay smoke remain unverified. Separate faction-relation and DBH toilet faults are not claimed fixed. [Current readiness](NEXT-UPDATE-READINESS.md) retains earlier ceremony/meal and other scoped limits.

Evidence directory: `C:/GitHub/AutomaticOutfitManager/.codex-audit/final-bugfix-closeout-0.4.3/`. It retains public responses, rendered-page HTML, the subscriber manifest, package/checksum, remote release metadata and junction-restoration record.
