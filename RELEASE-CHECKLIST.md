# Automatic Outfit Manager — 0.4.1 release closeout

Released on Workshop **2026-09-08**. Public copy and downloaded package verification are complete, and the normal development junction is restored. The matching [GitHub release](https://github.com/tekmojo/AutomaticOutfitManager/releases/tag/v0.4.1) is public and latest; [GitHub closeout](Tests/GITHUB-CLOSEOUT-0.4.1.md) records the tag and asset verification. [Workshop closeout](Tests/WORKSHOP-CLOSEOUT-0.4.1.md) records the evidence; [0.4.0 history](RELEASE-HISTORY-0.4.0.md) remains unchanged.

## Completed preparation and Workshop release

- [x] Align versions, player copy, About, documentation, Workshop fields and troubleshooting skills.
- [x] Release build reproduces `B8C1A1DAED38FF2AAC4BEBDA27DDED08A7466BCF60F0B2B0977677C8C5F47894`. Validate **3,431 checks across 29 suites**, plus 29 negative-control variants. See [copy preflight](Tests/RELEASE-COPY-AUDIT-0.4.1.md).
- [x] Deploy and stage the exact twelve-file package; inspect XML, version, IDs, asset limits and manifests.
- [x] Maintainer updates the existing Workshop item, description and change notes; verify all public copy and the downloaded package against the stage.
- [x] Retain the existing eight screenshots for 0.4.1 at the maintainer's request. Future capture suggestions are deferred, not a release gate.
- [x] Restore the installed junction after confirming the game is closed; verify the installed DLL matches the release.
- [x] Build and verify the GitHub ZIP directly from the audited stage: 612,315 bytes, SHA-256 `CB67C35380515AACF47354E55B4408782A982B2E1BCD73C6A725C8D9A4A63E15`.
- [x] Commit/tag the frozen release source, fast-forward the release branches, publish GitHub release v0.4.1 and verify both uploaded asset sizes and SHA-256 digests.

## Unverified gameplay follow-up

The maintainer proceeded with publication. These focused scenarios remain unverified; they are not recorded as passed or as outstanding screenshot work. Existing successful small/large-map evidence remains valid for its observed scope.

1. **Protected saved-item recovery:** keep the exact saved item inside the obstructing Work Area, provide an eligible hauler and accepting owner-accessible storage, and confirm PPE pickup, delivery and owner restoration. Retrieval after moving the item to an accessible cell does not close this case.
2. **Mental-state save/load:** save and reload while a pawn is suspended, then allow recovery; confirm ownership survives, obsolete civilian steps do not resume during the break, and the pawn returns to normal outfit handling afterward. Prior debug-ended recovery passed; save/load during the break remains untested.
3. **Existing-save shared-item cleanup:** exercise an inactive saved outfit containing selected or retained shared apparel and a weapon. Confirm held items use the safe pending return, loose items lose personal ownership without an unnecessary trip, and ordinary exact personal items remain saved. Contract coverage is not a native save migration pass.
4. **Candidate UI/load smoke after an authorized deployment:** version 0.4.1; short gameplay About; Pause/Resume activities; Recall on an observed child activity; automatic outfit filters/saved tags; rule versus storage condition tips; persistent overlap highlighting. Use Detailed logging only for reproduction.

Mark a case passed only with the tested DLL/hash and actual observed outcome. The robe storage issue is already resolved by the maintainer's storage-condition correction.


Subscriber files are verified byte-for-byte, but no subscriber-only game launch or automated gameplay was performed. The current version's publication and gameplay evidence are deliberately recorded separately.
