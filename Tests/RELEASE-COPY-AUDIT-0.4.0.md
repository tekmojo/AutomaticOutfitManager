# 0.4.0 copy and documentation audit — 2026-09-06

**Locally deployed; not published.** The candidate updates player text, display provenance, documentation and version metadata. Eight new screenshots are now prepared locally; public gallery replacement and full native UI/load validation remain pending.

## Subsequent release preflight — 2026-09-06

The maintainer confirmed blocked saved-item recovery, cooking after a Non-Work outfit change and Delete confirmation passed. All 1,954 checks in 21 suites then passed again on the current candidate. A 12-file Workshop stage and matching ZIP were created and verified. The packaging version was corrected from 0.3.8 to 0.4.0, and one byte-identical JPEG screenshot was renamed from `.png` to `.jpg`. No gameplay source or runtime bytes changed. See [package validation](RELEASE-PACKAGE-0.4.0.md) for current results; the original copy-pass observations below remain historical.

## Local deployment — 2026-09-06T19:08:41.8328008-06:00

User-authorized deployment completed with RimWorld closed. RC/live/installed now match `F00A4B06756118DBE8C6EA50681980F6A52248024F2AD1AA811761C65B08AF24`. The DLL, About version/description, main-tab description and storage-filter descriptions were deployed and independently hash-verified. Installed About/product version is 0.4.0 and file version is 0.4.0.0. Original live files were backed up. No source/documentation tree synchronization, game launch or publication occurred. In-game UI/load and focused gameplay checks remain pending.

The preparation evidence below records the earlier pre-deployment state.

## Candidate identity

- RC DLL SHA-256: `F00A4B06756118DBE8C6EA50681980F6A52248024F2AD1AA811761C65B08AF24`.
- Before-deployment live/installed DLL SHA-256: `98BC3B34BCEFF8443174D7472BB27908FCE090B5249411FDA98733FA0096A4E0`. That was the preceding gameplay-tested runtime.
- About/project/informational version: **0.4.0**; assembly/file version: **0.4.0.0**.
- Package ID `tekmojo.automaticoutfitmanager`, supported RimWorld 1.6, Harmony dependency, legal notices and existing Workshop ID **3792731788** retained.
- Worktree: `codex/workshop-release-prep`, starting HEAD `544c1f86ef10c46d7037b418b662aa5a5575e1e7`. Pre-existing source and untracked files were preserved. This is not a release commit.

## Player surfaces reviewed

| Surface | Result |
|---|---|
| Main tab and expanded/collapsed rule cards | Shorter option help; relevant colored area/locker header plus useful body retained. Rule descriptions remain editable. Existing colored badges, centering, access controls and layout behavior retained. |
| Activities, Hauling, Wandering and Children | Purposeful activity and robot-cleaning categories explained without migration or job-callback details. Outfit rules and safe-exit exceptions preserved in the wording. |
| Task Buffer, Workers/Occupants, Haulers/Wanderers | Successful completed counts, associated rule ownership and early conflicting-outfit changes explained. Removed repeated list diagnostics and unrelated preambles. Native job reports and actionable status reasons remain. |
| Saved-outfit preference, viewer and removal selector | Saved, fallback and retained meanings separated. Fallback labels/help are conditional on the saved-outfit preference. All Work Outfits, selective sources and shared-item retention remain accurate. |
| Apparel and primary-weapon selectors | Rule sources at the top, named conflicts, readable default hover text and neutral grey conflicts. Removed displayed technical Def IDs; name/Def-name search still works. All-garments versus one-primary behavior retained. |
| Availability, standards and readiness | Counts do not promise per-pawn reachability or eligibility. Selected-gear standards do not apply to saved restoration. Saved/fallback explanations appear only where relevant. |
| Item inspection and Forget blockers | Selected Work/Non-Work/fallback sources, retained types and exact personal owners distinguished. Non-Work selections no longer inherit a misleading required-work label. Disabled selections remain identified. Source names retain area colors. |
| Saved-item actions and confirmation dialogs | Owner, recall and release consequences reviewed. Recall wording covers both area kinds; releasing a saved item no longer promises to bypass ordinary outfit/storage rules. Existing Release/Delete confirmation behavior retained. Delete keyboard/native UI checks remain pending. |
| Logging settings | Added hover help for each logging option. Quiet/Basic/Detailed behavior and the full Detailed diagnostic implementation remain unchanged; Developer Mode is unnecessary. |
| About/loading-mod description and Def text | Work and Non-Work coverage, storage filters and main-tab description updated. No Def IDs, worker classes, filter defaults or save keys changed. No separate custom loading-screen description was found. |

