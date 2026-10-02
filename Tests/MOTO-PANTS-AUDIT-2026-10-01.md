# Moto pants removal and item command polish — 2026-10-01

Follow-up: the user authorized the recommended fix and deployment. The 2026-10-02 correction below supersedes the original audit's unfixed status; the original findings remain the evidence for that change.

## Verdict

Confirmed AOM removal during saved Non-Work outfit restoration, not hauling, a condition/quality rejection, or a tattered upgrade. Moto's saved outfit omitted the pants; the dining rule restored that older exact set after Moto had acquired the pants. This exposes a gap in updating inactive saved outfits after ordinary clothing changes. It does not show the system rejecting a saved garment at 90% condition. Gameplay correction remains a follow-up; this request's implemented changes are the item command icon and labels.

## Evidence boundary

Video: `fa30OXpQCi.mp4`, 695.633 seconds, 3892 × 1716. Log: attachment `d6100c55-4793-4dd6-8e34-c11495f709cd`, SHA-256 `5F8F6E22C9BD866CE3E36CCD521E721ED7E51FF605175B77C8D18F77BBB35E5D`, 6,074 lines. It first loads `GooseButter_ANOM_ALL_MODS_02` at line 1539, then `GooseButter_ANOM_ALL_MODS` at line 1818. Findings and counts below use the second load, lines 1818–6074. Do not use the prior session's `_02` save as this run's endpoint.

The already-preserved copy of `GooseButter_ANOM_ALL_MODS.rws` is the baseline: tick 54272696, SHA-256 `415817FADCBBDE392E2EEB8F2E601B0DEA91F8354B6CACDFEAEEE549F9791E7E`. Read-only save metadata still shows the same original files; no newer end save was available. Original saves were not modified. Local summary, baseline extraction and video frames are under `.codex-audit/moto-pants-d6100c55` in the live workspace.

Deployed session candidate: `A57703C611A9BED7B4B417BF0FCFE077AE95B17D730DA2B09B63498AF2F4308A`. New UI candidate is distinct, below.

## Exact item and sequence

- Moto is `Human1405898`. The item is `Apparel_Pants3775625`, Lightleather, Normal, 90 HP. At baseline it is loose at `(166,0,122)` and Pants is in the retained type catalog. Moto is not wearing it, and neither the active personal snapshot nor saved Non-Work snapshot includes it.
- The saved Non-Work outfit contains the blouse, flak armor, plate shoulderpads, repair kit, research kit and Skitarii vanguard helmet, plus the saved Hellgun. It has no leg garment. Dining Room 1 uses `Locker Radiation 1` as its changing area.
- Moto's earlier Radiation restoration completes at log line 3467, after returning the radiation suit/mask and restoring shoulderpads, helmet and Hellgun. There is no pants restoration in that queue.
- At line 5429, tick 54307410, AOM redirects a meal proposal for Dining Room 1 into saved Non-Work restoration. At line 5494 it reports exactly one restoration job.
- That job is `RemoveApparel#82290031`, targeting the exact pants, admitted at tick 54308816 (line 5518) and completed successfully at 54308935 (line 5595). The snapshot clears at line 5572 during the completion callback; the trailing job-end message is not a second restoration.
- The video around 9:50–9:56 shows Moto in the Radiation locker, the removal activity in the closely sampled sequence, then leaving for a meal. Later inspection shows the pants still Normal/90%, without a saved-owner tag. Gear views show Moto without pants while eating.
- At line 5660 AOM captures the next Radiation snapshot, again with the six garments above and no pants. Subsequent radiation-mask/suit/weapon preparation succeeds and resumes DoBill. Thus the pants removal preceded the new work outfit, rather than being caused by its condition requirements.

The initial pickup/Wear of the pants is not logged, so its exact tick and whether it was native optimization or a player order are unproven. Acquisition between the baseline and removal is established by the exact worn-item removal. Clearing retained stock makes ordinary use possible but does not itself assign that item to Moto's saved outfit.

## Source explanation and follow-up

`SavedNonWorkOutfit.ApparelSatisfied` requires an exact match, including no extra worn garments. `BeginNonWorkRestoration` replaces the active originals with `saved.Apparel` and adds every other worn item to managed removal. `RestorationPlanner.BuildJobs` then removes those managed items, independently of hit points or quality. Dining Room 1's configured locker explains the drop location.

The apparel Wear callback invokes `AdoptWornPersonalApparel`, but that method exits without an active apparel intervention. `RememberNonWorkOutfitBeforeWork` refreshes a personal snapshot before work; it does not keep an inactive snapshot current after every ordinary Wear. Consequently a valid ordinary garment acquired between interventions can be absent when a Non-Work rule next requests the old snapshot, and gets removed. This is a concrete saved-outfit synchronization concern, not evidence of arbitrary low-condition replacement.

