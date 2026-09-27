# Blueprint floor-removal eligibility — 2026-09-27

Implemented and deployed after explicit user authorization, conditional on confirming the log finding. No game launch, commits, source synchronization, settings changes, or save writes.

## Confirmed failure

Input attachment `87a0cec1-3f6c-4773-89b6-bbf4aa7464b3/Pasted text.txt`: line 405 rejects JL's RemoveFloor job because Fausto owns cell (193, 0, 87); line 434 reports that blueprint `Blueprint_UCStripedFloor3772288` passed HasJobOnThing but yielded no job. JL later resumes ordinary work, so this is a selection-contract error, not a demonstrated persistent lockup.

Installed native IL confirms a separate branch: blueprint HasJobOnThing calls CanDoRemoveExistingFloorWork and returns true directly. JobOnThing instead calls RemoveExistingFloorJob, whose cell-targeted job is rejected by the existing complete-candidate filter. The scanner's blueprint Thing does not match a cell-only preparation claim. Existing shared resource/blocker generation patches do not cover that early boolean return.

## Change

`Source/Patches/ConstructionFloorClaims_Patch.cs` patches only the successful native floor-removal eligibility return. It checks the exact pending cell claim, using the same forced/drafted/downed/mental/unspawned exemptions as the existing candidate filter. The claimant remains eligible. Blocker, material-delivery, no-cost, and failed-floor-eligibility branches remain native; no additional speculative job or scan is introduced, and global Thing/cell claim matching is unchanged.

The transpiler requires exactly one matching branch and reports an explicit error if the native shape changes. A future incompatible game/mod rewrite therefore needs renewed validation.

## Validation

- Repository build succeeded; git diff --check passed (existing line-ending advisories only).
- New native fixture: 15 checks execute the installed HasJobOnThing, JobOnThing, floor-eligibility and floor-job bodies, the built production patch, existing final candidate filter, and real claim registry. Disposable engine/world services are shimmed; this is not a full running-game test.
- Negative control omits only the new patch: native eligibility says yes, while the production final candidate filter removes the job. The intended assertion fails and the runner confirms that exact failure.
- Covered: competing claimant, rightful owner, explicit orders, native-control exemptions, claim release, unrelated cell, material delivery with no removable floor, earlier blocking-thing work, hauling scanner exclusion, native reservation denial, disabled work.
- Saved-gear recovery contracts: 312 passed.
- Existing construction-child native probe: 5 passed.
- Construction-child access contracts: 83 passed.
- Previous minified-install continuation native probe: 24 passed.
- Total: 439 passing checks plus the expected negative-control failure.

## Deployment

RC: `C:\GitHub\AutomaticOutfitManager\work\radiation-any-weapon`, branch `codex/workshop-release-prep`, HEAD `9ff91a34fa047f582e5c9a9f12f0c51c4dfab2ae` plus preserved intentional changes. Only `1.6\Assemblies\AutomaticOutfitManager.dll` was copied to the live repository with RimWorld closed. Installed junction targets `C:\GitHub\AutomaticOutfitManager`.

Previous hash: `1B970A7A40978B6FBDF6E3971A68947E7323FF7DD443008D34D1B7E443FF525F`.

Verified candidate/live/installed SHA256: `8B175655323C5F608E1A2BE652F409FCFCCD9FCBA8272DDCB34FC67824E9595E`.

Built: yes. Deployed: yes. Tested in a running game: pending. Next manual test is floor replacement with competing builders while one prepares an outfit; confirm the other selects available work without the blueprint/no-job error, then both complete their work and returns.
