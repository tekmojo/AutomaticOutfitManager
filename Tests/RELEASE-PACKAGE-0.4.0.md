# 0.4.0 package preflight — 2026-09-06

Historical pre-upload record. For subsequent publication and downloaded-package verification, see [Workshop closeout](WORKSHOP-CLOSEOUT-0.4.0.md).

**Staged locally; not uploaded or published.** The source remains on `codex/workshop-release-prep` above base commit `544c1f86ef10c46d7037b418b662aa5a5575e1e7`. No release commit/tag, push, Workshop change or installed-junction retarget occurred in this preflight.

## Candidate and tests

DLL SHA-256: `F00A4B06756118DBE8C6EA50681980F6A52248024F2AD1AA811761C65B08AF24`. About/product version is 0.4.0; file/assembly version is 0.4.0.0. RC, live and installed runtime files match. All 100 recorded source/runtime inputs, including 91 source/project files, match the validated build, so packaging reused that exact DLL without a rebuild.

The maintainer replied **“Those checks passed”** to the remaining blocked saved-item recovery, cooking after a Non-Work outfit change and Delete confirmation scenarios. This is maintainer-reported gameplay evidence. No new log/video accompanied the confirmation; it does not independently certify every contended/negative test variant.

All **1,954 checks in 21 suites** passed again:

| Suite | Checks | Suite | Checks |
|---|---:|---|---:|
| Access exit | 20 | Activity | 125 |
| Area nesting | 26 | Area permission | 295 |
| Child access | 30 | Ingest reservation | 27 |
| Managed work candidate | 31 | Meal handoff | 89 |
| Non-Work | 178 | Non-Work ingredient | 121 |
| Observed transition | 29 | Ocag follow-up | 17 |
| Preparation handoff | 134 | Prepared meal lifetime | 175 |
| Restoration | 50 | Return diagnostics | 26 |
| Saved gear recovery | 141 | Session audit fixes | 48 |
| Storage | 279 | Transition activity diagnostics | 47 |
| Weapon preparation | 66 | | |

Each runner executed in a separate PowerShell process with its exit code and full output retained. These checks cover production policies and relevant Harmony interceptions; they are not a replacement for native save/load, layout or compatibility testing.

## Package verification

The RC `package-workshop.ps1` still named 0.3.8. Its required version is now 0.4.0 and its manifest filename uses that same version variable. It created a fresh stage under `work/releases/0.4.0-rc1/AutomaticOutfitManager` after the exact absolute target was checked to be inside the intended release workspace and absent.

- Exactly **12 files**, totaling **981,162 bytes**, match the RC and SHA-256 manifest.
- The allowlist is the DLL; four About assets/metadata files; four Def XML files; main-tab icon; LICENSE; NOTICE.md. Source, tests, scripts, logs and gallery images are absent.
- Five XML files parse; package ID, title, author, RimWorld 1.6, Harmony and Workshop ID **3792731788** are preserved.
- The branded preview remains **640×360, 369,895 bytes**; both 64×64 icons retain transparency.
- `AutomaticOutfitManager-0.4.0.zip` contains the mod files directly at its root. All 12 extracted entry hashes match the stage; archive integrity passes.
- ZIP size: **593,543 bytes**. SHA-256: `1F93AC363FA55DB00A6B31D9DF7327270F66604D4990D85BFA514E11C1D3FFFD`.

The [eight replacement images](../Screenshots/README.md) retain their original bytes and aspect ratios. The seventh original is JPEG: its prepared filename was corrected to `07-activity-and-return.jpg`. Every filename extension, encoding, dimension, byte count and SHA-256 now matches the manifest. Every image is below 1,000,000 bytes; the largest is 938,376. Historical preparation receipts preserve the original naming mistake and the correction record.

## Publication inputs and remaining verification

The public Workshop page was read directly: **Automatic Outfit Manager**, creator **tekmojo**, ID **3792731788**, public, Harmony required, latest update **0.3.8**. GitHub's latest published release is **v0.3.8**. Neither surface was modified.

Ready-to-copy Workshop description, short description and full change note are alongside the ZIP. The [Workshop source](../WORKSHOP.md) and [changelog](../CHANGELOG.md) remain candidate drafts until publication. New screenshots replace all older gameplay images when the public galleries are updated; the branded cover stays separate.

The installed mod junction still targets the live repository. Before an upload-session launch, retarget it to the audited stage only when authorized and verify its About card and hashes. The staged package's load/UI smoke, Steam-downloaded package, public gallery, and remote release asset digest remain unverified. No CPU/TPS benchmark or broad performance improvement is claimed.

Local detailed evidence: `work/release-prep/2026-09-06-0.4.0/release-checks/`, `package-inputs.json`, `screenshots/format-extension-check.json`, and `work/releases/0.4.0-rc1/package-verification.json`. These development records do not enter the runtime package.