This pass does not change outfit planning, actual job admission, route/access policy, storage-filter decisions, saved ownership, success-only buffer credit or recovery timing. Display changes include using the shared selecting-rule lookup for item inspection and shortening provenance labels. Useful blocker reasons remain visible; technical source diagnostics stay in Detailed logging.

## Documentation and Workshop preparation

Rewrote the [player guide](../README.md), updated the [design document](../PROJECT-DESIGN.md), and added the [0.4.0 changelog entry](../CHANGELOG.md). The [Workshop file](../WORKSHOP.md) contains a short description, full BBCode description, **433-character compact Latest update block**, and a separate **1,368-character full change note**. The main description is **7,090 characters**, with balanced formatting tags. All are local drafts; no public description was changed.

The [release checklist](../RELEASE-CHECKLIST.md) and [readiness plan](NEXT-UPDATE-READINESS.md) now separate current gates from archived troubleshooting. Older audit text and dated Workshop change notes remain intact, with historical UI notes identified as such. Local documentation targets were checked; external publication state was not changed or newly certified.

The current cover was visually reviewed and retained: **640×360, 369,895 bytes**. The mod and main-tab icons are transparent **64×64** PNGs. Eight new captures are now preserved under [Screenshots/README.md](../Screenshots/README.md) with captions and an asset manifest. The maintainer requests replacing all previous gameplay screenshots at publication. The old local gallery remains historical until that step; no public images were changed by preparing the new set.

## Verification

- RC build passed against the installed RimWorld/Harmony assemblies. File/product metadata agrees with 0.4.0.
- **486 focused checks passed:** 178 Non-Work outfit/source contracts, 279 storage classification contracts and 29 observed-transition contracts. These validate the underlying meanings and preserved display ownership; they do not claim native visual testing.
- All **five About/Defs XML files** parsed. Def behavior fields and product identity/dependencies were compared with the pre-pass copies and are unchanged apart from descriptions/version. `AomLog.cs` is byte-identical.
- Diff whitespace check passed; Git reported existing line-ending conversion notices. A separate pre-pass comparison isolates this work from earlier intentional edits.
- Current document links, version strings, Workshop block sizes/tags and image dimensions/transparency checked. The package allowlist was inspected without running the staging script; documentation, source, tests and screenshot gallery do not enter the runtime package.
- RC/live/installed hashes verified separately. RimWorld was closed at the final process check. No game control, deployment, commit, push, package staging or Workshop operation occurred.

## Reusable skills

Updated six installed AOM skills: session audit, debugging, performance, release copy, release candidate and build/deploy. Nine changed/new files were copied with exact before/after hash checks; original skill files were backed up.

Lessons include actual native parent/child admission, full scanner target consistency, progress-aware blocked restoration, associated Non-Work buffers, nonhuman display versus access classification, player-facing versus Detailed text, focused test completion, and PowerShell result/hash verification. A reusable session summarizer now preserves log lines and counts only the events between explicit current-tick endpoints when reporting rates. It does not infer TPS or blame from errors.

The helper passed controlled fixtures, CLI validation, and comparisons against the latest small/large logs, including the large log's 144-message/22,675-tick window. Installed skills retain their exact frontmatter and invocation settings; structure and resource links were checked. The bundled Python validator was attempted but lacks PyYAML here, so equivalent checks for these unchanged flat headers were performed with the standard library.

## At the end of the copy pass

The open cases at that point were exact blocked saved-item recovery, early Non-Work cooking admission, candidate UI/load smoke, Delete confirmation and measured performance. The subsequent preflight above records the maintainer's scenario confirmation and packaging results. Use the [current focused test plan](NEXT-UPDATE-READINESS.md) for remaining staged-package checks. The healthy older sessions remain normal-flow evidence, not passes for conditions they never exercised.
