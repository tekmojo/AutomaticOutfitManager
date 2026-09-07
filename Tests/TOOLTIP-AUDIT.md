# Rule editor tooltip audit

> Historical implementation and test record. Earlier UI terms, palettes and candidate hashes below are preserved as evidence. Use the [0.4.0 player guide](../README.md), [copy audit](RELEASE-COPY-AUDIT-0.4.0.md) and [current readiness plan](NEXT-UPDATE-READINESS.md) for the current interface and release status.

Deployment completed with user authorization: `B525AD5BF6861B8B388A060657580552426F9940D84485A3AEB0DC87BCDD5259`. Activity coverage and per-rule cache fixes are deployed. Candidate/live/installed hashes match; build and whitespace checks previously passed. Only the runtime DLL was copied. RimWorld remained closed and was not launched. Native activity-list verification remains pending. This supersedes pending-deployment notes below.

Pending activity coverage correction: Workers/Occupants now include observed personal activity and idle/direct-order states in or approaching the area after outfit sessions finish. Hauling/wandering remain separate, and observed personal rows do not create sessions or offer Recall. Per-rule cache pruning avoids hiding actors with an outfit session elsewhere. Coverage and empty-state tooltips updated. Candidate `B525AD5BF6861B8B388A060657580552426F9940D84485A3AEB0DC87BCDD5259` builds and passes whitespace checks; not deployed. Session audit: work/testing/2026-09-05/IbpdHDzUBw/audit.md. Native verification pending.

Latest deployment completed with user authorization: `D56ED4AD5E8897A46121AB85E7A8312899188E77AD8145BA0348AC70CF0D6029`. Selected by source headings now lead both gear pickers' row tooltips; confusing/redundant prose is removed. RC/live/installed hashes match. Candidate build and whitespace checks passed. Only the runtime DLL was copied. RimWorld remained closed and was not launched. Native visual verification remains pending. This supersedes pending-deployment notes below.

Pending source-heading cleanup: Selected by now leads apparel and weapon row tooltips whenever the item has a selecting rule. Removed the vague rules-listed-below sentence and redundant generic fallback sentence. Current selections include the current rule; conflict headings list only rules actually selecting the hovered item, while the explanation still identifies conflicting requirements. Rule/area colors and policy explanations remain. Build and whitespace checks pass; not deployed.

Latest deployment completed with user authorization: `7D26C284F36187D511156DC6EC7B394305CFA0DB3545BA57BDD5F3C16F089A26`. Rule-name prefixes in both gear pickers, including +N for multiple sources, are now deployed along with all earlier changes. RC/live/installed hashes match. The candidate previously passed build and whitespace checks. Only the runtime DLL was copied after RimWorld closed; the game was not launched. Native visual checks remain pending. This supersedes pending-deployment notes below.

Pending source-name labels: both gear pickers now show [Rule Name] in place of [Selected elsewhere]. Multiple selecting rules display the first name plus +N, with every source still listed in the hover. Prefix width is limited so the item name remains visible; full names remain in the hover. Source colors, grey conflicts, and [Retained] are preserved. Candidate `7D26C284F36187D511156DC6EC7B394305CFA0DB3545BA57BDD5F3C16F089A26` builds and passes whitespace checks. Not deployed; native visual checks pending.

Deployment completed with user authorization: `EF8F333C869E53591D94928C632CF335442D710C18ECAFF70931AB1983347871`. All pending runtime changes are deployed, including wording/header updates, selected-elsewhere versus retained markers, and bidirectional conflict validation. RC/live/installed hashes match. Build passes; 153 contract checks previously passed for this candidate. Only the runtime DLL was copied. RimWorld remained closed and was not launched. Native UI/gameplay verification remains pending. This supersedes earlier pending-deployment notes below.

Latest pending candidate: `EF8F333C869E53591D94928C632CF335442D710C18ECAFF70931AB1983347871`. Selected-elsewhere versus retained classification and bidirectional Work/Non-Work conflict checks are implemented; source references identify fallback, disabled rules, and other maps. 153 contract checks, build, and whitespace validation pass. Not deployed. See WORK-NON-WORK-SELECTION-AUDIT.md for scope and native follow-up. This supersedes the earlier broad [Retained] marker note below.

