# Map-area selector colors — 2026-09-05

> Historical implementation and test record. Earlier UI terms, palettes and candidate hashes below are preserved as evidence. Use the [0.4.0 player guide](../README.md), [copy audit](RELEASE-COPY-AUDIT-0.4.0.md) and [current readiness plan](NEXT-UPDATE-READINESS.md) for the current interface and release status.

Previous baseline: included in repair/gear-route candidate `25A061DF947AF4E36BE75911A08DC090293B605C572A7F2A659E1310F5DD6ACE`. RC/live/installed DLL hashes match after user-authorized deployment with RimWorld closed. Visual validation remains pending.

Latest deployment: `F9850D4091CFF8745A2F78B8CADA4F482F77AD83640BE1D25825B2D16EE30E48`. All pending changes deployed with user authorization: restored explanatory selector hovers beneath concise colored headings, title-case row headers, responsive collapsed area/status layout, and removal of unrelated hover preambles. Detailed collapsed Area summaries are preserved. Build and whitespace checks pass; RC/live/installed hashes match. Only the runtime DLL was copied. RimWorld remained closed and was not launched. This supersedes prior pending deployment notes; native visual checks remain pending.

Latest deployment: `056EB6FCC69A8213E21626CD8B203FF21AC18F8F9B0CE570C9290D23C93F1E62`. Editable saved rule descriptions, concise expanded area/locker selector hovers, and the shortened Custom areas tooltip are deployed with user authorization. Collapsed Area hover summaries are preserved. Build and whitespace checks pass; 128 rule/outfit contracts previously passed for this candidate. RC/live/installed DLL hashes match. RimWorld remained closed. Supersedes pending deployment notes for these changes below; native UI/save-load checks remain pending.

Latest deployment: `45F2B734DCD3255EEBFE54EA20BC63827B98BCE052E0FFBB94A1E043EBF4F76A`. Tooltip audit, neutral muted states, native checkbox icon colors, and bright tooltip text with colored references deployed with user authorization. RC/live/installed hashes match; build and whitespace checks pass. Only the runtime DLL was copied. RimWorld remained closed. This supersedes pending deployment statements for these changes below; in-game hover/visual checks remain pending. [Tooltip audit](TOOLTIP-AUDIT.md).

Latest deployment: `F92BE0E2F82C0E0E70BA26C3783F38821A59E22B9605037BAB73591B408515A1`. User-authorized runtime DLL deployment completed with RimWorld closed; RC/live/installed hashes match. Includes badge click-to-center, grouped custom-first area menus, named selected-gear tooltips, accurate existing source names in apparel conflicts, and neutral muted grey for all conflict indicators/explanations. Build and whitespace checks pass. This supersedes pending deployment statements for these changes below; native UI validation remains pending. Game not launched.

The [session audit](../../testing/2026-09-05/HNcINJdhz5/audit.md) prompted the associated [gameplay fixes and manual checks](GEAR-THRASH-FIXES.md). The earlier UI-only candidate `70F77EDD843768261E37736498D26E9E483603D2B91C64C9309D1D354F2D3A4D` was superseded before deployment.

Deployed UI follow-up: the removal button now fits its text, and both gear pickers show other rules' non-conflicting apparel/weapons in the same full source-area color. Conflicts retain muted source colors; ordinary layer conflicts remain grey. Both Work and Non-Work badges now highlight their assigned map area on hover in expanded and collapsed views, using the existing area-selector preview logic. Badges also show "Rule Name - Work Area" or "Rule Name - Non-Work Area" in both views. Badge width follows its text up to 55% of the header; long names shorten while preserving the type, with the full name on hover. Work/Non-Work and locker-room dropdown entries now use the same readable area colors and preview the hovered area on the current map. The no-locker option stays neutral. Non-Work active sessions use "Occupants:" and "No active or returning occupants". "Work:" remains the work-job permission category for both rule types, with a clarified tooltip. Area/rule and locker names in status, source/conflict, badge, selector, removal-list and readiness hovers use map-area colors. Field labels remain neutral. Build and whitespace checks pass. User-authorized deployment completed with RimWorld closed; RC/live/installed SHA-256 all match `936A25CF9E6D3084EFB0974FAB60A9927DFB0BB7538EFAFC90334A45CAE1813A`. Only the runtime DLL was copied. Game not launched; native visual checks remain pending.

Pending follow-up: clicking a Work or Non-Work badge centers the camera on the midpoint of the painted area bounds, in collapsed and expanded views. Hover preview remains. Unassigned/empty areas do not move the camera; current-map resolution shares the existing gravship-safe hover resolver. Build and whitespace checks pass; not deployed. Native camera/visual verification pending.

