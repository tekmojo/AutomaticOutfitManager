# 0.4.4 release validation

The repair and nonhuman transit update is published on Steam Workshop. Public description/change note and all twelve downloaded files match the audited 0.4.4 package; the development junction is restored with the tested DLL. **Normal-play and Void provocation completion/restoration passed; focused route/access, repair/save-reload and subscriber-only gameplay checks remain open.** See [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.4.md).

DLL SHA-256: `ED72D9E73C80344960E1090FE3E46BD20973FB4D5B7E733B500A013974784609`. Source base: `6139388` plus uncommitted 0.4.4 changes. [Nonhuman routing implementation and tests](NONHUMAN-TRANSIT-0.4.4.md); [earlier repair implementation](REPAIR-COMPONENT-CLAIMS-0.4.4.md).

## Next focused gameplay tests

1. Startup has been observed on the routing build with no new patch-installation failure. Continue with the focused cases below.
2. Check a permitted mech's outside-to-outside route through an outfit-configured Work Area, then both selected-outfit and saved-personal Non-Work Areas. Repeat with an animal. Denied permissions, overlapping restrictions and applicable pause behavior must remain effective.
3. Confirm human outfit boundaries and direct bot construction deliveries remain correct.
4. Exercise contested breakdown repair with only a claimed component, then an eligible unclaimed alternative. Verify owner progress, forced repair, immediate claim release and fresh save/reload during preparation.

## Current evidence

This routing build passes 1,032 automated checks and an expected-failing previous-routing control: 153 routing/session checks, 650 category-access/pause/restoration checks, 214 native-control checks and 15 installed-native repair checks. Full limitations and manual cases are in [routing validation](NONHUMAN-TRANSIT-0.4.4.md).

Earlier repair-only build `7D7E9670FC84953BCE6C6FEE13A2A452DBC78D9420E8A69149D6C57F40CC1F61` loaded as 0.4.4 in the supplied sessions, with successful outfit transitions and no repeat of the repair-selection warning. That absence did not establish a focused contested-component pass. The subsequent mech audit reproduced excess nonhuman routing cost and motivated this build.

## First routing-build session

The bNdwQw6HUb recording and 5f506e1a attachment match this deployed build. Bots have 21 successful construction-delivery endings and five saved-weapon haul completions; humans have 50 successful gear-step endings, six completed restorations and no failed gear endings. Both locker-return rebuilds complete. Schmurda prepares for the actual void-monolith StudyInteract job, resumes it and later restores personal gear for Dining Room 1. The observed activity rows support permitted nonhuman access/transit; a controlled selected-mech shortest-route comparison and separate Non-Work mode tests remain pending. Thirteen recurring Arakis crib warnings remain.

[Full audit](C:/GitHub/AutomaticOutfitManager/.codex-audit/nonhuman-retest-5f506e1a/AUDIT.md). No new code or deployment resulted from this audit.

## Void provocation ritual session

The OqjNMFy6BA recording and 22402f69 attachment establish an Anomaly psychic-ritual completion on the deployed routing build. Zendaya, Lodewijk, Fausto, MJ and JL prepare, visibly participate, and complete saved-outfit restoration. The success letter confirms Void provocation finished. Staring remains at buffer 0/1 without premature returns; subsequent eligible jobs or Non-Work entry trigger normal cleanup. The whole excerpt has 47 successful gear endings, zero failed gear endings and eight completed restorations, including background pawns.

Nine save-reference warnings map to relationship/social records, VEF storyteller tracking, Vanilla Traits Expanded tracking and an unfinished sculpture; the matched missing IDs are absent from AOM state. Gonzalez's separate repeated idle waits after rejected automatic sidearm proposals remain a focused compatibility concern, with later work progress observed. The locker-storage warning does not block the five observed returns.

This is a normal-completion psychic-ritual pass, not Ideology ceremony, child attendance, cancellation, fresh save/reload or later entity-encounter coverage. [Full ritual audit](C:/GitHub/AutomaticOutfitManager/.codex-audit/ritual-22402f69/AUDIT.md). No source change or deployment resulted from this audit.

## Retained limits

Further Anomaly testing and fixes remain open for the next update. Monolith StudyInteract preparation/admission and later restoration, plus five-adult Void provocation completion and restoration, have now been observed. Other Anomaly jobs, fresh save/reload, broader ceremony/meal/mental-state/gravship cases and real route timings remain open. The Arakis crib loop, later Hospitality guest sleep loops and other mods' save-reference errors are not fixed here. Historical evidence remains in [0.4.3 final readiness](READINESS-0.4.3-FINAL-BUGFIX.md).
