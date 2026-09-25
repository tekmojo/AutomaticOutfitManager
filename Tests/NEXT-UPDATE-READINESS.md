# 0.4.3 release validation

Current scope: child access and Ideology ceremony gathering. Published 0.4.2 evidence is preserved in [READINESS-0.4.2.md](READINESS-0.4.2.md). The [release checklist](../RELEASE-CHECKLIST.md) is the current action list.

## Runtime identity

- Earlier gameplay-tested DLL: `1E9F72F40BE9CCC2A17B220C9217F23886D1C3956791200D03F431075EA562BA`. Its assembly version is 0.4.2; behavior includes the child and ritual changes.
- Historical initial Workshop 0.4.3 DLL: `149A319E8D6BDAB4494AD6727AA3607D3974DAE2BC2BDE1A6526BB18A005DF1D`. Initial subscriber verification is recorded in [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.3.md).
- Corrected public GitHub/Workshop, subscriber and live/installed DLL: `FB5F15960E286091A24CFB50E39989C5C1BDDDCA706E17924E17F7CDE7AA4BA9`. It corrects 23 garbled dash characters only. Build, package and uploaded asset checks pass; see [GitHub closeout](GITHUB-CLOSEOUT-0.4.3.md).
- Replacement Workshop manifest `8379366355831222618` and all twelve subscriber files match the corrected GitHub ZIP. With RimWorld closed, the installed junction was restored to the live repository and the corrected runtime deployed. Neither a dedicated UI smoke nor subscriber-only gameplay is established by publication.

## Observed in-game

| Scenario | Evidence and scope |
|---|---|
| qZ3tO7luJB role ceremony | At 90 seconds Arakis, Kyoto, Loto and Turoksen explicitly show Spectating while adults still prepare. Loto's tooltip identifies a child. No child outfit rejections or child entries in the wait list. At 210 seconds Arakis, Kyoto and Loto remain listed; no claim is made about Turoksen's later departure. |
| Adult gathering | Twelve newly outfitted adults resume exact SpectateCeremony jobs. Aoroto finishes the last robe at tick 51358088, then the hold releases. Ready children and the leader do not bypass the adult wait. |
| Moto's transition | Returns prior Radiation Zone gear, continues the exact saved-outfit queue through a temporary wait, clears the snapshot, then prepares the Temple outfit and joins. |
| Session health | 28 successful apparel endings, one successful weapon ending, no failed outfit endings or preparation/locker/restoration rebuilds. One completed restoration, before the ceremony. Full post-ceremony returns are not established. |
| Earlier old-build tests | 4OL1aIHIpY and 8xG3V4RAhm identify the old child wearability block despite the checkbox. They used the earlier runtime, so they are not failures of the deployed child-bypass change. |

Evidence inputs: `qZ3tO7luJB.mp4` and attachment `3b73eb36-e107-4c84-80b6-78938b12a6d9/Pasted text.txt`, GooseButter_04_ALL_MODS load at line 1527. Local audit: `C:/GitHub/AutomaticOutfitManager/.codex-audit/ideology-qZ3tO7luJB/AUDIT.md`. Log lines: Moto restoration 3830; Aoroto final Wear 4701; hold release 4723. Full reports preserve all source line references.

## Automated evidence

The deployed child candidate passed 2,078 checks across child access, Non-Work evaluator, paused hauling/restoration, pause cooldown, unavailable Non-Work, area permissions, preparation handoff, saved-gear recovery, meal/buffer, pause behavior, ritual, installed native ritual probe and access-exit suites. The prior production evaluator fails the new adult-only robe regression as expected. Runtime tests catch failures and exit without unhandled Windows crash dialogs.

These checks establish production policy/callback contracts; they do not replace in-game cancellation, newly created save/load, additional ritual types or migration of an old child gear session. The copy-only build needs proportionate UI/load validation, not repetition of every unaffected gameplay session.

## Remaining

Use the short ordered checklist in [RELEASE-CHECKLIST.md](../RELEASE-CHECKLIST.md). Keep the older meal/bill and native suspension follow-ups scoped to their original findings. Neither missing logs nor one completed restoration proves an unobserved outcome.
