# 0.4.0 focused readiness plan

The [release checklist](../RELEASE-CHECKLIST.md) is the current status. The [historical audit log](NEXT-UPDATE-HISTORY-2026-09-06.md) preserves older tests and superseded findings.

The latest small- and large-map normal-flow sessions are healthy for their observed scope on DLL `98BC3B34BCEFF8443174D7472BB27908FCE090B5249411FDA98733FA0096A4E0`. The 0.4.0 copy/inspection candidate is a new build; its [copy audit](RELEASE-COPY-AUDIT-0.4.0.md) records validation. Retain prior gameplay evidence unless a later change affects it.

Local deployment verified on 2026-09-06T19:08:41.8328008-06:00: current RC/live/installed DLL `F00A4B06756118DBE8C6EA50681980F6A52248024F2AD1AA811761C65B08AF24`, with matching About and Def descriptions. The maintainer subsequently reported **blocked saved-item recovery, cooking after a Non-Work outfit change and Delete confirmation passed**. No new log/video was attached to that confirmation; keep it distinct from the earlier independently reviewed recordings. All **1,954 checks in 21 suites** passed again and the matching package is staged; see [package validation](RELEASE-PACKAGE-0.4.0.md).

## Focused regression matrix

Rows 1, 2 and 4 have maintainer-reported scenario passes. This matrix preserves the detailed observations and variants for future regression checks, without claiming a fresh trace for every variant. The complete staged-package load/UI check is still pending; the measured performance baseline is optional and no CPU/TPS improvement is claimed.

| Order | Focus | Pass observation |
|---|---|---|
| 1 | Small map: exact saved item blocked in a protected area | Eligible hauler moves that exact item to valid owner-reachable storage; both pawns progress and owner restores. No destination-claim mismatch or repeated empty return. An unavailable destination waits without churn. |
| 2 | Large map: new cooking task conflicts with retained Non-Work outfit | Outfit change begins before the first ingredient pickup, resumes the intended bill and reaches actual bill completion. Only successful eligible jobs credit the associated buffer. |
| 3 | Copy candidate: both saves and a new rule | Readable labels and relevant hovers at actual UI scale; Work/Non-Work source labels and saved/retained distinctions; colors on references with neutral grey conflicts; no duplicate area/rule preamble or clipping. |
| 4 | Delete dialog | Cancel, Enter and Escape preserve; explicit Delete removes exactly the chosen Work/Non-Work rule from expanded/collapsed views. |
| 5 | Consistent performance sample | Record map scale, logging level, simulation speed and measured tick/frame behavior. Compare aligned tick windows; no general performance claim from diagnostic counts. |

For tests 1–2, retain the short recording and Detailed log from before the trigger through completion. If a run does not reach the trigger, mark it unexercised rather than passed or failed. Include save/load during an affected transition if it has not yet been demonstrated on the same gameplay implementation.

Eight supplied screenshots are now cataloged in [the replacement gallery](../Screenshots/README.md). They support inspection of the visible UI, but do not close the full interactive/save-load check in test 3. Replace all previous public gameplay screenshots when publishing the update. The next release step is verification of the staged package and publication surfaces; another broad exploratory audit is not required by these completed scenario reports.
