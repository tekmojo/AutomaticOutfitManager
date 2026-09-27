# Borrowed gear tracking cleanup

User evidence: stored Heater shields retained the `Automatic outfit apparel — borrowed` inspection line after their stock type was no longer selected or retained. Individual apparel/weapon IDs survived completion and Forget, so storage classification could also remain automatic.

The component now prunes unused exact gear IDs after intervention completion, on Forget, before saving, and after load repairs. It retains active session targets, held issued gear surviving a partial Non-Work return, held pending shared returns, and meal-trip outfit references. Selected/retained type catalogs and inactive saved personal preferences continue to use their existing classification. No pawn gear, jobs, saved outfit preferences, or stock selections are changed by the pruning helper.

Validation:

- 33 new production cleanup/lifecycle checks, including extracted completion, Forget, load and ID-index methods.
- Negative control: the previous production component fails `returned gear loses stale tracking after state removal`.
- 296 existing storage checks, 322 Non-Work checks, and 50 restoration checks passed.
- Build and `git diff --check` passed. The fixtures use native API doubles; gameplay is not yet verified.

Built and deployed locally on 2026-09-26 while RimWorld was closed. Only the runtime DLL was copied. Candidate, live repository and installed junction hashes match:

`B7E0192DA0FF0AD9EC28653E3F276F20922987A28AB775039993DFC7812D1CA8`

Previous runtime hash:

`ED72D9E73C80344960E1090FE3E46BD20973FB4D5B7E733B500A013974784609`

Manual verification: launch RimWorld and load the affected save. Inspect a returned, unsaved shield whose type is no longer selected or retained: the borrowed line should clear and ordinary storage should accept it when the native filters allow it. Selected/retained stock should remain automatic until removed/forgotten. Check an active borrowed outfit and a saved personal outfit still return correctly. Game was not launched, and no Workshop update was published.
