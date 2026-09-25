# Checkbox-only child access

Humanlike pawns in the Child developmental stage use the existing Allow Children checkbox for each enabled area. Checked permits ordinary activity, hauling, wandering and transit regardless of adult group permissions or area pause. An unchecked overlapping area still denies entry. Disabled rules do not restrict children.

Children do not prepare adult rule outfits, satisfy clothing or weapon standards, switch into saved Non-Work outfits, or acquire outfit task buffers. The ritual preparation gate does not wait for an allowed child to dress. Native job/age eligibility, reservations, emergencies, explicit player control, babies and carried pawns retain their established behavior. This does not change apparel definitions or permit borrowing someone else's saved gear.

The existing `allowChildWorkWatching` save key and default-off value are unchanged. On the runtime enforcement pass, a legacy child outfit session requests the normal recall/restoration path with its saved belongings retained. Old Non-Work buffers are removed. Old gear/access rejection entries expire when their rule allows that child, without resetting adult shortages.

## Automated coverage

- `run-child-access-contracts.ps1`: life-stage, humanlike, enabled and checkbox scope.
- `run-non-work-contracts.ps1`: actual evaluator bypasses an adult-only robe, clothing/weapon plans and Non-Work conflicts; adults retain requirements. `-PreviousChildEvaluator <prior RuleEvaluator.cs>` must fail the new robe assertion.
- `run-paused-haul-contracts.ps1`: actual access decisions, native-shaped scanner bodies with Harmony callbacks, pause, hauling/activity/wandering, native reservation refusal and independently denied overlapping transit.
- `run-pause-cooldown-contracts.ps1`: actual rejection registry immediately releases an allowed child's old block.
- `run-meal-handoff-contracts.ps1`: native child dining destination remains unchanged; child buffers are neither created nor retained.
- Ritual, preparation, saved-gear recovery, access-exit, area-permission, pause and unavailable Non-Work suites cover adjacent behavior.

## In-game checks still required

1. Repeat the recorded ceremony with Allow Children on and the adult-only robe selected. Children should attend when Ideology allows, wear their existing clothes, consume no ceremony outfit stock and create no outfit hold. Adults should still dress before the hold releases.
2. Try meals, rest, learning, hauling and wandering with adult permissions off and with the area paused. Checked children should continue native activity; unchecked children should use the existing safe-exit behavior.
3. Overlap checked and unchecked areas, including a route through the unchecked area. Check the actual doorway as well as the selected job target.
4. Load an older save containing a preparing/active child outfit session. Verify a single normal return restores saved clothing/weapon and releases claims; repeat while returning/restoring and during native emergency suspension.
5. Toggle the checkbox after an outfit shortage, save/reload, and verify new rules still default off. Verify a pawn growing into the Adult stage resumes adult rule behavior.

The child behavior was deployed and its allowed-child role-ceremony case passed in qZ3tO7luJB. Full return, permission-edge and loaded legacy-session checks above remain pending. See NEXT-UPDATE-READINESS.md for the exact tested hash; later copy/version candidates are separate builds.
