# September 25 maintenance closeout — version 0.4.3

The maintainer requested completion of the remaining closeout after uploading through RimWorld's Mods menu and updating the separate change note. Version remains **0.4.3**; assembly/file version remains **0.4.3.0**.

## Published Workshop and local runtime

- Existing [Workshop item 3792731788](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788) retains its identity. The [maintenance note](https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788) is verified.
- Upload: **2026-09-25 17:11:06 UTC**, or **11:11:06 America/Denver**. Subscriber manifest: **2204596392603111389**.
- All twelve subscriber files match the audited stage by path and contents: **1,052,784 bytes** total.
- RC, live, stage, subscriber and restored installed DLL SHA-256: `4CA32704045B015AB50A6FA20AFA236A72E717E90DDF812F785830CB1A0E9B32`.
- The temporary upload junction was restored to the live repository with RimWorld closed. The installed runtime hash was verified. The dirty live source checkout remains separate and preserved.
- [Deployment evidence](MAINTENANCE-DEPLOYMENT-0.4.3.md) records the upload, download comparison and restoration.

## GitHub maintenance download

Published to the existing [GitHub 0.4.3 release](https://github.com/tekmojo/AutomaticOutfitManager/releases/tag/v0.4.3), directly from the frozen Workshop stage. Every decompressed ZIP entry matches the stage and downloaded subscriber copy. GitHub's reported size and SHA-256 digest match both new assets. The older undated assets retain their original asset IDs and digests. The release notes identify the dated maintenance download first and preserve the original release notes as history.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| [AutomaticOutfitManager-0.4.3-maintenance-20260925.zip](https://github.com/tekmojo/AutomaticOutfitManager/releases/download/v0.4.3/AutomaticOutfitManager-0.4.3-maintenance-20260925.zip) | 620,204 | `22AB6D175F7BBBDD7379C389DC119CF7D3A79DFB82EE4CA0A2A6F1952222D979` |
| [AutomaticOutfitManager-0.4.3-maintenance-20260925.zip.sha256](https://github.com/tekmojo/AutomaticOutfitManager/releases/download/v0.4.3/AutomaticOutfitManager-0.4.3-maintenance-20260925.zip.sha256) | 121 | `51505C5C5AAEDCCFC7D86497572064C1A6456229803F5D0A1A9E35C23C687C91` |

The original annotated `v0.4.3` tag remains at `442cbd428cdbe07a7f7a195e65a3104f498190f7`; its tag object is `c36f8fb5d84318d106baa6b511092b058b639bd7`. Automatic source archives attached to that tag represent the original release. The maintenance source is identified separately by commit, without moving the tag or creating a new version.

Maintenance source commit: [`d35ca0f572d575e20d82f0e4257e3d0647686c46`](https://github.com/tekmojo/AutomaticOutfitManager/commit/d35ca0f572d575e20d82f0e4257e3d0647686c46). Both `main` and `codex/workshop-release-prep` were fast-forwarded and pushed from `2e02f76`. This subsequent documentation commit records verified publication without changing the runtime. Its final commit ID and matching remote references are retained in the local evidence folder after the closeout push.

## Verification and limits

The existing successful maintenance build is preserved. Closeout checks scan 111 source/XML/project files and all 1,571 compiled user strings without findings; runtime XML parses; ZIP contents match the twelve-file allowlist and subscriber copy. The three archived publication documents preserve their original contents. The code diff consists of two string literals; gameplay logic and save keys are unchanged.

The supplied Mods-menu screenshot confirms title, version, preview and concise About description. Task Buffer and selected-outfit tooltip visual checks, another outfit restoration and subscriber-only gameplay smoke remain unverified. The older Anomaly, ceremony, child-permission, save/load, meal/bill and crib follow-ups remain disclosed in [current readiness](NEXT-UPDATE-READINESS.md). Publication does not mark those scenarios passed.

Local evidence is retained under `C:/GitHub/AutomaticOutfitManager/.codex-audit/maintenance-0.4.3-2026-09-25/`, including the package, checksum, asset metadata, text scan, publication responses and final remote references. Personal skill guidance was already updated and verified during [preflight](MAINTENANCE-PREFLIGHT-0.4.3.md).
