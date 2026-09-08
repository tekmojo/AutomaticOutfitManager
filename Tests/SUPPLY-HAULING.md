# Supply hauling expansion — 2026-09-07

User authorized applying the recommended expansion "for hauling purposes only," then explicitly authorized deployment with "good to deploy." The verified candidate is now installed locally.

## Behavior

The earlier extra exception recognized only HaulToContainer deliveries to frames/blueprints and excluded apparel/weapons. The additional permission now recognizes native HaulToCell and HaulToContainer transport of any item category member: food, fuel, ingredients, apparel, weapons, and other supplies. The native hauling driver must still be present; a replaced driver is not assumed to perform transport only.

All pickup targets must remain valid on the pawn's map. All recipients using this extra exception must be outside the paused rule, including queued destinations and multi-cell footprints. Work and Non-Work source areas retain their own Hauling permission, child access and outfit policy. PPE entry checks, native/saved ownership reservations, storage acceptance, exact continuation ownership and buffer accounting remain in force. Exterior locker routes still avoid unnecessary protected crossings.

Ordinary permitted hauling to interior storage keeps its existing behavior. Construction material deliveries into a paused area, including an interior recipient hidden in a mixed queue or footprint, remain blocked.

## Transport versus supply use

This change does not grant a whole cooking, refueling, rearming, repair, meal or outfit-use job permission to run merely because it carries something. Familiar supply-use jobs and refueling drivers follow Activities access even when a mod or the native turret work giver assigns Hauling priority. Work priority and existing display classification are preserved.

The change does not synthesize new ingredient/fuel hauls or split a DoBill/Refuel/repair job into transport and work phases. It permits native hauling already chosen by RimWorld. Unknown combined mod job types are not declared transport-only from their item targets. Existing normally classified mod hauling retains its behavior.

## Scanner consistency

The prior generic pause predicate could reject a general hauler's source item before the native job existed. Both HasJobOnX and JobOnX now share a scanner-aware pause decision. General haulers can scan source items under Hauling access, including Complex Jobs FSFHauling and a reclassified native general-haul scanner. Building/frame worksites cannot borrow this pickup exception. Refuel scanners use Activities permission, including the native Hauling-priority turret variant. Explicit forced queries keep native behavior.

## Verification

1,522 checks passed across paused hauling (302), preparation handoff (134), storage (279), Non-Work (178), area permissions (295), pause behavior (69), work candidates (48), boundary admission (42), activity classification (125), child access (30), and access exit (20).

The paused-haul fixture executes production decisions, full pending-job viability, boundary permission, footprint matching, ownership transfer, and scanner HasJob/JobOnThing postfixes. Real Harmony runs those postfixes around a native-shaped scanner body and the existing queue-parent/finalizer body. New sequences cover cell/container transfers of multiple supply types, saved-item reservation denial, queued source/destination validation, moved targets, a single continuation through PPE, and paired scanner results. Fourteen independent negative controls reproduce former decisions, including the construction-only restriction, supply-use leakage, early scanner blocking, refuel scanner permission, and anchor-only recipient checks. Five existing pause negative controls also pass by failing their old decisions.

The native game definitions confirm that RearmTurret/Refuel share JobDriver_Refuel and their Atomic variants share JobDriver_RefuelAtomic. Build against installed RimWorld assemblies and git diff --check passed. Fixtures use controlled game objects/path results, so live gameplay validation is still required.

## Candidate

RC: `C:/GitHub/AutomaticOutfitManager/work/radiation-any-weapon`, branch `codex/workshop-release-prep`, baseline HEAD `76821a1607128ddec2b635c15d35081380128482`; pre-existing dirty/untracked work preserved.

Built candidate SHA-256:

`0D58D4850FC4F525E3E328E4E47046C6DE2346B9A43ADC5F65256730E18EFB31`

Previous installed/live runtime:

`7689736D43A4BF15562819014819C1FA78E499D53BCC169750E7CBF87B46825B`

Deployment completed with RimWorld closed and the installed mod junction verified against `C:/GitHub/AutomaticOutfitManager`. Only the runtime DLL was copied. Candidate, live, and installed hashes all match `0D58D4850FC4F525E3E328E4E47046C6DE2346B9A43ADC5F65256730E18EFB31`.

No game launch, source synchronization, commit, push, or Workshop update was performed. Live gameplay validation remains pending.

## Focused manual check after deployment

1. Pause a protected Work Area with Hauling allowed. Have ordinary haulers move food, fuel, ingredients and permitted outfit stock to exterior storage. Expect PPE only for actual entry, one preparation per owned haul, then completed delivery.
2. Repeat an exterior supply transfer from a paused Non-Work area. Its own outfit and removal choices still apply.
3. Keep exterior locker-only transport outside protected areas when a route exists. Check that successful-task counts remain accurate through brief native waits.
4. Try interior construction delivery, a queued/large recipient overlapping the pause, and automatic refueling/rearming or crafting inside the pause. Those activities must remain blocked.
5. Revoke Hauling/Children or issue Recall during preparation. Verify that source selection and job creation agree, saved-item ownership is preserved, and no scanner mismatch or repeated outfit restart occurs.
