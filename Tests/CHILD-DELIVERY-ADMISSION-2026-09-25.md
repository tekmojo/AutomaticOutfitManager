# Child delivery admission and runtime agreement

Version 0.4.3. Built, verified, and locally deployed with RimWorld closed; RC/live/installed hashes match. Subsequent in-game tests are recorded in [final session evidence](FINAL-BUGFIX-SESSIONS-2026-09-25.md): allowed-child delivery, intentional toggle-driven exit and saved-outfit restoration are observed; held-off exterior delivery remains pending. No save-format, settings, or UI changes.

Candidate SHA-256: `85AD264B6CF071229DEFE128B6342D460776C37E7D5A6ED6CAFC9C85BB861D04`.
Previous deployed SHA-256: `5027B8AECF741427081B47BA8F33001D895B76B774566E6C1A36608407F96163`.

## Evidence and correction

The two September 25 child-access videos confirm entry while allowed and safe exit after disallowing, with no recurrence of ErroredPather. They also show accepted child deliveries cancelled after pickup. In RfVjGSFAV2, Loto's second job (#75897066, log lines 3437–3473) is newly proposed after the access-stop event, carries 29 steel, then is cancelled by the activity restriction branch. It has one cooler recipient and no recipient queue. These remain distinct from stopping Turoksen's previously selected interior job when access changes.

The previous construction helper used the native exact adjacent-cell picker at selection and movement, while DeniedActivityRule used generic Touch/material-route checks at admission and periodic enforcement. The checks could disagree after pickup. Native LocalTargetInfo.Cell was inspected: it already returns PositionHeld; this fix does not claim an invalid carried-item coordinate bug.

The scanner and activity route check now share the exact construction approach decision for managed, non-forced child boundary deliveries. The helper validates remaining spawned material pickups and approaches from their cells, skips the already-carried source, checks current and queued construction recipients, and preserves the native two-pass picker and footprint fallback. Runtime movement continues to use that picker from the pawn's actual position. Existing direct target/occupancy checks remain ahead of the shared route result, preserving protected sources, inside-child exit, and overlapping restrictions.

No saved job, persistent route result, new work generator or retry timer is introduced. Detailed rejection messages identify job, route leg, target, route origin, carried item and restricted areas. Individually blocking rules are identified when possible; a combination-only denial retains a representative rule. Allowed children, adult/mech/animal behavior, forced orders and native safety control remain outside this helper.

## Verification

| Check | Passed |
|---|---:|
| Production child scanner/picker + extracted production DeniedActivityRule integration | 83 |
| Installed native picker / patch binding / native fallback execution | 5 |
| Paused haul and restoration contracts | 600 |
| Pause behavior contracts | 73 |
| Child access contracts | 54 |
| Routing/construction/session contracts | 73 |
| Total | 888 |

The new negative control removes the shared activity-route decision, retains the new scanner, and fails specifically when runtime rejects an otherwise legal carried-phase delivery. The prior unfiltered scanner negative control also fails as expected. Native map/path outcomes are controlled sinks in the integration fixtures; the native probe executes the real picker with substituted map enumeration. This is not a full native-map or gameplay pass.

Two broader fixtures were updated with an explicit out-of-scope adapter for the child-boundary helper; child integration executes the actual helper separately. Build uses the repository build.ps1 against installed RimWorld 1.6. git diff --check passed before deployment. Existing source edits and published Workshop state remain separate.

## Short in-game retest

1. With children off, let Loto/Turoksen deliver to the same perimeter cooler or door with an accessible exterior approach. Confirm material delivery without a pickup-then-InterruptForced sequence.
2. With children off and only an interior approach, confirm rejection before pickup. Include an additional source stack and queued recipient.
3. Allow children, start interior work, then disable access. Confirm safe exit and no repeated re-admission of the blocked job.
4. Keep ordinary adults/robots working and verify no-outfit Anomaly remains without equipment preparation.

Local build/deployment does not publish the update. Retain version 0.4.3 and use the in-game Mods menu for any separately requested Workshop upload.
