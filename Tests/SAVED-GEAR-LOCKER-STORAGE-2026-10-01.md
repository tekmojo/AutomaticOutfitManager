# Pass-through locker storage regression

Built correction, locally deployed with explicit user approval on October 1, 2026. In-game verification remains pending. Candidate SHA-256: `FE68E11C610EE7B4DABB4BF65BC5D1F8172B436AFEB4F0FF795263FB4DBEBB2E`.

## Observed failure

Video `KtAehdbFN3.mp4` (324.933 seconds, 3812 x 1666) shows Gonzo's builder's jacket on the Radiation locker floor at 1:30 and an apparel locker containing only one cloth tribalwear stack out of twenty at 2:35. Saved personal gear remains loose while other stock can enter lockers. The session log is attachment `21e53d77-e0be-4d43-b66b-3bb6c7458ef7`; it does not log individual storage-cell rejection reasons. It reports native haul releases for Moto's Hellgun and Gonzo's Autogun, but those messages alone do not verify their destination or restoration completion. A VEF raid-group reference warning (`Lord_426`) is separate from this failure; no AOM exception or ten-jobs warning was found.

The matching save was copied for read-only inspection; the original was not changed. Copy SHA-256: `415817FADCBBDE392E2EEB8F2E601B0DEA91F8354B6CACDFEAEEE549F9791E7E`. Gonzo and Moto have Active states with persisted exact-item associations to the Radiation Zone rule, confirming the new locality records were saved. Local evidence, extracted log, contact sheet, exact frames and save summary are under `.codex-audit/locker-storage-21e53d77` in the live workspace.

## Root cause and correction

The deployed `E4E3E1DC498777E6A8F25CC1FFFD036782B2CDD15C36079D1457D57BAF57AC45` candidate reused `UsableGround` when validating storage cells. That requires native `GenGrid.Standable`. Expanded Storage's `ReelStorageNewLargeLocker` inherits `PassThroughOnly` from `AdaptiveStorageBase`; native Standable rejects every cell containing such furniture even though it is walkable and valid for storage. The saved-item policy therefore vetoed valid lockers after native storage acceptance and fell back to the floor. Retained shared stock bypasses this saved-personal-only check, explaining the different behavior.

`SavedGearLockerPolicy` now separates reachable in-room cells from standable loose-ground cells. Storage keeps the native acceptance/capacity check plus owner reachability, hazards and protected-route validation. Only a new loose-floor drop requires Standable. No filter, area, saved ownership, hauling priority or restoration rule is relaxed.

Retained stock remains automatic-outfit storage membership by design, independently of this regression. Storage item/condition/quality filters still decide whether that stock can enter a particular locker. Forget removes unused retained membership; it is not needed to repair this saved-personal storage bug.

## Verification

The old fixture replaced Standable with an always-true world hook and missed this distinction. The updated fixture runs the actual native Standable body against walkable PassThroughOnly locker furniture. It fails the local-storage assertion with the full previously deployed DLL and passes with the corrected candidate.

- `run-saved-locker-native-probe.ps1`: 46 checks pass, including the new native furniture distinction, local storage selection, already-stored stability, ground fallback, saved weapon behavior, forced hauling, filters, shared cleanup and XML persistence.
- `run-saved-locker-native-probe.ps1 -ExpectedShelfRegression -CandidateAssemblyDirectory <preserved-old-DLL-directory>`: expected failure with the deployed DLL confirms the regression.
- `run-saved-gear-recovery-contracts.ps1`: 312 checks pass.
- `run-storage-contracts.ps1`: 296 checks pass.
- Build, encoding scan and `git diff --check` pass.

Deployment completed with RimWorld closed and the installed junction targeting the live repository. Only the runtime DLL was copied. Candidate, live and installed SHA-256 values all match `FE68E11C610EE7B4DABB4BF65BC5D1F8172B436AFEB4F0FF795263FB4DBEBB2E`. RimWorld was not launched.

Next, verify Gonzo's saved jacket/helmet and Moto's saved apparel move into accepting Radiation lockers; then test full/rejecting storage floor fallback and a save/reload/recall. Automated native-body checks do not substitute for this test with the actual storage and hauling mods.
