# 0.4.4 release status

**Published and verified on Steam Workshop on 2026-09-26.** The maintainer uploaded through RimWorld's Mods menu. Public copy and all twelve subscriber files match the frozen package, and the development junction is restored. [Closeout](Tests/WORKSHOP-CLOSEOUT-0.4.4.md) records exact identities and limits. The matching GitHub release, source tag and verified download are complete; see [GitHub closeout](Tests/GITHUB-CLOSEOUT-0.4.4.md).

## Candidate scope

- [x] Skip other pawns' prepared-work component claims within native repair material selection, retaining eligible alternatives.
- [x] Preserve native forbidden/reachability/reservation checks, forced orders, owner access and immediate claim release.
- [x] Clear nested selection context on normal return and exceptions. No persistent state or rule-default changes.
- [x] Align RC About, project, assembly and package expectations to 0.4.4. Archive published 0.4.3 records and prepare draft copy.
- [x] Preserve the earlier repair fix and add permitted nonhuman Work/Non-Work routing. Run 1,032 checks plus the expected-failing previous-routing control; see [validation](Tests/NONHUMAN-TRANSIT-0.4.4.md).
- [x] New local DLL deployment verified with RimWorld closed; candidate/live/installed hashes match.
- [x] Routing build starts as 0.4.4 with no new patch-installation failure. Initial normal-play smoke shows bot deliveries and human monolith-study preparation/restoration; see [session evidence](Tests/NEXT-UPDATE-READINESS.md).
- [x] Five adults complete Void provocation and restore outfits; Anomaly psychic-ritual normal flow only.
- [x] Release descriptions, player guide, versions, assets, source/XML/compiled encoding and local links reviewed. Separate change note covers both fixes.
- [x] Package allowlist verified; all twelve installed files match the 0.4.4 stage. RimWorld closed during installation.
- [ ] Verify permitted mech/animal transit through Work and Non-Work Areas, denied access/overlaps, pause behavior and human outfit boundaries.
- [ ] Reproduce contested repair with no alternate, then with an available alternate; verify both workers progress without scanner mismatch.
- [ ] Verify forced repair, owner continuation, real claim release and fresh save/reload.

Candidate DLL SHA-256: `ED72D9E73C80344960E1090FE3E46BD20973FB4D5B7E733B500A013974784609`.

The installed junction has been restored to the live repository with RimWorld closed; its DLL matches the published stage and subscriber copy. The live checkout retains its separate 0.4.3 metadata. [Upload preparation](Tests/UPLOAD-PREP-0.4.4.md) records the frozen stage used for publication.

## Publication and closeout

- [x] Maintainer reports successful update of existing Workshop item `3792731788` through the Mods menu.
- [x] Public description and separate change note match the prepared 0.4.4 copy; title, creator, visibility and Harmony dependency are retained.
- [x] Steam's refreshed subscriber manifest matches the public content handle; all twelve files match the audited stage by paths, bytes and hashes.
- [x] With RimWorld closed, restore the installed junction to the live repository and verify the published DLL hash.
- [ ] Subscriber-only gameplay smoke. File verification is complete; launching/testing the downloaded copy remains distinct.
- [x] Release source committed and tagged as v0.4.4; main and release branch pushed. GitHub release published with verified ZIP/checksum. Tag remains on the release-content commit.

Controlled route/access and repair/save-reload cases above remain pending. The in-game About card was not independently captured. Arakis's crib loop, Gonzalez's background sidearm/idle concern and other mods' save-reference warnings are not claimed fixed. Prior ceremony, meal and save/load limits remain in [archived readiness](Tests/READINESS-0.4.3-FINAL-BUGFIX.md).
