# Automatic Outfit Manager — 0.4.2 Workshop closeout

Published on Workshop **2026-09-08** (America/Denver). Public copy and all twelve downloaded files are verified, and the development junction is restored. See [0.4.2 Workshop closeout](Tests/WORKSHOP-CLOSEOUT-0.4.2.md). GitHub release/source-control closeout remains pending. The [0.4.1 closeout](RELEASE-HISTORY-0.4.1.md), [published Workshop copy](WORKSHOP-PUBLISHED-0.4.1.md) and [0.4.1 readiness](Tests/READINESS-0.4.1.md) are preserved. Item identity remains 3792731788.

## Current preparation

- [x] Preserve existing gameplay fixes and archived release records.
- [x] Align version metadata, About, Non-Work tooltip, player guide, changelog and separate draft Workshop fields at 0.4.2.
- [x] Refresh design, test guidance and current evidence without promoting unexercised cases to passed.
- [x] Complete and record build, 3,502 checks across 30 contract suites, seven focused negative controls and static checks in [copy preflight](Tests/RELEASE-COPY-AUDIT-0.4.2.md).
- [x] Install and hash-verify the three reviewed AOM skill-guidance updates, preserving backups and unchanged frontmatter/discovery settings.
- [x] Deploy 0.4.2 and prepare the existing-item in-game upload workflow as authorized. All twelve installed files matched the audited stage before upload. The temporary link has since been restored; see [package preparation](Tests/RELEASE-PACKAGE-0.4.2.md) and [closeout](Tests/WORKSHOP-CLOSEOUT-0.4.2.md).
- [ ] After an authorized deployment, inspect the 0.4.2 option tooltip and perform a load/UI smoke. Only text/version changes were added during this preparation; preserve unrelated gameplay evidence from the preceding tested hash.

## Focused gameplay still needed

The maintainer proceeded with publication. These remain follow-up coverage and are not recorded as passed.

1. **Ocagobrei Kitchen/Dining simple meal:** reproduce his original meal source and destination, follow pickup, outfit handoff and actual consumption without repeated preparation/restoration. The 4:05 small-map run used packaged survival food for him and ended before the old loop's onset. This known finding remains unresolved; other colonists' successful handoffs do not close it, and 0.4.2 does not claim its fix.
2. **Outside ingredient pickup:** follow Roboto across the Non-Work boundary with saved-personal preference unchecked and selected gear available outside. Require outfit completion before entry, then actual bill completion. Also resolve Zendaya's cancelled exact cooking continuation through a focused test; later hauling/eating only proves recovery.
3. **Unwearable child alternative:** after denying entry, confirm a child actually takes another available task outside. Loto's exit improved; Kyoto's final denial has no observed follow-up. Recheck child-sized routes and nearby protected storage.
4. **New transition save/reload:** save during preparation or exact restoration, reload that newly created file and confirm target/owner/queue continuity. The latest small-map run loaded the unchanged initial save; no fresh save was produced.

## Carry-forward coverage

Exact protected saved-item recovery now has concrete completion evidence: Jono via Bowman on the released 0.4.1 hash, and Ocagobrei via Jonah on the latest gameplay hash. Keep these passes scoped to their actual items, routes and builds. [Current readiness](Tests/NEXT-UPDATE-READINESS.md) records details.

Mental-state save/load and inactive shared-snapshot cleanup remain unverified from 0.4.1. Neither is closed by normal post-load restoration. A subscriber-only launch also remains distinct from downloaded-package byte verification.

## Publication and remaining closeout

- [x] Preserve the final DLL hash and 117 source/package input hashes. This records released content; the focused gameplay findings above remain open.
- [x] Retain and verify the existing eight-image gallery. [Optional replacements](Screenshots/CAPTURE-PLAN-0.4.2.md) remain suggestions, not completed captures.
- [x] Stage with package-workshop.ps1 and validate the exact twelve-file runtime allowlist, metadata, image limits and manifest.
- [x] Preserve PublishedFileId and verify the installed uploader source against the audited stage before launch. The maintainer completed the in-game update.
- [x] Verify the public 0.4.2 item, full description, compact update, separate change note, dependency, gallery and byte count. A visual UI/load smoke remains unverified above.
- [x] Verify Steam's refreshed subscriber manifest and all twelve files against the stage. Restore the development junction with RimWorld closed and verify the installed 0.4.2 DLL.
- [ ] Prepare the matching GitHub ZIP/release when requested; it must use the audited stage. The existing GitHub release remains 0.4.1.
- [ ] Commit/tag/push and publish final notes only when requested; record release date, source commit, asset hashes and any expressly deferred checks at closeout.
