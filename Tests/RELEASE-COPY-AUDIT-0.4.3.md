# 0.4.3 copy and release preflight

Historical preparation snapshot. Later publication is recorded in [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.3.md); a subsequently found text-encoding fault and its corrected build are recorded in [punctuation correction](PUNCTUATION-CORRECTION-0.4.3.md). The original copy checks below did not catch that fault.

Prepared in `C:/GitHub/AutomaticOutfitManager/work/radiation-any-weapon`, branch `codex/workshop-release-prep`, starting HEAD `5f0befe8862c1e1f5cf51e8d2c830d5f247b41e2`. Earlier tracked and untracked gameplay changes are preserved. This candidate is unreleased; no publication date is assigned.

## Completed

- Correct child-access, adult Activities/Hauling/Wandering, selected Non-Work gear and Pause/Resume tooltips. UI comparison against the start of this preparation has identical code tokens after string literals and whitespace are removed.
- Align About, project, assembly and package-script expectations at 0.4.3. Package ID, Workshop ID, Harmony dependency, supported RimWorld 1.6 version, English-only scope, existing assets and MIT/third-party notices remain unchanged.
- Update the player guide, current design, changelog and separate About/Workshop description/compact update/full change note. Describe native ritual participation and the scope of child exceptions without claiming new native jobs or broad meal fixes.
- Archive exact previous contents as [0.4.2 closeout](../docs/releases/history/RELEASE-HISTORY-0.4.2.md), [0.4.2 published copy](../docs/workshop/history/WORKSHOP-PUBLISHED-0.4.2.md) and [0.4.2 readiness](READINESS-0.4.2.md).
- Refresh [current readiness](NEXT-UPDATE-READINESS.md), [release checklist](../docs/releases/CHECKLIST.md) and [capture plan](../Screenshots/CAPTURE-PLAN-0.4.3.md). No screenshots are represented as new approved gallery assets.
- Install four narrow skill-guidance updates covering checkbox-only child rules, native ritual gathering, positive attendance evidence and the matching test/copy boundaries. Original files and staged candidates are backed up; before/after hashes were checked. Frontmatter and discovery settings were preserved.

## Candidate versus tested installation

| State | Metadata | DLL SHA-256 |
|---|---|---|
| Built RC candidate | Product 0.4.3; file 0.4.3.0 | `149A319E8D6BDAB4494AD6727AA3607D3974DAE2BC2BDE1A6526BB18A005DF1D` |
| Live and installed, gameplay-tested | Product 0.4.2 with new child/ritual implementation | `1E9F72F40BE9CCC2A17B220C9217F23886D1C3956791200D03F431075EA562BA` |

The installed junction still targets the live repository. RimWorld was closed during verification. The new copy/version candidate was built but not deployed or launched. It needs proportionate UI/load smoke; the observed child/role-ceremony pass remains scoped to the tested hash above.

## Validation

Build succeeds and `git diff --check` passes. Four relevant suites rerun successfully: 73 pause behavior, 599 paused hauling/restoration, 54 child-access and 78 ritual checks — **804 checks**. Earlier broader behavioral coverage is retained in current readiness; it was not relabeled as newly rerun.

Static checks: six XML files parse; version fields agree; package/dependency/Workshop identities match; current local documentation links resolve. The package script was inspected and its expected version updated; it was not run to create a stage. Runtime allowlist remains About metadata/images/ID, Defs, Textures, DLL, LICENSE and NOTICE, with no source/scripts/PDBs. No translations are present, consistent with English-only documentation.

Preview: 640×360, 369,895 bytes. About icon: 64×64 with 3,891 transparent/translucent pixels. Main-button icon: 64×64 with 3,397 transparent/translucent pixels. Existing images are unchanged.

Workshop main description: 7890 characters / 7894 UTF-8 bytes. Separate full change note: 1404 characters / 1406 bytes. The compact update remains a brief summary plus the changelog link within the main description.

Skill validation: the standard Python validator was attempted but PyYAML is absent in both available runtimes. Explicit equivalent checks verified unchanged frontmatter, allowed fields, names, description limits, no unfinished scaffolds and intact reference links; installation hashes match the reviewed candidates. No dependency was installed merely for this check.

## Remaining gates

Finish post-ceremony adult returns, child exit/overlap/pause edges, one fresh preparation save/load and native interruption/additional ritual type, and loaded legacy child outfit restoration. Keep older meal/bill findings separate. Then complete candidate UI/load smoke, screenshot review, source freeze and separately authorized staging/publication/source-control operations. No deployment, staging, upload, commit, tag or public release was performed during this preparation.

Detailed local backups, skill manifest, validation output and test logs: `C:/GitHub/AutomaticOutfitManager/.codex-audit/release-copy-0.4.3`.
