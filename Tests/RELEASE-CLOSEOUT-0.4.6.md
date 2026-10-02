# 0.4.6 release closeout — 2026-10-02

The maintainer requested completion of closeout and deferred remaining troubleshooting to a later patch. The earlier recovery-investigation hold is superseded by that decision, not by a claim that recovery passed. [Follow-up scope](PATCH-FOLLOWUP-0.4.6.md) preserves the findings and validation limits.

## Workshop and installed files

The public [Automatic Outfit Manager item 3792731788](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788), creator tekmojo (`76561197960452376`), reports update time **2026-10-02 07:27:22 UTC**, content handle **4512023653260537327**, and **1,078,898 bytes**. Visibility, identity, Harmony dependency and Mod/1.6 tags are verified unchanged; the public page retains its preview. Public description matches the prepared BBCode after newline normalization. The [separate change note](https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788) matches the prepared wording and renders a bold heading with six list items.

Steam's installed/latest manifest is **4512023653260537327**. All **12 subscriber files** match the audited stage and RC inputs by path, length and SHA-256, with no extra files. This verifies downloaded contents, not subscriber-only gameplay.

RimWorld was closed when the temporary installed junction was restored from the stage to `C:/GitHub/AutomaticOutfitManager`. The release DLL hash was verified through the restored junction. The separate dirty live checkout was preserved; its source/metadata were not synchronized or reset. No game launch or further Workshop upload occurred.

- DLL SHA-256: `A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE`.
- Stage: `C:/GitHub/AutomaticOutfitManager/work/workshop-staging-0.4.6-upload-20261002/AutomaticOutfitManager`.
- Stage manifest SHA-256: `A0C2ACFFC433BCCB481A9058922A11CCD12015759DC848417B2E58D522FA5C6D`.

## GitHub assets and source

The ZIP was created directly from the frozen stage with mod files at archive root. All 12 entries were read back and verified against staged bytes/hashes. Uncompressed total: **1,078,898 bytes**. No source, scripts, PDBs or audit evidence are included.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| AutomaticOutfitManager-0.4.6.zip | 632,840 | `2B90CB1F3926394A9C9A2E900D4B3C128A4103A477E08CAC1AF0387D320CF60C` |
| AutomaticOutfitManager-0.4.6.zip.sha256 | 99 | `6DA9B211B641EB8DF7CBFC03E9ED147277F99F577DFBEFDB1CA259B789FB2C67` |

Published [GitHub release 0.4.6](https://github.com/tekmojo/AutomaticOutfitManager/releases/tag/v0.4.6), release ID **401647112**, at **2026-10-02 08:26:12 UTC**. It is the latest public release, not a draft. GitHub assets **605157602** (ZIP) and **605157601** (checksum) have the exact sizes and SHA-256 digests above. Draft notes and digests were verified before publication; public/latest metadata was checked afterward.

Release-content commit: **`5058262117e70ac0ef318321596b6940b3002747`**. Annotated tag **`v0.4.6`** remains fixed on this commit. Main and `codex/workshop-release-prep` advanced together from `e6bb309ff541dd82b2409ae4b62954aef9fe9339` by atomic, non-forced push. A later documentation-only closeout commit records the publication evidence and corrected checksum byte count; it does not move the tag or replace assets. Its identity is preserved in local final-closeout evidence and Git history.

## Validation and deferrals

[Upload preflight](UPLOAD-PREP-0.4.6.md) passed 1,082 focused checks and four expected negative controls. The final 0.4.6 [small-map retest](SMALL-MAP-RETEST-AUDIT-2026-10-02.md) has 26 completed restorations, 176 successful apparel endings and 18 successful weapon endings, none failed; all seven pawns reach rest. Revised saved-outfit text and locker tooltip are visible. Earlier gameplay covers Moto's personal pants, Gonzo's original outfit and ritual/rescue restoration on the gameplay-equivalent pre-version/display build.

Blocked saved-item recovery, full storage, exact transfer attribution, fresh changed-state reload, Hospitality arrival interruption, separate compatibility errors, independent About/downed UI capture and subscriber-only gameplay are deferred. No performance improvement or successful test of these scenarios is claimed. See [patch follow-up](PATCH-FOLLOWUP-0.4.6.md).

Evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/closeout-0.4.6/` contains public/API snapshots, file verification, ZIP/checksum, restored-junction evidence, GitHub records and live-checkout status comparison.
