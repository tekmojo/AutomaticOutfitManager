# Deferred follow-up after 0.4.6

On 2026-10-02 the maintainer requested release closeout and chose to troubleshoot remaining findings in a later patch. These items are deferred, not fixed or passed. This list does not authorize implementation or game/save changes.

1. **Blocked exact gear and capacity.** [Ocag investigation](OCAG-BLOCKED-VEST-AUDIT-2026-10-02.md): vest `Apparel_FlakVest1659117` became inaccessible inside an unrelated protected area; no accepting owner-accessible recovery destination was found. Establish how it moved before attributing a locality regression. Consider safe unzoned-ground recovery, preserving filters, ownership, claims and access, only after implementation is requested. Test a blocked item with free accepting storage, then full storage. The [later pass](SMALL-MAP-RETEST-AUDIT-2026-10-02.md) kept Ocag's 51% sash and gave the vest to Bowman, so it does not close this case.
2. **Persistence.** Load a copy of the preserved large-colony autosave to check Arakis's downed suspension and MJ's return. Two native family relationships reference an absent pawn; no missing AOM item reference was found. Keep this separate from the small map. Verify saved locker associations and exact item continuity across a fresh save/reload; no such live result is claimed.
3. **Hospitality arrival continuation.** Reproduce the original guest-arrival interruption with the same kind of task. Other guest groups and ordinary colonist transit do not exercise it. See [guest audit](RETAINED-GEAR-GUEST-AUDIT-2026-10-01.md).
4. **Mod-load compatibility.** The small-map log reports missing `Fortified.MapComponent_ModificationIndex` and 12 armor PostLoadInit null exceptions, including a LayeredApparel/GW4K callback stack. Investigate separately; an AOM cause is not established.
5. **Distribution/UI checks.** Subscriber files match the stage; subscriber-only gameplay, independent 0.4.6 About-card capture and revised downed wording during live suspension remain unverified.
6. **Inherited limits.** Retain dropped-installation recovery, save/reload during floor preparation and older route/access/repair cases from [0.4.5 readiness](READINESS-0.4.5.md). Normal work does not close those cases.

No further broad session is needed merely to repeat normal-flow passes. Choose an exact outstanding scenario and preserve a copy before changing its conditions.
