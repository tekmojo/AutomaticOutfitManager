# Workshop 0.4.2 closeout — 2026-09-08

The maintainer uploaded the update through RimWorld and applied the description and change notes. Read-only verification confirms the public result and refreshed subscriber package. The release date uses America/Denver; Steam's update timestamp is **2026-09-09T03:55:41Z**, or **2026-09-08 21:55:41 MDT**.

- Public item: [Automatic Outfit Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788), creator tekmojo (`76561197960452376`), public, Harmony required. Item ID **3792731788** is unchanged.
- Public content handle and refreshed subscriber manifest: **1114274176429525394**.
- The full public BBCode description matches the reviewed 0.4.2 copy after line-ending normalization. The rendered latest [change note](https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788) matches the separate prepared note. The compact Latest update block remains part of the description.
- **12 files, 1,035,376 bytes**: the subscriber folder matches every staged path, length and SHA-256. The RC's 117 frozen source/package inputs remain unchanged. No subscriber-only gameplay launch is claimed.
- DLL SHA-256: `EB08621418BA99D2DF42ABCF345DF094E65D75D04927DF317BBF0B11B8C10C6C`.
- All eight public gallery image IDs are unchanged; Harmony remains required. No new screenshot capture or upload is claimed.

## Restored development setup

RimWorld was closed when the installed junction was restored from the upload stage to `C:/GitHub/AutomaticOutfitManager`. Its DLL matches 0.4.2. The separate live About file's version and short gameplay description were aligned with 0.4.2 after backing up the original. Stable identity and other metadata remain intact.

The live development checkout retains its existing support-file differences and intentional source changes. It is not treated as a byte-for-byte release package. Runtime/package verification uses the audited stage and subscriber cache; future staging should continue from the RC worktree.

## Remaining scope

The [validation record](NEXT-UPDATE-READINESS.md) preserves the focused unverified gameplay cases, including the earlier Ocagobrei simple-meal loop. No new gameplay evidence was obtained during closeout. The copy-build UI/load smoke, mental-state save/load, inactive shared-snapshot cleanup and subscriber-only gameplay remain unverified.

GitHub release operations were completed under the maintainer's subsequent authorization. The matching ZIP and checksum are public as the latest release, with remote asset digests verified. See [GitHub closeout](GITHUB-CLOSEOUT-0.4.2.md) for the tagged release commit, package hashes and publication timestamp.

Local evidence: `C:/GitHub/AutomaticOutfitManager/work/releases/0.4.2-upload/` contains the public baseline/result, rendered note, subscriber hashes, frozen inputs, package, manifests, restored-junction result and live About backup. These records are outside the runtime package.