Pending retained-stock clarification: both pickers prefix unselected managed-stock entries with [Retained], including conflicting/source-owned entries, and no longer use cyan as a stock indicator. Full source-area colors and neutral-grey conflicts remain. Hover text explains that stock tracking does not require wearing/equipping the item, plus Add/Forget actions; count help explains that the marker does not guarantee available stock. Candidate `8A67022F8FD3CF78C82F5DBCD68399B16BEA5A5C0752E253ADD861489E17798E` builds successfully. Live/installed remain `F9850D4091CFF8745A2F78B8CADA4F482F77AD83640BE1D25825B2D16EE30E48`; not deployed. Native visual validation remains pending.

Pending outfit terminology audit: automatic descriptions now say Return work outfits, Return selected work outfits, or Keep work outfits. Related return statuses, selector help, removal-window copy, gear-policy help, and current README settings copy use outfit terminology for whole-outfit actions. Individual/shared-item explanations retain gear terminology. Custom descriptions are preserved. Includes the pending Work Outfits to Remove heading, All Work Outfits option/button, and conflict wording refinements. Candidate SHA-256: `D775CDE87F086F91741494DBB8D8C5C413C08251070815BA89D75937042B1AED`. Build passes; live and installed remain `F9850D4091CFF8745A2F78B8CADA4F482F77AD83640BE1D25825B2D16EE30E48`. Not deployed; native visual checks remain pending.

Pending approved wording refinement: both gear pickers and readiness help now say "change the 'Remove Work Outfits' selection". Build and whitespace checks pass; not deployed.

Pending wording clarification: apparel and weapon removal-conflict hovers now say the gear matches a named rule selected under Remove Work Outfits, and direct the player to choose different gear or change that selection. Related readiness/count help uses the exact Remove Work Outfits label. Removed redundant snapshot implementation wording from the conflict reason; the existing gear-policy explanation remains. Colored references and neutral conflict rows are preserved. Build and whitespace checks pass; not deployed.

Latest deployment: `F9850D4091CFF8745A2F78B8CADA4F482F77AD83640BE1D25825B2D16EE30E48`. All pending changes deployed with user authorization: restored explanatory selector hovers beneath concise colored headings, title-case row headers, responsive collapsed area/status layout, and removal of unrelated hover preambles. Detailed collapsed Area summaries are preserved. Build and whitespace checks pass; RC/live/installed hashes match. Only the runtime DLL was copied. RimWorld remained closed and was not launched. This supersedes prior pending deployment notes; native visual checks remain pending.

Latest deployment: `056EB6FCC69A8213E21626CD8B203FF21AC18F8F9B0CE570C9290D23C93F1E62`. Editable saved rule descriptions, concise expanded area/locker selector hovers, and the shortened Custom areas tooltip are deployed with user authorization. Collapsed Area hover summaries are preserved. Build and whitespace checks pass; 128 rule/outfit contracts previously passed for this candidate. RC/live/installed DLL hashes match. RimWorld remained closed. Supersedes pending deployment notes for these changes below; native UI/save-load checks remain pending.

Candidate: `45F2B734DCD3255EEBFE54EA20BC63827B98BCE052E0FFBB94A1E043EBF4F76A`.
User-authorized deployment completed. RC/live/installed DLL hashes match the candidate above. Only the runtime DLL was copied; RimWorld remained closed and was not launched. Native hover validation remains pending.

Scope: the AOM rule editor, apparel/weapon pickers, area/locker dropdowns, saved-snapshot menu, Work outfit removal window, and pawn activity/status hovers. This is a source-based UI copy audit, not a release/package preflight or a native hover-rendering pass. Existing pending muted-row, checkbox-icon and readable-tooltip corrections are included.

## Findings corrected

