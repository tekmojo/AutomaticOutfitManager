# Nonhuman Work and Non-Work transit — 0.4.4

## Fix

The September mech audit reproduced a permitted nonhuman outside haul receiving a 10,000-per-cell AOM avoidance cost only because the crossed area selected human clothes. The path restriction selector now skips that outfit-only cost when the exact activity is allowed and RaceProps.Humanlike is false. Both Work and Non-Work rules are covered, including selected gear and saved-personal Non-Work behavior. Humanlike robots remain subject to outfit requirements.

This changes only Source/Patches/ProtectedPathAvoidance.cs. Activities/Hauling/Wandering permissions are checked before the new exemption; existing pause policy, overlapping denied areas, native pathing, safe egress and owned outfit-transition routing remain intact. Ordinary permitted hauling/wandering and the existing nursing exception retain their established pause behavior. No save data or defaults change.

## Verification

- 153 session-audit fixture checks: 80 new Work/Non-Work, mech/animal, clothing/weapon, empty/saved-personal, denied/overlapping, pause, egress, direct-delivery and humanlike-control cases, plus 73 prior checks.
- Negative control: run-session-audit-fix-contracts.ps1 -PreviousNonhuman removes only the new exemption and fails the permitted-nonhuman-transit expectation.
- 650 paused hauling/restoration checks, including 48 new cases combining the real category-boundary permission method with the actual restriction selector, plus updated nursing routing expectations and denied-area controls.
- 214 native rule-control checks preserve human next-cell boundaries, emergency/mental suspension and admission.
- 15 installed-native repair checks pass against the new candidate, retaining the earlier 0.4.4 repair fix.
- Total this build: **1,032 passing checks**, plus the expected-failing old-decision control. Clean build; diff/encoding checks pass. Encoding scan: 115 files, 1,583 compiled strings, zero findings.

The routing fixtures execute the production restriction selector with controlled map/job inputs; the access suite also executes production category-boundary decisions. Native path search/grid allocation is not exercised by these fixtures. Real route shape, travel time and UI still require manual gameplay observation.

## Runtime

Candidate DLL SHA-256: `ED72D9E73C80344960E1090FE3E46BD20973FB4D5B7E733B500A013974784609`. Version 0.4.4; based on 6139388 plus intentional uncommitted candidate changes. The earlier repair-only DLL is retained under `.codex-audit/nonhuman-transit-fix-0.4.4/AutomaticOutfitManager-before.dll` in the live repository.

Local deployment status and gameplay gates are recorded in [current readiness](NEXT-UPDATE-READINESS.md). No Workshop/GitHub publication or automatic game launch is included.

## Manual test

1. Start RimWorld manually and verify 0.4.4 loads cleanly.
2. Select a mech hauling between outside targets on opposite sides of an allowed Work Area with human apparel requirements. Confirm its normal route can cross the area.
3. Repeat through a Non-Work Area with selected clothes, and through one configured to restore saved personal outfits. Repeat with an animal.
4. Deny the relevant activity and verify the same pawn avoids or cannot enter. Check an overlapping denied area and applicable pause restrictions.
5. Confirm a human still obeys outfit/locker rules and a direct mech construction delivery completes normally.

Initial normal-play smoke is now observed in bNdwQw6HUb / 5f506e1a: useful bot deliveries and human monolith preparation/restoration continue. This does not close all focused route/access cases above; see [current evidence](NEXT-UPDATE-READINESS.md). Separate crib and Hospitality guest-sleep errors remain unresolved.
