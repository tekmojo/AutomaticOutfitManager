# Additional child-access test audit — 2026-09-25

**Verdict: clean for the observed Turoksen delivery and subsequent ordinary activity. No new AOM fault demonstrated.** The broader modded-session log still contains faction-relation errors and a Dubs Bad Hygiene exception.

## Evidence

- Recording: `C:\Users\tekmojo\Documents\ShareX_HDR\ShareX\Screenshots\2026-09\RKbtGCQ9Gy.mp4`; duration 313.0667 seconds, 4128 × 1886, 30 fps.
- Attachment: `C:\Users\tekmojo\.codex\attachments\b604830e-bb18-4300-88e0-7f2f0e04bf60\Pasted text.txt`; 4,986 lines, 177 AOM messages.
- Attachment SHA-256: `8D52D2CB925D21EA944194458EAFCA4A9B972EF53E1B05651B6696C6EC355C3E`.
- RC, live and installed DLLs independently checked during this audit: all `85AD264B6CF071229DEFE128B6342D460776C37E7D5A6ED6CAFC9C85BB861D04`.
- RimWorld PID 12244, started 15:52:19, remains running. No game control, source edit, build, deployment, save edit, or version change performed. Version remains 0.4.3.
- This is a separate replay/excerpt: construction IDs and outcomes differ from the previous attachment, despite a similar opening and baseline. Do not add the two logs together as one continuous session. This excerpt has no startup/load header, so precise load identity and loaded-module fingerprint are not independently established by it.

## Turoksen: material delivered, followed by normal work

The same job #75901643 is proposed at line 2392 and accepted at 2410. It requests one prefab concrete for `Frame_GW_ReinforcedWall3547601` at (201, 99), which initially holds nine concrete. At line 3503 the ending is **Incompletable**, with the one-piece source consumed/destroyed, nothing carried, and the same frame containing ten concrete.

This is evidence that the material reached the receiver, not that access blocked delivery or the material vanished. The stack follows the native driver's toil/end checks; it does not show the previous runtime activity-enforcement cancellation. Preserve the actual Incompletable result in counts; do not upgrade it to Succeeded or assign successful buffer credit.

Video at 2:55 and 3:10 shows the concrete haul. By 3:20 Turoksen is **Cleaning dirt** inside Anomaly, then later moves outside the area and through the farm/animal-building surroundings (4:25). This is progress through different activities, not an observed Standing or delivery retry loop.

A subsequent wall-lamp delivery proposal #75910993 (3528) has no matching accepted or ending event for Turoksen. HaulingBot 4 accepts that target at 3600 and finishes at 4296. A proposal alone does not prove native admission, an AOM cancellation, or successful delivery by Turoksen.

## Allow Children timing

- At 0:30 Anomaly visibly allows children and has no required outfit.
- At 4:40 Allow Children is still on.
- At 4:44 it is off; Turoksen has already left Anomaly in the preceding inspected frames.
- There are no child boundary-stop messages in this log. The late toggle therefore must not be treated as the cause of the earlier construction ending, nor as a new inside-child egress pass.
- The checkbox is scrolled out of view during much of the work. Inspected UI states and actions support allowed-child operation, not continuous instrumented setting history.

This does **not** establish a delivery with Allow Children held off from pickup through completion. That narrower case remains unexercised by the observed sequence.

## Other construction and outfit evidence

- **18 Succeeded, 12 Incompletable, zero InterruptForced and zero ErroredPather** construction endings.
- Nine Incompletable endings show consumed sources and live frames containing material; three show destroyed blueprint recipients. No new repeated AOM access-cancellation pattern is established.
- Loto's steel delivery #75895306 is accepted at 1329 and appears in the video around 1:50. No matching ending appears in the attachment. Its full outcome remains ungraded rather than inferred from admission alone.
- Gonzo advances buffer 1/2 to 2/2 (2875, 3295), returns temporary gear, restores four apparel steps and the exact saved autogun, and clears the snapshot at 4157. The final apparel callback at 4181 reports state None and an empty queue. There are no failed outfit steps, preparation/restoration rebuilds, or later restoration restarts in this excerpt. Three short waits continue the existing queue with intervening progress; they are not a loop.
- Saved-weapon ownership remains protected against automatic bills and secondary re-equipping. Zendaya's safe baby placement ends Succeeded at 3167 without extra buffer credit.
- Ordinary construction continues in the access-only Anomaly area. No rapid-job warning, reservation-failure family, or unresolved load reference was found in this excerpt.

## Separate session concerns

**Faction/save state:** 51 messages report `Faction GooseButter has null relation with The Adeptus Astartes. Returning dummy relation.` This remains repeated error spam. The log does not identify the creator of the missing relation or demonstrate AOM causation. No CPU/TPS estimate is inferred from counts.

**Dubs Bad Hygiene:** Sullivan, not Rodion in this replay, gets the same toilet-driver NullReferenceException at toil 3 (1768–1772). Job #75895558 targets `ToiletAdvStuff1357697`; the throwing frame is `DubsBadHygiene.JobDriver_UseToilet.<MakeNewToils>b__1_1`, source line 73. Across the supplied tests, the family now affects more than one pawn and toilet. This merits a separate investigation; the throwing frame alone does not establish the underlying cause or exclude another mod's interaction.

Three already-assigned-bed messages and one SpeakUp repeated-reply warning also appear. None independently establishes an AOM work/outfit loop.

## Audit limits and disposition

No further child-access fix is supported by this test. Current evidence supports the observed allowed-child movement, material delivery, cleaning and ordinary hauling, with a later independent disable toggle. It does not certify all routes, the earlier exact cooler job, held-off exterior delivery, save/reload continuity, or complete DLC behavior.

The anchored current-tick window is 51501561–51503457, containing only 32 AOM messages; 145 AOM messages fall outside it. Counts above describe the whole excerpt and are not converted to rates over that shorter interval.

Evidence artifacts: `C:/GitHub/AutomaticOutfitManager/.codex-audit/child-final-test-2026-09-25/` contains the summary, extracted events and inspected frames. No production files changed.
