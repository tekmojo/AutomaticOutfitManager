# Steam Workshop copy — 0.4.4 published

**Published by the maintainer through the in-game Mods menu on 2026-09-26.** Existing item `3792731788`, title, dependency and public visibility are preserved. Public copy and all twelve Steam-downloaded files match the audited package. [Closeout](../../Tests/WORKSHOP-CLOSEOUT-0.4.4.md) records verification and remaining limits.

Use [DESCRIPTION.txt](DESCRIPTION.txt) for the full Workshop description and [0.4.4.txt](change-notes/0.4.4.txt) for the separate change-note field. Keep About/loading text concise and feature-focused. The compact Latest update block summarizes both fixes; it is not the full change note.

The published 0.4.3 text remains in [the dated archive](history/WORKSHOP-PUBLISHED-0.4.3-FINAL-BUGFIX.md) and [description archive](history/WORKSHOP-PUBLISHED-0.4.3-FINAL-DESCRIPTION.txt).

Normal-play and five-adult Void provocation completion/restoration passed on the candidate DLL. Controlled route/access cases, contested repair and fresh save/reload remain unverified; see [readiness](../../Tests/NEXT-UPDATE-READINESS.md). This update does not claim to fix the Arakis crib loop, background sidearm/idle concern or other mods' save-reference warnings.

The temporary upload-stage junction has been restored to the live repository with RimWorld closed and the DLL hash verified. The matching [GitHub release is verified](../../Tests/GITHUB-CLOSEOUT-0.4.4.md). Subscriber-only gameplay smoke remains pending.

## Published copy archive

| Release | Published copy |
| --- | --- |
| 0.4.3 | [Initial release](history/WORKSHOP-PUBLISHED-0.4.3.md), [text maintenance](history/WORKSHOP-PUBLISHED-0.4.3-TEXT-MAINTENANCE.md), [final bug-fix notes](history/WORKSHOP-PUBLISHED-0.4.3-FINAL-BUGFIX.md), [final description](history/WORKSHOP-PUBLISHED-0.4.3-FINAL-DESCRIPTION.txt) |
| 0.4.2 | [Published copy](history/WORKSHOP-PUBLISHED-0.4.2.md) |
| 0.4.1 | [Published copy](history/WORKSHOP-PUBLISHED-0.4.1.md) |

## File layout for future updates

- `DESCRIPTION.txt`: current full Workshop description, with its compact Latest update block.
- `change-notes/<version>.txt`: separate change-note field. Use a dated suffix for another update of the same version; preserve previous published notes.
- `history/`: previous published descriptions and copy records, preserved before replacing current copy.
- This page: current publication status, field guidance and links to the evidence.

Keep the in-game About description in `About/About.xml` concise and feature-focused. Packaging still uses the repository-root `package-workshop.ps1`; documentation is not part of the Workshop package. See the [release index](../releases/README.md) for preparation and validation records.
