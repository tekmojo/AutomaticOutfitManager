# Workshop formatting standard

Use Steam BBCode in all paste-ready Workshop text. Use Markdown only in repository documentation. Match the established 0.4.4 change-note presentation; a new release must not silently switch to plain hyphen bullets.

## Separate change note

Use this structure for every new change note and same-version maintenance update:

```text
[b]<version> - <short sentence-case summary>[/b]
[list]
[*]<Player-visible change. Add one short qualification only when needed.>
[*]<Next change.>
[/list]
<Brief save compatibility, migration or rule-default note, when relevant.>
```

- One bold heading, with version first and the same ` - ` separator.
- One actual `[list]` block with one `[*]` per change; no Markdown headings, `**bold**`, hyphen bullets or manually typed bullet glyphs.
- Use concise active statements (Fix, Clear, Prevent, Allow). Keep one distinct change per bullet and end each with a period.
- Put compatibility information in a plain paragraph after the list. Omit it when it adds no useful information; do not invent a no-save-format-change claim when a saved field was added.
- Steam supplies the author and publication date, so do not repeat them in the heading.
- Do not paste local build hashes, test counts, internal source names or the full mod description into a change note.

## Full Workshop description

Keep the established order: title/tagline, concise purpose/dependency line, compact Latest update quote, feature/getting-started sections, compatibility/help and legal notice. Preserve the existing feature-section names/order unless the content actually requires a change.

- Use `[h1]` for section headings, `[b]` for short emphasis, `[list]`/`[*]` for features and `[olist]`/`[*]` for sequential setup.
- Use `[url=...]label[/url]` for links. Never paste Markdown links.
- Keep `[quote][b]Latest update - <version>[/b]` to one or two short summary sentences plus the existing Full change notes link. The detailed bullets belong in the separate change-note field.
- Use blank lines between major blocks, not between every list item. Keep terminology/capitalization consistent: Work Area, Non-Work Area, Locker Room, Task Buffer and saved personal outfit.
- The in-game About/loading description stays concise, feature-focused plain text, without release notes or Steam-only BBCode.

## Preflight and correction

Compare the new note with the previous published note before handoff. Check BBCode pairing, list markers, heading format, links and the compact update block. Verify the rendered result on Steam after publication; a text match alone does not prove formatting.

Preserve published text as dated history before editing a local copy. For a formatting-only correction to an already published note, use that entry's **Edit** control and replace its text. A DLL rebuild, mod reupload or additional change-note entry is unnecessary. Track the local correction as pending until the public rendering is verified. Do not rewrite older published notes merely to impose the standard retroactively.