- Workers/Occupants captions and empty states had no tooltip. They now explain tracked outfit sessions, qualifying jobs, preparation/return/restoration and the separate Haulers/Wanderers rows. An empty list does not mean the physical area is empty. Populated Haulers/Wanderers captions now have help too.
- Ordinary unselected gear labels had no tooltip, even when another rule selected that type. They now identify the destination rule, source rules, Add action and current rule policy.
- Work-only gear wording was misleading for Non-Work fallback settings. A shared policy explanation now accompanies picker count, row and Add tooltips. Existing snapshots take priority when enabled; missing saved gear does not activate fallback. Unchecked preferences and empty categories are described separately from Work-gear removal.
- Removal All, deleted-source rows and the empty-source state lacked direct help. Source rows now explain selected/unselected/locked states, shared retention and apply-on-close behavior.
- Area and locker menu entries, group headings, No locker room, snapshot pawn choices and the no-eligible-pawns choice now have native option tooltips. These preserve the existing area previews. Custom classification is described as editable allowed-area type, without claiming author provenance.
- Readiness no longer implies every rule requires both apparel and weapons, or that stock counts guarantee access. Non-Work snapshot readiness and fallback counts are distinguished.
- Recall wording now applies to either rule type. Untracked entries describe the observed missing-session condition rather than claiming every brief observation proves a protection failure. Pause wording no longer implies ordinary transit bypasses AOM checks.
- Locker help differentiates Non-Work fallback, exact personal gear and the narrow source-first carried-meal handoff. It does not promise a universal source-first transition for every job.
- Source lists retain colored references but omit repeated general color instructions. Hover prose remains normal-brightness text; conflict row/button and disabled-row muting remain separate.

## Coverage reviewed

| Surface | Coverage |
| --- | --- |
| Rule creation, naming, enable, collapse, delete, map editing | Existing help reviewed; map editing now names both rule types. |
| Work/Hauling/Wandering group headers, All and individual permissions, child watching | Existing help present; Work is explicitly a job category for both rule types. |
| Badge, area/locker dropdowns and groups | Hover, click, area reference and grouping help present. |
| Task buffer, apparel/weapon availability, condition and quality | Existing help reviewed against successful-task counting and personal-restoration exemptions. |
| Saved preference, snapshot inspection, fallback labels, removal selector | Policy and state-specific help present. |
| Gear search/count, selected/unselected/retained/conflicting rows, Add/Remove/Forget | Explanations cover states and rule/source identity; existing Forget blocker messages retained. |
| Readiness, pause/resume, Workers/Occupants and empty states, pawn recall, Haulers/Wanderers | Help present and corrected as above. |
| Native window close buttons / standard RimWorld dialogs | Native behavior retained; no custom close controls introduced. |

Build against installed RimWorld/Harmony and whitespace checks pass. No gameplay logic changed and no redundant automated wording tests were added. Native visual validation remains required: hover all listed states in both rule types, including disabled options, empty Occupants, long/colorized source names, and overlapping tooltip regions. Confirm no clipped or competing tooltip and that ordinary text is bright while references retain area colors.

Pending selector simplification: selected Work/Non-Work area and locker buttons now show only their own field name and colored selected area name (or None selected). Detailed behavior help remains on the field labels; hover map preview is unchanged. Build and whitespace checks pass; not deployed.

Pending approved copy simplification: Custom areas heading tooltip now says only "Editable allowed areas, sorted by name." Grouping behavior is unchanged. Included with pending expanded-selector hover simplification; not deployed.

Pending editable descriptions: description hovers expose full text, click-to-edit behavior, and current automatic settings summary. The editor explains Save, Cancel, Use default and its text field. [Scope and checks](RULE-DESCRIPTIONS.md).

Latest pending correction: expanded Work Area, Non-Work Area and Locker Room selector tooltips retain their concise field-specific, colored selected-area heading and again include the relevant general behavior explanation beneath it. Label and selector help share the same text to avoid drift. Non-Work explanation respects selective/shared work-gear removal. Collapsed Area summaries are unchanged. Supersedes the earlier one-line-only selector interpretation. Build and whitespace checks pass; not deployed.

Pending row-header capitalization: Work Area, Non-Work Area, Locker Room, Task Buffer, and Remove Work Outfits now use title case. Default to Saved Personal Outfit retains lowercase "to". Build and whitespace checks pass; not deployed.

Latest pending hover-context audit: Workers/Occupants help starts with its own explanation; expanded Readiness starts with its status. Repeated rule/area/locker preambles are removed. Removal-source hovers retain only Source Rule; badge hovers retain their rule and assigned area, without unrelated locker information. Expanded selectors retain the relevant field heading and restored explanation. The approved detailed collapsed Area summary and colorized meaningful references are preserved. Build and whitespace checks pass; not deployed.