Pending menu organization: both area selectors group ordinary editable Area_Allowed instances first under Custom areas, followed by special-purpose area classes under Native / mod areas. Each group is alphabetized; nonselectable headings mark the boundary, and No locker room remains first. Hover previews and colors remain. Classification uses runtime type, not names or creation order; a mod-created ordinary Area_Allowed cannot be distinguished from a player-created one and shares the custom group. Build passes; native menu layout/order check pending; not deployed.

Pending tooltip follow-up: selected apparel and weapons say "Selected for this rule (Rule Name)" with the rule name in its map-area color. Applies to Work and Non-Work rules. Build and whitespace checks pass; not deployed.

Pending conflict-tooltip correction: the proposed destination rule is named separately from each garment's existing selected sources. Formal vest selected by Kitchen now shows Kitchen, rather than the temporary USS assignment used only to evaluate adding it. Multiple current sources are listed; unselected gear says not currently selected. Source names retain map-area colors. No conflict eligibility changed. Build and whitespace checks pass; not deployed.

Latest pending styling correction: every conflict label, disabled Conflict button, and conflict explanation uses the shared neutral muted grey. Embedded source-name colors are neutralized within conflict explanations, and conflicting rows omit source-color marks. Normal selected/source gear colors remain unchanged. Supersedes the earlier tinted-conflict design below. Build and whitespace checks pass; not deployed.

Pending muted-state consistency correction: locked/disabled Work-outfit removal rows now use the shared neutral muted grey. Removed the area-tinted muted helper entirely; muted source labels/marks also resolve to neutral grey. Enabled, editable rule rows and ordinary source references retain full area colors. Checkbox selection and locking behavior are unchanged. Build and whitespace checks pass; not deployed.

Pending checkbox color correction: Work-outfit removal rows color only the label text. Checkbox textures use white GUI tint, retaining native red X / green check colors and normal disabled rendering. Selection and full-row click behavior remain unchanged. Build and whitespace checks pass; not deployed.

Latest pending tooltip correction: hover explanations use normal text with full area-colored rule/area references, including conflict, blocked-button and readiness tooltips. Only conflict rows/buttons and locked/disabled row labels are muted grey. Removes the blanket tooltip color stripping/muting from the earlier implementation. Includes pending neutral-muted rows and native checkbox-color corrections. Build and whitespace checks pass; not deployed.

Latest pending tooltip audit: missing empty-state, gear-row and dropdown option help added; Work/Non-Work policy, readiness, recall and locker wording corrected. [Scope, coverage and validation](TOOLTIP-AUDIT.md). Candidate `45F2B734DCD3255EEBFE54EA20BC63827B98BCE052E0FFBB94A1E043EBF4F76A` builds; not deployed.

## Presentation

- Rule badges and selected gear use the assigned map area's color. Work/Non-Work text still distinguishes types in expanded and collapsed cards. Unconfigured rules use neutral text; very dark area colors are lightened while retaining their hue.
- Apparel, Weapons, both Fallback labels, and policy descriptions return to neutral text.
- Other enabled rules on the same map that select a gear definition supply matching full source-area colors for non-conflicting apparel and weapons. This includes both Work and Non-Work rules. Multiple sources use up to three small color marks; the tooltip lists all sources. Selected rows retain their own rule's main color.
- Work-outfit removal conflicts use muted source colors. Weapon-overlap conflicts identify the relevant overlapping rules. Existing layer conflicts remain standard grey. Conflicts still have their text, explanations, disabled Add action, and Remove action for existing selections.
- The Work outfits to remove list uses each source area's color when editable, and neutral muted grey when All locks the row or the source rule is disabled. Checkbox state and selection behavior are unchanged.
- Retained stock without another enabled source on this map remains cyan. Sorting and Forget behavior are unchanged.

RimWorld's installed `Verse.Area.Color` property was verified, and the build compiles against the actual game assemblies. Build and `git diff --check` pass. The color changes themselves do not affect gameplay logic and have no dedicated automated UI tests. The combined deployed candidate also contains the separately documented gameplay fixes.

## Visual checks — pending

1. Assign different map colors to Kitchen, USS and a Non-Work area. Confirm badges and selected picker entries follow them, while Apparel/Weapons/Fallback labels stay neutral.
2. Inspect shared gear with one and several source rules. Check muted hues, separate marks, complete hover names, and readable text for very dark colors.
3. Verify a Non-Work removal conflict uses source colors and remains blocked; an ordinary apparel-layer conflict stays grey. Existing conflicting selections remain removable.
4. Toggle All in Work outfits to remove. Selected/locked and deselected/editable behavior must remain unchanged.
5. Rename/recolor an area, inspect collapsed cards, and inspect a rule with no area. No color should become a runtime rule identity or affect gameplay eligibility.

Pending collapsed-layout correction: readiness width now follows its measured text within the available summary space, right-aligned before Pause/Resume with a fixed gap. Area names and readiness messages are independently truncated without wrapping, and the detailed summary tooltip stops before the button. Full area/status details remain on hover. Build and whitespace checks pass; not deployed. Native long-name/status checks pending.
