# Interrupted installation pickup count

The 7454aaa3 gameplay audit captured Sosossnove's HaulToContainer #81143403: native pickup consumed count 1 to 0, AOM interrupted at the Anomaly boundary, the minified bioferrite harvester was dropped for outfit preparation, and replay restarted pickup with count 0. Native Toils_Haul.ErrorCheckForCarry logged an error and reset it to 1. The matching save confirmed successful installation despite the warning.

The fix repairs only exhausted counts on AOM-resumed HaulToContainer installation jobs with a spawned, undestroyed, single MinifiedThing matching the exact spawned Blueprint_Install on the same map. It runs on the detached boundary-admission working copy and on prepared-job replay. It does not change positive counts, ordinary resource stacks, storage hauling, still-carried items, mismatched targets or player-forced jobs. Existing native reservation and area checks remain in place. Dining Room saved-outfit policy is unchanged.

Validation:

- Build and git diff --check passed.
- 24 native installation checks passed. The candidate helper and installed native ErrorCheckForCarry body execute in a disposable process with engine/world properties shimmed. The native blueprint setter and exact-item getter are exercised. Built admission call sites are checked.
- Negative control omits the new repair, reproducing the original replay behavior: native zero-count recovery occurs and the no-recovery assertion fails as expected.
- 47 boundary-admission contracts passed, including working-copy-only repair without changing the stored snapshot.
- 134 preparation-handoff, 33 borrowed-gear tracking and 50 restoration checks passed. Total: 288 checks.
- Manual in-game replay remains pending. Suggested test: move a minified building across a protected boundary that requires changing outfits, confirm installation completes without Invalid count, then verify ordinary partial resource hauling.

Built and deployed locally with RimWorld closed. Candidate/live/installed hashes match:

`1B970A7A40978B6FBDF6E3971A68947E7323FF7DD443008D34D1B7E443FF525F`

Previous deployed hash: `B7E0192DA0FF0AD9EC28653E3F276F20922987A28AB775039993DFC7812D1CA8`.

Only the runtime DLL was deployed. No game launch, save edits, commits or Workshop publication.
