# Release documentation

Start with the [current release checklist](CHECKLIST.md) and [remaining gameplay checks](../../Tests/NEXT-UPDATE-READINESS.md). The latest published version is **0.4.4** on Steam Workshop and GitHub.

## Current release

- [Workshop upload text and instructions](../workshop/README.md)
- [0.4.4 package preparation](../../Tests/UPLOAD-PREP-0.4.4.md)
- [0.4.4 Workshop verification](../../Tests/WORKSHOP-CLOSEOUT-0.4.4.md)
- [0.4.4 GitHub verification](../../Tests/GITHUB-CLOSEOUT-0.4.4.md)
- [Player changelog](../../CHANGELOG.md)
- [Test and audit index](../../Tests/README.md)

## Earlier release records

These are dated records, not the current checklist. Their validation limits and publication claims remain historical evidence.

| Release | Records |
| --- | --- |
| 0.4.3 | [Initial release](history/RELEASE-HISTORY-0.4.3.md), [text maintenance](history/RELEASE-HISTORY-0.4.3-TEXT-MAINTENANCE.md), [final bug-fix update](history/RELEASE-HISTORY-0.4.3-FINAL-BUGFIX.md) |
| 0.4.2 | [Release record](history/RELEASE-HISTORY-0.4.2.md) |
| 0.4.1 | [Release record](history/RELEASE-HISTORY-0.4.1.md) |
| 0.4.0 | [Release record](history/RELEASE-HISTORY-0.4.0.md) |
| 0.3.x | [Release records](history/RELEASE-HISTORY-0.3.x.md) |

Published Workshop copy is indexed in the [Workshop archive](../workshop/README.md#published-copy-archive).

## Keeping this organized

Keep one current `CHECKLIST.md`. When starting the next candidate, preserve the completed checklist in `history/` and update this index. Keep player-facing changes in the root changelog, upload text in `docs/workshop/`, and detailed test, preparation and closeout evidence in `Tests/`. Link to the existing records instead of copying their contents into new summaries.

Documentation maintenance after publication does not require rebuilding the mod or moving an existing release tag.
