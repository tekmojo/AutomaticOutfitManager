# 0.4.3 maintenance preflight — September 25 preparation

Prepared in the RC worktree on `codex/workshop-release-prep`, starting HEAD `2e02f76`. The maintainer requested no version increase and an update through RimWorld's in-game Mods menu. This record covers local preparation, not upload or publication. Subsequent requested deployment and staging are recorded in [maintenance deployment](MAINTENANCE-DEPLOYMENT-0.4.3.md).

## Change scope

- Fix the Task Buffer decrement label using `\u2212`, preserving the original minus glyph without a literal encoding round trip.
- Restore the exact pre-update concise About/loading description. Keep version 0.4.3 and all dependency/package metadata.
- Clarify that selected Non-Work outfit requirements apply to adults. Remove the stale child-wearability example from the guide; children use Allow Children independently.
- Refresh current design, release/readiness records, test index and Workshop draft. Archive the previous published checklist, readiness and Workshop fields intact. Keep the compact Workshop summary and full maintenance note separate from the About description.
- Retain the existing preview, icons and legacy gallery. No additional public screenshots are needed for this narrow correction.

The two source edits change string literals only. No gameplay logic, save keys, defaults, assembly version or package identity changes. Neither Arakis's crib loop nor the other mods' loading errors is claimed fixed.

## Candidate identity and checks

Candidate SHA-256: `4CA32704045B015AB50A6FA20AFA236A72E717E90DDF812F785830CB1A0E9B32`.

Prior published/live/installed/subscriber SHA-256: `FB5F15960E286091A24CFB50E39989C5C1BDDDCA706E17924E17F7CDE7AA4BA9`.

Version remains 0.4.3; assembly/file version remains 0.4.3.0. The RC build succeeds. `Tests/check-text-encoding.py` scans 111 source/XML/project files and all 1,571 compiled user strings without findings. Running the same compiled-text check against the published baseline correctly rejects its remaining corrupt minus sign. The script reads assembly metadata without loading game code. It is an encoding heuristic, not a substitute for normal-scale visual inspection.

Validation also checks exact restoration of the old About description, unchanged non-string source content, XML parsing, version agreement, image dimensions/transparency, current documentation links, Workshop field sizes, and `git diff --check`. The package allowlist is inspected without running the staging script. Evidence is saved under `C:/GitHub/AutomaticOutfitManager/.codex-audit/maintenance-0.4.3-2026-09-25/`.

Results: five runtime XML files plus the project XML parse; nine current documentation files have no broken local Markdown links. Comparing complete source files and compiled string tables shows exactly two changed literals and no other source changes. All three publication archives match the previous HEAD text. Preview is 640×360 at 369,895 bytes; both icons are 64×64 with transparency. Workshop main copy is 7,928 characters / 7,932 UTF-8 bytes; the separate maintenance note is 324 characters / 326 bytes. No broad gameplay-suite rerun is needed or claimed for these string edits.

## Skill guidance

Narrow updates cover all-symbol source/compiled text checking, concise loading copy, explicit same-version maintenance, the maintainer's in-game Mods-menu upload route, and cautious attribution of patched native job loops. Existing skill frontmatter and discovery settings are preserved. Original files, reviewed candidates and installation hashes are recorded locally. The standard validator requires unavailable PyYAML; equivalent checks verify unchanged frontmatter, reference paths and absence of unfinished scaffolds without installing a dependency.

Five reviewed files across the release-copy, release-RC and session-audit skill packages are installed and verified against their recorded SHA-256 values. Unrelated skill files and agent metadata are unchanged.

## Remaining work at the end of preparation

The candidate is built, not deployed, staged, uploaded or gameplay-smoked. The local About description was already restored at the maintainer's earlier request; the installed DLL remains the published baseline.

After a deployment request, check Task Buffer controls at 0, 1 and 20 for both rule kinds, the selected-outfit tooltip and About card, then one load/restoration smoke. Stage only when requested and verify the installed upload source before a manual Steam launch. Use the existing Workshop item and the in-game Mods menu, with the draft maintenance note from [Workshop copy](../docs/workshop/README.md). Preserve the version tag and existing GitHub assets unless separately instructed.

Broader gameplay findings remain in [current readiness](NEXT-UPDATE-READINESS.md); previous release evidence is archived separately. No source-control or public publication operations were performed.
