# Retained gear and Hospitality guest audit — 2026-10-01

## Evidence and scope

User supplied `U7v4ECCEjq.mp4` (672.033 seconds, 3774 × 1694) and log attachment `a064dbb5-35d1-4fdb-8dc4-e0e88273825d`. Log SHA-256: `64DBF49D49163E21E1C052EB1C1EC1FE765B02E530A7AFEC5888AB6F6D61A0C3`; 6,692 lines, 220 AOM messages. The log loads `GooseButter_ANOM_ALL_MODS` once. Inspection used a copy of the later `GooseButter_ANOM_ALL_MODS_02.rws`, tick 54314223, SHA-256 `0833C763FF96A379F5E28DE9EED1FEB5F8E149B59A875E94978C82BF5FDCA17E`. Original saves were not modified.

Extracted events, summary, contact sheet, selected frames, copied save and guest job fragments are in the live workspace's `.codex-audit/retained-guests-a064dbb5` directory. Session runtime was the deployed locker-storage correction `FE68E11C610EE7B4DABB4BF65BC5D1F8172B436AFEB4F0FF795263FB4DBEBB2E`.

This request authorizes an item-level retained-stock control and audits the guest behavior. No guest routing or Hospitality compatibility change is included.

## Retained stock and personal replacements

Comparing the previous audited save with the new copy, the user cleared retained catalog entries for Pants (`Apparel_Pants`), Tribalwear (`Apparel_TribalA`) and Tribal kilt (`VAE_Apparel_TribalKilt`). Four apparel types remain: Radiation mask, Radiation suit, Priest robe and Ceremonial hood. Retained membership is type-wide, independently of exact saved-item ownership. The save records membership, not its historical cause, so this audit cannot identify which earlier selection or migration retained those three types.

Source entry points include selector changes, rule cleanup and load-time catalog seeding from existing rules/managed gear. `AdoptWornPersonalApparel` does not add retained catalog entries: after a successful replacement, it calls `ForgetSavedApparel` for displaced saved items, updates the personal snapshot and registers the replacement. It preserves independently selected/retained types and any required borrowed-gear return.

This log records Human replacing a cloth tribal poncho with a rhino leather parka, and Dove replacing a cloth tribal kilt with mink pants. Their later saved snapshots contain the replacements instead of the displaced garments. There is no evidence here that those upgrades created the old retained entries, and no new automatic-upgrade cleanup change is justified by this session.

## New item control

Selecting retained apparel or a retained weapon now exposes **Forget retained apparel** or **Forget retained weapon**, including items without a saved owner. This invokes the same guarded `ForgetManagedStockDefinition` operation as the selectors. Its tooltip explicitly states that it clears the type for all matching items, preserves exact saved/borrowed gear and respects storage filters. Types still selected by a rule are not retained-only; an active outfit transition can disable forgetting with the existing reason. The action rechecks its guard when clicked.

Implementation: `Source/UI/RetainedStockCommands.cs` and the existing `ThingWithComps.GetGizmos` ownership patch in `Source/Patches/SavedApparelOwnership_Patches.cs`. Player documentation explains the type-wide effect. This is not an individual-item exemption from a retained type.

## Guest finding 1: arrival destinations are inside Anomaly

Hospitality's `Lord_427` visit has `chillSpot` `(198,0,90)`. The painted Anomaly area covers x178–200 and z87–105 (437 cells). The preserved native arrival destinations all lie inside it:

| Guest | Native arrival job | Destination |
| --- | --- | --- |
| Dove | `Goto#82244950` | `(191,0,91)` |
| Human | `Goto#82244971` | `(191,0,87)` |
| Trebo | `Goto#82244986` | `(192,0,93)` |

Thus the initial protected-entry preparation does not demonstrate an unnecessary shortcut through the area. A native destination inside a protected area requires its outfit even when the pawn is not studying an entity. The video also shows guests permitted by the rule. Moving the Hospitality gathering/allowed destination outside Anomaly is the appropriate configuration test; this audit did not change those settings.

