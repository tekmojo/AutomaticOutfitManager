# Final 0.4.3 upload handoff

Prepared 2026-09-25T18:41:36.5306754-06:00. The maintainer requested the upload deployment after copy preparation. RimWorld was verified closed and remains unlaunched by this task.

- Created the new stage with the repository packaging script using the already-built, gameplay-tested DLL. No rebuild or gameplay changes.
- All 12 staged files match the audited input paths, sizes and SHA-256 values.
- Retargeted only the verified installed junction from the live repository to the audited stage. The live repository and its source changes are intact.
- Read every installed file through the junction and verified all 12 against the stage. Parsed title, author, version, package ID, description and Workshop ID match.
- Version: **0.4.3**. DLL: `85AD264B6CF071229DEFE128B6342D460776C37E7D5A6ED6CAFC9C85BB861D04`.
- Existing Workshop item: **3792731788**. No upload or publication has occurred in this handoff.

Installed junction: `F:\Steam\steamapps\common\RimWorld\Mods\AutomaticOutfitManager`.

Current stage target: `C:\GitHub\AutomaticOutfitManager\work\radiation-any-weapon\work\workshop-staging-0.4.3-final-bugfix-20260925\AutomaticOutfitManager`.

Restore target after upload, with RimWorld closed: `C:\GitHub\AutomaticOutfitManager`. Restore only the junction, never delete either target directory.

The maintainer can now launch manually and use the Mods menu. Verify the About card and use the **Final bug-fix change note** in [Workshop copy](../docs/workshop/README.md). This dated handoff is superseded by [final closeout](FINAL-BUGFIX-CLOSEOUT-0.4.3.md): public upload and subscriber comparison pass, and the junction is restored. Downloaded-copy gameplay smoke remains unclaimed. Historical readiness gaps remain as recorded in [current readiness](NEXT-UPDATE-READINESS.md).

Per-file identities, parsed installed metadata and restore target: `C:/GitHub/AutomaticOutfitManager/.codex-audit/final-bugfix-prep-0.4.3/upload-handoff.json`.
