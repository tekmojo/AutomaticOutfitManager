# Final 0.4.3 bug-fix update — closeout

**Published through the Mods menu; public copy and all twelve downloaded files verified. The installed junction is restored to the live repository.** Keep version **0.4.3** and existing Workshop item **3792731788**. Upload through **RimWorld's in-game Mods menu** as usual. Preserve the concise About/loading description, item identity and public visibility.

The previous published text-only maintenance update is archived in [its release record](RELEASE-HISTORY-0.4.3-TEXT-MAINTENANCE.md), [Workshop reference copy](WORKSHOP-PUBLISHED-0.4.3-TEXT-MAINTENANCE.md) and [readiness](Tests/READINESS-0.4.3-TEXT-MAINTENANCE.md). Its DLL is not this candidate.

## Prepared scope and evidence

- [x] Access-only Work Area transit and buffered-travel correction.
- [x] Actual adjacent construction destination recognition.
- [x] Child construction selection/runtime route agreement, remaining pickups and queued recipients.
- [x] Existing automated validation: 888 checks and two expected-failing negative controls; see [implementation record](Tests/CHILD-DELIVERY-ADMISSION-2026-09-25.md).
- [x] Current candidate gameplay: permitted child deliveries, no-outfit ordinary work, intentional disable-and-exit, saved-outfit restoration. See [session evidence](Tests/FINAL-BUGFIX-SESSIONS-2026-09-25.md).
- [x] Guide, design, changelog, Workshop draft and current readiness aligned. Existing player tooltips and short About text retained.
- [x] Version, XML, compiled-text, assets, local links and package-input preflight. Results: [final preflight](Tests/FINAL-BUGFIX-PREFLIGHT-0.4.3.md).

Candidate/live/installed DLL: `85AD264B6CF071229DEFE128B6342D460776C37E7D5A6ED6CAFC9C85BB861D04`.

Earlier published text-only maintenance DLL: `4CA32704045B015AB50A6FA20AFA236A72E717E90DDF812F785830CB1A0E9B32`.

## Remaining validation limits

- [ ] A complete exterior-accessible boundary delivery with Allow Children held off throughout, including blocked/queued pickup and recipient cases.
- [ ] Exact earlier cooler route and fresh preparation/save-reload continuity.
- [ ] Earlier ceremony/meal compatibility follow-ups remain as recorded; no full matrix pass is claimed.

These are disclosed unperformed cases, not reproduced defects in the latest recordings. Faction-relation spam and Dubs Bad Hygiene's toilet exception remain separate concerns, not claimed fixes. Current scope and evidence are in [readiness](Tests/NEXT-UPDATE-READINESS.md).

## Mods-menu workflow used

1. Close RimWorld before changing its installed upload source. Preparation does not close the running game.
2. Run `package-workshop.ps1` into a distinct final-bugfix stage. Verify the twelve-file runtime allowlist, 0.4.3 metadata, preview/icons, `About/PublishedFileId.txt` (`3792731788`) and candidate DLL hash. Do not reuse the old text-maintenance stage as proof of this update.
3. Verify the installed mod is the expected junction. When the upload handoff is authorized, point it at the audited stage and compare installed About, Workshop ID, preview and DLL before the maintainer's manual launch.
4. In the Mods menu, verify title, preview, short description and version, then update the existing item using the **Final bug-fix change note** from [Workshop copy](WORKSHOP.md). The compact Latest update block is a separate description field; keep the About text unchanged.
5. Verify the public change note and content identity; compare every Steam-downloaded file with the stage after the subscriber cache refreshes. Record any unperformed downloaded-copy gameplay smoke honestly.
6. With RimWorld closed, restore any temporary stage junction to the live repository and verify its DLL. Source-control and GitHub closeout remain later operations; keep the existing version tag and historical assets intact.

## Current operation state

- [x] Public item identity, Harmony dependency, shortened description and separate final bug-fix note verified.
- [x] Refreshed Steam manifest **4781208902276209270** and all twelve downloaded files match the stage (1,061,488 bytes).
- [x] With RimWorld closed, temporary upload junction restored to the live repository; DLL verified and dirty live source preserved.
- [x] Matching twelve-file ZIP and checksum built directly from the audited stage.
- [ ] Publish and verify the new dated GitHub assets; preserve the original tag and older assets.
- [ ] Commit and push the final source and closeout records.

See [final closeout](Tests/FINAL-BUGFIX-CLOSEOUT-0.4.3.md). Publication does not mark the unperformed gameplay checks above as passed.
