# 0.4.2 copy and release preflight — 2026-09-08

Prepared in the authoritative `codex/workshop-release-prep` RC worktree, starting at `4ba1ad56b63be78a90ca08f4941dbfb31a03a7d9` with the existing gameplay changes preserved. Version 0.4.2 remains **unreleased**; no publication date is assigned.

## Changes

- Align About, project, assembly and package-script version expectations at 0.4.2. Keep package identity, Workshop item, dependency, supported game version and license unchanged.
- Clarify the saved-personal preference tooltip: unchecked selected gear is equipped before entry, an inside mismatch leaves safely before changing, and unavailable/unwearable gear allows other reachable tasks outside. The empty saved-outfit dialog now says selected gear rather than calling every selection fallback.
- Update README, changelog, About gameplay summary, design documentation, and separate draft Workshop description/compact update/full change note. No claim that the earlier Ocagobrei simple-meal loop is fixed.
- Archive the completed [0.4.1 closeout](../RELEASE-HISTORY-0.4.1.md), [readiness](READINESS-0.4.1.md) and [published Workshop copy](../WORKSHOP-PUBLISHED-0.4.1.md) before replacing current status.
- Refresh [readiness](NEXT-UPDATE-READINESS.md) with the latest small/large-map evidence and explicit remaining cases. Mark the existing gallery as legacy and prepare [optional captures](../Screenshots/CAPTURE-PLAN-0.4.2.md).
- Install and hash-verify three narrow skill additions: session/save evidence boundaries, selected Non-Work exit and native path ordering, and the matching release test matrix. Backups and proposed files are kept outside the runtime/package tree; original files were checked for intervening changes before replacement.

## Build and runtime distinction

| State | Version | DLL SHA-256 |
| --- | --- | --- |
| Built RC candidate | 0.4.2 | `EB08621418BA99D2DF42ABCF345DF094E65D75D04927DF317BBF0B11B8C10C6C` |
| Live checkout | Prior gameplay candidate, 0.4.1 metadata | `F634D86039F215FC98FD883C667701DFB66F8E990A2221AD60292CFD5ACF2FD5` |
| Installed junction | Same prior gameplay candidate | `F634D86039F215FC98FD883C667701DFB66F8E990A2221AD60292CFD5ACF2FD5` |

Release build succeeded. The runtime manifest comparison against the start of this preparation permits only About version/description, project and assembly versions, and two string changes in MainRulesWindow. Stripping string contents leaves the same UI code tokens. No gameplay decision, Def, texture, save key or rule default changed during this copy preparation. The earlier gameplay fixes remain part of the candidate.

## Validation

All **30 contract suites passed: 3,502 positive checks**. The unavailable Non-Work and native movement families' **seven explicit previous-decision controls** reproduced their intended failures and passed their runners. Expected assertion failures were handled without Windows crash dialogs. Existing inline negative checks in ordinary runners are separate from the positive total.

Static preflight passed: six XML files including the project, 76 local documentation links, stable package/Workshop/dependency identities, and agreeing 0.4.2 metadata. The preview is 640×360 and 369,895 bytes. Both icons are 64×64 and contain actual transparent/translucent pixels (3,891 in the About icon, 3,397 in the main-button icon). Workshop description is 7,597 characters; the separate change note is 999. Source manifest comparison and `git diff --check` passed. Packaging allowlist inspection is read-only: About assets/identity, Defs, Textures, runtime DLL, LICENSE and NOTICE are the runtime inputs. Source, tests, skills and local audits remain excluded.

The interface remains English-only; no translation files or new language claims were added. Existing preview/icons are unchanged. Full static measurements and skill installation verification are recorded with the local preparation artifacts.

The stock skill validator cannot start under the available Python because PyYAML is absent. No parser validation is claimed. The proposed changes preserve original bytes and frontmatter, use append-only guidance, introduce no new reference links, and leave discovery/UI policy unchanged; those properties are checked independently.

## Remaining steps

The [release checklist](../RELEASE-CHECKLIST.md) lists the unresolved Ocagobrei reproduction, Roboto/Zendaya ingredient-to-bill completion, unwearable-child alternate activity, and new transition save/reload. Mental-state save/load and inactive shared-snapshot cleanup remain carry-forward limits. A UI/load smoke is needed after an authorized deployment of this copy/version build.

No deployment, staging, upload, publication, commit/tag/push, junction change or game launch was performed. The live and installed runtime remain on the gameplay-tested hash above.

Local artifacts: `C:/GitHub/AutomaticOutfitManager/work/release-prep/2026-09-08-0.4.2/` contains before copies, runtime and skill manifests, test outputs and static results. These local artifacts are not package inputs.
