# Work Areas inside Non-Work Areas

2026-09-05. Built, not deployed or tested in-game.

Baseline: this conversation's RC on `codex/workshop-release-prep`, HEAD `544c1f86ef10c46d7037b418b662aa5a5575e1e7`, with all intentional pending changes preserved. Baseline/live runtime SHA-256: `9D636037FF6F33065C7E1FCFB6C67B68EE40CE5581CE4D38B8F72171B797ED17`.

Candidate SHA-256: `3BA847D1538AAC8E60B963473298B2AEBF315936D89C42EFE64AA763C93F9996`.

Subsequent session-audit build: `CE5AE80115D3A6432FCB374919F965ED15C918C57238974DB55A430CA43FCCE9` also contains two diagnostic wording/ordering corrections. Nesting behavior is unchanged. All 561 checks passed again; this combined candidate remains undeployed.

## Behavior

- A nonempty Work Area wholly contained by an enabled Non-Work Area on the same map is a configuration conflict. Identical painted shapes and assigning the same area also conflict. Actual painted cells determine containment, including holes and disconnected sections.
- Both directions of area assignment and rule enabling use the existing configuration rejection path. Disabled counterparts impose no restriction. Pausing a rule does not remove the configuration restriction.
- Readiness uses neutral muted conflict text; the tooltip names the rules and areas in their map colors and describes the resolution. Disabled inner Work rules retain an explanation while the enabled outer Non-Work rule still contains them.
- Existing saves and repainting are checked on the normal 30-game-tick maintenance pulse, before outfit enforcement. Each conflicting inner Work rule is disabled once, retains its settings and managed stock catalog, and uses the existing requirements-change recall path. Exact tracked gear returns through the normal safe transition. No direct garment removal or job tracker mutation is added.
- The outer Non-Work rule remains enabled. Fix the painted areas or disable the outer rule, then manually enable the Work rule. No automatic re-enabling occurs.
- Partial overlaps, Work inside Work, Non-Work inside Non-Work, and a smaller Non-Work Area inside a larger Work Area retain existing behavior. Locker areas are not evaluated by this policy.

## Verification

Build passed. All 561 headless checks passed: 26 new area nesting checks, 178 Non-Work contracts, 52 meal/buffer contracts, 26 activity checks, and 279 storage checks. `git diff --check` passed (existing line-ending configuration warnings only).

The new tests exercise containment direction, equality, holes, partial overlap, empty/missing areas, map identity, disabled counterparts, recovery after repainting, one-time disable notification, and preservation of the outer rule. These do not simulate live pawn restoration or native UI.

## Pending in-game checks

1. Assign a small Work Area inside an enabled Non-Work Area, then try the reverse assignment order. Both assignments should reject with the named conflict; the previous assignment should remain unchanged.
2. Configure one rule while disabled, then try enabling it. Enabling should reject until its conflict is resolved.
3. Repaint an existing enabled pair into full containment, including while a Work pawn is active. Within the next maintenance pulse the inner rule should disable once, report the reason, and recall tracked pawns safely without repeated notifications or outfit cycling.
4. Load a save already containing the conflicting pair and verify the same recovery. After resolving the layout, re-enable the Work rule and verify normal preparation.
5. Check partial overlap, an outer area with an unpainted hole, opposite-direction nesting, and locker selection remain usable. Inspect both collapsed and expanded conflict hovers.

Deployment update — 2026-09-05: User-authorized deployment completed. RimWorld was closed; installed junction targeted C:/GitHub/AutomaticOutfitManager. Only the runtime DLL was deployed. Candidate/live/installed SHA-256 all match CE5AE80115D3A6432FCB374919F965ED15C918C57238974DB55A430CA43FCCE9. Includes the area-nesting restriction and both diagnostic corrections. The build and all 561 checks passed before deployment. Game remains closed; in-game validation of these changes is pending. This supersedes earlier undeployed status in this document.
