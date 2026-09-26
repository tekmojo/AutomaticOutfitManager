# Access-only Work Area routing — September 25 candidate

The maintainer authorized the recommended fixes and local deployment after the construction/transit diagnosis. Version remains 0.4.3. This candidate is separate from the published maintenance package.

**Follow-up observation:** `UcGhfx3Tc1.mp4` and its 8,804-line log confirm ordinary construction progress on the deployed F30038F9 build: 21 successful delivery endings, completed perimeter construction, three prepared-job resumptions and two completed restorations. Four captured path failures remain, all denied child destinations. The separate [child delivery follow-up](CONSTRUCTION-CHILD-ACCESS-2026-09-25.md) is now built and locally deployed; its in-game retest is pending. The original build/contract/deployment record below is retained; the earlier pending ordinary-construction check is now observed, while actual PPE-boundary construction and new-state save/reload remain unverified.

## Change

- Identify Work rules without required apparel, exact weapons or legacy weapon categories as access-only. Non-Work saved-outfit and stock-return rules remain distinct.
- For permitted ordinary jobs, omit access-only Work rules from outfit-related path avoidance. Keep denied activities, paused restrictions, unchecked child access and unrelated outfit areas enforced.
- Apply the same distinction to buffered transit and protected-transit session matching, avoiding unnecessary reroutes and transit-only outfit sessions.
- Recognize a construction driver's current adjacent cell for HaulToContainer to a live Frame/Blueprint while carrying material, or FinishFrame to a live Frame. Require the same current job/map and actual cell destination within the building's expanded footprint. Keep real boundary access and required-gear checks active. Do not broaden building membership to every possible adjacent cell.
- Keep owned outfit-transition routing unchanged. No save keys, version identifiers or UI descriptions change.

## Verification

- Build succeeds with the installed RimWorld assemblies.
- 73 routing/presentation checks pass, including 25 access-only/construction checks. Production restriction selection, access-only/weapon properties and construction-destination helper execute against deterministic native-type fixtures. These verify the decision, not full native pathfinding or delivery completion.
- The prior production restriction decision fails the new allowed-empty-Work regression. Removing only the construction-destination exemption fails the adjacent delivery regression while the access-only fix remains enabled. Both negative controls exit normally and report the intended failure.
- 600 paused hauling/restoration checks pass, including production permission checks and empty-rule transit-session exclusion.
- 94 meal/buffer/childcare controller checks pass, including no reroute for permitted access-only transit and continued avoidance for denied access.
- 214 native rule-control checks pass, exercising the production next-cell guard, suspension and admission blocks, plus integration wiring checks.
- Total: **981 passing checks**, plus two negative controls. Actual native construction completion and route choice still require the next in-game session.
- Encoding scan: 112 source/XML/project files, 1,571 compiled user strings, no findings. `git diff --check` passes.
- Older fixture dependencies were refreshed: the routing fixture now includes the child policy boundary, and the native control runner compiles the production child-access helper. Supporting pause/buffer fixtures explicitly represent the new property; the actual property is tested by the routing suite.

Candidate DLL SHA-256: `F30038F990615E73C70E58D64A902AD7394096C6B2B4841CAD8FD9D7DE8F0566`.

Prior published maintenance DLL SHA-256: `4CA32704045B015AB50A6FA20AFA236A72E717E90DDF812F785830CB1A0E9B32`.

## Deployment and next observation

Deployed with RimWorld closed. RC, live and installed DLL hashes all match the candidate above; the installed junction targets the live repository. The result and timestamp are saved in deployment.json. The old installed DLL is backed up in the local evidence folder. Only the runtime DLL is deployed; the separate live source checkout is preserved. No Workshop/GitHub update or source-control operation is included.

Launch manually through Steam, load the same save with the Anomaly rule unchanged, and watch delivery to the southern perimeter door frames. Confirm material reaches the frame and construction proceeds, ordinary permitted traffic crosses the empty-outfit area, and an unchecked child/prisoner remains denied. Then check one adjacent construction task at an actual outfit-required boundary for equip-before-entry. Do not mark native delivery or outfit-boundary gameplay passed from the contract tests alone.

Evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/access-only-routing-fix-2026-09-25/`. Diagnosis and video frames: `../transit-diagnosis-2026-09-25/` under the same audit root.
