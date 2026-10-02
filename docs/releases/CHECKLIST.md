# 0.4.6 release checklist

Published through the maintainer's RimWorld Mods-menu update of existing public item 3792731788. On 2026-10-02 the maintainer requested final closeout with remaining troubleshooting deferred to a later patch. Published 0.4.5 is preserved in [history](history/RELEASE-HISTORY-0.4.5.md).

## Candidate and copy

- [x] Preserve accumulated locker, saved-outfit and retained-item changes.
- [x] Align About/project/assembly/package version to 0.4.6; keep About feature-focused.
- [x] Update player guide, status/tooltips, design, changelog and separate Steam BBCode fields.
- [x] Archive previous release/readiness/Workshop description before replacement.
- [x] Record normal-flow gameplay evidence without claiming untested persistence or guest cases passed.
- [x] Build and run 1,082 focused checks plus four negative controls; validate encoding/XML/assets/links and record hashes.
- [x] Stage twelve allowlisted files and verify installed contents with RimWorld closed.

## Maintainer and distribution

- [x] Record final-runtime gameplay and visible revised saved-outfit/locker text.
- [x] Upload through RimWorld Mods to existing item 3792731788 (maintainer).
- [x] Verify public metadata, compact description and rendered separate change note.
- [x] Verify all twelve refreshed subscriber files against the audited stage.
- [x] Confirm RimWorld is closed and restore the temporary junction to the live repository.
- [x] Record the maintainer's decision to defer remaining investigation and manual checks.
- [ ] Complete authorized commit/tag/push and matching GitHub release; record remote digests.

## Explicitly deferred

About-card/downed-status capture, preserved-autosave reload, subscriber-only gameplay, blocked-item recovery/capacity and other focused gameplay/compatibility cases remain unverified. They are carried in [patch follow-up](../../Tests/PATCH-FOLLOWUP-0.4.6.md), not counted as passes. These deferrals no longer hold release closeout.

[Readiness](../../Tests/NEXT-UPDATE-READINESS.md) retains validation limits. [Upload preparation](../../Tests/UPLOAD-PREP-0.4.6.md) records the frozen build/package, and [closeout](../../Tests/RELEASE-CLOSEOUT-0.4.6.md) records publication and asset evidence.
