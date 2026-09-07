# Editable rule descriptions

> Historical implementation and test record. Earlier UI terms, palettes and candidate hashes below are preserved as evidence. Use the [0.4.0 player guide](../README.md), [copy audit](RELEASE-COPY-AUDIT-0.4.0.md) and [current readiness plan](NEXT-UPDATE-READINESS.md) for the current interface and release status.

Latest deployment: `F9850D4091CFF8745A2F78B8CADA4F482F77AD83640BE1D25825B2D16EE30E48`. All pending changes deployed with user authorization: restored explanatory selector hovers beneath concise colored headings, title-case row headers, responsive collapsed area/status layout, and removal of unrelated hover preambles. Detailed collapsed Area summaries are preserved. Build and whitespace checks pass; RC/live/installed hashes match. Only the runtime DLL was copied. RimWorld remained closed and was not launched. This supersedes prior pending deployment notes; native visual checks remain pending.

Candidate: `056EB6FCC69A8213E21626CD8B203FF21AC18F8F9B0CE570C9290D23C93F1E62`. User-authorized deployment completed. RC/live/installed hashes match. Only the runtime DLL was copied; RimWorld stayed closed and was not launched. Native UI/save-load verification remains pending. Includes pending expanded-selector and Custom areas tooltip simplifications.

Click the one-line description beside either rule badge to edit it, in expanded or collapsed view. The editor accepts up to 160 characters. Save stores it with the rule; Cancel/close discards edits. Use default clears it immediately and closes. Saving blank text or the current default also restores automatic wording. Editing a description never changes outfit requirements, access permissions, or live pawn jobs.

Automatic Work descriptions distinguish configured gear from access-only rules. Non-Work defaults follow all/selected/no Work-gear removal and the saved-personal preference or selected/empty fallback. Custom notes remain as written after settings change; the description hover always shows the current automatic summary separately. The full custom note is available on hover when the header truncates it. Badges retain their separate type/location help, map preview and camera-centering click.

Build and whitespace checks pass. 128 existing/new rule and outfit contracts pass, including two checks for legacy saves without the description key and loading a custom Unicode description. These use Scribe API doubles; native save/load and UI checks remain pending.

Manual checks: click both kinds of description in both views; edit/save/cancel; reset to default; save/load custom and default rules; change requirements and confirm only automatic text follows them. Check long descriptions, narrow headers, modal sizing, and that badge clicks still center the area rather than opening the description editor.
