# Unavailable selected Non-Work gear — 2026-09-08

## Current candidate: entry remains outside until preparation

**Built and deployed; gameplay verification pending.** Current RC/live/installed DLL: `F634D86039F215FC98FD883C667701DFB66F8E990A2221AD60292CFD5ACF2FD5`. Only the DLL was copied under the existing deployment approval, with RimWorld closed and the expected junction verified. No launch occurred.

Follow-up `eN89V3kPdn.mp4` and its log exposed Juroksen/Roboto crossing after an outside boundary denial, then taking a mismatch exit. Native `EndCurrentJob(false)` leaves movement alive until another job controls it; the next movement tick could enter while the current job was null. The boundary guard now stops the denied path before job cleanup. The retained exact task still prepares and resumes normally. The saved-personal checkbox and selected gear policy are unchanged.

`run-native-rule-control-contracts.ps1` now has **214 checks**, including movement between job-tracker intervals for DoBill, hauling and meals, cleanup ordering, outside gear retrieval, equipped re-entry and mismatched outward movement. `-PreviousMovementDecision` removes the stop and reproduces the old unprotected entry. Total: **2,103 passing checks across 12 suites**, plus the focused negative control; build and diff checks pass. The map/native ordering is a controlled fixture corroborated by local assembly inspection, not an in-game pass. Full evidence is under `.codex-audit/entry-followup-2026-09-08/AUDIT.md` in the live repository.

## Previous candidate: child/adult exit follow-up

**Built and deployed with explicit authorization; awaiting gameplay verification.** Current RC, live and installed DLL SHA-256: `7AD1B85733CD5E57AFC4BDBD5006B24675FF835D15E0939E9A8C2F35B9A286CB`. RimWorld was closed; only the DLL was deployed. No game launch occurred. The historical candidate below was subsequently tested in `JTghnVJnZ9.mp4`: adult activity recovered, but Loto remained Standing with 26 occupied-area gear retries.

The maintainer clarified that an unchecked Non-Work rule must send a mismatched occupant outside even when the newly selected apparel is available. Checked saved-personal behavior keeps the existing source policy. The current change applies the exit decision before occupied-area preparation and before ordinary native job continuation. It preserves native control, active exact preparation/restoration continuations, hazard protection and carried-item placement. Existing exit jobs are admitted without being redirected into another exit.

Exit selection now probes nearby floor beyond an unusable boundary ring. Its route query omits the starting occupied rules from entry-avoidance costs while validating the complete rule list afterward; initial egress is allowed, but re-entry and unrelated protected-area shortcuts remain rejected. A failed search emits a coalesced position/rule diagnostic. The code-level search failures are reproduced by fixtures; the exact rejected native predicate from Loto's recorded run was not logged, so the room-specific gameplay outcome remains unverified.

Validation: **2,076 checks across 12 suites** (44 unavailable Non-Work, 30 child access, 295 area permission, 20 access exit, 187 native rule control, 309 Non-Work, 45 boundary admission, 134 preparation handoff, 563 paused hauling/restoration, 312 saved-gear recovery, 89 meal handoff, 48 session audit). Six negative controls fail the intended previous decisions, including the narrow exterior search, occupied-rule path penalty, and preparation before exit. The new fixture executes production occupancy, redirect, exit selection, path customization and traversal decisions, including native-style exit-job re-admission. Map/path inputs remain controlled fixtures, not a running game. Release build and `git diff --check` pass.

Next manual check: change selected apparel with an adult and child inside Dining and saved-personal default unchecked. Both should exit, including when the new adult outfit is available. Adults can prepare before returning; an incompatible child should continue eligible activities elsewhere. Repeat with the option checked and a saved personal outfit, then confirm unrelated protected storage and existing outfit restoration stay correct.

## Reproduction and diagnosis

Evidence: `d77CLVCn9g.mp4` (12:32.967, 3970 × 1848) and the supplied log excerpt (379 AOM messages). At 9:20 the video shows Dining Room 1 with **Default to Saved Personal Outfit unchecked**, **Allarus Terminator armor: 0 available**, and the yellow unavailable-apparel warning. Later footage shows Terminator power armor in storage. The maintainer reports normal activity resumed after correcting the selected apparel.

