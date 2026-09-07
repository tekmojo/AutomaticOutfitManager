# Work / Non-Work selection audit

Deployment completed with user authorization: `EF8F333C869E53591D94928C632CF335442D710C18ECAFF70931AB1983347871`. All pending runtime changes are deployed, including wording/header updates, selected-elsewhere versus retained markers, and bidirectional conflict validation. RC/live/installed hashes match. Build passes; 153 contract checks previously passed for this candidate. Only the runtime DLL was copied. RimWorld remained closed and was not launched. Native UI/gameplay verification remains pending. This supersedes earlier pending-deployment notes below.

## Implemented after user approval

Candidate `EF8F333C869E53591D94928C632CF335442D710C18ECAFF70931AB1983347871` is built, not deployed. Live and installed remain `F9850D4091CFF8745A2F78B8CADA4F482F77AD83640BE1D25825B2D16EE30E48`.

- Added shared selection classification, including disabled rules, other maps, and legacy weapon categories. Gear rows show [Selected elsewhere] for other rule selections and [Retained] only for remembered stock with no selecting rule. Source tooltips identify Non-Work fallback versus regular Non-Work requirements and inactive/other-map references. Neutral-grey conflicts and full source colors remain. Long names truncate with the Conflict suffix reserved.
- Added reverse Work-picker validation alongside the existing Non-Work check. Existing selections remain removable. Rule enable and area reassignment reject contradictory configurations without deleting requirements; Work readiness also identifies an affected Non-Work rule.
- Removal-window changes validate proposed All/individual selections without mutating the live rule. Newly introduced conflicts are rejected with named-rule messages; existing conflicts do not prevent changes that remove them.
- Configuration conflicts compare assigned maps. Historical issued-item return and source-identity tracking are unchanged. Unassigned pairs retain pre-existing configuration validation; one unassigned and one configured rule are checked when the area is assigned.
- Adding requirements, enabling a rule, and changing its area now use the existing requirement-change notification to refresh snapshots and safely recall affected sessions. No new transition route was introduced.
- Forget eligibility and blocker explanations now recognize legacy weapon categories as current rule selections.

Validation: build and whitespace checks pass. All 153 production-based contract checks pass (25 added for reverse candidates, both preference modes, selective/all removal previews, disabled/other-map rules, legacy/exact weapons, and retained classification). Existing snapshot ownership and entry-guard checks still pass. Native UI and gameplay checks are pending; no gameplay-loop resolution is claimed from headless tests.

Native follow-up: in both gear pickers, check selected-here/elsewhere/retained labels, inactive/other-map source references, long labels and grey conflicts. Try a Work candidate matching Dining fallback with its source selected for removal; confirm rejection and named explanation. Keep the source and verify valid sharing. Try All/individual removal, enable, and area reassignment with conflicting existing selections; verify no silent selection loss. Verify safe current-session recall after a valid requirement edit and no outfit swap loop during subsequent Work/Non-Work entry.

The findings below describe the pre-fix source and are retained as audit history.

Source audit prompted by Non-Work fallback shoulder pads appearing as retained stock in a Work picker. No gameplay or UI source changes made during this audit. Earlier pending UI changes remain undeployed.

## Findings

- Both pickers define retainedStock as not selected here plus membership in the managed definition index. Those indexes include requirements from all rules as well as remembered stock. Thus a current Non-Work fallback selection can be called Retained in another picker. The pending [Retained] marker inherits this overly broad classification.
- Fallback is an application mode, not a stock-retention state. With saved preference enabled, the selection applies only to pawns without a snapshot. With preference disabled, it is a regular Non-Work requirement.
- NonWorkFallbackPolicy.ConflictingSource only accepts Non-Work destinations. Adding a matching Work requirement can therefore invalidate an existing Non-Work removal/fallback combination without an Add-time warning. Runtime RuleEvaluator guards detect the resulting conflict for pawns actually using the selected requirements; saved-snapshot users bypass dormant fallback conflicts.
- WorkGearSnapshotPolicy excludes matching actual items from saved personal outfits when an enabled Work rule on the same map matches their definition and applicable standards. A Non-Work selection does not override this designation. Mere remembered-stock membership does not cause snapshot exclusion.
- Shared definitions are not inherently contradictory. A Non-Work rule can keep a Work source by leaving it unselected in Remove Work Outfits; ordinary apparel-layer and primary-weapon compatibility still apply. Assigned exact items remain subject to existing ownership/reservation checks.
- Current source lookup in NonWorkFallbackPolicy does not filter map, unlike UI GearSources and WorkGearSnapshotPolicy. A future conflict-policy change must explicitly reconcile configuration/map scope and historical source identities instead of copying the current UI source list blindly.

## Recommended follow-up

Distinguish current selections, selections by another named Work/Non-Work rule (show fallback mode when appropriate), and genuinely retained stock with no current selecting rule. Disabled/other-map selections must not be mislabeled abandoned stock just because GearSources hides them. Keep native Forget eligibility and exact-item ownership separate from the display classification.

Validate contradictory removal/selection combinations from either editor and when removal settings or rule state changes. Identify the affected rule before accepting a new conflict; avoid silently deleting saved selections. Preserve intentional shared requirements and exact saved-item protection. Cover apparel, exact weapon alternatives, legacy weapon categories, both saved-preference modes, partial/all removal, disabled rules, and map scope.

Validation: existing production-based rule/outfit contracts pass all 128 checks, including the failure-reporting check. These verify current policy behavior, not a new fix or native gameplay transitions. No claim of a reproduced gear loop is made from this screenshot.
