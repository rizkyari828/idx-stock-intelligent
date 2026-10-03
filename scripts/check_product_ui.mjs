// Real React/HTTP acceptance in an OWNED disposable database. Node 22+, native CDP, no packages.
import assert from 'node:assert/strict';
import {checkScreener} from './check_screener_ui.mjs';
import {checkStocks} from './check_stocks_ui.mjs';
import {checkDecisions} from './check_decisions_ui.mjs';
import {checkVerification} from './check_decision_verification_ui.mjs';
import {mkdtemp, mkdir, writeFile, rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {spawn} from 'node:child_process';
import {createServer} from 'node:net';
const [base,portfolioId,mode='portfolio',fixtureRoot]=process.argv.slice(2);
assert.ok(/^http:\/\/127\.0\.0\.1:\d+$/.test(base),'Only the disposable loopback API is allowed');
assert.notEqual(new URL(base).port,'5080','Never run synthetic UI acceptance on the operational port');
const output=process.env.IDX_TEST_UI_OUTPUT||join(tmpdir(),'idx-product-ui-previews');
const profile=await mkdtemp(join(tmpdir(),'idx-product-ui-chrome-'));
const port=await new Promise(resolve=>{const server=createServer();server.listen(0,'127.0.0.1',()=>{const port=server.address().port;server.close(()=>resolve(port));});});
const chrome=spawn(process.env.IDX_TEST_CHROME||'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',['--headless','--disable-gpu','--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-component-update','--disable-sync',`--remote-debugging-port=${port}`,`--user-data-dir=${profile}`,'about:blank'],{stdio:'ignore'});
let chromeError;chrome.on('error',e=>chromeError=e);
let socket;let sequence=0;const pending=new Map(),exceptions=[],requests=[];
const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function request(path,body){const response=await fetch(base+'/api'+path,body?{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}:{});assert.equal(response.status,200,await response.clone().text());return response.json();}
function send(method,params={}){return new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params}));});}
async function evaluate(expression){const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw new Error(JSON.stringify(r.exceptionDetails));return r.result.value;}
async function until(expression){for(let i=0;i<160;i++){if(await evaluate(expression))return;await wait(50);}throw new Error('Browser timed out: '+expression+'\n'+await evaluate('document.body.innerText'));}
const click=selector=>evaluate(`document.querySelector(${JSON.stringify(selector)}).click()`);
async function set(selector,value){await evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});const proto=e instanceof HTMLSelectElement?HTMLSelectElement.prototype:e instanceof HTMLTextAreaElement?HTMLTextAreaElement.prototype:HTMLInputElement.prototype;Object.getOwnPropertyDescriptor(proto,'value').set.call(e,${JSON.stringify(value)});e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));})()`);}
const submit=selector=>evaluate(`document.querySelector(${JSON.stringify(selector)}).requestSubmit()`);
const nav=label=>evaluate(`Array.from(document.querySelectorAll('nav button')).find(e=>e.title===${JSON.stringify(label)}).click()`);
async function size(width,height=1000){await send('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile:false});}
async function noOverflow(){const d=await evaluate('({width:innerWidth,scroll:document.documentElement.scrollWidth})');assert.ok(d.scroll<=d.width+1,JSON.stringify(d));}
async function shot(name){await evaluate('window.scrollTo(0,0)');await evaluate('document.fonts.ready');const r=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:true});await writeFile(join(output,name+'.png'),Buffer.from(r.data,'base64'));}
async function file(format,content,name){await evaluate(`(()=>{const e=document.querySelector('#import-file');const data=new DataTransfer();data.items.add(new File([${JSON.stringify(content)}],${JSON.stringify(name)},{type:${JSON.stringify(format==='JSON'?'application/json':'text/csv')}}));e.files=data.files;e.dispatchEvent(new Event('change',{bubbles:true}));})()`);await until('!document.querySelector("#import-file").disabled');}
const buttonText=text=>evaluate(`Array.from(document.querySelectorAll('main button')).find(e=>e.textContent.trim().startsWith(${JSON.stringify(text)})).click()`);
try {
 if(mode!=='screener'&&mode!=='stocks'&&mode!=='decisions'&&mode!=='verification')assert.equal((await request(`/portfolios/${portfolioId}`)).portfolio.name,'SYNTHETIC HISTORICAL RESTORE','UI acceptance requires the owned historical fixture');
 await mkdir(output,{recursive:true});let pages;
 for(let i=0;i<100;i++){if(chromeError)throw chromeError;try{pages=await (await fetch(`http://127.0.0.1:${port}/json`)).json();break;}catch{await wait(50);}}
 assert.ok(pages,'Chrome startup');socket=new WebSocket(pages.find(p=>p.type==='page').webSocketDebuggerUrl);await new Promise((resolve,reject)=>{socket.onopen=resolve;socket.onerror=reject;});
 socket.onmessage=event=>{const m=JSON.parse(event.data);if(m.method==='Runtime.exceptionThrown')exceptions.push(m.params.exceptionDetails);if(m.method==='Network.requestWillBeSent')requests.push(m.params.request);if(m.id){const cb=pending.get(m.id);pending.delete(m.id);m.error?cb.reject(m.error):cb.resolve(m.result);}};
 await send('Page.enable');await send('Runtime.enable');await send('Network.enable');await size(1440);await send('Page.navigate',{url:base});await until('!!document.querySelector(".portfolio-select button")');
 if(mode==='verification'){await checkVerification({base,send,evaluate,until,click,nav,size,noOverflow,shot,requests,exceptions});}else if(mode==='decisions'){await checkDecisions({base,portfolioId,send,evaluate,until,click,set,submit,nav,size,noOverflow,shot,buttonText,requests,exceptions,output});}else if(mode==='stocks'){await checkStocks({base,portfolioId,send,evaluate,until,click,set,submit,nav,size,noOverflow,shot,buttonText,requests,exceptions,output});}else if(mode==='screener'){await checkScreener({base,portfolioId,fixtureRoot,send,evaluate,until,click,set,submit,nav,size,noOverflow,shot,buttonText,requests,exceptions,output});}else {
 await evaluate(`localStorage.setItem('idx-portfolio-id',${JSON.stringify(portfolioId)});localStorage.setItem('idx-theme','dark')`);await send('Page.reload',{ignoreCache:true});await until('!!document.querySelector(".holdings-table")');
 assert.match(await evaluate('document.querySelector("main").innerText'),/Valuation is incomplete/);assert.equal(await evaluate('document.querySelectorAll(".holdings-table tbody tr").length'),2);await shot('portfolio-1440');
 const initial=await request(`/portfolios/${portfolioId}`);const historyBefore=await request(`/portfolios/${portfolioId}/events`);
 await nav('Reconcile');await until('!!document.querySelector("#snapshot-form")');await set('[name=expectedCash]',String(initial.cash));
 for(let i=0;i<initial.holdings.length;i++){await buttonText('Add holding');const h=initial.holdings[i];await set(`[aria-label="Holding ${i+1} symbol"]`,h.market.displaySymbol);await set(`[aria-label="Holding ${i+1} shares"]`,String(h.position.shares));await set(`[aria-label="Holding ${i+1} average cost"]`,String(h.position.averageCost));}
 await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');assert.match(await evaluate('document.querySelector("main").innerText'),/Your snapshot matches the ledger/);await shot('reconcile-match');
 await buttonText('Edit snapshot');await set('[aria-label="Holding 1 shares"]',String(initial.holdings[0].position.shares+1));assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0);await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');assert.match(await evaluate('document.querySelector("main").innerText'),/Differences need your review/);await buttonText('Needs review');assert.equal(await evaluate('document.querySelectorAll(".reconcile-table tbody tr").length'),1);await shot('reconcile-review');
 // Snapshot CSV stays local; applying replaces holdings and preserves explicit cash.
 const archiveBeforeSnapshot=await request(`/portfolios/${portfolioId}/export`);
 const snapshotStart=requests.length;
 const loadSnapshot=async(content,name='private-snapshot.csv')=>{
  await evaluate(`(()=>{const e=document.querySelector('#snapshot-file');const data=new DataTransfer();data.items.add(new File([${JSON.stringify(content)}],${JSON.stringify(name)},{type:'text/csv'}));e.files=data.files;e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
  await until('!!document.querySelector(".snapshot-preview") || !!document.querySelector("#snapshot-editor [role=alert]")');
 };
 const h=initial.holdings[0],other=initial.holdings[1];
 const snapshotCsv='source_account,symbol,lots,shares,average_cost,mandate\n'+`Stockbit,${h.market.displaySymbol},,1,${h.position.averageCost},LONG_SWING\nBroker_2,${h.market.displaySymbol.toLowerCase()},,${h.position.shares-1},${h.position.averageCost},FAST_SWING\nBroker_2,${other.market.displaySymbol},,${other.position.shares},${other.position.averageCost},INVEST\n`;
 await loadSnapshot(snapshotCsv);
 assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0);
 assert.match(await evaluate('document.querySelector(".snapshot-preview").innerText'),/3 rows read · 3 valid · 2 holdings · 2 source accounts · 0 errors/);
 assert.match(await evaluate('document.querySelector(".snapshot-preview").innerText'),/1 duplicate-symbol groups consolidated/);
 assert.equal(requests.slice(snapshotStart).filter(r=>r.url.includes('/api/')&&r.method==='POST').length,0,'Upload and parse must make no API writes');
 for(const width of [1440,390]){await size(width);await noOverflow();await shot('snapshot-preview-'+width);}await size(1440);
 await buttonText('Apply Snapshot');assert.equal(await evaluate('document.querySelectorAll(".expected-table tbody tr").length'),2);
 assert.equal(await evaluate('document.querySelector(".expected-table tbody tr td:nth-child(3) input").value'),String(h.position.shares));
 assert.equal(await evaluate('document.querySelector("[name=expectedCash]").value'),String(initial.cash));
 assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0);
 assert.equal(requests.slice(snapshotStart).filter(r=>r.url.includes('/api/')&&r.method==='POST').length,0,'Apply must make no API writes');
 await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');
 const snapshotRequests=requests.slice(snapshotStart).filter(r=>r.url.includes('/api/')&&r.method==='POST');
 assert.equal(snapshotRequests.length,1);assert.ok(snapshotRequests[0].url.endsWith(`/portfolios/${portfolioId}/reconciliation/preview`));
 const snapshotBody=JSON.parse(snapshotRequests[0].postData);assert.equal(snapshotBody.expectedCash,String(initial.cash));
 assert.deepEqual(Object.keys(snapshotBody.holdings[0]).sort(),['averageCost','instrumentId','shares','symbol']);assert.equal(snapshotBody.holdings[0].instrumentId,null);
 assert.match(await evaluate('document.querySelector("main").innerText'),/Your snapshot matches the ledger/);
 await loadSnapshot('symbol,lots,shares,mandate\nANTM,2,100,UNKNOWN');assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0);
 assert.equal(await evaluate('Array.from(document.querySelectorAll(".snapshot-preview button")).find(e=>e.textContent==="Apply Snapshot").disabled'),true);
 assert.match(await evaluate('document.querySelector(".snapshot-preview").innerText'),/Row 2/);await buttonText('Cancel');
 await loadSnapshot('symbol,shares,mandate\nANTM,100,UNKNOWN');assert.match(await evaluate('document.querySelector(".snapshot-preview").innerText'),/unknown mandate/);await buttonText('Cancel');
 await loadSnapshot('symbol,shares\nREPLACEMENT,100');await buttonText('Apply Snapshot');assert.equal(await evaluate('document.querySelectorAll(".expected-table tbody tr").length'),1);
 await loadSnapshot(snapshotCsv);await buttonText('Apply Snapshot');await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');
 await buttonText('Edit snapshot');await set('[aria-label="Holding 1 shares"]',String(h.position.shares+1));assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0);await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');
 assert.deepEqual(await request(`/portfolios/${portfolioId}/export`),archiveBeforeSnapshot,'No ledger, thesis or snapshot persistence');
 assert.ok(requests.slice(snapshotStart).every(r=>!r.url.includes('/portfolio-imports')),'Snapshot must never use transaction import');
 // Local draft reload, explicit canonical registration, and Clear Draft use the same disposable fixture.
 const draftStart=requests.length,missingSymbol='UI_SNAPSHOT_EQUITY';
 await loadSnapshot(snapshotCsv+`Broker_3,${missingSymbol},1,100,10,INVEST\n`);await buttonText('Apply Snapshot');
 await until('document.querySelector("[aria-label*=registry]")?.innerText.includes("1 missing")');
 assert.equal(requests.slice(draftStart).filter(r=>r.method==='POST').length,0,'Draft persistence and registry check are read-only');
 await submit('#snapshot-form');await until('document.querySelector(".reconcile-table")?.innerText.includes("Unknown instrument")');
 const snapshotKey='idx.reconcile.draft.v1:'+portfolioId;
 const persisted=await evaluate(`JSON.parse(localStorage.getItem(${JSON.stringify(snapshotKey)}))`);
 assert.equal(persisted.expectedCash,String(initial.cash));assert.equal(persisted.holdings.length,3);assert.equal(persisted.result,undefined);assert.equal(persisted.holdings[2].mandates[0],'INVEST');
 await send('Page.reload',{ignoreCache:true});await until('!!document.querySelector(".holdings-table")');await nav('Reconcile');await until('document.querySelectorAll(".expected-table tbody tr").length===3');
 assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0,'Result must never restore');assert.equal(await evaluate('document.querySelector("[name=expectedCash]").value'),String(initial.cash));
 await until('document.querySelector("[aria-label*=registry]")?.innerText.includes("1 missing")');
 const registrationStart=requests.length;await buttonText('Review Missing Instruments');
 assert.match(await evaluate('document.querySelector("[aria-label*=registration]").innerText'),/UI_SNAPSHOT_EQUITY.*EQUITY.*UI_SNAPSHOT_EQUITY.*Broker_3/s);
 assert.equal(requests.slice(registrationStart).filter(r=>r.method==='POST').length,0,'Review requires separate confirmation');
 await size(390);await noOverflow();await shot('missing-instruments-390');await size(1440);
 await buttonText('Register 1 Instruments');await until('document.querySelector("[aria-label*=registry]")?.innerText.includes("Completed.")');
 assert.equal(await evaluate('document.querySelector("[name=expectedCash]").value'),String(initial.cash));assert.equal(await evaluate('document.querySelectorAll(".expected-table tbody tr").length'),3);
 const registrationPosts=requests.slice(registrationStart).filter(r=>r.method==='POST');assert.equal(registrationPosts.length,1);assert.ok(registrationPosts[0].url.endsWith('/api/instruments'));const registrationBody=JSON.parse(registrationPosts[0].postData);assert.equal(registrationBody.symbol,missingSymbol);assert.equal(registrationBody.name,missingSymbol);assert.equal(registrationBody.type,'EQUITY');assert.equal(registrationBody.validFrom,initial.through);assert.equal(registrationBody.mandate,undefined);
 const afterRegistration=await request(`/portfolios/${portfolioId}/export`);assert.deepEqual(afterRegistration.events,archiveBeforeSnapshot.events);assert.deepEqual(afterRegistration.theses,archiveBeforeSnapshot.theses);assert.deepEqual(afterRegistration.portfolio,archiveBeforeSnapshot.portfolio);
 await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');const registeredRow=await evaluate('Array.from(document.querySelectorAll(".reconcile-table tbody tr")).find(r=>r.textContent.includes("UI_SNAPSHOT_EQUITY")).innerText');assert.match(registeredRow,/Missing from ledger/);assert.doesNotMatch(registeredRow,/Unknown instrument/);
 // Context, manual cash/holdings edits and portfolio isolation persist independently.
 await buttonText('Edit snapshot');await set('[name=expectedCash]','55');await set('[aria-label="Holding 3 shares"]','200');
 await set('[name=through]','2026-09-30');await set('[name=cutoff]','2026-10-01T09:00:00');await submit('.context-bar');
 await until(`JSON.parse(localStorage.getItem(${JSON.stringify(snapshotKey)})).through==='2026-09-30'`);
 await send('Page.reload',{ignoreCache:true});await until('!!document.querySelector(".holdings-table")');await nav('Reconcile');await until('document.querySelectorAll(".expected-table tbody tr").length===3');
 assert.equal(await evaluate('document.querySelector("[name=expectedCash]").value'),'55');assert.equal(await evaluate('document.querySelectorAll(".expected-table tbody tr td:nth-child(3) input")[2].value'),'200');assert.equal(await evaluate('document.querySelector("[name=through]").value'),'2026-09-30');assert.match(await evaluate('document.querySelector("[name=cutoff]").value'),/^2026-10-01T09:00(?::00)?$/);assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0);
 const otherPortfolio=(await request('/portfolios',{name:'SYNTHETIC DRAFT ISOLATION'})).id;
 await click('.portfolio-select button');await set('#open-portfolio-form [name=portfolioId]',otherPortfolio);await submit('#open-portfolio-form');await until('!document.querySelector("dialog") && document.querySelectorAll(".expected-table tbody tr").length===0');assert.equal(await evaluate('document.querySelector("[name=expectedCash]").value'),'');await set('[name=expectedCash]','77');
 await click('.portfolio-select button');await set('#open-portfolio-form [name=portfolioId]',portfolioId);await submit('#open-portfolio-form');await until('!document.querySelector("dialog") && document.querySelectorAll(".expected-table tbody tr").length===3');assert.equal(await evaluate('document.querySelector("[name=expectedCash]").value'),'55');
 await buttonText('Clear Draft');await until('document.querySelectorAll(".expected-table tbody tr").length===0');assert.equal(await evaluate(`localStorage.getItem(${JSON.stringify(snapshotKey)})`),null);assert.equal(await evaluate(`JSON.parse(localStorage.getItem('idx.reconcile.draft.v1:'+${JSON.stringify(otherPortfolio)})).expectedCash`),'77');assert.equal(await evaluate('document.querySelector("[name=expectedCash]").value'),'');
 // Restore the original snapshot/context for the existing regression scenarios.
 await set('[name=through]',initial.through);await set('[name=cutoff]','');await submit('.context-bar');await set('[name=expectedCash]',String(initial.cash));await loadSnapshot(snapshotCsv);await buttonText('Apply Snapshot');await set('[aria-label="Holding 1 shares"]',String(h.position.shares+1));await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');
 // Backend errors remain visible without changing accounting.
 await buttonText('Edit snapshot');await set('[aria-label="Holding 1 stable ID"]',initial.holdings[1].position.instrumentId);await submit('#snapshot-form');await until('!!document.querySelector("main [role=alert]")');assert.match(await evaluate('document.querySelector("main [role=alert]").innerText'),/same equity identity/);await set('[aria-label="Holding 1 stable ID"]','');await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');
 await request('/instruments',{id:crypto.randomUUID(),name:'SYNTHETIC UI MISSING',symbol:'UI_MISSING',type:'EQUITY',validFrom:'2000-01-01'});
 await buttonText('Edit snapshot');await set('[name=expectedCash]',String(initial.cash+1));await click('[aria-label="Remove holding 2"]');
 for(const [i,symbol] of [[2,'UI_MISSING'],[3,'UI_UNKNOWN']]){await buttonText('Add holding');await set(`[aria-label="Holding ${i} symbol"]`,symbol);await set(`[aria-label="Holding ${i} shares"]`,'100');await set(`[aria-label="Holding ${i} average cost"]`,'10');}
 await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');const statuses=await evaluate('document.querySelector(".reconcile-table").innerText');for(const status of ['Review','Missing from ledger','Missing from expected','Unknown instrument'])assert.ok(statuses.includes(status),status);
 // Editing the context invalidates old results before Apply, too.
 await set('[name=through]','2026-09-30');assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0);assert.match(await evaluate('document.querySelector("main").innerText'),/Context changed/);await submit('#snapshot-form');await wait(100);assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0);await set('[name=through]',initial.through);await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');
 await set('[name=through]','2026-09-30');await submit('.context-bar');await evaluate('Array.from(document.querySelectorAll(".context-bar button")).find(e=>e.textContent.trim()==="Now").click()');assert.equal(await evaluate('document.querySelector("[name=through]").value'),'2026-09-30');await set('[name=through]',initial.through);await submit('.context-bar');await submit('#snapshot-form');await until('!!document.querySelector(".reconcile-table")');
 for(const width of [1440,1366,1280,1024,768,390]){await size(width);await noOverflow();await shot('reconcile-'+width);if(width<=768){await evaluate('document.querySelector(".reconcile-table").parentElement.scrollLeft=10000');await noOverflow();await shot('reconcile-scrolled-'+width);}}
 await size(1440);await nav('Transactions');await until('!!document.querySelector(".transaction-table")');assert.match(await evaluate('document.querySelector("main").innerText'),/SUPERSEDED/);
 const active=historyBefore.find(e=>e.type==='BUY'&&!e.correctedBy);await click(`[data-event-id="${active.id}"] .table-action`);await until('!!document.querySelector("dialog[open]")');assert.match(await evaluate('document.querySelector("dialog").innerText'),/Original recorded time · immutable/);
 await size(390);await noOverflow();await shot('correction-390');const bounds=await evaluate('(()=>{const r=document.querySelector("dialog").getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom};})()');assert.ok(bounds.left>=0&&bounds.right<=390&&bounds.top>=0&&bounds.bottom<=1000);
 for(let i=0;i<24;i++){await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});assert.ok(await evaluate('document.querySelector("dialog").contains(document.activeElement)'));}
 await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});assert.equal(await evaluate('document.querySelectorAll("dialog[open]").length'),0);assert.ok(await evaluate('document.activeElement!==document.body'));
 await size(1440);await click(`[data-event-id="${active.id}"] .table-action`);await set('dialog [name=price]',String(active.price+1));await submit('#event-form');await until('!document.querySelector("dialog") && !!document.querySelector(".transaction-table")');
 const historyAfter=await request(`/portfolios/${portfolioId}/events`);assert.equal(historyAfter.length,historyBefore.length+1);const original=historyAfter.find(e=>e.id===active.id),replacement=historyAfter.find(e=>e.supersedes===active.id);assert.equal(original.price,active.price);assert.equal(original.correctedBy,replacement.id);assert.equal(replacement.price,active.price+1);assert.equal((await request(`/portfolios/${portfolioId}?cutoff=${encodeURIComponent(initial.knowledgeCutoff)}`)).cash,initial.cash);await shot('transactions-1440');
 await nav('Thesis');await until('!document.querySelector("main [aria-busy]") && !!document.querySelector(".thesis-current")');await buttonText('Create New Version');await set('dialog [name=text]','SYNTHETIC production UI version');await set('dialog [name=invalidation]','SYNTHETIC review condition');await submit('#thesis-form');await until('!document.querySelector("dialog") && document.querySelector(".thesis-copy")?.innerText==="SYNTHETIC production UI version"');await buttonText('Invalidate Thesis');await set('dialog [name=invalidation]','SYNTHETIC condition met');await submit('#thesis-form');await until('!document.querySelector("dialog") && document.querySelector(".thesis-current-top")?.innerText.includes("INACTIVE")');assert.ok(await evaluate('document.querySelectorAll(".timeline-entry").length>=5'));await shot('thesis-1440');
 await nav('Import / Export');await until('!!document.querySelector("#import-file")');assert.equal(await evaluate('Array.from(document.querySelectorAll("main button")).find(e=>e.textContent.trim().startsWith("Preview")).disabled'),true);
 const archive=await request(`/portfolios/${portfolioId}/export`);await file('JSON',JSON.stringify(archive),'synthetic.json');await buttonText('Preview');await until('!!document.querySelector(".import-review")');assert.equal(await evaluate('Array.from(document.querySelectorAll("main button")).find(e=>e.textContent.trim().startsWith("Confirm Import")).disabled'),false);
 await set('#restore-mode','RESTORE_EXISTING_EMPTY');assert.equal(await evaluate('document.querySelectorAll(".import-review").length'),0);await buttonText('Preview');await until('!!document.querySelector(".import-review")');await file('JSON','{"bad":true}','invalid.json');assert.equal(await evaluate('document.querySelectorAll(".import-review").length'),0);await buttonText('Preview');await until('!!document.querySelector(".import-review")');assert.equal(await evaluate('Array.from(document.querySelectorAll("main button")).find(e=>e.textContent.trim().startsWith("Confirm Import")).disabled'),true);
 await evaluate('Array.from(document.querySelectorAll(".format-option")).find(e=>e.textContent.includes("CSV")).click()');assert.equal(await evaluate('document.querySelectorAll(".import-review").length'),0);
 const day=initial.through;const csv='trade_date,type,instrument_id,symbol,quantity,unit,price,fees,cash_amount,external_reference,note\\n'.replace('\\n','\n')+`${day},CASH_DEPOSIT,,,0,SHARES,0,0,100,ui-acceptance-deposit,SYNTHETIC ONLY\n`;
 await file('CSV',csv,'synthetic.csv');await buttonText('Preview');await until('!!document.querySelector(".import-review")');await buttonText('Confirm Import');await until('!document.querySelector(".import-review")');await until('document.querySelector("main").innerText.includes("IMPORTED")');
 const previewRequests=requests.filter(r=>r.url.endsWith('/portfolio-imports/preview'));const commits=requests.filter(r=>r.url.endsWith('/portfolio-imports'));assert.equal(commits.length,1);assert.deepEqual(JSON.parse(commits[0].postData),JSON.parse(previewRequests.at(-1).postData));
 await file('CSV',csv,'synthetic.csv');await buttonText('Preview');await until('!!document.querySelector(".import-review")');await click('.portfolio-select button');await set('#create-portfolio-form [name=name]','SYNTHETIC UI EMPTY');await submit('#create-portfolio-form');await until('!document.querySelector("dialog") && !document.querySelector(".import-review")');
 // Check every actual production surface at every required width.
 await click('.portfolio-select button');await set('#open-portfolio-form [name=portfolioId]',portfolioId);await submit('#open-portfolio-form');await until('!document.querySelector("dialog")');
 for(const width of [1440,1366,1280,1024,768,390]){await size(width);for(const page of ['Portfolio','Transactions','Thesis','Import / Export']){await nav(page);await until('!document.querySelector("main [aria-busy]")');await noOverflow();await shot(page.replaceAll(' ','-').replace('/','')+'-'+width);}}
 await size(1440);await nav('Portfolio');await click('[aria-label="Switch to light theme"]');await noOverflow();await shot('portfolio-light');await click('[aria-label="Switch to dark theme"]');
 assert.equal(exceptions.length,0,JSON.stringify(exceptions));assert.ok(requests.every(r=>r.url.startsWith(base+'/')||r.url.startsWith('data:')),'External request');
 console.log(JSON.stringify({status:'PASS',checks:'local snapshot CSV/duplicate preview, local draft reload/context/isolation/clear, explicit EQUITY registration, UNKNOWN to missing-ledger, unchanged cash and ledger/thesis history, no snapshot import writes, real reconciliation/errors, correction append and history, thesis version/invalidation, JSON/CSV preview gates, exact confirmation payload, file/format/mode/portfolio invalidation, responsive overflow/dialogs, keyboard focus/Escape, light theme, no external requests',widths:[1440,1366,1280,1024,768,390],screenshots:output},null,2));
 }
} finally {socket?.close();chrome.kill();await new Promise(resolve=>{if(chrome.exitCode!==null)resolve();else {chrome.once('exit',resolve);setTimeout(resolve,3000);}});await rm(profile,{recursive:true,force:true});}
