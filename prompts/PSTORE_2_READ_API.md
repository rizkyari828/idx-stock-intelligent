# Personal store — phase 2: read-only API

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash

Routing note: rated NORMAL/BALANCED (qwen3.6 via Ollama). Ollama is NOT installed, so run
on Flash. No escalation needed for a bounded read-only endpoint.
```

Depends on phase 1: the files `data/personal/bars/<YYYY>/<YYYY-MM-DD>.json` must exist.

## Task

One read-only endpoint that serves the personal store, entirely separate from the canonical
reader and database:

`GET /api/personal/bars?from=YYYY-MM-DD&to=YYYY-MM-DD` (both optional; default: newest
seven session files) returning:

```json
{
  "source": "PERSONAL_FILE_STORE",
  "generatedAt": "...Z",
  "sessions": [
    { "sessionDate": "2026-10-26", "sessionProof": "UNPROVEN_PERSONAL", "capturedAt": "...",
      "rows": [ { "symbol": "BBCA.JK", "open": "…", "high": "…", "low": "…", "close": "…",
                  "adjustedClose": "…", "volume": 123, "qualityStatus": "UNPROVEN_PERSONAL" } ] }
  ]
}
```

Rules:

- Read-only. No writes, no canonical queries, no DB connection, no caching table.
- Values stay **exact decimal strings**; never parse to floating point.
- Missing directory, empty range or a malformed file must produce an explicit, bounded
  error/empty result — never a partial silent success.
- Validate `from <= to` and a sane bound (reject absurd ranges with a clear message).
- The response must state `sessionProof` per session so a consumer cannot mistake personal
  capture for a proven session.

## Read first

`src/IdxStockIntelligence.Api/Program.cs` (endpoint style, `/api/data-health` from phase A),
an existing read-only Infrastructure reader for shape, `src/IdxStockIntelligence.Api/*.csproj`
for the content-root conventions.

## Do not

- Do not reuse or modify the canonical bar reader, `CompletedSessionPolicy` or any migration.
- Do not resolve the data directory from a user-supplied absolute path; keep it relative to
  the application content root.
- Do not add authentication, caching or background refresh.

## Acceptance

- Focused tests: happy path, missing directory, malformed JSON, reversed range, and a
  session file with `UNPROVEN_PERSONAL` surfaced verbatim.
- Full root `dotnet test` from the repo root passes (baseline 1,271 total / 1,067 passed /
  204 skipped / 0 failed); report new totals.
- `git diff --check` clean; commit scoped; no push. Report the exact JSON shape.
