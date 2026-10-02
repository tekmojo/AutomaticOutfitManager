# 0.4.6 release closeout — 2026-10-02

The maintainer requested completion of closeout and deferred remaining troubleshooting to a later patch. The earlier recovery-investigation hold is superseded by that decision, not by a claim that recovery passed. [Follow-up scope](PATCH-FOLLOWUP-0.4.6.md) preserves the findings and validation limits.

## Workshop and installed files

The public [Automatic Outfit Manager item 3792731788](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788), creator tekmojo (`76561197960452376`), reports update time **2026-10-02 07:27:22 UTC**, content handle **4512023653260537327**, and **1,078,898 bytes**. Visibility and identity are unchanged. Public description matches the prepared BBCode after newline normalization. The [separate change note](https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788) matches the prepared wording and renders a bold heading with six list items.

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
| AutomaticOutfitManager-0.4.6.zip.sha256 | 100 | `6DA9B211B641EB8DF7CBFC03E9ED147277F99F577DFBEFDB1CA259B789FB2C67` |

Release commit, annotated tag, remote digests and publication identity are recorded below after publication. The tag remains on the release-content commit; subsequent evidence updates are separate closeout commits.

## Validation and deferrals

[Upload preflight](UPLOAD-PREP-0.4.6.md) passed 1,082 focused checks and four expected negative controls. The final 0.4.6 [small-map retest](SMALL-MAP-RETEST-AUDIT-2026-10-02.md) has 26 completed restorations, 176 successful apparel endings and 18 successful weapon endings, none failed; all seven pawns reach rest. Revised saved-outfit text and locker tooltip are visible. Earlier gameplay covers Moto's personal pants, Gonzo's original outfit and ritual/rescue restoration on the gameplay-equivalent pre-version/display build.

Blocked saved-item recovery, full storage, exact transfer attribution, fresh changed-state reload, Hospitality arrival interruption, separate compatibility errors, independent About/downed UI capture and subscriber-only gameplay are deferred. No performance improvement or successful test of these scenarios is claimed. See [patch follow-up](PATCH-FOLLOWUP-0.4.6.md).

Evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/closeout-0.4.6/` contains public/API snapshots, file verification, ZIP/checksum, restored-junction evidence, GitHub records and live-checkout status comparison.
