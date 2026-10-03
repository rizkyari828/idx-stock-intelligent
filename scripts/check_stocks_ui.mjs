// Read-only production React acceptance; synthetic data belongs to the existing disposable fixture.
import assert from 'node:assert/strict';
export async function checkStocks({base,portfolioId,send,evaluate,until,click,set,submit,nav,size,noOverflow,shot,buttonText,requests,exceptions,output}) {
 const id=n=>`10000000-0000-4000-8000-${String(n).padStart(12,'0')}`;
 const text=()=>evaluate('document.querySelector("main").innerText');
 const ready=()=>until('!!document.querySelector("[aria-label=\\\"Current registry metadata\\\"]") && !document.querySelector("main [aria-busy=true]")');
 const pin=async()=>{await set('[name=stockThrough]','2026-09-30');await set('[name=stockCutoff]','2026-10-01T19:00:00');await submit('[aria-label="Stock context"]');await ready();};
 await nav('Stocks');await until('!!document.querySelector(".stocks-table")');
 await set('[name=stockSearch]','zzunknown');await until('document.querySelectorAll(".stocks-table tbody tr").length===1 && document.querySelector(".stocks-table")?.innerText.includes("ZZUNKNOWN")');
 assert.match(await text(),/UNKNOWN/);assert.match(await text(),/UNAVAILABLE/);
 for(const width of [1440,1366,1280,1024,768,390]){await size(width);await noOverflow();await shot('stocks-unknown-'+width);}
 await click('.stocks-table a');await ready();assert.equal(await evaluate('location.pathname'),'/stocks/'+id(99));
 assert.match(await text(),/No visible canonical price/);assert.match(await text(),/Insufficient usable history/);assert.match(await text(),/Not evaluated by current Screener universe/);
 for(const width of [1440,1366,1280,1024,768,390]){await size(width);await noOverflow();await shot('stock-detail-unknown-'+width);}
 await size(1440);await evaluate('history.back()');await until('!!document.querySelector("[name=stockSearch]")');
 await set('[name=stockSearch]','syn1');await until('document.querySelectorAll(".stocks-table tbody tr").length===1 && document.querySelector(".stocks-table")?.innerText.includes("SYN1")');await click('.stocks-table a');await ready();assert.match(await text(),/STALE/);await pin();
 await set('[name=stockThrough]','2026-08-24');await submit('[aria-label="Stock context"]');await ready();assert.match(await text(),/Insufficient usable history/);assert.equal(await evaluate('document.querySelectorAll(".stock-chart").length'),0);await pin();
 for(const token of ['Current registry metadata','Currency UNKNOWN','WARMUP','CONFIRMED','No portfolio selected','2026-09-30'])assert.ok((await text()).includes(token),token);
 assert.equal(await evaluate('document.querySelectorAll(".stock-chart").length'),1);assert.ok(await evaluate('document.querySelector(".stock-chart").getBoundingClientRect().height>=100'),'Chart must override shell icon dimensions');
 for(const width of [1440,1366,1280,1024,768,390]){await size(width);await noOverflow();await shot('stock-detail-pilot-'+width);await evaluate('document.querySelector("details").open=true');await noOverflow();await shot('stock-provenance-'+width);await evaluate('document.querySelector("details").open=false');}
 await size(1440);await set('[name=stockThrough]','2026-09-29');assert.equal(await evaluate('document.querySelectorAll(".stock-history-table").length'),0);await submit('[aria-label="Stock context"]');await ready();assert.match(await text(),/Through 2026-09-29/);
 await set('[name=stockCutoff]','2026-09-30T19:00:00');await submit('[aria-label="Stock context"]');await ready();assert.match(await text(),/No visible canonical price/);
 await buttonText('Open another portfolio');await until('!!document.querySelector("#open-portfolio-form")');await set('#open-portfolio-form [name=portfolioId]',portfolioId);await submit('#open-portfolio-form');await until('!document.querySelector("dialog")');await ready();await pin();assert.match(await text(),/Held/);assert.match(await text(),/FAST_SWING/);
 await set('[name=stockCutoff]','2026-09-30T19:00:00');await submit('[aria-label="Stock context"]');await ready();assert.match(await text(),/Not held in selected portfolio/);
 await send('Page.navigate',{url:base+'/stocks/bad'});await until('document.querySelector("main [role=alert]")?.innerText.includes("Invalid stable instrument ID")');await noOverflow();
 await send('Page.navigate',{url:base+'/stocks/'+id(98)});await until('document.querySelector("main [role=alert]")?.innerText.includes("Instrument not found")');
 await nav('Screener');await until('!!document.querySelector("[name=screenerThrough]")');await set('[name=screenerThrough]','2026-09-30');await set('[name=screenerCutoff]','2026-10-01T19:00:00');await submit('[aria-label="Screener context"]');await until('!!document.querySelector(".screener-table a.stock-link")');await click('.screener-table a.stock-link');await ready();assert.equal(await evaluate('location.pathname'),'/stocks/'+id(1));await evaluate('history.back()');await until('!!document.querySelector("[name=screenerThrough]")');
 await nav('Portfolio');await until('!!document.querySelector(".holdings-table")');await click('.holdings-table tbody tr button');await until('!!document.querySelector("dialog a.stock-link")');await click('dialog a.stock-link');await ready();assert.equal(await evaluate('document.querySelectorAll("dialog[open]").length'),0);
 assert.equal(requests.filter(r=>r.method!=='GET').length,0,'Browsing must not write');assert.ok(requests.every(r=>r.url.startsWith(base+'/')||r.url.startsWith('data:')),'No external requests');assert.equal(exceptions.length,0,JSON.stringify(exceptions));
 console.log(JSON.stringify({status:'PASS',widths:[1440,1366,1280,1024,768,390],checks:'search; stable routes/back; pilot and unknown/no-price; WARMUP and CONFIRMED; context invalidation; exact historical cutoff; held/unheld chronology; Screener and Portfolio links; invalid and missing UUID; responsive table/provenance; GET only; no external requests',screenshots:output},null,2));
}