## Guest finding 2: obsolete arrival plans survive Hospitality's transition

The first robe jobs for Dove, Human and Trebo all ended `InterruptForced` at tick 54294452 (log lines 4129, 4221 and 4175). Their stack traces identify Hospitality's `LordToil_CustomTravel` arrival callback, `Lord.ReceiveMemo`, and `TransitionAction_EndAllJobs`. The visit transition ended the jobs; AOM's remaining preparation queue then reached the hood jobs, which succeeded at ticks 54295964–54296075.

At video 6:20, Human wears the hood and is **Claiming a bed** outside Anomaly. Around 7:00, AOM still reports **Preparing for Moving**, with a robe missing. AOM subsequently rebuilds robe preparation for the same old arrival jobs:

| Guest | Rebuilt robe job | Robe succeeds | Old movement resumed |
| --- | --- | --- | --- |
| Dove | `Wear#82256945`, tick 54302160 | 54305315 | `Goto#82244950` |
| Human | `Wear#82260611`, tick 54303690 | 54306591 | `Goto#82244971` |

Trebo remains Preparing in the later save, preserving `Goto#82244986`, while the native current job is `LayDown#82259933`, asleep at `(97,0,52)` outside Anomaly, with an empty native queue. No completed Trebo robe retry or guest departure/restoration is established by this excerpt.

This is a concrete compatibility concern: AOM continues an arrival plan after Hospitality has transitioned the group to visiting. It explains the hood-first, robe-later behavior beyond the initial gathering-point choice. A focused follow-up should invalidate obsolete pending travel when its originating guest duty/visit phase ends, while preserving valid entry protection and unrelated jobs. No infinite loop is claimed, and this audit does not establish a general alternate-route failure. Later LayDown protected-route messages alone do not prove one.

## Other log results and verification limits

The excerpt includes 22 successful apparel endings, three interrupted apparel endings (the guest robes above), four successful weapon endings, seven completed restorations and four resumed prepared jobs. No AOM exception or ten-jobs warning was found. The generic summarizer reports zero preparation rebuilds because it misses this particular idle-rebuild message; manual inspection establishes the two guest robe rebuilds above. Its zero concern count is not a clean-session verdict.

Built item-control candidate SHA-256: `D996E3F5B87A206B89E2CC389F6EC323E665B346F9E9007C95F1B6E18E9A61AE`.

- Build passed.
- Managed-gear tracking contracts: 33 checks passed.
- Storage contracts: 296 checks passed.
- Encoding scan: 121 source files and 1,594 compiled user strings, no findings.
- `git diff --check` passed.

The new button is built but not deployed or visually verified in-game. The current video's behavior belongs to the previous runtime, not this candidate. Manual follow-up: select an ownerless retained item, use the Forget retained command, verify all unowned copies revert to ordinary filter membership while exact saved items remain protected; also check an active transition blocks forgetting. Guest compatibility remains an audited issue, not a fixed or validated behavior.

## Label clarification

The item command uses Forget retained apparel / Forget retained weapon and invokes the selector's exact Forget operation immediately, without a confirmation dialog. Only the labels and documentation changed after the checks above. Rebuild, encoding scan (1,595 compiled strings) and diff check passed; revised candidate SHA-256: `A57703C611A9BED7B4B417BF0FCFE077AE95B17D730DA2B09B63498AF2F4308A`. Not deployed; in-game visual verification remains pending.

## Authorized deployment

User approved deployment of the fixes/changes. RimWorld was closed and the installed mod junction targeted the live repository. Deployed only the runtime DLL; candidate, live and installed copies all verified SHA-256 `A57703C611A9BED7B4B417BF0FCFE077AE95B17D730DA2B09B63498AF2F4308A`. This supersedes the earlier undeployed status. The Forget retained apparel/weapon command is now installed, with no confirmation prompt. No guest continuation fix is included. Game was not launched; in-game button verification remains pending.