A focused correction should reconcile successful ordinary personal clothing changes with inactive saved outfits, while excluding selected/retained work stock, borrowed items, another pawn's saved gear, AOM preparation and return jobs. It needs a regression covering restoration complete → ordinary pants Wear → dining entry, as well as unchanged shared-stock exclusions. No such gameplay change is included here.

## Implemented UI changes and validation

- Forget retained apparel/weapon now uses `TexCommand.ForbidOn`, exactly the existing Release command icon.
- Release item is now **Release apparel** or **Release weapon**, according to the selected item.
- Forget retains its immediate type-wide selector action without confirmation. Release still affects the exact owned item and retains its existing confirmation.

Build passed. Encoding scan checked 121 source files and 1,596 compiled strings without findings; diff check passed. New candidate SHA-256: `CB90821EEB1183932AB31110FFEFCF1200CE1FB2B039EB48B14DF3C5E9B73468`. These label/icon-only edits do not change ownership or restoration logic; no new behavioral fixture was added. Not deployed and not visually verified in-game.

The selected log interval contains 174 AOM messages, six completed restorations, 16 successful apparel endings and three successful weapon endings, with no failed apparel/weapon endings. Those aggregate counts do not erase the pants finding. The different guest group does not retest the previously documented stale Hospitality arrival continuation, which remains open.

## Authorized correction — 2026-10-02

`AdoptWornPersonalApparel` now updates an existing inactive Non-Work preference after the native Wear callback confirms the new garment is actually worn. It adds the new exact item and releases conflicting saved garments. It does not create a preference for a pawn without one, recapture unrelated clothing, or reinterpret an ongoing non-apparel transition. Inactive preferences gain exact storage tracking without creating a new permanent owner reservation; active interventions preserve existing owner assignment.

Selected/retained definitions, preparation gear, borrowed exact items, pending shared returns and another pawn's protected items remain excluded. Replacements remove displaced references before pruning their old tracking. Unrelated saved garments remain recorded even when currently off the pawn. No schema, guest behavior, rule settings or save file is changed. This is a successful-Wear correction, not a retroactive adoption of every garment already worn in an old save.

`PersonalWearNativeProbe.cs` loads the compiled runtime and its actual Harmony Wear patch. It executes native `Pawn_ApparelTracker.Wear`, `ThingOwner` membership changes and `ApparelUtility.CanWearTogether`, followed by the production adoption and saved-outfit satisfaction check. The fixture supplies body-group/eligibility inputs and suppresses world/render notifications, with no running game. It does not simulate Hospitality or claim live gameplay verification.

Verification:

- Personal Wear native probe: 23 checks pass, covering ordinary pants acquisition → saved dining outfit satisfaction, successful replacement, displaced ownership cleanup, failed Wear, no existing preference, retained/selected stock, borrowed/pending returns, other-pawn ownership, active adoption and preparation exclusions.
- Preserved pre-fix candidate `CB90821EEB1183932AB31110FFEFCF1200CE1FB2B039EB48B14DF3C5E9B73468` fails the exact inactive-outfit assertion through the same native callback (`-ExpectedRegression`), confirming the negative control.
- Non-Work contracts: 322 checks pass, plus their existing previous-decision negative control.
- Restoration contracts: 50 pass; storage contracts: 296 pass; managed-gear tracking: 33 pass.
- Build, encoding scan (121 source files / 1,596 compiled strings) and diff check pass.

Candidate SHA-256: `74C5192A1AB2B70A3F3886A82261D877BE7CA16A22E7D8869EDA70EBF8A2B6D8`. Includes the requested shared icon and Release apparel/weapon labels. Next live check: after a work restoration, let Moto successfully wear ordinary pants, then enter Dining Room 1 and verify the pants remain worn. Guest continuation remains a separate open finding.

Deployment completed with RimWorld closed and the installed junction targeting the live repository. Only the runtime DLL was copied. Candidate, live and installed copies all match `74C5192A1AB2B70A3F3886A82261D877BE7CA16A22E7D8869EDA70EBF8A2B6D8`. The game was not launched. In-game verification remains pending.

## Subsequent gameplay validation — 2026-10-02

The [full session audit](FULL-SESSION-AUDIT-2026-10-02.md) now verifies the specific regression: Moto successfully adopts the exact Normal/90% pants, includes them in the next Radiation snapshot, completes his later return without removing them, and is subsequently seen eating in Dining Room 1. The matching later autosave has those exact pants both worn and saved. This closes the normal-flow pants case; reloading the new state and the separate Hospitality continuation case remain unverified.
