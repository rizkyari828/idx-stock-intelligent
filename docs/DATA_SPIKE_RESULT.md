# Phase 0 Data Spike Result

## Status as of 2026-09-24

- Overall foundation: **BLOCKED** for unattended canonical ingestion pending rights clarification and source/semantics repair.
- Security master: **PARTIAL** technical feasibility; rights `UNCLEAR`.
- Market calendar: **PARTIAL** holiday-announcement feasibility; rights `UNCLEAR`; full session status unproven.
- No OHLCV, IHSG, universe backfill, production provider, or strategy work was performed.

## Security Master & Calendar Spike

### Rights and access gates

[IDX terms](https://www.idx.co.id/id/syarat-penggunaan) explicitly disallow web scraping/crawling, even where noncommercial quotation is allowed with attribution and access date. Commercial use/redistribution requires prior written permission. IDX profile, listing-activity, and holiday pages were researched without automated collection. A website or discoverable endpoint is not an approved data feed.

KSEI exposes an explicit [monthly master download](https://web.ksei.co.id/archive_download/master_securities) and [holiday notice PDF](https://web.ksei.co.id/files/1767843003_Penyesuaian_Pengumuman_Hari_Libur_dan_Cuti_Bersama_PT_KSEI_Ta....pdf). Both direct files returned HTTP 200 without login during a single bounded test. KSEI's disclaimer endpoint returned HTTP 500 during review; no explicit permission for recurring automated retrieval, local raw retention, or redistribution was established. Status remains **UNCLEAR**. No rate limit was found; absence of a published limit is not unlimited permission. Stop routine fetching until rights are clarified.

### Retrieval and preserved evidence

Exactly two file GETs were made after two HEAD size checks. Each GET was capped at 2 MiB, timed out after 30 seconds, and not retried. Raw bytes are in Git-ignored `data/raw` via the existing version-1 manifest contract. Request parameters were empty; no authentication, cookies, browser automation, CAPTCHA, private API, or workaround was used.

| File | Request / source | Fetched UTC | HTTP / type | Bytes | SHA-256 | Local raw URI | Header evidence |
|---|---|---|---|---:|---|---|---|
| KSEI master snapshot | [StatisEfek20260831.txt.zip](https://web.ksei.co.id/Download/StatisEfek20260831.txt.zip) | 2026-09-24 15:26:49 | 200 / application/zip | 155,962 | `8044caf8d83a609f90e3b74ddee5c95dc6e852efd0f40ecdb93f8ea7460dc9a8` | `80/8044caf8d83a609f90e3b74ddee5c95dc6e852efd0f40ecdb93f8ea7460dc9a8.zip` | Last-Modified 2026-08-31 23:46:07 GMT; ETag `"2613a-65a6067f732b3"` |
| KSEI amended 2026 holiday notice | [PDF](https://web.ksei.co.id/files/1767843003_Penyesuaian_Pengumuman_Hari_Libur_dan_Cuti_Bersama_PT_KSEI_Ta....pdf) | 2026-09-24 15:26:49 | 200 / application/pdf | 1,036,672 | `ca444520365cb1d8a74eed4c0e9c72c5718333e9a2745b1002bf6256c454a96e` | `ca/ca444520365cb1d8a74eed4c0e9c72c5718333e9a2745b1002bf6256c454a96e.pdf` | Last-Modified 2026-01-08 06:58:41 GMT; ETag `"fd180-647daf0598cce"` |

The archive-only manifest parser version is `archive-only-1`. The KSEI text inspection version is `ksei-master-inspect-1` and produces research-only candidate records. Raw artifacts are not tracked in Git. HTTP headers were noted here; automated HTTP metadata capture is still a follow-up item.

### SECURITY MASTER — PARTIAL

The ZIP contains one pipe-delimited text member dated 2026-08-31 with 28 named columns. The local parser inspected 3,655 well-formed records and detected two malformed physical lines (633–634) that split one non-equity description. It identified 982 `Type=EQUITY` candidates, 538 structured warrants, 11 warrants, and other debt/crowdfunding classes. EQUITY is a provider category, **not yet proof that every row is an ordinary share**. There were no duplicate equity codes or ISINs within this one snapshot. Ninety-one EQUITY rows have a blank `Listing Date`, including BBRI; these remain unknown, never inferred or zero-filled.

`Isin Code` is a plausible security-level key, but stability through ticker changes, issuer mergers, delisting, and reissuance has not been tested. `Status=ACTIVE` denotes an undefined KSEI record status here; it must not be interpreted as an unsuspended, tradable share. `Sector` exists, but versioned classification meaning is unverified. No board/segment or ticker-history field was established. The file includes a snapshot population, not a demonstrated historical delisted universe. A strict canonical ingestion must reject/quarantine malformed records and unresolved required dates rather than silently repair them.

**Required now:** verified security identity, code, class, listing boundary, and current listed population. **Optional later:** sector, board, issuer linkage, delisting and symbol-event history. **Unavailable/unverified:** complete delisted population, historical symbol/board/sector states, security-type coverage for rights/ETFs, and explicit ongoing-use permission. Future all-market historical research is **BLOCKED** for survivorship bias until those gaps are resolved or explicitly scoped to surviving securities only.

### MARKET CALENDAR — PARTIAL

The KSEI notice dated 2026-01-08 amends an earlier 2025-10-01 notice and references IDX announcement Peng-00171/BEI.POP/09-2025. It lists 22 weekday closures for 2026, including 2026-01-01 and the exchange-specific 2026-12-31 closure. The notice says schedules may change with IDX or Bank Indonesia announcements. These are announced closures of KSEI operations; they are useful corroboration, not a full observed IDX trading-session ledger.

The Friday 2026-01-02 is treated in a test as an **explicit fixture** for a trading session; the holiday PDF itself does not prove the exchange actually traded that day. Saturday 2026-01-03 is a weekend. An unobserved weekday remains `UNKNOWN`, not automatically `TRADING`. The .NET calendar evidence store keeps duplicate evidence idempotent, returns dates in order, and rejects conflicting evidence pending a revision policy. This validates chronology behavior with fixtures but is not ingestion from the PDF. The PDF was archived but no production PDF parser was added. Exceptional exchange closures, instrument suspensions, and no-trade days are not supplied by this document.

### Separate gate assessment

| Gate | Security master | Market calendar |
|---|---|---|
| Direct access | Succeeded once | Succeeded once |
| Parser/normalizer | Partial; malformed rows and missing dates detected | PDF content reviewed; no automated source parser |
| Semantics | Instrument type and candidate ISIN understood partly | Announced weekday closures understood; actual sessions unproven |
| Rights for unattended personal/local automation | UNCLEAR | UNCLEAR |
| Historical adequacy | Monthly snapshot observed; delisted/ticker history unverified | One 2026 revision tested; earlier years and exceptional updates unverified |

## Request and maintenance cost

Measured transfer: 2 GETs, 1,192,634 bytes total; curl transfer times 0.221 and 0.463 seconds (0.684 seconds summed). Two HEAD size checks preceded them. There was also one failed KSEI disclaimer GET (HTTP 500). Search/browser research requests are not included in these transfer figures. These measurements cover only two small files; no sustainable cadence or long-term storage cost was measured. Maintaining a licensed feed would require monitoring file publication, column drift, amendments, identity changes, and exception notices.

## Outcome and next gate

Neither source is PASS. The immediate next step is to obtain written or clearly documented KSEI terms for personal automated downloading and archiving, plus an authorized IDX calendar/security-master route. Then validate historical snapshots, ticker/delisting events, and an actual observed trading-session source before canonical PostgreSQL ingestion. OHLCV remains a later experiment.
