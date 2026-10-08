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

## Slice 5A.2b — authoritative source confirmation (2026-10-08 WIB)

Checked across **2026-10-07 UTC / 2026-10-08 WIB**. This is an additive research
checkpoint; the preceding 5A.2 ledger and decision remain historical findings.
Initial `main` HEAD `e39b59f5f0fb37fd1189a8ae475e0148f279de22`, clean,
**9 ahead / 0 behind** local `origin/main`. No remote fetch or push.

**C — `LEVEL1_SOURCE_STACK_BLOCKED_ON_EXACT_CLAIMS`.** Public official artifacts
and a permitted manual IDX document path are demonstrated below. A complete
50-session admitted stack is still unproved. **Paid source necessity is not
proved. Daily Runner remains blocked.** No adapters or operational evidence were
created, no permission/source registry was changed, and no schema was deployed.

### Mandatory premise verdicts

Each verdict covers the whole specified premise, including applicability and
retained-use gates. A verified narrower fact does not upgrade the whole premise.

| Mandatory premise | Verdict | Exact remaining limitation |
|---|---|---|
| Positive exact-session TradingStatus | STILL_UNCONFIRMED | No BBCA target-session normal/tradeable status artifact or authenticated initial state plus closed transition history |
| Genuine/non-synthetic JK price semantics | STILL_UNCONFIRMED | Vendor documents do not resolve JK carry-forward, placeholders, non-trading dates or zero-volume semantics; generic raw OHLC text is insufficient |
| Independent completed-session sequence | STILL_UNCONFIRMED | Actual dated IDX daily PDF obtained; all 50 reports, authoritative completion-clock binding and closure/amendment coverage not obtained |
| Complete corporate-action coverage | STILL_UNCONFIRMED | Positive events and explicit remarks flags exist; no complete all-type window/amendment coverage basis establishes the negative side |
| Applicable identity/listing/board/mechanism | STILL_UNCONFIRMED | BBCA identity/type and historical listing date corroborated; historical identity continuation, dated instrument board, operative mechanism and exceptions remain incomplete |
| Source retrieval / retained-use permission for whole stack | STILL_UNCONFIRMED | IDX manual noncommercial use and qualifying EODHD private storage have published bases; KSEI/BCA retained-use scope and actual source admission remain unresolved |

No mandatory premise is classified CONFIRMED_ADMISSIBLE or CONFIRMED_MANUAL_ONLY
merely because one relevant document downloads. No source-wide rejection or
proof that a free/manual path cannot exist is inferred from a failed request.

### Surgical artifact ledger and exact support

