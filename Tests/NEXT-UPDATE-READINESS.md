# 0.4.5 release validation

Four fixes follow the published 0.4.4 release: borrowed-gear identity cleanup, interrupted installation pickup, floor selection under competing claims and prepared blueprint-floor continuation. The release build changes version metadata to 0.4.5 without changing the tested gameplay logic.

Gameplay-tested pre-version-bump DLL: `35E9D0097762005F37709C6EE41827CD1C09F58E56613880E91138B19E50D8D4`. RC base: `9ff91a34fa047f582e5c9a9f12f0c51c4dfab2ae` plus preserved changes. Final candidate and package hashes belong in the upload preparation record.

## Implementation evidence

- [Borrowed-gear cleanup](BORROWED-GEAR-CLEANUP-2026-09-26.md): obsolete exact tracking IDs pruned while active returns and saved/selected/retained storage semantics remain intact. Follow-up startup removed 87 obsolete records and the maintainer reported the tags fixed.
- [Interrupted installation](PREPARED-INSTALL-COUNT-2026-09-26.md): narrowly repair automatic zero-count pickup of the exact single dropped minified building for its live installation blueprint. Native fixture covers the defect; ordinary relocation passed, but the exact dropped-item branch still needs gameplay coverage.
- [Floor claims](CONSTRUCTION-FLOOR-CLAIMS-2026-09-27.md): native blueprint eligibility and generated floor jobs agree on competing cell claims.
- [Prepared floor continuation](PREPARED-FLOOR-CONTINUATION-2026-09-27.md): retain a numeric blueprint identity, validate using the original native Thing scanner and preserve the original queued job. Legacy missing identity safely falls back to normal selection. Native tests cover capture, refresh, claim ownership and lifecycle; they are not a running-game persistence test.

## Latest gameplay evidence

Floor retest `7df8ceae` plus `6FUb8cMPpp.mp4`: all three prepared floor jobs refresh and become native current jobs; the recording shows floor removal and subsequent building. No earlier refresh rejection or blueprint no-job warning recurs. Thirty apparel and one weapon step succeed; four restorations complete. [Full local audit](C:/GitHub/AutomaticOutfitManager/.codex-audit/floor-7df8ceae/AUDIT.md).

Small-map regression `0ca0fa74`: 162 apparel and 18 weapon successes, zero failed gear steps, 24 completed restorations, five meal handoffs with native current Ingest, and successful tattered-personal-apparel replacement. No exception or rapid-job warning. Jonah's brief repeated idle handoff resolves without renewed dressing. This excerpt contains no floor work and no matching end save. [Full local audit](C:/GitHub/AutomaticOutfitManager/.codex-audit/small-map-0ca0fa74/AUDIT.md).

## Remaining focused checks

1. Load/UI smoke for the version-bumped candidate during the maintainer's upload session.
2. Save/reload while preparing blueprint-triggered floor removal; verify exact native admission, progress and eventual restoration.
3. Reproduce the dropped-minified-building installation interruption, then verify the same install completes.

No blanket fix is claimed for crib/guest sleep loops, background sidearm/idle waits or other mods' reference errors. Older controlled route/access, contested repair and broader save/load limits remain in [0.4.4 readiness](READINESS-0.4.4.md). Publication and subscriber validation remain separate from local packaging.

## Release preflight completed

The 0.4.5 candidate builds and passes 592 focused checks (42 native floor, 24 native installation, 33 gear tracking, 134 preparation handoff, 47 boundary admission and 312 saved-gear recovery), plus three expected-failing old-decision controls. Source/XML/compiled string encoding, six XML/project files, asset constraints and maintained-document links pass. All twelve staged and installed files match. [Upload preparation](UPLOAD-PREP-0.4.5.md) records exact identities and the pending UI/gameplay limits.

## Workshop publication

The maintainer uploaded 0.4.5 on 2026-09-27. Public description and all twelve subscriber files match the audited stage; the closed-game development junction is restored. [Closeout](WORKSHOP-CLOSEOUT-0.4.5.md) records exact identities. This does not close pending gameplay checks. The corrected bold heading and BBCode list are verified on the public latest note.
