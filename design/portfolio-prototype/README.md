# Portfolio UI reference

Entry file: **`design/portfolio-prototype/index.html`**. Keep `styles.css` and `app.js` beside it. Open the HTML directly in a modern browser; no build, package installation, API or network assets are required.

For a local preview, run from the repository root:

```sh
python3 -m http.server 5081 --bind 127.0.0.1 --directory design/portfolio-prototype
```

Open http://127.0.0.1:5081. The default view is reconciliation. The footer's **View design system** opens tokens, components and inspectable reconciliation/import states. Reload resets all example interactions.

## QA handoff

Continued the existing visual direction: dark neutral surfaces, restrained mint actions, amber review states, aligned tabular numbers and compact financial tables. No production frontend or backend files changed.

| Area | Result |
| --- | --- |
| Visual consistency | Compact snapshot disclosure keeps results prominent. Missing-from-ledger uses a filled violet journal badge; missing-from-expected uses an outlined document badge. Market-data degradation has a separate outlined status. |
| Responsive | Browser-checked at 1440, 1366, 1280, 1024, 768 and 390 px. Tables contain horizontal overflow; narrow views show a scroll cue and retain reconciliation symbols while scrolling. Summary metrics reflow, navigation becomes a rail then a menu, and dialogs stay within the viewport. |
| Accessibility | Visible focus, semantic controls/status text, native disclosures/dialogs, explicit Tab wrapping, Escape/Cancel dismissal, focus restoration, keyboard tabs and reduced-motion support. Eight dark text roles against the panel surface measure at least 5.69:1. This is a targeted pass, not a full assistive-technology certification. |
| Reconciliation | Read-only, with match/review/empty/loading/error examples. Cash and holdings show ledger minus expected; unavailable values are dashes, zeros are quiet. Changed inputs clear results. Unknown instruments and missing holdings have named states. Price availability is separate. |
| Corrections | Visible Correct action opens immutable original context plus editable replacement fields. Append correction preserves the original, marks it superseded and adds a linked active example row. Cancel writes nothing. |
| Thesis | Current version, mandate and active/inactive state are prominent. Creating or invalidating appends a version. Historical copies remain inspectable through disclosures. |
| Portfolio | All six holdings remain present. ASII has no price. Complete value/P&L are unavailable; priced subtotal and 5/6 coverage are explicitly partial. Stale data retains its date. |
| Import/export | JSON is canonical lossless backup/restore; CSV is transaction onboarding. Preview precedes explicit confirmation. Invalid examples disable Confirm. File, format, destination and portfolio changes invalidate the preview. |

## Tokens and components

`styles.css` contains semantic theme variables for canvas, sidebar, panels, raised/input surfaces, text, borders, action and status colors. Light mode overrides these aliases. Spacing follows 4/8/12/16/20/24/32/40 px; principal radii are 5/8/12 px. System fonts avoid downloads. Body text is 13 px, table values 11 px and headings 17/30 px; smaller metadata is subordinate. Monetary columns use tabular numerals and right alignment. Buttons and inputs share compact heights; shadows are reserved for overlays.

Reuse these visual boundaries when translating into React:

- App shell, navigation and portfolio/date/knowledge-cutoff context.
- Page header, panel, section heading and metric strip.
- Status badge, notice, state panel and loading skeleton.
- Data table with its own scroll region, numeric cell and instrument cell.
- Field, button, disclosure, tabs and accessible dialog.
- Correction lineage, thesis version timeline and import stepper.

Keep status vocabulary separate: reconciliation, event lifecycle, thesis lifecycle and market-data quality are different concepts. Do not use a single unexplained green/red indicator for them.

## Interaction limits

Everything is synthetic and held in memory. The prototype makes no API calls, reads no selected file contents and persists nothing. File selection retains only a name and checks extension/size; previews are fixed examples. Export shows explanatory feedback without generating a backup. Correction and thesis demonstrations do not recalculate portfolio fixtures.

There is one portfolio and one historical fixture (through 30 September 2026, known by 1 October 2026 at 08:00 WIB). Other dates produce an explicit fixture limitation. The example editor is bounded to 20 holdings. Prototype arithmetic is illustrative, not production accounting. Overview intentionally reuses the portfolio surface. Screener, Stocks and Market are reserved disabled navigation entries.

Browser acceptance uses Chrome; Safari/Firefox and screen-reader testing remain implementation checks. Dense tables intentionally scroll on narrow screens rather than dropping financial columns.

## React implementation guidance

Port the semantic markup, tokens and component boundaries into the existing frontend incrementally, starting with reconciliation. Reuse existing application contracts and state patterns. Do not port this file's fixture mutations or comparison arithmetic into production accounting.

.NET remains authoritative for ledger reconstruction, decimal values, comparison, revisions and valuation completeness. Bind only fields supported by actual contracts; verify summary metrics before displaying them. Preserve null/unknown values, original price dates, through dates and knowledge cutoffs. Convert the explicitly labelled WIB datetime at the API boundary without conflating it with a trade date.

Corrections submit new events with explicit lineage; thesis changes submit new versions. Keep original identifiers and recording times read-only. For imports, bind confirmation to the exact successful preview and invalidate it whenever file, format, mode or portfolio changes; discard stale async responses and use the backend's validation rules. Preserve the JSON/CSV distinction and existing preview/confirm contracts.

Retain table semantics, labels, focus restoration, Escape handling and reduced-motion behavior. Recheck text size at normal browser zoom and use real API error/empty/loading states. Build the requested surfaces first; future research screens need no implementation yet.

## Runnable browser check

Requires Node 22+ and an isolated Chrome with native DevTools enabled. Start the static server above. In a separate terminal on macOS:

```sh
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless --disable-gpu --no-first-run --no-default-browser-check --remote-debugging-port=9224 --user-data-dir=/tmp/idx-design-chrome-profile about:blank
```

Then run from the repository root:

```sh
node design/portfolio-prototype/check.mjs /tmp/idx-design-previews
```

The dependency-free check exercises reconciliation states/validation, correction append, thesis versioning, CSV/JSON confirmation, preview invalidation, responsive overflow, narrow dialog bounds, keyboard focus, text contrast, standalone file opening and absence of backend/external requests. It saves screenshots outside the repository. Visual inspection supplements these assertions. Stop the isolated Chrome when done; do not reuse a personal browser profile.
