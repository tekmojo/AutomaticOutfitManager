# Allow Children and robot base-travel follow-up

2026-09-05 candidate `5C6CEFCC36F7B330E2155B98B7EF243E695E3DEA4ECF405C3A35472E337EFE81`. Built, not deployed or tested in-game. All 608 checks passed.

Allow Children replaces the watching-only checkbox in Work and Non-Work rules. Its existing saved boolean/key is retained; new rules default off. The option gates ordinary activity and transit, and does not override group permissions or outfit requirements. Child checks are now consistent at scanner, thinker, StartJob, runtime and path-boundary activity checks. Existing outfits recall safely; egress and exact owning-area outfit-return permissions remain distinct from entry through unrelated restricted areas. Babies and native/player safety exceptions remain native.

Misc Robots' AIRobot_GoRecharge, AIRobot_GoDespawn and AIRobot_GoAndWait are observed Wanderers. The change is display-only, excludes actual hauling, and does not relabel robot repair or deconstruction work by broad name matching.

## Native checks pending

Deployment update (2026-09-05): User-approved candidate `5C6CEFCC36F7B330E2155B98B7EF243E695E3DEA4ECF405C3A35472E337EFE81` is now deployed. Candidate/live/installed hashes match; RimWorld remained closed. Native checks below remain pending.

1. Load checked and unchecked legacy rules; confirm the same checkbox values appear under Allow Children. Check save/reload and a new rule defaulting off.
2. In a safe room, toggle child access and observe meals, rest, learning/work watching, work, hauling, wandering and pass-through. Group permissions and outfit compatibility must still constrain allowed children.
3. Turn access off while a child is inside, then while a child has an active outfit session. Verify safe exit or normal safe outfit return, with no preparation/rejection loop.
4. Verify the child can leave an area it is barred from entering, and cannot use that exit to enter another child-disabled area. Necessary outfit transitions must not cross unrelated restricted areas without their normal permission.
5. Exercise drafting/direct orders, native emergency movement, babies being carried, and a child unable to wear required gear.
6. Observe a cleaning bot return to base/charge next to an actual hauling bot. They should appear in Wanderers and Haulers respectively, not both. Verify ordinary cleaning and idle waits remain Wanderers, and genuine production/repair work is not reclassified by a name substring.

Detailed video/log evidence and source baseline: `C:/GitHub/AutomaticOutfitManager/work/testing/2026-09-05/VjMXKcR8mx/audit.md`.
