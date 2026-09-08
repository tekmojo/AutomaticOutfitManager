# Locker overlap and storage warnings

Run `./Tests/run-locker-warning-contracts.ps1` for the production geometry,
storage assessment, cached warning construction, and overlay/camera requests.
The native UI/world effects are fixture hooks; the full build checks the actual
RimWorld API signatures. Run `run-storage-contracts.ps1` and
`run-saved-gear-recovery-contracts.ps1` for managed filters and restoration access.

The geometry uses actual painted cells, including disjoint areas, holes, multiple
overlapping Work rules and the current rule's own Work Area. Paused Work rules
remain protected; disabled rules do not count as overlap. Non-Work overlaps have a separate advisory row and overlay, without treating every shared cell as unusable.
Red indicates no standable changing cell outside the Work Areas, not an automatic
rule disable or a guarantee about an individual pawn's safety/reachability.

Native storage settings are evaluated separately. Exact saved items are checked
against `StorageSettings.AllowedToAccept`, so ordinary type/condition/quality and
managed filters participate. No storage space, reservations, paths or permissions
are created by a warning. Other reachable storage may still serve the saved outfit.

Cached snapshots refresh once per second of real time while the tab is open,
including when paused. Height calculation and drawing share one snapshot.
Map changes and closing the window discard the cache. Highlighting uses only
overlapping cells; the tab requests a persistent map-update overlay, and warning/button/badge hovers focus that locker; area colors and painted cells are not modified.

Manual checks after restarting with the deployed DLL:

- Open Jono's rule and hover the warning beneath Locker Room. Only the shared
  locker/Work cells should be marked. Click to center the map on the overlap.
- Collapse the rule: warnings should remain visible without overlapping its rows.
- Repaint part, then all, of the locker outside the Work Area; verify the warning
  updates even while paused. Check a locker with several overlapping Work rules.
- Allow Automatic outfit apparel and the exact saved jacket's type/condition/quality in
  storage outside the Work Area. The storage warning should clear when an
  accepting group exists there. Free space and reachability still matter.
- Hover Jono while the exact jacket remains blocked: the restricted rule should
  be named. Move the saved jacket to reachable accepting storage outside that
  restriction and verify the existing recovery completes. The warning does not
  itself move the item or bypass PPE.

Automated checks do not establish an in-game visual or gameplay pass.
