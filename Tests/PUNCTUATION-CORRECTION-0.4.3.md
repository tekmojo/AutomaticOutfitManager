# 0.4.3 interface punctuation correction

The GitHub pre-release source review found 21 em dashes and two en dashes incorrectly re-encoded in `Source/UI/MainRulesWindow.cs`. The initial published 0.4.3 DLL contained those strings. The maintainer requested correction before GitHub publication and a replacement Workshop package.

Only those 23 string fragments were corrected. Reversing the substitutions reproduces the pre-fix source exactly. The release build succeeds, contains the correct `Drafted — ` label and contains none of the two corrupted dash sequences. Gameplay code and the other eleven package files are unchanged. This is a build/string verification, not an in-game visual smoke.

- Initial published DLL: `149A319E8D6BDAB4494AD6727AA3607D3974DAE2BC2BDE1A6526BB18A005DF1D`.
- Corrected DLL: `FB5F15960E286091A24CFB50E39989C5C1BDDDCA706E17924E17F7CDE7AA4BA9`.
- Replacement package: twelve files, 1,053,123 bytes, staged with the repository packaging script at `work/workshop-staging-0.4.3-punctuation/AutomaticOutfitManager`.
- Corrected GitHub ZIP: 620,364 bytes; SHA-256 `48E3086B5AA8E8CA3E2505D1CC92AEA1C20623AC4D69B41613D447548617F56A`.
- Checksum file: 100 bytes; SHA-256 `8C20EA291504D44446715EE81EF22C7F24E0AA2C8375B2E9F6269699A4F05EEE`.

The installed mod junction was pointed to the corrected stage while RimWorld was closed; all twelve files, version, title, package ID and existing Workshop ID were verified through the installed path. At source freeze, the replacement Workshop upload was pending and the corrected GitHub package was being prepared as a draft. The corrected GitHub release is now public; see [GitHub closeout](GITHUB-CLOSEOUT-0.4.3.md). Replacement Workshop/subscriber verification remains pending; do not promote the initial subscriber verification to this corrected hash.

Local backups, source freeze, package verification and draft assets are under `C:/GitHub/AutomaticOutfitManager/.codex-audit/release-copy-0.4.3/`, including the `punctuation-github` subdirectory. The initial published package and its verification are retained separately.
