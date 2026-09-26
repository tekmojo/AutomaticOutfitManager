# Automatic Outfit Manager — September 25 maintenance closeout

Uploaded through the Mods menu on September 25. The public change note is verified; all twelve downloaded files match the maintenance stage. The installed junction is restored to the live repository with the maintenance DLL verified. Keep version **0.4.3** for these small corrections, as requested. Update existing Workshop item **3792731788** through **RimWorld's in-game Mods menu**. Preserve its identity and public visibility. Prior publication is archived in [0.4.3 release history](RELEASE-HISTORY-0.4.3.md), [published Workshop copy](../../workshop/history/WORKSHOP-PUBLISHED-0.4.3.md) and [published readiness](../../../Tests/READINESS-0.4.3-PUBLISHED.md).

## Prepared scope

- [x] Correct the Task Buffer minus sign using an explicit Unicode escape.
- [x] Restore the exact pre-0.4.3 concise About/loading description, retaining current metadata/version.
- [x] Correct stale child-wearability guidance in the player guide and clarify the selected Non-Work outfit tooltip for adults.
- [x] Preserve gameplay logic, save keys, rule defaults, package ID and version identifiers.
- [x] Prepare a compact Workshop update and separate maintenance change note in [Workshop copy](../../workshop/README.md).
- [x] Archive prior publication records and preserve their hashes and test limits.
- [x] Build the candidate and scan source/XML plus all compiled user strings. See [maintenance preflight](../../../Tests/MAINTENANCE-PREFLIGHT-0.4.3.md).

## Short remaining validation

- [x] Deploy the candidate DLL to the live repository, create the twelve-file stage and verify the installed upload source.
- [ ] Check Task Buffer at 0, 1 and 20 for both rule kinds; verify readable minus/plus, decrement to Immediate and the existing upper limit.
- [x] Inspect the concise About card, title, preview and version in the maintainer's Mods-menu screenshot.
- [ ] Inspect the selected-outfit tooltip at normal UI scale, then load the intended save and perform one outfit return.
- [ ] Independently check Arakis with an appropriate normal bed; the audit found a persistent crib loop. This text-only candidate does not claim a fix.

The [current readiness record](../../../Tests/NEXT-UPDATE-READINESS.md) preserves healthy restoration evidence and separates older ceremony, meal/bill, save/load and Anomaly cases. Do not restart the whole gameplay matrix for these text-only changes. The existing public gallery remains a legacy example; new marketing images are not required for this correction.

## Mod-menu Workshop handoff

1. Stage when requested, using `package-workshop.ps1` and a distinct maintenance output directory. Verify the twelve-file runtime allowlist, version 0.4.3, metadata, assets and new DLL hash. Preserve `About/PublishedFileId.txt` (`3792731788`).
2. While RimWorld is closed, verify the installed mod junction. Retarget to the audited upload stage only with authorization, then compare installed About, Workshop ID, preview and DLL with the stage **before** asking the maintainer to launch.
3. The maintainer launches manually through Steam. Verify the staged About card in the Mods menu: concise description, title, preview and version 0.4.3. Use that menu to update the existing Workshop item with the prepared note; do not create a new item or switch to a CLI uploader.
4. Verify the public update and note, then compare every downloaded file with the audited stage after the subscriber cache refreshes. Public byte count alone does not verify the subscriber copy.
5. With RimWorld closed, restore any temporary upload junction to the live repository and verify the intended runtime. Record upload time, manifest/content handle, metadata and DLL hashes, and any unperformed downloaded-copy smoke.

The maintenance closeout is complete. Workshop and subscriber-file verification pass, the installed junction is restored, and source commit `d35ca0f` is pushed to `main` and `codex/workshop-release-prep`. The dated maintenance ZIP and checksum are published on the existing GitHub 0.4.3 release with matching remote sizes and digests. The original version tag and older assets are preserved. This later documentation commit records publication; see [maintenance closeout](../../../Tests/MAINTENANCE-CLOSEOUT-0.4.3.md). The unperformed gameplay checks above remain follow-ups.

Candidate SHA-256: `4CA32704045B015AB50A6FA20AFA236A72E717E90DDF812F785830CB1A0E9B32`.

Prior published/subscriber baseline SHA-256: `FB5F15960E286091A24CFB50E39989C5C1BDDDCA706E17924E17F7CDE7AA4BA9`.

Upload-source record: [maintenance deployment](../../../Tests/MAINTENANCE-DEPLOYMENT-0.4.3.md). Candidate, live, staged, subscriber and installed DLL hashes match. Junction restoration is complete. Steam manifest: 2204596392603111389. Additional in-game UI and downloaded-copy smoke checks remain unclaimed.
