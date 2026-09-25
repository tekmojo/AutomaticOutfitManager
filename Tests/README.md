# Testing and audit records

Use [current maintenance readiness](NEXT-UPDATE-READINESS.md), [maintenance preflight](MAINTENANCE-PREFLIGHT-0.4.3.md) and the [release checklist](../RELEASE-CHECKLIST.md) for the published September 25 maintenance update. Its [maintenance closeout](MAINTENANCE-CLOSEOUT-0.4.3.md) records source and download verification. The [published 0.4.3 readiness](READINESS-0.4.3-PUBLISHED.md), [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.3.md), [GitHub closeout](GITHUB-CLOSEOUT-0.4.3.md) and [initial copy preflight](RELEASE-COPY-AUDIT-0.4.3.md) remain historical evidence. [Non-Work unavailable-gear contracts](NON-WORK-UNAVAILABLE-GEAR.md) record earlier gameplay fixes and negative controls.

The [0.4.1 validation record](READINESS-0.4.1.md), [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.1.md) and [copy preflight](RELEASE-COPY-AUDIT-0.4.1.md) remain historical. Their **3,431 checks across 29 suites** belong to that release, not the current candidate.

The completed [Workshop 0.4.0 closeout](WORKSHOP-CLOSEOUT-0.4.0.md), [GitHub closeout](GITHUB-CLOSEOUT-0.4.0.md), [0.4.0 readiness](READINESS-0.4.0.md) and [release package validation](RELEASE-PACKAGE-0.4.0.md) remain historical records. The 1,954-check/21-suite result belongs to that release, not the current candidate.

Files here include automated C# contract fixtures, their `run-*-contracts.ps1` runners and dated implementation/manual test notes. A note's candidate hash and observation date define its evidence. Older labels, pending fixes and counts describe that stage. [Historical readiness](NEXT-UPDATE-HISTORY-2026-09-06.md) retains the earlier chronology.

Contract checks do not replace native completion, compatibility, save/load or visual checks. Record exact scenario and hash before closing a release gate. A normal restoration from an accessible cell does not validate protected-item recovery; a debug-ended mental state does not validate save/load while suspended.

The concise [player guide](../README.md), [project design](../PROJECT-DESIGN.md), Detailed logs and [screenshot capture plan](../Screenshots/CAPTURE-PLAN-0.4.3.md) serve different audiences. Keep diagnostic identities out of ordinary player tooltips. Run `python Tests/check-text-encoding.py` after a build to scan source/XML and the compiled user-string table; a source-only check cannot establish what was packaged.