**X1 — IDX JATS Remarks2 dictionary.** The rendered [official circulars
index](https://www.idx.id/id/peraturan/surat-edaran) exposed the actual
[SE-00010/BEI/07-2026 PDF](https://www.idx.co.id/Media/nauhx32e/signed_se_00010_bei_2026_tampilan_informasi_perusahaan_tercatat_pada_kolom_remarks_dalam_jats.pdf).
Normal browser file retrieval succeeded, without login/payment/challenge, despite
web-reader failures. The 27-page document is dated **31 July 2026**, effective
**3 August 2026**, and revokes **SE-00002/BEI.PB2/01-2025**. Preserve the predecessor
for dates before 3 August, and its stated transitional notation dates; never
apply the new dictionary to the entire July–October window by convenience.

T1 for the exchange's own dictionary, not a BBCA observation. Appendix 1, page 3,
defines action codes, pre-opening eligibility and board/index codes; appendix 4,
page 27, defines special notations. Board digit 5 distinguishes MAIN,
DEVELOPMENT, ACCELERATION, SPECIAL_MONITORING and NEW_ECONOMY, with the document's
explicit board/index membership wording. It supplies interpretation, not an
instrument/date roster. Neither margin eligibility nor pre-opening eligibility
establishes unrestricted normal trading for a whole session. No BBCA Remarks2
row was obtained. Do not map the dictionary alone to TradingStatus, BoardRegime,
ExchangeRuleVersion mechanism or MechanismException facts.

The action-position code `--` explicitly means no corporate action in that
display convention. This is a useful positive dictionary definition, **not**
complete V0.2 ActionCoverage: no dated row sequence was obtained and the listed
codes do not establish all merger/conversion/ticker/basis/cancellation coverage.
Publication/effectivity dates come from the body; knowledge is this retrieval,
not July. Local raw hash/length binds this received version; a signed filename
does not establish cryptographic signature validation. No native correction
feed or immutable URL promise was demonstrated. Acquisition/retention mode is
manual attributed noncommercial IDX use under X3; routine crawling is prohibited.

**X2 — independent dated IDX statistics.** The normal rendered [daily-statistics
index](https://www.idx.id/id/data-pasar/laporan-statistik/statistik/) contained
dated rows and actual PDF links, rather than a Loading shell. Retained
[2 October 2026 daily PDF](https://www.idx.co.id/Media/4mdb3i3d/ds_261002.pdf):
nine pages; page 1 identifies the date and **Trading Day 178**; page 5 contains
exchange trading recapitulation. This is T1 first-party independent exchange
report evidence, not a session inferred from the chosen BBCA/EODHD bars. Its
aggregate volume unit does not define EODHD JK volume. Top-stock transaction
statistics do not prove BBCA's positive normal status or supply full BBCA OHLC.

The index shows several September/October dates and a date-range control, but
history back to 23 July and every intervening artifact have not been authenticated.
The row date is not an exact publication instant. Appendix version **v.2.2.0** is
publication-format information, not a native content-correction ID. The report
does not give an authenticated session `completedAt` instant; do not invent one
from the date, scheduled close, chart axis, download time or PDF metadata. The
publication/date finding is established; the full typed CompletedSession binding
still needs its required clock and sequence. Manual attributed noncommercial
retention uses X3; original bytes are retainable and fit the 4 MiB raw bound.

**X3 — current IDX permission text.** The actual linked [usage
terms](https://www.idx.id/id/syarat-penggunaan/) rendered fully in the normal
browser. Clauses 5–6 address downloaded information and allow noncommercial use
with complete source/access-date attribution; clause 6 prohibits web
scraping/crawling. Clause 12 permits changes without notice. X2's appendix and
index repeat the noncommercial attribution basis. This supports
**CONFIRMED_MANUAL_ONLY** for the narrow permission premise of private,
attributed use of the exact official downloaded documents, including retaining
the originals for that use. It does not license recurring website automation,
redistribution, commercial use or an unrestricted perpetual license. Authority
is the source owner's published permission; this is not a market evidence tier.
Retain the applicable terms version and access date with any later admission;
the current page has no immutable revision identifier. This research finding
does not set any source row to ALLOWED.

**X4 — KSEI revised 2026 service calendar.** Ordinary bounded GET retained the
actual [PENG-0002/DIR/KSEI/0126
PDF](https://web.ksei.co.id/files/1767843003_Penyesuaian_Pengumuman_Hari_Libur_dan_Cuti_Bersama_PT_KSEI_Ta....pdf),
dated **8 January 2026**, three pages. The literal four-dot suffix is the
observed working filename. It cites IDX **Peng-00171/BEI.POP/09-2025**, dated
**23 September 2025**, and reserves changes following IDX/Bank Indonesia notices.
It revises the service instructions referenced in K3's October 2025 notice;
preserve both identities rather than silently replacing the older ledger entry.
Its appendix includes **17 August** and **25 August 2026**.

T1 for KSEI's own service closure scope; only a candidate admitted T2 reference
for exchange scheduling. KSEI service exceptions and exchange-session closures
are different. The operative IDX calendar body and later exceptional closures
were not obtained. The smallest admissible composition remains an operative
IDX schedule (or admitted scoped reference with independent proof), all applicable
closure/amendment notices, and independent completed-session proof for each open
date. Weekday arithmetic, calendar silence and service closure alone are
insufficient. Raw retention works technically; KSEI permission remains UNKNOWN.

**X5 — BBCA identity and listing milestone.** Fresh [KSEI BBCA
detail](https://web.ksei.co.id/services/registered-securities/shares/lc/BBCA)
explicitly identifies issuer, BBCA, **ID1000109507**, **Saham Biasa**, IDX, IDR
and listing **31 May 2000**. This is T1 for that exact published depository
identity/type point; its registration Active is not TradingStatus. An as-of
ownership date is not automatically the identity record's effective date.
No complete historic identity/listing/delisting interval was obtained.

The issuer's [company-profile PDF](https://www.bca.co.id/-/media/Feature/Report/File/S8/ACGS/Laporan-ACGS/Indeks-Laporan-Tahunan/2021/20210330-profil-perusahaan-EN.pdf),
printed page **33**, explicitly labels **Listing Date: May 31, 2000**, with
BBCA/ISIN/IDX. Thus the separate IPO chronology date is not a competing listing
date. T2 reviewed issuer evidence is allowed for the listing fact by the frozen
contract; permission and retained original bytes still gate admission. Important:
the reader returned **Annual Report 2025** content under a 2021 filename. Do not
derive publication time/revision identity from that filename or claim a 2021
knowledge timestamp. Ordinary direct GET returned 403; no complete original was
retained in this checkpoint. Historical milestone corroboration does not prove
current board/mechanism or a closed continuing listing interval.

**X6 — action coverage deep check.** The current [KSEI action
calendar](https://www.ksei.co.id/en/service-support/schedule/schedule-of-corporate-actions)
offers separate cum/record/effective date filters and multiple action categories.
The tested category list is not an exhaustive frozen break-type coverage
statement. Issuer-name results also include debt events: match the exact equity
ISIN and effective market segment, never every event carrying BCA's name.
The [BCA action index](https://www.bca.co.id/id/tentang-bca/tata-kelola/aksi-korporasi)
shows RUPS/dividend/other categories, 2026 documents and historical material.
Neither tested index promises a complete bounded BBCA all-type export with
cancellations/amendments. K5/K6/B1 remain positive event examples, not absence
proof. Public download controls alone do not close KSEI/BCA retention permission.

Smallest safe manual procedure, **not performed or established admissible here**:
retain all scoped IDX/KSEI/BCA publication-index pages and originals needed for
BBCA/ISIN over the exact effective window; cover each split/reverse/bonus/rights/
stock-dividend/conversion/merger/ticker/share-basis category and predecessor/
amendment/cancellation chain, including earlier publications effective inside
the window. Require an authoritative complete-scope basis or issuer/registrar
statement explicitly closing the missing types/intervals. The review manifest
records covered types, interval, exact primary references, cutoffs and revisions.
A manual checkbox/search log is not that basis. If a gap remains, retain PARTIAL
coverage and comparability UNRESOLVED; do not append FULL or CLEARED. Cash
dividends preserve price-only treatment; a break restarts warmup.

### JK vendor semantics and permission decisions

Fresh [EOD documentation](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes)
defines raw as-traded OHLC, split/dividend-adjusted close and split-adjusted volume.
Its general one-row-per-trading-day statement does not document JK exceptions,
zero/no-trade padding, genuine provenance or price-unit continuity. The linked
[official OpenAPI reference](https://eodhistoricaldata.github.io/EODHD-openapi/redoc.html)
has an EOD response example without synthetic/placeholder/revision fields; it
does not resolve these premises. **JK genuine/synthetic and zero-volume meanings
remain unconfirmed; JK shares/lots/contracts remain undocumented.** Quantity and
liquidity remain feature-local. No extrapolation from US examples or exchange
lot size. Adjusted history recomputes; native raw correction lineage is still
undocumented. Append local received versions, never overwrite/backdate them.

[EODHD terms](https://eodhd.com/financial-apis/terms-conditions) explicitly allow
qualifying nonprofessional private storage/manipulation/analysis and restrict
redistribution. Exact active-account private scope can support ALLOWED_JUSTIFIED;
actual account/source admission was not changed or certified here. Post-termination
retained-use rights remain unconfirmed. The generic disclaimer is not sufficient
to label a particular JK row synthetic or genuine. No provider request was made.

| Candidate source/mode | Research permission classification | Basis / limit |
|---|---|---|
| IDX official manual downloads | ALLOWED_JUSTIFIED | X3 clauses 5–6 and X2 disclaimer: private noncommercial use with full source/access-date attribution; retain terms; no broader perpetual/commercial grant |
| IDX website scraping/crawling | DISALLOWED_JUSTIFIED | Current X3 clause 6; no implementation or prohibited endpoint traversal |
| EODHD qualifying private active-account documented API | ALLOWED_JUSTIFIED within that explicit scope | Published storage/analysis grant; real registry stays UNKNOWN pending its own scoped admission; no entitlement/termination assumption |
| KSEI public master/detail/calendar/action artifacts | REMAINS_UNKNOWN | Fresh [disclaimer](https://web.ksei.co.id/disclaimer) GET repeats accuracy limitations/copyright; no sufficient retrieval/immutable retained-use grant found |
| BCA issuer disclosures | REMAINS_UNKNOWN | Downloadable issuer disclosures exist; no applicable published retained-use grant established; banking-product terms do not answer document rights |

### Concrete BBCA 50-session acquisition package

Instrument **BBCA / ID1000109507 / retained local instrument binding**.
Candidate window **2026-07-23 through 2026-10-02 inclusive**. A standard-library
count confirms **52 weekdays**, or **50 candidate dates** after removing the two
X4 service-calendar dates. This is not a finding of 50 actual exchange sessions.
Any different operative closure/break requires recounting or an earlier start;
do not slide missing endpoints or invent a session to keep the count.

The exact acquisition recipe, pending source/permission gates, is:

1. Retain the per-security KSEI identity/type/currency artifact, issuer listing
   original (X5), and dated IDX BBCA listing/board/status records, operative
   mechanism/exception references. A current point alone cannot cover July.
2. Retain IDX calendar **Peng-00171/BEI.POP/09-2025** and applicable amendments/
   exceptional closures, with X4 as a separately scoped KSEI reference.
3. Manually download a dated IDX Daily Statistics original for **each** validated
   open date from the public date-range index. X2 is the concrete last-date
   artifact; obtain the other originals and documented completion-clock binding.
   Verify sequence, date identities and coverage independently of price rows.
4. After JK-specific convention/authenticity proof, acquire one bounded
   **BBCA.JK** EOD response with `from=2026-07-23`, `to=2026-10-02`, `period=d`,
   ascending JSON and the existing owner's entitled key. No key in documentation,
   no request now. Retain original response plus exact convention version,
   currency/price-unit, row classification and corrections. Missing/ambiguous
   rows fail closed; the daily-statistics PDF is not a full price replacement.
5. Complete X6's bounded action/reference-continuity review and retain its
   authoritative coverage basis and all applicable event/amendment originals.
   Retain both Remarks2 dictionaries for their applicable subwindows if used.
6. Retain source-specific terms, full attribution/access dates and approved
   source/parser bindings before any separately authorized import.

Expected later typed output is scoped StableIdentity, SecurityType, Currency,
ListingCoverage, BoardRegime, ExchangeRuleVersion/required exceptions,
TradingStatus for the exact target (plus suspension/reopening evidence where
applicable), ScheduledSession and CompletedSession for the authenticated sequence,
SourcePriceConvention, exact GenuinePriceObservation revisions and CorporateAction
events/coverage. Preserve raw hash/length/native references, economic/publication/
retrieved/known/recorded clocks separately. Missing publication/completion clocks
are not filled with retrieval time. PriceComparability is derived, never imported.
No existing prospective capture is retroactively repaired.

Feasibility today: **not a complete package**. X1/X2/X4 each fit 4 MiB; the KSEI
all-market master still exceeds the 512-record bound, so do not truncate it or
expand the panel. Exact status, JK semantics, full action/identity continuity,
all completion/closure clocks, dated board/mechanism and KSEI/BCA rights are still
gates. Fifty comparable completed sessions and authentic readiness are unproved.

### Minimum stack, top blockers and next surgical milestone

**Complete admitted minimum: none.** One candidate composition remains KSEI/X5
reference plus operative IDX status/board/rules; IDX calendar plus X4 and complete
X2-style independent completion reports; authenticated EODHD JK raw prices; and
IDX/KSEI/BCA action originals plus authoritative closed coverage. Use manual IDX
documents and only separately admitted source modes. No substitute/latest feed.

Top three exact blockers and a single next action for each:

1. **BBCA positive TRADING on 2026-10-02.** Obtain that official dated security
   status record with its operative status dictionary. X1 Remarks2 is not this proof.
2. **BBCA.JK EOD genuine/no-trade classification.** Obtain an explicit zero-cost
   vendor statement for `/api/eod` JK carry-forward/placeholder/non-trading-date/
   zero-volume behavior and identification of genuine observations, including
   field currency/unit and correction scope. No support message was sent.
3. **BBCA no comparability break over 2026-07-23…2026-10-02.** Obtain one issuer/
   registrar/exchange retained statement or complete-scope export covering every
   frozen break type and amendments for that interval; search silence cannot close it.

These are prioritized blockers, not claims that the other mandatory verdicts have
passed. **Next: 5A.2c, the single BBCA 2026-10-02 status-record confirmation**;
no production implementation slice is unblocked. The previous conditional 5A.3
order remains gated, not reauthorized. Existing **0007 then 0008** remain sufficient
for typed ingestion; schema stays **2/4/5/6**, V0.2 evidence table absent. No new
migration, deployment, adapter, Daily Runner, scheduler, FullIdx or soak activity.

### Retained research identities, failures and validation

| Research artifact | Bytes | SHA-256 |
|---|---:|---|
| X1 exact 27-page remarks PDF | 1,011,785 | `c50ea85cdbd18e41274e20a6ccd2531868b7ef11a918bdebff4ddf048808462a` |
| X2 exact nine-page 2 October daily PDF | 1,987,188 | `8809cf9d71c8474eff2d25962e36558c388d77f484c2716d1b40462b6779efa7` |
| X4 exact three-page KSEI January notice | 1,036,672 | `ca444520365cb1d8a74eed4c0e9c72c5718333e9a2745b1002bf6256c454a96e` |
| Fresh KSEI disclaimer HTML | 25,249 | `9d9d6794c2bef4d42e93f8ebeca5660f979f0be2a2e8f9545a06b52c8c028457` |

Browser downloads are research copies outside the repository; bounded working
copies are under temporary storage. No operational archiver/import was invoked.
Relevant dictionary/date pages were extracted and visually checked; no signature
validation is claimed. No downloaded market/document dataset is committed.

Meaningful failures: direct IDX terms/calendar/data-service reads failed; stock
summary/recap browser pages exposed controls without an actual dataset. Statistics
history expansion and its ordinary download control displayed connection failure;
the exact linked file was retrievable through normal browser file download.
Observed index totals changed between renders, so no complete-history count was
inferred. IDX PDF and BCA PDF direct GETs returned 403. Web-reader KSEI notice/
disclaimer reads failed, but bounded ordinary GETs returned 200. No challenge,
CAPTCHA, login, access-control bypass, undocumented API or hostname probing loop.
PDF inspection reused bundled tools after system Python lacked pypdf; a fontconfig
render failure was resolved with a temporary font/cache configuration, not an
installation or operational change.

External public research was nonzero: IDX terms/circular/daily-statistics and
candidate status/calendar/reference pages, KSEI detail/calendars/actions/disclaimer,
BCA listing/action disclosures and EODHD docs/terms/official OpenAPI. Four explicit
ordinary GET attempts outside the readers/browser: two PDF 403s and two successful
KSEI downloads. **Market-provider API calls/units 0/0; paid activation/cost 0.**
No credentials created, support contacted, source permissions changed or paid
service activated. Protected database/file fingerprint comparison, history-prefix
preservation, exact documentation/staged diff and whitespace checks validate this
checkpoint; no code build/test suite is required or claimed. FullIdx remains
disabled; prospective soak remains **1/10**. Fresh before/after checks matched all
**21 protected table/existence states** and **1,386 protected file hashes**, with
both prior documentation prefixes unchanged and all four research hash/length
checks passing. Operational Outcomes remain **0**, schema **2/4/5/6**, V0.2
evidence absent; frozen contracts, references, source registry/permission and
operational archives/ledger/portfolio are unchanged.

## Slice 5A.2c — BBCA positive TradingStatus confirmation (2026-10-08 WIB)

**Verdict: STILL_UNCONFIRMED.** Positive supported `TradingStatus` for **BBCA /
IDX / 2026-10-02** is not proved. Neither a direct authoritative status row nor
an authenticated positive starting state with a demonstrably complete transition
chain was obtained. This is an evidence gap, not a finding that BBCA was suspended.
BBCA / **ID1000109507** identifies the subject only. No production adapter is
unblocked; no synthetic status, negative-search clearance or inferred continuity.

### Exact sources checked and candidate decisions

| Exact official source / artifact | Check and narrow status decision |
|---|---|
| [IDX suspension/resumption index](https://idx.co.id/id/berita/suspensi/) | Normal public browser access worked. The table displayed both suspension and reopening PDFs, initially 1–10 of 1,913 entries. Its keyword filter `BBCA` returned `Data suspensi tidak ditemukan`, 0 results. This is search silence, not complete coverage or positive status. |
| [IDX stock master](https://idx.co.id/id/data-pasar/data-saham/daftar-saham/) | Followed the visible official `Data Pasar` → `Daftar Saham` link. Browser returned a page titled `503`, stating a database error. No dated BBCA row, status field or export was obtained. |
| [IDX stock summary](https://www.idx.id/id/data-pasar/ringkasan-perdagangan/ringkasan-saham/) | Public page exposed `All Stock`, `Margin`, `Short Selling` controls but no actual rows, date selector or downloadable status artifact in the inspected render. No undocumented endpoint was attempted. |
| [SE-00010/BEI/07-2026 Remarks2 dictionary](https://www.idx.co.id/Media/nauhx32e/signed_se_00010_bei_2026_tampilan_informasi_perusahaan_tercatat_pada_kolom_remarks_dalam_jats.pdf) | Rechecked exact retained 27-page original X1. Dictionary alone is **CONFIRMED_NOT_ADMISSIBLE for the requested positive status assertion**; no BBCA 2026-10-02 Remarks2 row was retained/authenticated. |
| [IDX Daily Statistics, 2 October 2026](https://www.idx.co.id/Media/4mdb3i3d/ds_261002.pdf) | Rechecked all nine retained pages of X2 and its linked field manual. Names BBCA, but no affirmative security/session status field. **CONFIRMED_NOT_ADMISSIBLE for the requested positive status assertion**; this does not reject its separately scoped statistical facts. |
| [Officially linked statistics manual](https://bit.ly/IDXstat-manual) → [public Manual Guides folder](https://drive.google.com/drive/folders/1-aiZVJwc17tfFOfmyjFH2V2igjttZ7y9) | X2 page 9 links the manual. Public folder offered `Manual Guides - IDX Statistical Publication v1.2.pdf`; native file identity `1-hJVpCmVmmzQ3wiy6wh3shqWiUafO4MY`, seven pages. Manual download worked without login; all pages extracted, page 4 visually checked. IDX Data Services authorship and official report linkage establish dictionary provenance; hosting on Google Drive alone would not. No trading-status dictionary field. |
| [IDX 2026 catalogue](https://www.idx.id/media/auobyarx/idx-catalogue-pricelist-updated-2026.pdf) | Web reader reported content exceeding its 10 MiB limit. No original downloaded or field semantics verified; no inference about a status product, entitlement or paid-source necessity. |

Focused official-domain searches for BBCA/2 October/suspension and positive
stock/trading-status dictionary terms returned no results. No snippet was admitted.
The stock master error and summary shell are access findings, not proof that IDX
has no suitable dataset. Scope stayed on the exact security/session.

### Remarks2, board/mechanism and statistical semantics

X1 was issued **31 July 2026**, effective **3 August 2026**. It revokes
SE-00002/BEI.PB2/01-2025, with transitional special-notation provisions stated
on page 2. The operative dictionary is not a security observation:

- Page 3 digits 1–2 `--` denote no corporate action in that display convention;
  this is neither no suspension nor all-type action-coverage proof.
- Digit 3 `M`/`S`/`U`/`D` encodes margin/short-selling eligibility or unsecured
  classification, not general target-session `TRADING`.
- Digit 4 `O`/`-` encodes pre-opening eligibility/ineligibility. It is not a
  general normal/suspended status field and no dated BBCA value was obtained.
- Digit 5 codes `1`…`5` encode board/index classifications; page 27 digit 30
  `X` denotes Special Monitoring Board. These do not collapse board, mechanism,
  suspension and general tradability into one claim.
- Digits 6–18 cover index/industry information; 19–30 cover special notations.
  No documented absence/default rule in this circular affirmatively establishes
  general `TRADING`. No `Remarks` field is silently equated with `Remarks2`.

X2 page 3 includes BBCA under **Top Stocks by Value** (833 billion rupiah, 6.80%)
and **Top Stocks by Frequency** (27,304, 1.69%). Page 4 includes BBCA in market-cap
and IHSG contribution rankings. The linked manual v1.2 page 4 defines the first
two as rankings of that day's total transaction value/frequency, and market cap
as capitalization at closing; page 5 defines leaders/laggards by index-point
contribution. These describe trading statistics or reported constituents, not
an affirmative permitted trading-state code. No status enum, supported mechanism
or uninterrupted tradability field appears. Even documented executions do not
become the separate frozen TradingStatus claim. No closing-price/volume fallback.
The manual adds no target-session BBCA row.

### Transition route, authority and chronology

The index visibly contains official reopening as well as suspension notices,
so individual acts may be useful when their exact instrument/effective scope and
predecessors are retained. The tested index/filter supplies neither an authenticated
BBCA positive starting state nor a closed, authoritative completeness statement
covering every subsequent status-changing event through **2026-10-02**. Initial
pagination counts and an empty keyword result do not establish that chain. No
transition-derived status was admitted, and no unrelated notice was substituted.

IDX-authored originals/dictionary are **T1 for their actual documented facts**;
authority does not supply the missing BBCA status assertion. No third-party/KSEI
registration fact was elevated to exchange status. X2's artifact/session date is
**2026-10-02**, not a verified publication timestamp. X1's issue/effective dates
are distinct from website publication and application knowledge. The linked
manual is v1.2, ©2023; Drive displayed modified **15 February 2023**, which is
not authenticated original publication time or proof of immutable history.

Fresh research access/check date: **2026-10-08 WIB**. Manual file receipt was
observed at **2026-10-08T00:34:36.925538+07:00** (local download metadata).
X1/X2 working-copy timestamps are **00:03:01 / 00:06:53 WIB** on the same date;
they are reused prior-checkpoint copies, not independently attested publication
or exact server receipt clocks. Native amended status-record lineage is unknown
because no such record was obtained. Hashes pin these research bytes, not external
truth. No publication/knownAt was invented; none was backdated to 2 October.
Later retention could support only a later-cutoff historical query under the
frozen chronology, never repair an earlier prospective capture.

### Retention, permission and future binding

| Research original | Bytes | SHA-256 |
|---|---:|---|
| X1 Remarks2, reused unchanged | 1,011,785 | `c50ea85cdbd18e41274e20a6ccd2531868b7ef11a918bdebff4ddf048808462a` |
| X2 2 October Daily Statistics, reused unchanged | 1,987,188 | `8809cf9d71c8474eff2d25962e36558c388d77f484c2716d1b40462b6779efa7` |
| Statistics manual v1.2, newly manually downloaded | 981,358 | `aab5f6cd9e341c38e6c04fa22c81c14a59d9a6aa974f56212bc16a28283a636c` |

Original PDFs are research copies outside the repository, with bounded temporary
working copies; no production archive/import. Preserve X3's [IDX terms](https://www.idx.id/id/syarat-penggunaan/)
classification: attributed private noncommercial manual documents
**ALLOWED_JUSTIFIED**; scraping/crawling **DISALLOWED_JUSTIFIED**. The manual
page 1 also states noncommercial citation/use with full source/access-date
attribution. No broader perpetual/commercial rights or automated website access
are asserted. Public manual downloads remain a valid Level-1 route if they
actually prove the required fact; current failure is semantic/evidentiary, not
an objection to manual acquisition. Registry and permission records remain untouched.

No admitted source-to-field mapping exists. The frozen destination, conditional
on a real source, is **TradingStatus / SESSION_FACT / INSTRUMENT**, exact retained
BBCA and IDX identities, `effective_from = effective_to = 2026-10-02`, supported
payload version 1 with `status = TRADING` and the authenticated exact `sessionId`.
The frozen binding admits authoritative T1/T2 session facts; a future official IDX
record would be T1. Its native status value/definition, source ID/reference,
namespaced revision series, completeness basis and publication/retrieval/known/
recording chronology cannot be filled without the missing record. No invented
source ID, session ID, FULL assertion or dictionary-to-status mapping is proposed.
Other pilot instruments have **no established reuse** without their exact dated
identifiers/fields; this checkpoint proves none.

### Single remaining item, next gate and validation

**Single missing item:** one official IDX **BBCA / 2026-10-02 security-status
record**, retained with the operative field definition explicitly making its
positive value supported general trading status. Obtain that exact historical
record and definition through legitimate official manual access; no further
broad source sweep. There is no proved URL/native field/value for that missing
artifact, and no claim that it is publicly available or requires payment.

**Next remains the same source gate**, a surgical continuation of 5A.2c for that
one item. **5A.3a — BBCA/IDX TradingStatus retained-artifact adapter remains
blocked** and is not frozen as an authorized implementation step. The overall
Level-1 stack remains blocked; the runbook next-step gate is unchanged, so no
runbook edit. Other source blockers were not investigated in this slice.

Validation: all three research PDF hash/length checks, exact append-only diff and
whitespace review, and protected before/after database/file fingerprints. No full
build/test suite is required or claimed. No production code, adapter, migration,
source/permission registry, evidence/universe, Outcome/Research data or frozen
contract changes. FullIdx remains disabled; soak remains **1/10**. Public browser
and web-reader research was nonzero; one new manual PDF download, two reused
PDFs. No provider API calls/units (**0/0**), paid usage, credentials, recovery,
scraping/crawling or access-control bypass; no Daily Runner/scheduler operation.
Fresh validation passed: **21 protected table/existence states** and **1,386
protected file hashes** matched before/after, including source registry and
permission-bearing configuration. Operational committed Outcomes remain **0**;
V0.2 evidence remains absent. All three PDF byte-length/SHA-256 checks passed,
the complete prior ledger prefix is preserved, and the research ledger is the
only changed file. The runbook and all frozen contracts are unchanged.

## Slice 5A.2d — Trading Eligibility gate feasibility review (2026-10-08 WIB)

**Decision A — KEEP_AFFIRMATIVE_TRADING_STATUS_REQUIRED.** Keep positive
authoritative status as a necessary premise of this conservative Level-1
market-eligibility boundary. Official actual-trade evidence is a distinct useful
fact, but is not sufficient alternative proof of that boundary. This decision
rests on the unclosed administrative-status risk below, not on the contract being
frozen or a preference for a literal source label `TRADING`. No frozen semantics
are amended and no source is admitted by this review.

### Traced purpose and actual implementation

The normative evidence contract §§7, 9, 13–16 separates target-scope status,
supported mechanism, evidence visibility and technical readiness. Its explicit
risks include unknown suspension, unsupported mechanisms, false/carry-forward
prices and retroactive reopening. The original requirement is **authoritative
meaning**, not that a source must spell an internal enum token. An explicitly
documented equivalent status or properly authenticated reopening/transition may
support that meaning; activity cannot be renamed into it.

Inspected implementation and tests:

- `ScreenerMarketEligibilityEvaluator.Evaluate` in
  `src/IdxStockIntelligence.Application/ScreenerEvidenceV02Evaluators.cs` checks
  identity, type, listing, board/mechanism, status and target completion. A resolved
  suspension is INELIGIBLE; non-VERIFIED/non-TRADING status is DATA_BLOCKED.
- `ScreenerEvidenceAsOf.TradingStatus` combines exact retained Suspension,
  Reopening and TradingStatus claims. The readiness composer matches sessionId,
  keeps incomplete suspension from positively excluding an instrument, and does
  not derive status from prices or completion.
- `ScreenerEvidenceReadinessService` reads one consistent bounded snapshot/cutoff
  and retrieves each historical day's market facts for episode replay. Today's
  status is not projected backwards. `ScreenerDataReadiness` separately requires
  eligible market facts, cleared prices and required available core features.
- Reviewed tests include `StatusRequiresPositiveCompleteAuthoritativeFacts`,
  `StatusTransitionsUseRetainedFactsAndNeverSessionsOrPrices`,
  `TechnicalFailuresNeverChangeMarketEligibility`,
  `ZeroVolumeExplicitlyGenuineIsAdmitted`, `AmbiguousZeroVolumeFailsClosed`,
  `GenuineZeroComputesMonetaryZeroWhileV01GateAndRelativeVolumeStayUnchanged`
  and `HistoricalMarketExclusionInterruptsEpisodeUsingRetainedStatus`.
  These tests were inspected, not rerun.

The existing model is date/session scoped (`DateOnly` effective intervals), not
an intraday state machine. This review does not invent a close-time status rule,
an opening clock, continuous permission throughout every instant, or future
tradability guarantees. It retains the requirement for affirmative status
applicable to the target scope. An aggregate daily transaction count has no such
administrative assertion and cannot resolve an intraday restriction's scope.

### Independent controls and funnel placement

| Risk / concern | Existing control and semantic placement | Residual role of status |
|---|---|---|
| Wrong identity / non-ordinary security | StableIdentity / SecurityType; MARKET_ELIGIBLE concerns the admissible subject | Not a replacement for these facts |
| Pre-listing / post-delisting | ListingCoverage / Delisting; MARKET_ELIGIBLE concerns legal listing scope | Listing does not establish unsuspended trading permission |
| Unsupported board / mechanism | BoardRegime/BoardChange, ExchangeRuleVersion/MechanismException; MARKET_ELIGIBLE defines product-supported regimes | A supported regime can still contain a suspended instrument |
| Suspension / temporary trading restriction / unconfirmed reopening | Authoritative status acts with effective scope and positive termination; MARKET_ELIGIBLE | Not closed by listing, board, calendar, executed trades or valid prices |
| Stale / synthetic / substituted / false OHLC | Exact provenance, SourcePriceConvention and GenuinePriceObservation admission; authenticated data dependencies | Status cannot authenticate a vendor bar; a bar cannot authenticate status |
| Exchange did not complete / wrong session | Independent CompletedSession and exact session identity; market target and price dependencies | Exchange completion says nothing about one security's permission |
| Incomparable prices / actions / insufficient warmup | Derived comparability and core feature readiness; DATA_READY | No reason to make an otherwise administratively eligible security ineligible |
| No executions / illiquidity | Distinct activity fact and feature-local liquidity diagnostics | No execution is not suspension; permission need not produce trades |

Placement finding: contract §12 names price authenticity among hard dependencies;
the actual market-facts evaluator has no price input, and readiness/history
composition enforces price admission. Tests explicitly keep market eligibility
while synthetic/uncleared inputs prevent DATA_READY. Contract wording and code
placement are not identical here. This review records that existing tension;
it does not resolve it by reinterpreting §12, relax price authentication, move a
gate, or claim that implementation uses price authenticity as a status substitute.

### Distinct ObservedTradingActivity and BBCA evidence

Provider-neutral hypothetical meaning: **instrument X on authenticated exchange
session E had actual executed exchange transactions**, with retained exact
security/exchange/session scope, documented executed-transaction field semantics,
native revision/provenance and normal PIT clocks. Minimum proposed authority for
this hypothetical fact is **T1 exchange-authored**; T3 vendor bars cannot supply
administrative eligibility. This is analysis only: no new domain member, payload
or persistence binding is frozen.

One documented positive measure can suffice for that limited fact; all three of
frequency/value/volume are not required. Positive transaction frequency is enough
when the source defines actual executions. Quotation counts, orders, estimates,
list membership and unexplained zeros do not suffice. Do not require positive
volume universally or derive an execution count from a vendor candle.

The retained **2 October 2026 IDX Daily Statistics**, page 3, gives BBCA
**transaction frequency 27,304**. Its officially linked manual v1.2, page 4,
defines Top Stocks by Frequency as stocks ranked by that day's total trading
frequency. Thus **YES, the retained report/dictionary support actual exchange
transaction activity for BBCA on E**; no further frequency-field dictionary
research is needed. Both original byte lengths/SHA-256 identities from 5A.2c
were rechecked against the Downloads copies; temporary working copies from the
earlier checkpoint were no longer present. This does not authenticate new typed
evidence, session completion clocks or a production import.
The originals were first retained by this research on 8 October, not established
known on 2 October. They cannot satisfy an earlier knowledge cutoff or reopen
an expired prospective Outcome enrollment deadline.

Executed transactions establish some exchange access and executed-price formation
at some time on E, and are inconsistent with *no executions throughout E*.
They support not being fully prevented from executing across the entire reported
scope. They do **not** prove normal administrative status for the target scope,
absence of an intraday suspension, product-supported execution segment, listing/
type, price comparability, genuine vendor OHLC, or future access. The report's
all-market transaction statistics are not a supported continuous-market status
code. Independent regime evidence remains necessary; no board research is added.

### Policy alternatives and source feasibility

The following YES/NO source assessment isolates the **positive-proof branch**:
assume all independent identity/listing/board/mechanism/completion gates were
separately satisfied and evidence visible at the evaluated cutoff. It does not
claim that those other BBCA gates currently pass.

| Option | Retained report sufficient for the option's positive-proof branch? | Semantic decision |
|---|---|---|
| 1. Affirmative authoritative status | **NO** | Recommended. Proves administrative eligibility, allows authenticated no-trade securities, and keeps unknown status explicit. |
| 2. Status OR T1 actual activity | **YES for the weaker activity branch only** | Rejected as an equivalent safe eligibility proof. A security can execute early and then become suspended; other independent gates can still pass. Known-negative overrides help only when the restriction is known and its scope resolved. Missing restriction evidence would now permit an administrative false positive. |
| 3. Negative status override only | **YES under its assumed no-visible-negative default; report not even necessary** | Rejected. Missing/partial status is effectively treated as permission. PIT visibility constrains evidence, but cannot turn an incomplete negative inventory into complete clearance. |
| 4. Retain activity as separate diagnostic fact | **YES for activity; NO for eligibility replacement** | Useful conceptual separation, not a different eligibility policy or authorization to implement a new claim now. |

**Actual current BBCA MARKET_ELIGIBLE admission: NO.** Positive activity is proved
in the research originals, positive status is not; other independently blocked
source premises also remain unchanged. BBCA is not newly potentially admissible
under the selected policy. Under the hypothetical weaker option 2 it would have
one qualifying activity input, not a complete admitted stack or DATA_READY result.
No `NEEDS_FIELD_DICTIONARY_CONFIRMATION` verdict is used for the inspected
frequency field; the missing meaning is administrative status, not frequency.

The decisive counterexample is early actual trades followed by a target-applicable
suspension, with no listing/type/board/mechanism change and genuine prices.
All non-status premises and activity can agree while administrative eligibility
fails. If the notice is missing at cutoff, option 2 admits where option 1 remains
UNKNOWN. Avoiding that result would require additional affirmative coverage/status
proof or an explicitly weaker product boundary. This is not duplicate control
of price authenticity and is not solved by a different label for executions.

### Cases, zero trade and conflict precedence

All cases assume the other independent market gates pass; data readiness remains
a separate requirement:

| Case | Recommended mapping |
|---|---|
| A: official positive activity | With VERIFIED supported status → ELIGIBLE; activity alone / missing status → DATA_BLOCKED. |
| B: legitimate active security, no executions | Affirmative applicable status may establish ELIGIBLE; no positive-volume/frequency requirement. Unproved status → DATA_BLOCKED, not inferred suspension. |
| C: authoritative resolved suspension | INELIGIBLE for its applicable scope; activity does not terminate it. |
| D: activity evidence missing | No new mandatory activity dependency: VERIFIED supported status may still establish ELIGIBLE; missing status remains DATA_BLOCKED. |
| E: only third-party bar | DATA_BLOCKED on missing authoritative market premises, regardless of apparent price/volume. |
| F: activity before later suspension | Resolved target-applicable suspension → INELIGIBLE; irreconcilable or temporally unrepresented scope → DATA_BLOCKED. Earlier execution never proves reopening. |

Genuine zero-volume/no-execution price observations remain admissible under their
exact documented convention/explicit flag and independent session proof. They
are not actual-trade evidence despite the existing `TRADED_OBSERVED_AT_T` price
reason token. With positive administrative status and cleared required history,
they can still reach DATA_READY/technical evaluation, as the inspected zero tests
demonstrate. Ambiguous zero remains rejected; optional relative-volume limits
remain feature-local. No new mandatory activity or volume gate is recommended.

For activity plus suspension effective before open, activity cannot override a
resolved applicable suspension; a documented same-scope assertion of forbidden
executions is retained as a diagnostic inconsistency, not synthetic reopening.
For intraday suspension, preserve its stated scope; daily aggregates cannot
order or disprove it. Unsupported intraday precision fails closed rather than
inventing sub-day intervals. Different security/exchange/session/market scopes
do not compete merely because their dates coincide. Irreconcilable eligible-state
evidence blocks the affected dimension; no newest-row-wins rule is introduced.

Retain existing effective applicability, cutoff visibility, authority and scope
specificity, then admitted correction/revision lineage. Activity and status are
different claims: transaction counts cannot supersede administrative acts by
being T1 or newer. Applicable suspension remains until affirmative same-scope
reopening; expired suspension without it remains reopening-unconfirmed unless
other applicable authoritative status covers. Later corrections/cancellations
apply only at their actual known/retrieved/publication boundaries; earlier-cutoff
replay and immutable captures remain unchanged. PARTIAL activity never proves
absence, continuity or an administrative state.

### Contract, capture and version boundary

**No contract amendment/version change is selected.** Preserve
`screener-evidence-v0.2.0`, its binding and TradingStatus semantics. No 5A.2e
amendment milestone is authorized by this recommendation.

If a future product decision deliberately accepts the weaker activity boundary,
that materially changes eligibility: use an explicitly additive policy such as
`screener-evidence-v0.2.1`, not a clarification of v0.2.0. A separate amendment
would have to resolve temporal/market scope and negative/conflict precedence,
new distinct domain claim/authority/payload/logical identity, persisted binding,
PIT reader selection, MarketEligibility inputs/reasons, readiness and historical
episode composition, capture projection/replay hashes, tests and documentation.
Schema/migration need is not decided or authorized here; do not promise that a new
claim can be stored without separately assessing immutable vocabulary constraints.

Existing captures/candidates/Outcomes remain historically valid under their
original policy/input bindings, not certified anew by this review. Capture hashes
include readiness history and exact evidence references; candidates require the
exact technical policy/schema. Outcome V0.2 enrollment and verification explicitly
support `screener-technical-candidate-v0.2.0` / `screener-evidence-v0.2.0`, schema 1,
capture schema 1. They would not automatically accept a new eligibility policy:
any future compatibility extension must be explicit/additive, with old resolvers
retained. No recalculation, conversion or repair of existing rows. Research V0.1
membership, denominators and contracts remain wholly independent and unchanged.

### Next exact action and validation

Keep the **BBCA / 2026-10-02 affirmative supported status** source gate. Recommend
targeted legitimate manual acquisition of the exact official historical status
record and definition, or an authenticated positive state plus demonstrably closed
transition history. No further broad survey. A different instrument/date can be
used only in a separately authorized demonstrator with explicit status; it would
not resolve this BBCA case. Paid-source investigation is not yet warranted by
evidence of necessity; an inaccessible public page does not prove payment is
required. No support message, purchase or provider request was made.

The next-step gate is unchanged, so the runbook is unchanged; 5A.3a remains
blocked. Review-only validation covers exact contract/code/test inspection,
retained source hash/length/field checks, append-only history, protected local-file
hash comparison and exact diff/whitespace review. No build/test suite was executed;
production code is unchanged. No database operation, source/permission mutation, evidence import,
adapter, migration, runner or soak operation. FullIdx disabled and soak 1/10
unchanged. No new external research/network requests; provider API calls/units
0/0. Other source blockers were not investigated.
Fresh local validation passed: **1,386 protected file hashes unchanged**, complete
prior ledger prefix preserved, no untracked files and only this research document
changed. The runbook, frozen contracts/binding and production files are unchanged.
