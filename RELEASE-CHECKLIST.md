# 0.4.0 release checklist

**Status: 0.4.0 published on Workshop; downloaded package verified. GitHub publication in progress.** This is the current checklist. Earlier audit decisions remain in the [release history](RELEASE-HISTORY-0.3.x.md); later fixes supersede older unresolved findings there.

User-authorized local deployment verified on 2026-09-06T19:08:41.8328008-06:00: DLL and the three updated runtime XML files match RC/live/installed. Current DLL `F00A4B06756118DBE8C6EA50681980F6A52248024F2AD1AA811761C65B08AF24`. The maintainer subsequently confirmed blocked saved-item recovery, cooking after a Non-Work outfit change and Delete confirmation passed. These are maintainer-reported results; no new recording/log accompanied that confirmation. See [package validation](Tests/RELEASE-PACKAGE-0.4.0.md).

## Candidate scope

Non-Work Area Rules, saved/fallback outfits, selective Work-outfit removal, activity/child access, buffers, colored rule/source UI, editable descriptions and Delete confirmation. The accompanying gameplay fixes address exact prepared jobs, restoration order, storage claims and repeated empty restoration plans. See [CHANGELOG.md](CHANGELOG.md), [README.md](README.md) and the [Workshop copy](WORKSHOP.md).

The 2026-09-06 copy pass changes player text, selection/inspection presentation and version metadata to **0.4.0**. It does not change gameplay policy. Build/preflight results and exact candidate/live/installed hashes are recorded in [the copy audit](Tests/RELEASE-COPY-AUDIT-0.4.0.md).

## Evidence carried forward

Last gameplay-tested DLL: `98BC3B34BCEFF8443174D7472BB27908FCE090B5249411FDA98733FA0096A4E0`.

- Small map, `1f0rIn7XTK`: 25 completed restorations, all 166 apparel/19 Equip endings successful, four actual meal admissions, both coolers supplied. No idle preparation/restoration rebuild or locker-restock mismatch. Startup warnings identify other mods.
- Large map, `6TbvSslDOI`: four completed restorations, all 20 apparel/six Equip endings successful, both prepared bills resumed, successful Work/Non-Work buffer credit and failed-task exclusion. Sampled animal/robot rows and compact counts agree. No new AOM fault established.
- Both recorded audits show normal flow on the fixes, but neither reproduces the exact blocked saved-item recovery condition. The large recording also does not exercise early first-ingredient admission. The maintainer's later test confirmation closes the reported scenarios; it does not turn these earlier recordings into evidence of those conditions.
- The **current 0.4.0 candidate passed all 1,954 checks in 21 suites again** during package preflight. Its 100 source/runtime inputs match the previously built candidate, including all 91 source/project files.

Raw local evidence links remain in the history; they are development records outside the public package. [Current focused test plan](Tests/NEXT-UPDATE-READINESS.md).

## Remaining gameplay and UI checks

- [x] Blocked saved-item recovery: maintainer reported the remaining scenario passed. The detailed negative/contended variants in the test plan remain useful regression cases; no new agent-observed trace is claimed.
- [x] Cooking after a Non-Work outfit change: maintainer reported the remaining scenario passed.
- [ ] Candidate UI/load smoke: load each existing save, inspect Work/Non-Work settings, saved/fallback and retained item hovers, removal choices, colored references, locked checks, and collapsed text at the actual UI scale. Exercise a new rule and reopen the save.
- [x] Delete confirmation: maintainer reported the remaining scenario passed. The full keyboard/layout matrix remains documented in the test plan.
- [ ] Optional measured performance baseline with consistent speed, map and logging conditions. No CPU/TPS improvement claim is made for this release from event counts.

## Documentation and assets

- Candidate versions, XML, links, code build and existing image constraints: consult the completed copy audit for actual results.
- [x] Receive eight new gameplay screenshots and prepare exact copies, captions, ordering and a manifest in the [replacement gallery](Screenshots/README.md).
- [x] Check Workshop image sizes: all eight originals are under 1,000,000 bytes (largest 938,376); branded cover 369,895 bytes. Recheck any later exports and the active uploader before publication.
- [x] Replace all older Workshop gameplay screenshots with the eight new images; public identity/order verified.
- [x] Prepare the matching GitHub guide/gallery and retire the nine older active gallery files; preserve Git history and a local backup. Public rendering is checked during closeout.
- [x] Retain and verify the current branded cover (640×360, 369,895 bytes) and transparent 64×64 icons. These are separate from the eight replacement gameplay screenshots.
- [x] Verify the published Workshop description, Latest update and full change note, including the maintainer's final wording change.
- [ ] Complete the GitHub wording/date check at its publication.

## Release operations

Workshop 0.4.0 is public on item **3792731788**, Automatic Outfit Manager by tekmojo, with Harmony required. Its eight replacement images and copy are verified. Steam manifest `1496049653910862886` contains the exact 12 staged files (981,162 bytes). The installed junction is restored to the live repository with the release DLL. See [Workshop closeout](Tests/WORKSHOP-CLOSEOUT-0.4.0.md). GitHub publication remains pending.

- [ ] Freeze the tested candidate; review intentional source changes and create the authorized release commit/tag.
- [x] Stage with `package-workshop.ps1`, inspect the exact 12-file allowlist/manifest and verify DLL, metadata and assets. Package bytes: 981,162; ZIP bytes: 593,543. Every ZIP entry matches the stage. [Recorded hashes and results](Tests/RELEASE-PACKAGE-0.4.0.md).
- [x] Verify existing Workshop item **3792731788** and preserve its `PublishedFileId.txt` in the stage. Updating an existing public item does not authorize changing its identity or visibility. Validate without disrupting current subscribers.
- [x] Verify the staged About card and runtime for the authorized upload; maintainer completed the upload.
- [x] Verify the Steam-downloaded package separately: exact entry set, lengths and SHA-256 values match all 12 stage files.
- [x] Restore the temporary stage junction to the live repository with RimWorld closed; verify installed DLL and official Workshop ID.
- [ ] Optional subscriber-loaded in-game smoke with the development junction disabled; not performed in closeout.
- [x] Publish and verify the approved Workshop description, compact update, full change note and eight-image gallery.
- [ ] Publish the matching GitHub release/package; verify remote asset size/digest and record the release commit/tag.

Build, deployment, game testing, packaging and publication are separate recorded states. Workshop runtime, copy and gallery publication are complete. GitHub release/gallery publication and native subscriber smoke remain separate pending states.
