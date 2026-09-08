# Automatic outfit storage names and saved-item scope

The four player-facing categories are **Automatic outfit apparel**, **Automatic
outfit weapons**, **Non-automatic outfit apparel**, and **Non-automatic outfit
weapons**. Exact saved items use **Automatic saved apparel** / **Automatic saved
weapons** tags, including valid saved Non-Work preferences outside an active
outfit transition. Source-rule and retained-stock details remain visible.

Storage membership includes selected/retained types and exact borrowed/saved
items. It does not classify every copy of a saved item's type as automatic, or
extend gameplay ownership, forbidding rules, PPE exemptions or reservations.
`AutomaticOutfitStorageScope` is deliberately separate from the existing
gameplay classifiers. Deleted, orphaned and retired prototype saved preferences
do not acquire new storage membership.

Enabling an automatic category enables currently known saved/borrowed types as
well as selected/retained types. Existing ordinary filter choices are not
rewritten on load. An existing storage group may need the automatic category
toggled on again or its missing item types enabled manually. Ordinary condition,
quality, item exclusions and other native/mod storage filters still apply. Rule
gear standards do not filter exact saved restoration.

The Def names, save keys, worker class names and default checkbox settings stay
unchanged. Deploying the visible filter names requires the updated
`Defs/SpecialThingFilterDefs/SpecialThingFilters.xml` as well as the built DLL.

Validation:

- `run-storage-contracts.ps1`: current storage membership, enablement and native
  rejection boundaries, including inactive saved preferences and ordinary copies.
- `run-locker-warning-contracts.ps1`: Work and Non-Work overlap geometry,
  cached warnings and persistent map-update rendering requests. Warning, button
  and badge focus uses exact overlap cells. Closing the tab clears the overlay.
- `run-saved-gear-recovery-contracts.ps1`: existing recovery and source-area gates.
- `run-restoration-contracts.ps1`: existing restoration ordering and updated labels.
- Previous-policy negative controls reproduce both the inactive saved-membership
  gap and the missing saved-type enablement gap (see dated audit artifacts).

Manual checks after deployment/restart:

1. Open existing storage: renamed categories retain their saved check states.
2. Enable Automatic outfit apparel/weapons in a suitable locker. A currently
   saved personal type should be enabled even when no Work rule selects it.
3. With only automatic categories allowed, test a saved item and an ordinary copy
   of its otherwise unselected type. Only the exact saved item belongs there.
4. Verify a saved item rejected by storage condition/quality still produces an
   accurate warning; relax that storage limit to allow it. Do not change Work
   outfit standards to solve a storage restriction.
5. Open the mod tab: locker intersections with Work and Non-Work Areas remain
   highlighted while paused or running. Hover warnings, Locker Room buttons and
   badges to focus a locker; close the tab and verify overlays disappear.

Automated map-drawing hooks do not establish a native visual gameplay pass.
