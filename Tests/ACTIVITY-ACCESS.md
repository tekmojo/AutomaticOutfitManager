# Activity access validation — 2026-09-05

Deployed candidate: `5A5335CF898256790FEBE0CCF584F488DB0AA5CBA2E4B8A9F5DD2C204D123516`. Candidate/live/installed hashes matched; RimWorld remained closed. No native gameplay pass is claimed.

Activities replaces the Work checkbox label without changing serialized keys or values. Meals, rest, learning, recreation and assigned work use this permission for every humanlike group. Genuine hauling is separate; idle movement and robot cleaning use Wandering. Status rows share the classifier. Guest, slave and prisoner settings remain independent columns under the same classification policy.

All 985 checks passed, including 125 production classifier checks and 295 production permission-selector checks. API doubles cover activity precedence, work-thinker context, custody/group precedence and independent checkbox masks. They do not reproduce RimWorld pathfinding, native reservations, prisoner rooms, outfit callbacks or XML load behavior.

Manual validation pending:

1. Guest Activities on / Wandering off: eat and play roulette in Dining; idle loitering should remain restricted. Reverse the toggles and test again.
2. Repeat for colonist, allowed child, slave and prisoner, in both Work and Non-Work Areas. Check apparel/primary requirements and Non-Work personal/fallback precedence.
3. Check hauling independently. Robot cleaning stays Wandering; real hauling stays Hauling; base/charging duties retain native control.
4. Toggle Activities off during a real job and during an active outfit session. Verify safe interruption/return without retry storms or extra buffer completion.
5. Confined prisoner: a toggle must not walk the pawn out through the cell door. Essential rest with no safe exit remains possible only inside the occupied rule. Native breakouts/departure retain authority.
6. Check alternative routing around another denied area, late-bound dining destinations, pause/resume, direct orders, emergencies and checkbox persistence after save/reload.

Session evidence and deployment record: `C:/GitHub/AutomaticOutfitManager/work/testing/2026-09-05/joq4YkXZVH/audit.md`.
