# Testing and audit records

[Workshop 0.4.0 closeout](WORKSHOP-CLOSEOUT-0.4.0.md) records public copy/gallery verification, the exact downloaded package and restored local junction. [GitHub closeout](GITHUB-CLOSEOUT-0.4.0.md) records the completed release, tag and verified assets/gallery.

Use [NEXT-UPDATE-READINESS.md](NEXT-UPDATE-READINESS.md) and the [release checklist](../RELEASE-CHECKLIST.md) for the current 0.4.0 candidate. [RELEASE-COPY-AUDIT-0.4.0.md](RELEASE-COPY-AUDIT-0.4.0.md) covers this copy/inspection build, its checks and the distinction from the last gameplay-tested DLL.

Files in this directory include automated C# contract fixtures, their `run-*-contracts.ps1` runners and dated implementation/manual test notes. A note's candidate hash and observation date define its evidence. Older labels, color choices, pending fixes and pass counts describe that stage, not necessarily today's candidate. [Historical readiness](NEXT-UPDATE-HISTORY-2026-09-06.md) retains the chronological troubleshooting; later fixes supersede earlier open findings.

Contract checks do not replace native in-game completion, compatibility, save/load or visual layout checks. Run the suites relevant to a change, broaden when new failures or risks justify it, and record build/deployed/tested states separately. The copy pass initially reran 486 focused checks; subsequent package preflight reran **all 1,954 checks in 21 suites successfully** on the current candidate. [Release package validation](RELEASE-PACKAGE-0.4.0.md) records that result and the maintainer's later gameplay confirmation.

The concise player guide is in [README.md](../README.md). State-machine and compatibility details belong in [PROJECT-DESIGN.md](../PROJECT-DESIGN.md) and Detailed logs. Updated screenshots follow [the capture plan](../Screenshots/README.md).
