# GitHub 0.4.3 closeout — 2026-09-24

[Automatic Outfit Manager 0.4.3](https://github.com/tekmojo/AutomaticOutfitManager/releases/tag/v0.4.3) is public and verified as the latest GitHub release. Publication timestamp: **2026-09-25T05:08:54Z**, or **2026-09-24 23:08:54 MDT**. Release ID: **396318998**.

## Source and assets

- Annotated tag **v0.4.3** points to release-content commit [`442cbd428cdbe07a7f7a195e65a3104f498190f7`](https://github.com/tekmojo/AutomaticOutfitManager/commit/442cbd428cdbe07a7f7a195e65a3104f498190f7).
- `main` and `codex/workshop-release-prep` were fast-forwarded from `5f0befe8862c1e1f5cf51e8d2c830d5f247b41e2` and pushed with the tag. This later documentation closeout records publication evidence without moving that tag.
- The ZIP contains twelve files and **1,053,123 uncompressed bytes**, directly from the corrected audited stage. Its entry set and all decompressed contents match the stage. Files sit at the archive root for extraction into an `AutomaticOutfitManager` mod folder.
- Both uploaded assets' sizes and SHA-256 digests match the local files. GitHub's latest-release endpoint identifies this public, non-prerelease version. Release notes match the prepared notes.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| [AutomaticOutfitManager-0.4.3.zip](https://github.com/tekmojo/AutomaticOutfitManager/releases/download/v0.4.3/AutomaticOutfitManager-0.4.3.zip) | 620,364 | `48E3086B5AA8E8CA3E2505D1CC92AEA1C20623AC4D69B41613D447548617F56A` |
| [AutomaticOutfitManager-0.4.3.zip.sha256](https://github.com/tekmojo/AutomaticOutfitManager/releases/download/v0.4.3/AutomaticOutfitManager-0.4.3.zip.sha256) | 100 | `8C20EA291504D44446715EE81EF22C7F24E0AA2C8375B2E9F6269699A4F05EEE` |

Released DLL SHA-256: **FB5F15960E286091A24CFB50E39989C5C1BDDDCA706E17924E17F7CDE7AA4BA9**. The [punctuation correction](PUNCTUATION-CORRECTION-0.4.3.md) restores 23 garbled dash characters without gameplay changes. All 115 frozen source/package inputs remained unchanged after the corrected build. The separate live checkout's pending changes and tracked file contents were preserved.

## Workshop and local installation

**Final state:** replacement Workshop content handle/subscriber manifest `8379366355831222618` is verified, and all twelve downloaded files match the corrected GitHub ZIP. The correction note is live and the previous detailed note is preserved. RimWorld subsequently closed; the development junction and corrected live DLL are restored and verified. See the final section of [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.3.md). The following paragraphs record the earlier state at GitHub publication.

At GitHub publication, Workshop item [3792731788](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788) still reports content handle **818263818669951996** and update time **2026-09-25T04:50:18Z**. The subscriber folder still contains the initial 0.4.3 DLL `149A319E8D6BDAB4494AD6727AA3607D3974DAE2BC2BDE1A6526BB18A005DF1D`; only that DLL differs from the corrected stage. The byte count is unchanged, so size alone cannot verify this replacement. Initial publication evidence remains in [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.3.md).

The maintainer renewed authorization to proceed with GitHub after the corrected draft was prepared. GitHub is published; replacement Workshop upload and subscriber refresh remain pending. RimWorld is running, so the installed junction remains on `work/workshop-staging-0.4.3-punctuation/AutomaticOutfitManager`. Do not restore it or replace the live runtime while the game is running. After the replacement upload and game shutdown, verify Steam's new manifest and file hashes, restore the development junction and deploy the corrected DLL to the live repository.

## Validation limits

The corrected package builds successfully; static source/string checks, XML parsing, source freeze and ZIP verification pass. Earlier coverage includes 2,078 behavioral checks and 804 checks rerun during copy preparation. The punctuation change did not trigger a repeat of unaffected gameplay tests. Publication adds no gameplay evidence.

Full post-ceremony adult returns, additional ritual types, child permission edges, fresh transition save/load and native interruption, final UI/load smoke and subscriber-only gameplay remain scoped follow-ups. The earlier simple-meal loop is not claimed fixed. See [current readiness](NEXT-UPDATE-READINESS.md).

Local evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/release-copy-0.4.3/punctuation-github/` contains ZIP/checksum, asset manifest, prepared notes, draft/public/latest responses, publication preflight, Steam details and live-checkout preservation evidence. The final closeout commit ID and remote refs are recorded there after the documentation push.
