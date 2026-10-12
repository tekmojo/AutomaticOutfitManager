# Testing and audit records

Published Workshop maintenance: [closeout](RELEASE-CLOSEOUT-0.4.6-RIMWORLD-1.7.md), [0.4.6 RimWorld 1.7 maintenance readiness](NEXT-UPDATE-READINESS.md) and [package record](UPLOAD-PREP-0.4.6-RIMWORLD-1.7.md). The maintainer accepted current gameplay coverage; remaining focused tests are deferred. The original [0.4.6 readiness](READINESS-0.4.6.md) is archived.

Latest 1.7 test: [saved-personal Non-Work video follow-up](RIMWORLD-1.7-NON-WORK-2026-10-12.md#video-follow-up--october-12) shows ordinary eating followed by further activity, billiards and restored personal gear. Its log adds six complete restorations with no failed clothing/weapon steps. Precise boundary ordering and the earlier hauling-conflict cause remain open; selected-outfit Non-Work is deferred by the maintainer. The earlier [weapon-only, combined swaps and combined save/reload](RIMWORLD-1.7-WEAPONS-2026-10-12.md) remain healthy within their observed scope.

Upcoming game compatibility: [RimWorld 1.7 preparation](../docs/releases/RIMWORLD-1.7-PREPARATION.md), [October 12 baseline](RIMWORLD-1.7-BASELINE-2026-10-12.json) and read-only inventory helper `Get-CompatibilityBaseline.ps1`. The [new-colony apparel/locker audit and save/reload follow-up](RIMWORLD-1.7-NEW-COLONY-2026-10-12.md) are healthy within their tested scope; broader 1.7 compatibility remains pending.

Current release: [0.4.6 closeout](RELEASE-CLOSEOUT-0.4.6.md). The maintainer chose to finish closeout and [defer remaining troubleshooting](PATCH-FOLLOWUP-0.4.6.md) to a later patch. Public Workshop copy and subscriber files are verified.

Latest audit: [unattended Standing reproduction run](UNATTENDED-TWO-DAY-AUDIT-2026-10-02.md) finds no persistent Standing fault: 47 restorations, 317 successful apparel endings and 32 successful weapon endings, with all seven pawns resting at the endpoint. Hanh's jacket contention recovers automatically. Ocag keeps her sash through 49% while Lumi owns the former problem vest, so [Ocag's earlier blocked saved vest](OCAG-BLOCKED-VEST-AUDIT-2026-10-02.md) remains unresolved. The matching log anchors about 1.64 in-game days and retains separate mod-load/Research Reinvented warnings. The [earlier small-map retest](SMALL-MAP-RETEST-AUDIT-2026-10-02.md) remains separate evidence.

Current validation: [0.4.6 readiness](NEXT-UPDATE-READINESS.md), [release checklist](../docs/releases/CHECKLIST.md) and [upload preparation](UPLOAD-PREP-0.4.6.md).

Latest gameplay evidence: [full session audit, October 2](FULL-SESSION-AUDIT-2026-10-02.md) verifies Moto's ordinary pants adoption through later work/return/dining, Gonzo's original outfit return, and ritual/rescue restoration. It records a native missing-pawn autosave warning, misleading downed status text and configuration-based Anomaly weapon-storage warnings; save/reload and the earlier guest continuation case remain open.

Latest local follow-up: [Moto pants removal correction and item command polish](MOTO-PANTS-AUDIT-2026-10-01.md) updates inactive saved outfits after successful ordinary Wear and includes the requested icon/type-specific release labels. Native regression and related contracts pass; the specific pants gameplay regression now passes in the audit above.

Included in 0.4.6: [retained-item control and Hospitality guest audit](RETAINED-GEAR-GUEST-AUDIT-2026-10-01.md) adds Forget retained apparel/weapon to item selection; guest continuation compatibility remains an audit finding. The locally deployed [pass-through locker storage correction](SAVED-GEAR-LOCKER-STORAGE-2026-10-01.md) follows the [saved gear locker locality](SAVED-GEAR-LOCKER-LOCALITY-2026-10-01.md) deployment and fixes saved gear remaining on the floor beside available lockers.

