# Screener V0.2 Level-1 source acquisition decision

Slice **5A.2**, checked **2026-10-07 UTC**. Research and documentation only.
This decision applies to the retained bounded universe of ten equities; it does
not authorize acquisition, source admission, adapters, schema deployment or a
daily runner. The [frozen evidence contract](SCREENER_EVIDENCE_V0_2_CONTRACT.md),
[persisted binding](SCREENER_EVIDENCE_V0_2_PERSISTED_BINDING.md) and existing Slice
5A ingestion boundary remain unchanged.

## Decision and immediate gate

**C — `LEVEL1_SOURCE_STACK_NOT_YET_FEASIBLE`.** Official downloadable artifacts
exist for several individual claims, and manual acquisition is a realistic
candidate. A complete permitted zero-cost/manual combination is **not yet
demonstrated**. No paid source is proved necessary; outcome D is not justified.

The decisive gaps are:

1. Positive authoritative `TRADING` at the exact target session, or an applicable
   continuing state with demonstrably complete subsequent transition coverage.
   No tested daily status artifact establishes this for the bounded universe.
2. JK endpoint-specific genuine-price semantics: non-synthetic observations,
   placeholders/substitution, currency/price-unit binding and retained revision
   identity. Raw-field documentation alone does not prove these premises.
3. Complete corporate-action/identity/currency continuity for the entire technical
   seed and recurrence segment. Individual events are available; no tested public
   coverage artifact establishes the required no-break conclusion.
4. Independent completed-session evidence across the warmup, current authoritative
   board/mechanism applicability, listing gaps and source-specific retention rights.

Quantity/volume units, actual traded value, liquidity and benchmark/RS coverage
are **feature-local**, not additional mandatory Level-1 gates. Missing quantity
proof does not invalidate an independently authenticated price. Ambiguous zero
bars still fail the genuine-price gate.

## Fresh repository and operational observations

Initial branch `main`, HEAD `26d6df9948b67e5ff31c1eafec99933aefa72f68`, clean,
**8 ahead / 0 behind** the local `origin/main` reference. No fetch was required.
Read-only inventory confirms schema versions **2, 4, 5, 6**; the V0.2 evidence
table is absent. There are **45** registered EODHD raw artifacts, **45/45** intact
by SHA-256 and byte length, **9 DEGRADED** ingestion runs, **86** canonical bar
revisions and **0** database market-session rows. The source registry contains
only `eodhd`, with permission **UNKNOWN**. One immutable Decision Snapshot run
has ten rows and **0** committed V0.1 Outcome rows.

Slice 5A.1's retained session ledger and reference findings remain historical
inventory, not newly admitted V0.2 evidence. No current web fact was backdated into
them. FullIdx remains disabled and soak remains **1/10**; neither was advanced.

## Access and source ledger

All links below were checked directly unless explicitly marked discovery-only.
Search results located candidates; their snippets were not admitted evidence.
Public HTML/PDF success means reading succeeded without login, not that routine
automation or indefinite archival use is licensed. `UNKNOWN` retention below
means no sufficient source-specific grant was established, not a proven ban.
No undocumented internal API or browser challenge was used.

