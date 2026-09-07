# Workshop 0.4.0 closeout — 2026-09-06

Subsequent GitHub publication is complete; see [GitHub closeout](GITHUB-CLOSEOUT-0.4.0.md). The statements below describe the earlier Workshop-only closeout.

Workshop publication is complete. The maintainer uploaded the runtime, saved the final description/change note and replaced the gameplay gallery. Read-only browser checks confirmed the public result. GitHub release publication remains pending; no release commit, tag or push was made during this closeout.

- Public item: [Automatic Outfit Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788), creator tekmojo, ID `3792731788`, public, Mod/1.6, Harmony required.
- Updated display: Sep 6 @ 8:08pm. Description and change note show 0.4.0. The Choose outfits by area list omits the unwanted bold labels; the rest of the approved copy is retained in [WORKSHOP.md](../WORKSHOP.md).
- Gallery: exactly eight new images in the [documented order](../Screenshots/README.md), replacing the old gameplay images. Local originals' SHA-1 values match Steam's public image URL identifiers. Public previews were inspected; full remote image bytes were not downloaded. The branded cover is retained separately. Individual public caption text was not certified.
- Steam subscriber manifest: `1496049653910862886`, update timestamp `1788746925`, 981,162 bytes. The downloaded folder contains the exact 12 staged paths, with every length and SHA-256 matching the audited package and RC. Native subscriber-loaded gameplay smoke was not performed.
- DLL SHA-256: `F00A4B06756118DBE8C6EA50681980F6A52248024F2AD1AA811761C65B08AF24`.
- ZIP SHA-256: `1F93AC363FA55DB00A6B31D9DF7327270F66604D4990D85BFA514E11C1D3FFFD`, 593,543 bytes. Original stage, ZIP and prepublication manifests remain unchanged.

## Local restoration

RimWorld was closed when the installed development junction was restored from the release stage to `C:/GitHub/AutomaticOutfitManager`. The installed DLL matches the published release. A pre-existing different live Workshop pointer (`3794854019`) was backed up and corrected to the official item (`3792731788`). Its original purpose was not established.

The first completion script stopped after that successful restoration because it incorrectly expected all twelve release files to match the separate dirty live checkout. The final check uses the required installed DLL match and records the support-file differences: live ModIcon and WorkGivers XML differ; Preview, LICENSE and NOTICE are absent. WorkGivers differences are player labels only. No whole-repository synchronization was performed. These local differences do not affect the staged or Steam-downloaded package; continue packaging from the audited RC stage.

## Evidence and remaining work

Local records are under `C:/GitHub/AutomaticOutfitManager/work/releases/0.4.0-rc1/workshop-update/`: `public-verification.json`, `subscriber-verification.json`, `restore-junction.json`, the old live ID backup, final copy files and document backups. [Package preflight](RELEASE-PACKAGE-0.4.0.md) retains the original 1,954-check result and maintainer-reported gameplay checks.

The matching GitHub source commit/tag/release and gallery retirement remain pending. A subscriber-loaded in-game smoke and optional measured performance baseline are not claimed by these publication checks.
