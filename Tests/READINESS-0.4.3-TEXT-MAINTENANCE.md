# 0.4.3 maintenance readiness

The September 25 published maintenance update corrects presentation only: Task Buffer minus sign, concise About description and adult/child wording for selected Non-Work outfits. Version remains 0.4.3. No gameplay logic or saved data changes. Use the [release checklist](../RELEASE-CHECKLIST.md) for the in-game Mods-menu upload workflow and [maintenance preflight](MAINTENANCE-PREFLIGHT-0.4.3.md) for build evidence.

| State | DLL SHA-256 |
|---|---|
| Published maintenance; stage, subscriber and installed hashes match; About card observed | `4CA32704045B015AB50A6FA20AFA236A72E717E90DDF812F785830CB1A0E9B32` |
| Prior published/subscriber baseline audited September 25 | `FB5F15960E286091A24CFB50E39989C5C1BDDDCA706E17924E17F7CDE7AA4BA9` |

## September 25 session evidence

The supplied 3,927-line excerpt has 108 AOM messages matching the full process log's later `GooseButter_03_ALL_MODS` load. Current-tick anchors span 51,333,900–51,343,237; the newly saved `GooseButter_ANOM_ALL_MODS` is at 51,343,736. The earlier `GooseButter_04_ALL_MODS` load is a separate interval. Saving the new file is not a demonstrated reload.

- Oto, Schmurda and Gonzalez complete personal restoration. Four weapon and 14 apparel step endings succeed, with no failed outfit endings. Two isolated locker recoveries and seven wait continuations resolve; no repeated post-completion restoration is shown.
- Schmurda's exact saved weapon is released from another pawn's inventory, hauled and successfully restored. Gonzalez receives buffer credit after successful deliveries and adopts a replacement robe only after successful wear.
- Moto finishes Radiation Zone preparation and resumes the exact Rimatomics machining-table DoBill. Actual bill completion and Moto's later full return are not established.
- Arakis has 12 rapid-job warnings for the same crib. The end save shows a 10.69-year-old still assigned that crib, with no AOM outfit state; pawn and crib endpoints are outside configured areas. Test a normal-bed assignment before attributing cause. This candidate does not fix or conceal that loop.
- Full-log compatibility concerns include a missing Fortified map component, 39 apparel initialization error headlines with expanded traces into Layered Apparel, and a missing trench component type. These are not represented as AOM fixes.
- The saved Anomaly Work rule has no selected gear. Anomaly study, psychic rituals, pit travel and new-save reload have not been demonstrated by this excerpt. Ideology gathering coverage does not establish Anomaly psychic-ritual coverage.

Detailed evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/anomaly-text-2026-09-25/AUDIT.md`. These observations apply to the baseline DLL, not newly repeated candidate gameplay tests.

## Candidate checks and carry-forward coverage

Build and whole compiled-text verification apply to the candidate. The supplied Mods-menu screenshot verifies the concise About card, title, preview and version. Task Buffer controls for both rule kinds, the selected-outfit tooltip and one normal restoration remain unverified in-game. No broad gameplay-suite rerun is claimed for a string-only edit.

The [published 0.4.3 readiness](READINESS-0.4.3-PUBLISHED.md) preserves child/role-ceremony gathering and earlier automated checks. Full post-ceremony returns, child exit/overlap/pause edges, another ritual type, fresh preparation save/load, cancellation and legacy child-session restoration remain distinct follow-ups. [0.4.2 readiness](READINESS-0.4.2.md) retains older meal/bill, mental-state save/load and snapshot-cleanup cases; this session does not close them.

The [maintenance deployment](MAINTENANCE-DEPLOYMENT-0.4.3.md) verifies matching candidate/live/staged/subscriber/installed hashes, the public maintenance note, manifest 2204596392603111389 and restoration of the live junction. Additional UI/gameplay smoke remains unverified. See [maintenance closeout](MAINTENANCE-CLOSEOUT-0.4.3.md) for source and download records.
