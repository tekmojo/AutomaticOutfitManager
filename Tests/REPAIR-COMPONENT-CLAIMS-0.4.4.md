# Repair component claims — 0.4.4 candidate

## Observed defect

In the September 25 long-session attachment, line 4819 records AOM rejecting JL's FixBrokenDownBuilding because Bracher holds Component x435 at (117, 0, 109). At 4848 native selection reports the fridge `sbz_LargeFridge861907` supplied a target but no actual job. The early AOM target check sees the building; the complete later job check sees its component stack. Native component search previously knew native reservations but not AOM's temporary claims.

## Implementation

`Detection/RepairComponentClaims.cs` wraps the native component validator with exact prepared-work claim eligibility. `Patches/RepairComponentClaims.cs` installs that wrapper inside the shared native FindClosestComponent search and scopes automatic versus forced selection around both HasJobOnThing and JobOnThing. Filtering inside candidate enumeration preserves alternate stacks. A struct saves previous thread/pawn context; a Harmony finalizer restores it after return or exceptions. No-claim calls retain the native delegate and avoid the additional wrapper closure.

Both patches join the existing deferred scanner installation after definitions initialize. The generic complete-candidate guard remains intact. Native forbidden, reservation and path decisions are preserved; owner, forced, drafted, downed and mental-state cases retain their established handling. No serialization, save-default, apparel, area, buffer or claim-lifetime policy changes were made.

## Validation

- `run-repair-component-claims.ps1`: 22 checks with production registry and policy, including alternatives, ownership, release, expiry/reset, nested/forced contexts and exception cleanup.
- `run-repair-component-native-probe.ps1`: 15 checks against the installed RimWorld assembly and built candidate. Native component gate, job construction, shared search, candidate validator and production patches execute.
- Native probe `-PreviousDecision`: omits only the new search filter, retains the same harness and production scope, and fails claimed-only early rejection as expected.
- Existing `run-managed-work-candidate-contracts.ps1`: 48 checks.
- Existing `run-native-rule-control-contracts.ps1`: 214 checks plus wiring checks.
- Existing `run-session-audit-fix-contracts.ps1`: 73 checks.

**Total: 372 checks plus one expected-failing negative control.**

The native probe controls irrelevant building eligibility and world/path enumeration, native forbidden/reservation responses, claim inputs and engine job allocation. It does not run a full colony or all third-party patches. The production claim-registry suite covers actual identity/release/expiry logic separately. No current-candidate gameplay or save/reload pass is claimed.

Built DLL SHA-256: `7D7E9670FC84953BCE6C6FEE13A2A452DBC78D9420E8A69149D6C57F40CC1F61`; assembly/file version `0.4.4.0`, product version `0.4.4`. Deployment completed with RimWorld closed: candidate, live and installed DLLs match this hash and product version. The previous 0.4.3 DLL is retained in the local audit directory. See the [release checklist](../RELEASE-CHECKLIST.md).

Evidence directory: `C:/GitHub/AutomaticOutfitManager/.codex-audit/repair-fix-0.4.4`. Baseline investigation: `C:/GitHub/AutomaticOutfitManager/.codex-audit/repair-selection-2026-09-25/INVESTIGATION.md`. The source worktree and installed build are separate states. The live About remains published 0.4.3 metadata under the DLL-only deployment contract; RC About/project/package versions are 0.4.4.

## Scope limits

This fixes the observed native breakdown-repair material selection. Arbitrary modded scanners that replace the shared finder can still hit the generic late guard. A claim changing between the two native calls is still protected by that guard. No broad claim is made about every scanner type. The Arakis crib loop, one recovered Bracher preparation gap and unrelated mod errors remain separate.