Previous 0.4.5 release: [0.4.5 readiness](READINESS-0.4.5.md), [release checklist](../docs/releases/CHECKLIST.md), [borrowed-gear cleanup](BORROWED-GEAR-CLEANUP-2026-09-26.md), [installation pickup](PREPARED-INSTALL-COUNT-2026-09-26.md), [floor claims](CONSTRUCTION-FLOOR-CLAIMS-2026-09-27.md) and [floor continuation](PREPARED-FLOOR-CONTINUATION-2026-09-27.md). Native floor and installation runners include explicit previous-decision negative controls. [GitHub closeout](GITHUB-CLOSEOUT-0.4.5.md) records the source tag and verified downloads.

Published 0.4.4: [readiness](READINESS-0.4.4.md), [repair-component validation](REPAIR-COMPONENT-CLAIMS-0.4.4.md), [nonhuman transit validation](NONHUMAN-TRANSIT-0.4.4.md), [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.4.md) and [GitHub closeout](GITHUB-CLOSEOUT-0.4.4.md).

## Published 0.4.3 evidence

Use [0.4.3 final readiness](READINESS-0.4.3-FINAL-BUGFIX.md), [final preflight](FINAL-BUGFIX-PREFLIGHT-0.4.3.md), [latest gameplay evidence](FINAL-BUGFIX-SESSIONS-2026-09-25.md) and the [archived release checklist](../docs/releases/history/RELEASE-HISTORY-0.4.3-FINAL-BUGFIX.md) for the final 0.4.3 bug-fix update. [Final closeout](FINAL-BUGFIX-CLOSEOUT-0.4.3.md) records its verified Workshop publication and matching download assets. The earlier text-only update is preserved in [archived readiness](READINESS-0.4.3-TEXT-MAINTENANCE.md) and [maintenance preflight](MAINTENANCE-PREFLIGHT-0.4.3.md). Its [maintenance closeout](MAINTENANCE-CLOSEOUT-0.4.3.md) records source and download verification. The [published 0.4.3 readiness](READINESS-0.4.3-PUBLISHED.md), [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.3.md), [GitHub closeout](GITHUB-CLOSEOUT-0.4.3.md) and [initial copy preflight](RELEASE-COPY-AUDIT-0.4.3.md) remain historical evidence. [Non-Work unavailable-gear contracts](NON-WORK-UNAVAILABLE-GEAR.md) record earlier gameplay fixes and negative controls.

The [0.4.1 validation record](READINESS-0.4.1.md), [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.1.md) and [copy preflight](RELEASE-COPY-AUDIT-0.4.1.md) remain historical. Their **3,431 checks across 29 suites** belong to that release, not the current candidate.

The completed [Workshop 0.4.0 closeout](WORKSHOP-CLOSEOUT-0.4.0.md), [GitHub closeout](GITHUB-CLOSEOUT-0.4.0.md), [0.4.0 readiness](READINESS-0.4.0.md) and [release package validation](RELEASE-PACKAGE-0.4.0.md) remain historical records. The 1,954-check/21-suite result belongs to that release, not the current candidate.

Files here include automated C# contract fixtures, their `run-*-contracts.ps1` runners and dated implementation/manual test notes. A note's candidate hash and observation date define its evidence. Older labels, pending fixes and counts describe that stage. [Historical readiness](NEXT-UPDATE-HISTORY-2026-09-06.md) retains the earlier chronology.

Contract checks do not replace native completion, compatibility, save/load or visual checks. Record exact scenario and hash before closing a release gate. A normal restoration from an accessible cell does not validate protected-item recovery; a debug-ended mental state does not validate save/load while suspended.

The concise [player guide](../README.md), [project design](../PROJECT-DESIGN.md), Detailed logs and [screenshot capture plan](../Screenshots/CAPTURE-PLAN-0.4.3.md) serve different audiences. Keep diagnostic identities out of ordinary player tooltips. Run `python Tests/check-text-encoding.py` after a build to scan source/XML and the compiled user-string table; a source-only check cannot establish what was packaged.
