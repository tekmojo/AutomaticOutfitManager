# 0.4.1 copy and release preflight — 2026-09-07

Prepared in the authoritative RC worktree on `codex/workshop-release-prep`, preserving accumulated post-0.4.0 source changes. This preparation did not deploy, stage a package, upload, publish, commit/tag/push or launch the game.

## Changes

- Align About/project/assembly/package version expectations at **0.4.1**; retain Workshop and package identity. About remains a short gameplay description, with notices in LICENSE/NOTICE and public documentation.
- Update README, Workshop description plus separate draft change note, changelog, project design, current readiness and release checklist. Preserve the completed 0.4.0 records and screenshots as history.
- Clarify the four rule condition/quality tips, shorten the activity-list explanation, and make the saved-item storage warning explicitly refer to the storage's independent settings. Detailed log code is unchanged.
- Prepare the 0.4.1 screenshot list without editing images. Keep Choose outfits by area bullets unbolded in Workshop copy.
- Update the session-audit, debugging and release-copy skills with bounded lessons about storage, exact recovery evidence, native-control boundaries, shared snapshots and extracted test fixtures. Originals and proposed files are backed up locally.

## Build and runtime comparison

| State | Version | DLL SHA-256 |
| --- | --- | --- |
| Rebuilt RC candidate | 0.4.1 | `B8C1A1DAED38FF2AAC4BEBDA27DDED08A7466BCF60F0B2B0977677C8C5F47894` |
| Live checkout | prior gameplay build | `09BB39D2D970EB399D46D247050A74F8CFE2AE455A19630C59FD8EEC7ED94905` |
| Installed junction | prior gameplay build | `09BB39D2D970EB399D46D247050A74F8CFE2AE455A19630C59FD8EEC7ED94905` |

The Release build completed without compiler diagnostics. Relative to the before-preparation runtime manifest, only two UI source files, assembly/project version metadata, About version and the rebuilt DLL changed. Token comparison of the two UI files after removing string contents found no non-text code changes. Defs, package identity, defaults, save keys and all other gameplay sources are unchanged. Test fixture maintenance does not enter the runtime build.

## Contract validation

**3,431 positive checks across 29 suites passed.** The initial full run exposed two older fixtures missing the new native-control dependency. Their runners now compile the real `NativeRuleControl` helper; explicit surrounding fixture stubs supply the native/component boundary. Dedicated native-control tests still exercise suspension cleanup. Both affected suites passed after correction, with no production gameplay edits.

All **29 existing negative-control variants** in the affected fixture families also reproduced their intended failures, confirming that synchronization did not erase the regression assertions. These variants are separate from the positive-check total.

| Suite | Checks | Result |
| --- | ---: | --- |
| run-access-exit-contracts.ps1 | 20 | Pass |
| run-activity-contracts.ps1 | 125 | Pass |
| run-area-nesting-contracts.ps1 | 26 | Pass |
| run-area-permission-contracts.ps1 | 295 | Pass |
| run-boundary-admission-contracts.ps1 | 45 | Pass |
| run-buffer-combat-contracts.ps1 | 79 | Pass |
| run-child-access-contracts.ps1 | 30 | Pass |
| run-ingest-reservation-contracts.ps1 | 27 | Pass |
| run-locker-warning-contracts.ps1 | 39 | Pass |
| run-managed-work-candidate-contracts.ps1 | 48 | Pass |
| run-meal-handoff-contracts.ps1 | 89 | Pass |
| run-native-departure-handoff-contracts.ps1 | 47 | Pass |
| run-native-rule-control-contracts.ps1 | 187 | Pass |
| run-non-work-contracts.ps1 | 309 | Pass |
| run-non-work-ingredient-contracts.ps1 | 121 | Pass |
| run-observed-transition-contracts.ps1 | 29 | Pass |
| run-ocag-followup-contracts.ps1 | 17 | Pass |
| run-pause-behavior-contracts.ps1 | 73 | Pass |
| run-pause-cooldown-contracts.ps1 | 38 | Pass |
| run-paused-haul-contracts.ps1 | 563 | Pass |
| run-preparation-handoff-contracts.ps1 | 134 | Pass |
| run-prepared-meal-lifetime-contracts.ps1 | 233 | Pass |
| run-restoration-contracts.ps1 | 50 | Pass |
| run-return-diagnostics-contracts.ps1 | 26 | Pass |
| run-saved-gear-recovery-contracts.ps1 | 312 | Pass |
| run-session-audit-fix-contracts.ps1 | 48 | Pass |
| run-storage-contracts.ps1 | 296 | Pass |
| run-transition-activity-diagnostics-contracts.ps1 | 59 | Pass |
| run-weapon-preparation-contracts.ps1 | 66 | Pass |

## Static checks and assets

- Parsed 5 About/Defs XML files plus the project XML; checked version and stable IDs.
- Existing branded preview: 640×360, 369,895 bytes, 16:9. Both 64×64 icons retain transparency.
- All eight original 0.4.0 gallery images remain below 1,000,000 bytes each; none was modified. New captures are still pending.
- Draft Workshop description: 7,354 characters; separate 0.4.1 change note: 1,795 characters. Reviewed packaging allowlist without invoking staging.
- Verified all 85 local links across the active documentation and preserved release/readiness records. `git diff --check` passed. Historical release claims remain dated rather than treated as new verification of external pages.
- Skill frontmatter was unchanged. The three installed skill updates passed append-only content, local-reference and hash checks. The stock `quick_validate.py` was attempted but could not import PyYAML; no full YAML-parser validation is claimed, and no new dependencies were installed.

## Remaining release evidence

The maintainer confirmed the large-map robe warning disappeared after correcting storage condition. That finding is resolved without a code change. See [readiness](NEXT-UPDATE-READINESS.md) for the recent small/large-map outcomes and their limits.

Protected saved-item recovery in the actual blocked cell, mental-state save/load, inactive shared-item migration, and the rebuilt candidate's UI/load smoke remain focused checks. Gallery refresh and publication are later steps in [the release checklist](../RELEASE-CHECKLIST.md).

Local before snapshots, initial and corrected test logs, negative controls, skill backups and JSON manifests are under `C:/GitHub/AutomaticOutfitManager/work/release-prep/2026-09-07-0.4.1/`; they are excluded from package inputs.