The log records Juroksen, Schmurda, Roboto and Oto stopped at the Dining boundary during DoBill, followed by attempts to leave for `(167, 0, 121)`. Juroksen subsequently remains at `(141, 0, 58)` and Roboto at `(160, 0, 57)` with repeated expired Wait jobs, no AOM state/queue and the distant locker as their path destination. The observations reach 900 stationary ticks for each. Animals also report unwearable requirements. This is a gear-availability/area routing problem; the evidence does not establish a separate defect in storage acceptance.

Two production decisions contribute:

- A failed gear search blocks inside-area targets, but an outside DoBill worksite or late-bound destination can still require entry. Without remembering the actual failed candidate and retiring its boundary continuation, native selection can keep choosing the same inaccessible task.
- Missing-gear egress prefers the configured locker using ordinary reachability. An available local exterior cell is considered only afterward, and ordinary reachability does not prove that the route avoids re-entry or other protected areas.

Requirement edits previously recalled existing AOM states without clearing shortage records for pawns rejected before a state was created.

## Change

For Non-Work rules with saved-personal preference unchecked, failed selected-gear preparation now records the concrete autonomous candidate in the existing bounded unavailable-work registry, releases its managed claim and retires its boundary retry. Native scanning can choose other tasks. This applies to apparel availability, unwearable/conflicting requirements and selected-weapon failure. It does not relax the requirement or silently choose saved gear.

Missing-gear occupants of such a rule choose a nearby exterior cell instead of a distant locker. The route is checked for initial egress with no re-entry, and against all enabled map rules so the exit cannot become a shortcut through unrelated protected storage. Already-outside pawns get no new egress trip. Existing saved-personal and ordinary Work locker preference stays unchanged.

Editing requirements clears only that rule's unavailable-work records across all pawns, including pawns without an outfit state. Native activity may reconsider the corrected selection on its next opportunity.

## Validation and build state

- Release build succeeds using the repository `build.ps1`.
- Candidate DLL SHA-256: `00B7834AD07EC58E4047F20314C5C99E3DD08C6E67CA6DB33EB6F6209EE1742D`.
- `run-unavailable-non-work-contracts.ps1`: **18 checks**. Extracts the production rejection, egress, cell selection, route traversal and edit-notification decisions, and compiles the actual unavailable-work registry. Native selection and map/path inputs are deterministic fixtures, not a running RimWorld test.
- Three negative controls: the former area-only rejection, former distant-locker preference and former edit behavior each fail their intended regression.
- Existing suites: paused hauling/restoration 563; boundary admission 45; Non-Work contracts 309; native rule control 187; preparation handoff 134; session route/UI decisions 48; saved-gear recovery 312; meal/buffer controller 89. **1,705 checks across nine suites**, plus the negative controls.
- `git diff --check` passes. Existing Git line-ending warnings are not whitespace errors.

**Built and deployed after maintainer authorization. Not yet tested in-game.** RimWorld was confirmed closed, the installed junction targeted the live repository, and rebuilding reproduced the candidate hash. Only the runtime DLL was copied. Candidate, live and installed DLLs all match SHA-256 `00B7834AD07EC58E4047F20314C5C99E3DD08C6E67CA6DB33EB6F6209EE1742D`. This replaces the previously installed release hash `B8C1A1DAED38FF2AAC4BEBDA27DDED08A7466BCF60F0B2B0977677C8C5F47894`. No game was launched; no original save, mod list or storage filter was changed.

## Focused gameplay verification

1. With saved-personal preference unchecked, select an unavailable garment while workers are inside and outside Dining. Inside workers should leave locally; outside workers should continue other reachable native tasks. A task whose actual materials/destination cannot be reached without entry must be deferred.
2. Include an outside DoBill worksite requiring an ingredient in Dining and an unrelated valid worksite. The former should yield to the latter without repeated empty waits or growing queues.
3. Correct the selection and confirm preparation and the original work resume. Repeat with a weapon requirement and with a route near unrelated protected storage.
4. Confirm checked saved-personal behavior, valid queued restoration and ordinary Work locker returns still behave normally.

The previously audited **Ocagobrei Kitchen/Dining prepared-meal loop remains a separate, unfixed issue**. This candidate targets the unchecked selected-gear shortage report and does not claim to close that earlier finding.
