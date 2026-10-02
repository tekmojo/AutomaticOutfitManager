# Saved personal gear stays with its locker

Unreleased RC candidate, built and locally deployed with user approval on October 1, 2026. In-game verification remains pending.

## Evidence and scope

User screenshots and observation identify saved personal gear moving from the Anomaly locker to the Radiation locker: Gonzalez's Cadian helmet and Soto's dog leather captain's coat still carry their saved-owner tags. The Anomaly shelves show spare capacity, but the screenshots do not establish every applicable filter or native reservation. The session log does not record the source and destination of those native hauls, so its exact destination-choice cause cannot be reconstructed.

The source had exact saved-item ownership and restoration recovery, but no persisted per-item locker preference during active work. Native storage selection could therefore choose another accepting locker. This candidate adds that preference, as requested, with same-room floor space ahead of remote storage.

## Behavior

- Exact saved apparel and the previous primary weapon use their originating rule's locker while Preparing or Active. Storage comes first, then suitable ground within that area. Remote storage priority does not override a usable home locker.
- A recorded item outside its locker can be hauled back to accepting local storage or empty local ground. Already valid local storage is left alone. Loose local items remain tracked without a same-cell hauling loop.
- The new item-ID to rule-ID dictionary uses value serialization. Existing saved gear references remain the identity authority. Resolving the rule's current area preserves existing gravship remapping. Old saves infer an association from the item's actual source area or a single unambiguous source locker; multiple ambiguous lockers are not guessed.
- Native hauling creation, native storage cell selection during re-search, and AOM apparel/weapon restock scans use the policy. Linked storage groups are filtered by individual destination cell. True container storage remains excluded for these protected saved items, which native Wear/Equip expect to retrieve as spawned items.
- Filters, enabled storage, native hauling eligibility, claims and owner access still apply. Player-forced hauling, active restoration, departure, missing lockers, inaccessible lockers and lack of usable local space retain their fallback behavior. A requested primary-weapon restore also releases locality while the overall outfit is Active.
- Shared work stock and rules without a locker retain their existing behavior. No settings, painted areas, or existing saves were edited.

## Automated verification

Candidate SHA-256: `E4E3E1DC498777E6A8F25CC1FFFD036782B2CDD15C36079D1457D57BAF57AC45`.

`build.ps1 -RimWorldDir F:/Steam/steamapps/common/RimWorld` succeeds.

| Runner | Result |
| --- | --- |
| `run-saved-locker-native-probe.ps1` | 45 checks pass |
| Same runner with `-PreviousDecision` | Expected failure of the locality assertion; negative control passes |
| `run-saved-gear-recovery-contracts.ps1` | 312 checks pass |
| `run-restoration-contracts.ps1` | 50 checks pass |
| `run-managed-gear-tracking-contracts.ps1` | 33 checks pass |
| `run-storage-contracts.ps1` | 296 checks pass |

The new probe loads the compiled candidate and native RimWorld assembly. It executes native WorkGiver_Haul.JobOnThing, HaulToStorageJob, native best-cell search/worker, production Harmony callbacks, and production PawnApparelState.ExposeData through native Scribe save/load (including reference resolution and post-load initialization). World services such as reachability, reservations, storage capacity and map grids are controlled fixtures. The previous-decision control omits the new patches and demonstrates the remote destination being selected. Restoration fixtures isolate the new active-locality policy so their existing recovery checks remain focused.

Follow-up verification adds 12 checks for shared work apparel and weapons through the actual restock scanners with the new saved-personal patches installed. Both still return to a selecting rule's locker, obey rejecting storage filters, stop requesting restock for disabled/no-locker rules, and leave correctly stored items alone. No runtime change was needed for this clarification. Existing recovery/locker contracts cover reservations, claims, unreachable/forbidden items and scanner access rejection. Rule condition/quality standards govern wearing selected work gear; storage's own standards govern putting it away, so cleanup does not silently strand worn-out work stock.

RimWorld 1.6 storage interfaces use default interface implementations, so this probe requires .NET 8. Its temporary host uses an already installed net8 Harmony 2.3.1.1 assembly via `-RuntimeHarmonyPath`; production still builds against installed Harmony 2.4.1. The fixture does not replace the game's Harmony files or run Unity. XML tests validate the new persisted association and missing-field compatibility, not a complete colony save or an actual gravship flight.

Deployment verified with RimWorld closed and the installed junction targeting the live repository. Only `1.6/Assemblies/AutomaticOutfitManager.dll` was copied. Candidate, live repository and installed junction all match `E4E3E1DC498777E6A8F25CC1FFFD036782B2CDD15C36079D1457D57BAF57AC45`, replacing the toolbar candidate `93C959F8B6304BDB11622630DBDA3BD8A8165508C7949DD033EADC6F894801DA`. The game was not launched.

## Pending manual verification

On a copy of the duplicate colony save, using the deployed candidate:

1. Send Gonzalez and Soto into Anomaly work. Confirm their exact saved items remain in Anomaly storage even with higher-priority accepting Radiation storage.
2. Fill or reject Anomaly storage while leaving suitable local floor space. Confirm saved gear stays on that floor and later restores to its owner. Re-enable local storage and confirm ordinary haulers put it away locally.
3. Watch a destination become full during an active haul. Confirm any re-search/delivery remains local when possible and no repeated hauling loop occurs.
4. Save/reload with one saved item stored and one on the floor, then recall both owners. Verify exact item return and no lost ownership.
5. Check a no-locker rule, an explicit forced haul, an inaccessible/deleted locker, and saved-primary restoration. Repeat with the colony's actual storage and hauling mods.

These remain gameplay gates. The automated probe does not establish completion of a Unity hauling driver or compatibility with every modded scanner.