| ID / organization / exact source | Format and access observed | Free, account and acquisition mode | History, revisions and retention |
|---|---|---|---|
| K1 KSEI [master index](https://web.ksei.co.id/archive_download/master_securities), [2026-09-30 download](https://web.ksei.co.id/Download/StatisEfek20260930.txt.zip) | HTML and ZIP containing pipe-delimited text; ordinary bounded GETs both HTTP 200; web reader cannot parse ZIP | Public download, no account/login/payment; MANUAL_RETAINED_ARTIFACT candidate; recurring automation permission UNKNOWN | Index links Jan–Sep 2026 snapshots; older depth not established. Snapshot date exists, native correction lineage not documented. Original bytes downloadable; retention rights UNKNOWN |
| K2 KSEI [registered shares](https://web.ksei.co.id/services/registered-securities/shares), [BBCA detail](https://web.ksei.co.id/services/registered-securities/shares/lc/BBCA) | Public HTML with identity/type/exchange/currency and price-history table; directly readable | No account/login/payment; MANUAL_RETAINED_ARTIFACT candidate; no admitted automated feed | Current point view; historical identity intervals/revisions not established. Original HTML can be saved technically; retention UNKNOWN |
| K3 KSEI [2026 holiday notice](https://web.ksei.co.id/files/Pengumuman_Hari_Libur_dan_Cuti_Bersama_PT_KSEI_Tahun_2026.pdf), [schedule index](https://web.ksei.co.id/services/schedule-c-best-instruction) | Actual three-page PDF readable; index links annual calendars, calendar-link fetch failed in web reader | Public, no login/payment; MANUAL_RETAINED_ARTIFACT candidate | Notice has publication/reference identity and amendment caveat. It governs KSEI operations, cites IDX calendar; exchange applicability must be authenticated separately. Retention UNKNOWN |
| K4 KSEI [current announcements](https://www.ksei.co.id/en/publication/announcement), [action calendar](https://www.ksei.co.id/en/service-support/schedule/schedule-of-corporate-actions) | Public HTML with dated references, PDF downloads and action categories | No login/payment for tested pages/PDF; MANUAL_RETAINED_ARTIFACT candidate; bulk automation unapproved | Pagination/month view exists; exhaustive historical/cancellation coverage and snapshot exports not proved. No native complete-coverage statement observed; retention UNKNOWN |
| K5 KSEI [HMETD index](https://web.ksei.co.id/publications/corporate-action-schedules/rights-distribution), [BUVA rights notice](https://web.ksei.co.id/Announcement/Files/BUVA_RIGHT_20261015_ID.pdf) | Actual dated two-page PDF with security identities and segment-specific schedule | Public, no login/payment; MANUAL_RETAINED_ARTIFACT candidate | Event reference and publication date visible; current index has month/year controls. Event evidence, not universal absence coverage; retention UNKNOWN |
| K6 KSEI [merger/split/reverse category](https://web.ksei.co.id/publications/corporate-action-schedules/masr), [ADMF–MFIN final merger schedule](https://web.ksei.co.id/Announcement/Files/185416_ksei_23593_jku_0925_202509262039.pdf) | Category HTML and actual three-page PDF readable; a different discovered XL/FREN PDF returned 404 | Public, no login/payment for successful files; MANUAL_RETAINED_ARTIFACT candidate | Final schedule references an earlier announcement and separates conversion, suspension and delisting dates. Does not prove all-event coverage; retention UNKNOWN |
| K7 KSEI [bonus discovery page](https://web.ksei.co.id/publications/corporate-action-schedules/share-bonus?setLocale=en-US), [C-BEST action guide](https://web.ksei.co.id/Download/Corporate%20Action/Pedoman_fasilitas.PDF) | Bonus direct fetch returned 500; actual ten-page guide readable | Bonus result DISCOVERY_ONLY. Guide public, but C-BEST reports require participant access/permissions, not a free personal API | Guide describes bonus/rights/stock-dividend/merger/split/reverse/conversion categories, not completeness of public publications; retention UNKNOWN |
| K8 KSEI [AUTO dividend notice](https://www.ksei.co.id/storage/announcements/AUTO_DIV_20261015_ENG.pdf) | Actual one-page PDF with reference, publication and separate ex/record/payment dates | Public, no login/payment; manual event candidate | Demonstrates current downloadable event publication, not complete coverage. Cash dividend preserves frozen price-only treatment; retention UNKNOWN |
| K9 KSEI [disclaimer](https://web.ksei.co.id/disclaimer) | Ordinary bounded GET HTTP 200; web reader returned 500 | Public, no login/payment; rights review only | Accuracy disclaimer and copyright observed; no explicit recurring acquisition/private archival grant established |
| I1 IDX [stock summary](https://idx.co.id/id/data-pasar/ringkasan-perdagangan/ringkasan-saham/), [data FAQ](https://data.idx.co.id/faq.php?p=sFWjtRbH1z2wag4) | Both direct web fetches HTTP 403; indexed download/field claims unverified | DISCOVERY_ONLY in this review; free manual download/format/login requirements NEEDS_CONFIRMATION | History, completeness, no-trade conventions, units, corrections and retention not verified. No internal endpoint guessed or tested |
| I2 IDX [company profiles](https://idx.co.id/id/perusahaan-tercatat/profil-perusahaan-tercatat/), [special monitoring roster](https://www.idx.id/id/perusahaan-tercatat/daftar-efek-pemantauan-khusus) | HTML shells with Loading/download UI, no actual dated roster/export obtained | DISCOVERY_ONLY; manual export candidate unverified; login buttons do not establish mandatory login | No retained effective board/listing/status roster; no revision/completeness proof |
| I3 IDX [trading hours/mechanism](https://www.idx.id/id/produk-layanan/jam-dan-mekanisme-perdagangan/) | Actual public rule-explanation HTML, naming II-A/II-X versions and market segments | Public, no login/payment to read; MANUAL_RETAINED_ARTIFACT candidate | Current explanation is not a historical instrument roster or actual session-completion fact. Operative rule/effective interval needed; recurring scraping unapproved; retention UNKNOWN |
| I4 IDX [suspension index](https://idx.co.id/id/berita/suspensi/), [GGRP reopening notice](https://www.idx.id/StaticData/NewsAndAnnouncement/ANNOUNCEMENTSTOCK/From_EREP/202508/ec0ab4408d_be31903c8f.pdf) | Index HTTP 403; actual one-page official PDF readable, with notice/suspension references and segment/session scope | Index DISCOVERY_ONLY; PDF MANUAL_RETAINED_ARTIFACT candidate, no login/payment | Positive reopening event possible; not normal status for ten different instruments/current sessions. Subsequent coverage/corrections/retention unresolved |
| I5 IDX [terms](https://www.idx.co.id/id/syarat-penggunaan), [circulars](https://idx.co.id/id/peraturan/surat-edaran/), [2026 data catalogue](https://www.idx.id/media/auobyarx/idx-catalogue-pricelist-updated-2026.pdf) | Direct reads failed; no current replacement terms or catalogue admitted | Historical repo review records scraping restriction and licensed paid data products; those are historical findings, not fresh availability proof | Website crawling remains blocked pending an authorized route. Manual private archival rights and paid product specifications need confirmation |
| B1 BCA issuer [share chronology](https://www.bca.co.id/id/tentang-bca/Hubungan-Investor/Informasi-Saham/Kronologis-Pencatatan-Saham), [action index](https://www.bca.co.id/id/tentang-bca/tata-kelola/aksi-korporasi), [split schedule](https://www.bca.co.id/-/media/Feature/Report/File/Aksi-Korporasi/Aksi-Korporasi-Lainnya/20211007-pengumuman-jadwal-dan-tata-cara-pemecahan-nilai-nominal-saham-stock-split-id.pdf) | HTML and actual two-page historical PDF readable, not just an indexed title | Public, no login/payment; MANUAL_RETAINED_ARTIFACT candidate; no routine scraping approved | Historical positive event and named market-effective dates available. Mutable chronology alone is not coverage/transition proof; retention UNKNOWN |
| E1 EODHD [EOD field documentation](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes) | Current public HTML; endpoint describes JSON/CSV, no market-data request made | Third-party observation candidate; documentation free/no login. Actual JK calls require own registered key; demo does not cover JK | Free past year potentially covers warmup; documentation is not authenticated JK observations. No raw-bar native correction feed documented |
| E2 EODHD [splits/dividends](https://eodhd.com/financial-apis/api-splits-dividends) | Current public HTML with endpoint/event schemas; no data request | Discovery/corroboration only for authoritative actions; free endpoint entitlement not independently tested | Documented events/adjustments do not prove rights/bonus/merger and no-break completeness |
| E3 EODHD [source partners](https://eodhd.com/financial-apis/our-data-sources-and-data-partners), [terms](https://eodhd.com/financial-apis/terms-conditions), [limits](https://eodhd.com/financial-apis/api-limits) | Actual public documentation readable | Free documentation; account required for data. Free allowance 20 units/day, EOD one unit/request; generic minute limit 1,000, account-specific header authoritative | Private noncommercial storage/analysis permitted for qualifying registered users; redistribution prohibited. Post-termination durable rights unresolved. Named exchange agreements do not identify JK upstream; other feeds include market makers/CFDs |

Unpublished official-source rate limits, historical depth or correction promises
remain **unknown**, rather than assumed unlimited. No subscription, participant
account, CAPTCHA/WAF bypass, access-token recovery or source contact occurred.
OJK is relevant to governing regulation, but no claim was assigned to an untested
OJK market-data/status/completeness feed. Secondary news/aggregators were not
promoted into official authority.

## New artifact inspection findings

K1 was saved **only in temporary research storage**, not the repository/operational
archive. ZIP: **145,689 bytes**, SHA-256
`5ff2815dc72fcb1c12c6b489492ce7b497644766b28b1283d93a4805c73e043e`.
Existing `ksei-master-inspect-1` read **3,678 well-shaped records**, **981 EQUITY
candidates**, **2 malformed lines (3619, 3620)** and **90 equity candidates without
listing dates**. All ten configured equity codes are present. Within that panel,
BBRI, LPIN, PTRO and RAJA lack listing dates; VKTR has 2023-06-19. This is an
inspection result, not a production import or universal ordinary-share assertion.

The file fits the 4 MiB raw bound but its full record population exceeds the
existing 512-record ingestion bound. Do not silently truncate/filter an all-market
import or expand FullIdx. Per-security official artifacts are the smaller candidate
for bounded admission; the master inspector remains a research cross-check.

K2 positively identifies BBCA, its ISIN, ordinary-share type, IDX and IDR at the
observed point. Its `Active` means the published registration status, **not**
session-specific `TRADING`. K1/K2 show listing 2000-05-31 while B1's chronology
labels IPO 2000-05-11. These may describe different milestones; obtain the operative
IDX listing notice/definition before mapping either date to `ListingCoverage`.
Do not manufacture a same-claim conflict or choose a date by convenience.

K9 temporary HTML: **25,249 bytes**, SHA-256
`9d9d6794c2bef4d42e93f8ebeca5660f979f0be2a2e8f9545a06b52c8c028457`.
Its successful inspection closes the earlier disclaimer-access uncertainty, **not**
the source-permission question. Hashes prove downloaded identity, not completeness,
source truth or license.

## Source-to-claim admission matrix

IDs link to exact sources in the ledger. Results assess the proposed claim/source
combination, **not operational admission now**. `CONDITIONALLY_ADMISSIBLE` denotes
an observed relevant source with specified unmet bindings/permission; it does not
permit setting VERIFIED today. T1 is claim-specific operative evidence, not a
blanket organizational grant; EODHD stays T3 for its own observations.

| Claim | Candidate source | Authority | Free | Manual | Automated | Historical | Revision visible | Retain raw bytes | Admission result | Missing premise |
|---|---|---|---|---|---|---|---|---|---|---|
| Stable identity / exchange / issuer | K1/K2 | T1 exact depository record candidate | Yes, public | Yes | Access works; rights unknown | Monthly snapshots/current detail | Snapshot date; no full chain | Technically yes; rights unknown | CONDITIONALLY_ADMISSIBLE | Exact local-ID/ISIN/code interval; retained point scope; permission |
| Ordinary security type | K2, issuer security disclosure | T1 exact issuer/depository claim | Yes | Yes | Unapproved | Current point/historical notices | No universal history | Yes technically; rights unknown | CONDITIONALLY_ADMISSIBLE | EQUITY label alone not ORDINARY; per-security classification and interval |
| Listing / delisting | I2; B1 reviewed issuer; K6 merger schedule | T1 operative IDX; T2 reviewed issuer where allowed | Tested documents free; roster unknown | Documents yes; roster unproved | Unapproved | Individual dated facts exist | Notice references; roster unknown | Documents yes; rights unknown | NEEDS_CONFIRMATION | Operative listing date; active point/continuation; missing panel dates; exact delisting act |
| Board and current mechanism | I2 roster + I3 operative rules + I4 exception notices | T1/T2 as claim admitted | Readable rule free; export unproved | Candidate | Unapproved | Rules/notices exist; full history unproved | Rule/notice IDs; incomplete chain | Yes technically for readable sources; rights unknown | NEEDS_CONFIRMATION | Actual dated board roster, effective rule and instrument exceptions |
| Positive TRADING | I1 exact daily status/remarks specification; I4 complete transitions | T1 required | Free daily artifact unproved | Candidate, not proved | No approved route | Exact target history unproved | Notice IDs; transition coverage incomplete | Unknown for daily artifact | NEEDS_CONFIRMATION | Positive exact-session status; authoritative FULL completeness/continuation |
| Suspension / reopening event | I4 exact PDF + referenced suspension | T1 exchange act | Yes, tested PDF | Yes | Scraping blocked/unapproved | Example 2025 retained URL | Notice/predecessor references | Yes technically; rights unknown | CONDITIONALLY_ADMISSIBLE | Required predecessor, full scope, timestamps and archive permission; not generic TRADING |
| Scheduled closures / exceptions | K3 + cited operative IDX calendar/amendments | T1 IDX calendar or admitted T2 closure reference | K3 yes | Yes | Unapproved | Actual 2026 PDF | Reference + change caveat | Yes technically; rights unknown | CONDITIONALLY_ADMISSIBLE | Exchange applicability/latest amendments; exceptional closures; calendar not completion |
| Completed exchange session | I1 independent dated exchange closing report; admitted independent report alternative | T1/T2, independent of selected bars | Not established across window | Candidate | No approved route | Full sequence unproved | Unproved | Required artifact not obtained | NEEDS_CONFIRMATION | Exact date, independent completion and completed_at across warmup |
| Raw OHLC field convention | E1 retained documented version | T3 source convention | Docs yes | Yes | Documented API, key/permission gate | Past year advertised free | Local capture of doc version required | Private storage allowed under E3 scope | CONDITIONALLY_ADMISSIBLE | Exact endpoint/field/version binding; does not grant genuine-price admission |
| Genuine price observations | E1 JK feed plus metadata; I1 official observation fallback | T3 admitted direct feed | EOD free candidate; IDX download unproved | EOD/IDX candidate | EOD collector exists; no admitted V2 adapter | Price window plausible, not authenticated | Local append history; native raw corrections unproved | EOD private storage allowed; IDX unknown | NEEDS_CONFIRMATION | Non-synthetic/placeholder/substitution semantics, exact session/convention/revision links |
| Currency / price unit continuity | K2 + source-specific price metadata + exact identity | T1/T2 metadata; T3 field scope | Yes for tested K2 | Yes | Unapproved metadata route | Point, not all-window proof | No demonstrated continuity chain | Yes technically; permissions scoped/unknown | CONDITIONALLY_ADMISSIBLE | IDR security metadata must bind exact price unit/feed/window; nominal value is not close |
| Quantity / segment (optional) | E1 plus JK-specific specification; I1 data dictionary | T2/T3 convention | E1 docs free; I1 unverified | Candidate | EOD API possible | Split-adjusted documented | Exact native revisions unknown | EOD private storage allowed | NEEDS_CONFIRMATION | Exact shares/lots/contracts, adjustment and segment; exchange lot size does not define vendor field |
| Positive action event | B1 split PDF; K5 rights; K6 merger; K7 bonus/reverse candidates | T1 exact issuer/depository record | Tested PDFs yes | Yes | Recurring retrieval unapproved | 2021/2025/current examples | References/final notice; complete chain unproved | Yes technically; rights unknown | CONDITIONALLY_ADMISSIBLE | Exact subject/effective market date/terms and retained amendments; bonus/reverse artifacts still needed |
| Complete action-window coverage | K4/K5/K6/K7 + bounded issuer disclosures | T1 or explicitly admitted T2 coverage | Public candidates | Review possible, completeness not proved | No approved complete feed | Exact window unproved | Cancellation/correction coverage unproved | Technical saving possible; rights unknown | NEEDS_CONFIRMATION | Authoritative coverage scope, all required event types and revisions; silence is insufficient |
| Derive PriceComparability | Selected admitted identity/currency/convention/session/action facts | T4 derivation only | Local | n/a | Existing evaluator | Exact seed/recurrence | Exact selected identities | Existing manifest/history | NOT_ADMISSIBLE | Required external inputs incomplete; importer must not assert CLEARED |

## EODHD premise reassessment

These are scoped findings from current E1/E2/E3 documentation and existing retained
pilot evidence. `PROVEN` for documentation means an explicit provider convention,
not that every JK row passed admission.

| Premise | Classification | Consequence |
|---|---|---|
| EOD open/high/low/close raw, unadjusted | PROVEN | Preserve raw fields; authenticate documented version |
| adjusted_close includes splits and dividends | PROVEN | Not interchangeable with frozen raw calculations |
| Volume integer and split-adjusted | PROVEN | Preserve exact integer and adjustment tag |
| Exact JK volume unit / segment | NOT_DOCUMENTED | Shares/lots/contracts/segment remain unknown; optional features unavailable |
| JK feed currency/price unit/window continuity | NOT_PROVEN | KSEI IDR alone does not specify the provider field |
| JK actual tape upstream / non-synthetic guarantee | NOT_PROVEN | Partner page omits JK attribution; no genuine-price grant |
| Universal claim that every EODHD feed is direct exchange data | CONTRADICTED | E3 expressly distinguishes exchange agreements and other market-maker/CFD feeds; this does not prove a particular JK row is a CFD |
| Every retained JK date is a completed exchange session | CONTRADICTED | Existing Slice 5A.1 documented closure/padding observations prohibit that shortcut; independently confirm each session |
| JK zero/no-trade/carry-forward distinction and placeholder flags | NOT_DOCUMENTED | No universal zero/positive-volume admission rule |
| Native raw-bar correction ID / revision feed | NOT_DOCUMENTED | Local hashes/append revisions preserve received versions, not provider publication/correction chronology |
| adjusted_close stable over future dividends | CONTRADICTED | Documented historical recomputation; keep exact retained bytes |
| Splits/dividends API establishes complete V2 action coverage | NOT_PROVEN | Cannot replace authoritative rights/bonus/merger/coverage facts |
| Free EOD past year; registered key; 20/day | PROVEN | Bounded candidate only; actual account entitlement not tested now |
| Qualifying private noncommercial storage/analysis | PROVEN | Potential scoped future ALLOWED; existing registry remains UNKNOWN |
| Indefinite retained use after account/contract termination | NOT_DOCUMENTED | No buy-and-cancel architecture assumed |

To make a future JK observation admissible, retain endpoint/version documentation
or explicit source metadata establishing actual non-synthetic observation and
placeholder/substitution behavior; map exact issuer/ISIN/local ID/date; bind the
independent completed-session ID, authenticated convention ID, original bytes,
exact bar revision/hash and original knowledge clocks. Zero observations need the
additional frozen positive zero-proof convention/flag. Currency/unit continuity
must cover the calculation segment. New clarification cannot be backdated to
repair old captured evidence. A local reconciliation sample cannot substitute for
a missing source-wide semantic guarantee.

## Minimum Level-1 stack and acquisition boundary

**Complete admitted minimum today: none.** The smallest concrete *candidate* is:

1. KSEI per-security records plus operative IDX listing/board/status artifacts and
   rules: identity, ordinary type, currency, listing, board and exact target status.
2. Operative IDX calendar/closure amendments (K3 only as a scoped reference) plus
   independent daily completion artifacts: confirmed exchange sequence.
3. KSEI/issuer exact action notices **and an authenticated coverage basis** for each
   selected warmup/recurrence segment: positive events and no-break coverage.
4. EODHD bounded EOD prices **only after** JK authenticity/convention confirmation;
   otherwise a permitted, specified official IDX price artifact, still unproved.

KSEI alone cannot supply normal trading or board applicability. An issuer event
index alone cannot establish coverage. EODHD is not an authoritative action/status
source. This candidate is not outcome A or B and is not approval to build adapters.

After proof and permission, the existing flow is sufficient:
explicit bounded local artifact → `RawArtifactArchiver` → reviewed Python
normalization/admitted adapter tuple → `BoundedEvidenceIngestionService` → typed
V0.2 records → existing PIT/readiness/technical evaluation. PriceComparability is
derived from selected facts, never imported as a source clearance.

Realistic future modes:

- **AUTOMATED_ZERO_COST candidate:** existing bounded EOD collector, after source
  and permission gates. Ten equities cost ten ordinary EOD units; optional benchmark
  adds one. Existing local ceiling remains 16; free quota is not permission to
  widen the panel or ignore retries/metadata calls.
- **MANUAL_RETAINED_ARTIFACT candidates:** per-security metadata when changed;
  board/rule/status artifacts with exact applicability; calendar plus amendments;
  each completion report; action notices and explicitly covered review windows.
  Change-only acquisition is safe only where the source proves continuation and
  complete intervening coverage. Otherwise acquire the needed point each session
  or remain UNKNOWN. Manual typing/checkboxes without primary retained evidence
  are not enough.
- **DISCOVERY_ONLY:** blocked/Loading pages, indexed snippets and unverified exports.
- **NOT_OPERATIONALLY_USABLE now:** automatic IDX webpage crawling; incomplete
  public searches treated as status/action coverage; C-BEST participant feeds
  assumed available to a personal user; current KSEI price table treated as a
  documented genuine/raw/unit-complete replacement feed.

## Historical warmup

At least **50 consecutive admitted, comparable completed exchange sessions** after
the last relevant break are needed for EMA50, not 50 calendar days. Retain closure
and exceptional-session proofs; a missing/unproved weekday breaks continuity.
The complete active EMA seed/recurrence, not merely the last nominal 50 bars, needs
coverage. Raw splits/rights/bonus/conversion/merger breaks restart warmup; do not
bridge them using adjusted_close or invented factors. Cash dividends retain the
existing price-only policy.

A past-year EOD range could provide enough candidate observations in one bounded
request per equity, within quota. Official historical PDFs demonstrate that dated
events are obtainable, but a consecutive complete 50-session evidence package is
**not proved**. The current eight-date pilot is insufficient. The smallest safe
fallback is a narrower, explicitly covered post-break segment with retained manual
official evidence; when completeness cannot be established, leave it UNRESOLVED.
Prospective collection is an alternative after the same gates close; it is not a
retroactive historical snapshot. No historical universe/survivorship inference is
introduced. Benchmark and 61-session RS60 history remain optional.

## Permission and revision decisions

| Source | Proposed future permission decision | Required justification |
|---|---|---|
| EODHD private bounded active-account API | ALLOWED only after scoped registry review; **no change now** | E3 private storage/analysis grant plus actual user's qualifying account/entitlement and retained terms version; no redistribution/post-termination assumption |
| IDX website automation | BLOCKED research decision | Existing reviewed scraping prohibition; current terms retrieval failed, no superseding license proved. No bypass |
| IDX manual documents/exports or licensed delivery | UNKNOWN | Confirm personal local retention and exact allowed delivery; public viewing/noncommercial quotation is not automatically a full archival grant |
| KSEI public master/pages/calendar/notices | UNKNOWN | Public downloads and accuracy disclaimer do not establish routine automated acquisition/private immutable retention rights |
| Issuer disclosures (BCA example) | UNKNOWN | Confirm bounded private document retention/acquisition under applicable terms; PDF access alone is not the permission decision |

The existing database enum is `UNKNOWN / ALLOWED / DISALLOWED`, not BLOCKED.
BLOCKED here names an operational gate; do not introduce a new enum/migration.
Use DISALLOWED only for an established applicable prohibition, and retain UNKNOWN
where rights are unproved. No source entry or permission was changed.

For each later admitted source, retain native document/fact reference, source and
parser versions, original publication/receipt/known times, exact hash/length and
effective scope. A changed web page is a new received artifact, not automatically
a correction of every earlier fact. Explicit predecessor/amendment relationships
must satisfy the existing importer and PIT logical-fact rules. No latest-document
fallback, equivalent archive substitution or historical knowledge backfill.

## Repository reuse and schema deployment

| Source / claim | Existing capability | Later work after gates close |
|---|---|---|
| EODHD observations | `collectors/python/src/idx_stock_collector/pilot.py`, `eodhd_experimental.py`, collector manifest/archive and local append revisions | Reuse bounded explicit retrieval/parsing; reviewed V2 convention/price adapter required; permission review, no new vendor fallback |
| KSEI master | `ksei_master.py`, `ksei-master-inspect-1` local inspector | Inspection reusable; not a production parser. Prefer bounded per-security artifact; review malformed/missing fields and semantic scope, no 3,678-record import |
| IDX/KSEI/issuer PDFs and metadata | Local-artifact input plus `RawArtifactArchiver` and admitted source tuple | Acquisition can be manual; claim-specific normalization/adapter and scoped source entries still required. A manual file alone does not emit typed evidence |
| Calendar/completed session/status | Existing manual pilot JSON is reference inventory | Reviewed official-artifact adapter/bindings, positive exact status/completion and permitted source required; no bar-derived session/status |
| Corporate action event/coverage | Existing `ActionEventValue` / `ActionCoverageValue`, bound codecs and PIT/comparability | Parse documented event facts; coverage only from authenticated complete scope, not author-created absence assertion |
| All source admission | `BoundedEvidenceIngestionService`, `ScreenerEvidenceV02Store`, source registry | Exact source/parser/content type/claim/class/tier/scope tuple; ALLOWED gate; immutable retries/corrections. No ingestion architecture redesign |

Operational V0.2 ingestion requires existing **0007_screener_evidence_v02.sql**
then **0008_screener_evidence_binding.sql**, starting from current version 6.
0007 creates evidence records; 0008 adds strict class/schema/scope bindings and
TradingStatus. Both are sufficient for the existing ingestion schema: **no new
migration** is needed. Capture 0009, candidate 0010 and Outcome 0011 belong to later
activation, not the source decision. No migration was run. Deployment is a separate
operational task and cannot cure missing source semantics.

## Exact next actions and conditional implementation order

Next is **5A.2b — authoritative source confirmation**, not a daily runner:

1. Legitimately obtain one bounded official IDX daily status/remarks export with
   its operative dictionary, date/session scope and positive normal-trading meaning.
   Alternatively obtain an authoritative state plus complete intervening transition
   coverage. Test one target first; a list of found suspension PDFs is insufficient.
2. Obtain JK-specific EODHD provenance/non-synthetic/placeholder/no-trade/correction
   clarification and currency/unit convention. If unavailable, inspect a permitted
   official stock-summary artifact/dictionary as the price alternative. Do not call
   a paid feed merely to discover whether it is necessary.
3. Obtain a documented complete KSEI/IDX/issuer action publication/export scope,
   including amendments/cancellations and all frozen event types, for one bounded
   50-session segment. Include actual bonus/reverse-split artifacts; category labels
   alone do not prove coverage. If still partial, keep comparability UNRESOLVED.
4. Retain operative listing/board/rule and independent completion/closure artifacts
   for that segment; resolve panel listing gaps and permission/archival rights.
   Source-contact questions are proposed only; no messages were sent.

Only after this yields A or B, propose independently testable slices:
**5A.3a** scoped source registry/terms and identity/type/currency/listing/board/status
adapters; **5A.3b** calendar/completion adapters; **5A.3c** authenticated raw-price
convention/observation adapter; **5A.3d** authoritative action event/coverage adapter;
**5A.3e** separately approved deployment of existing 0007/0008 and authentic bounded
import/readiness proof. Each includes fail-closed missing/conflicting/correction
cases through existing ingestion/PIT tests. No slice is implemented/authorized by
this research decision. The daily runner remains blocked until authentic readiness
is proved; FullIdx and soak remain separate.

## Validation and research activity

Public web research was nonzero: first-party documentation, official pages and
PDFs listed above, including failed reads. Three explicit ordinary public GETs
were made outside the web reader: KSEI master index, its published ZIP and disclaimer;
successful payloads were bounded and temporary. The web reader's internal request
count/cache behavior is not exposed. **Market-provider API calls 0, units 0, paid
activation/cost 0.** No recurring scraper or recovery was built.

Lightweight validation only: baseline/diff review, existing local KSEI inspector,
registered-archive SHA-256/length authentication and before/after fingerprints of
**21 protected database tables/existence states** and **1,386 protected files**.
The extra table is the source registry. An initial sandbox Docker access failure
was resolved with authorized read-only access; system Python lacked `file_digest`,
so the existing Python 3.13 runtime was used. These were inspection setup failures,
not product test failures. No test suite/build ran; no production code changed.
Exact documentation and staged diffs plus `git diff --check` are reviewed before
commit. Frozen contracts, operational tables/archives/references/ledger files,
source permission, portfolio and schema remain unchanged.
