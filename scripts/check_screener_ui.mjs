// Uses the existing Chrome/CDP harness. Every response comes from the disposable API.
import assert from 'node:assert/strict';
import {readFile, writeFile} from 'node:fs/promises';
import {basename, join} from 'node:path';
import {execFileSync} from 'node:child_process';

export async function checkScreener({base,portfolioId,fixtureRoot,send,evaluate,until,click,set,submit,nav,size,noOverflow,shot,buttonText,requests,exceptions,output}) {
 assert.match(process.env.IDX_TEST_SCREENER_DATABASE ?? '',/^idx_screener_test_[a-f0-9]{32}$/);
 assert.match(basename(fixtureRoot),/^idx-screener-http-/,'Only an owned temporary reference directory');
 const reference=join(fixtureRoot,'pilot','screener-reference.json');const original=await readFile(reference);
 const start=requests.length;
 const screenRequests=()=>requests.slice(start).filter(r=>r.url.includes('/api/screener?'));
 const query=()=>new URL(screenRequests().at(-1).url).searchParams;
 const ready=()=>until('!!document.querySelector("[aria-label=\\\"Discovery results\\\"]") && !document.querySelector("main [aria-busy=true]")');
 const held=()=>evaluate('Array.from(document.querySelectorAll("[aria-label=\\\"All Held Positions\\\"] tbody tr")).map(row=>row.dataset.instrumentId)');
 const text=()=>evaluate('document.querySelector("main").innerText');
 const id=n=>`10000000-0000-4000-8000-${String(n).padStart(12,'0')}`;
 const key=async(key,code,virtual)=>{await send('Input.dispatchKeyEvent',{type:'keyDown',key,code,windowsVirtualKeyCode:virtual});await send('Input.dispatchKeyEvent',{type:'keyUp',key,code,windowsVirtualKeyCode:virtual});};
 try {
  await nav('Screener');await until('!!document.querySelector("[name=screenerThrough]")');
  await set('[name=screenerThrough]','2026-09-30');await set('[name=screenerCutoff]','2026-10-01T19:00:00');
  assert.equal(await evaluate('document.querySelectorAll(".screener-table").length'),0,'Draft edit clears old rows');
  await submit('[aria-label="Screener context"]');await ready();
  assert.equal(query().get('view'),'shortlist');assert.equal(query().get('limit'),'20');assert.equal(query().has('inputHash'),false);
  assert.equal(query().get('cutoff'),'2026-10-01T12:00:00.000Z');assert.equal(query().has('portfolioId'),false);
  assert.match(await text(),/Configured/);assert.match(await text(),/Coverage is partial/);assert.match(await text(),/CONFIRMED/);
  assert.equal(await evaluate('document.querySelectorAll("[aria-label=\\\"All Held Positions\\\"]").length'),0);
  await shot('screener-discovery-only');
  const resolved=await evaluate('document.querySelector(".read-context").innerText');assert.match(resolved,/Through 2026-09-30/);assert.match(resolved,/Target session 2026-09-30/);
  await buttonText('All instruments');await ready();const pinned=query().get('inputHash'),cutoff=query().get('cutoff');
  assert.match(pinned,/^[a-f0-9]{64}$/);assert.equal(await evaluate('document.querySelectorAll("[aria-label=\\\"Discovery results\\\"] tbody tr").length'),5);
  await buttonText('Open another portfolio');await until('!!document.querySelector("#open-portfolio-form")');await set('#open-portfolio-form [name=portfolioId]',portfolioId);await submit('#open-portfolio-form');await until('!document.querySelector("dialog")');
  await set('[name=screenerThrough]','2026-09-30');await set('[name=screenerCutoff]','2026-10-01T19:00:00');await submit('[aria-label="Screener context"]');await ready();
  assert.equal(query().get('portfolioId'),portfolioId);assert.equal(query().has('inputHash'),false);
  const expectedHeld=[id(1),id(4),id(5),id(6)];assert.deepEqual(await held(),expectedHeld);
  assert.match(await text(),/FAST_SWING/);assert.match(await text(),/Unassigned/);assert.match(await text(),/2026-09-29/);assert.match(await text(),/Not evaluated/);assert.match(await text(),/Canonical: DEGRADED/);
  await shot('screener-portfolio-default20');
  const portfolioPin=await evaluate('document.querySelector("[aria-label=\\\"Discovery results\\\"] tbody tr").dataset.instrumentId');assert.equal(portfolioPin,id(1));
  await set('[name=screenerSetup]','FAILED');await ready();assert.equal(query().get('setup'),'FAILED');assert.equal(query().get('offset'),'0');assert.equal(query().get('cutoff'),cutoff);assert.equal(await evaluate('document.querySelectorAll("[aria-label=\\\"Discovery results\\\"] tbody tr").length'),0);assert.deepEqual(await held(),expectedHeld);
  const heldPin=query().get('inputHash');await buttonText('All instruments');await ready();await set('[name=screenerSetup]','ALL');await ready();await set('[name=screenerEligibility]','INELIGIBLE');await ready();assert.equal(query().get('inputHash'),heldPin);assert.equal(query().get('eligibility'),'INELIGIBLE');assert.deepEqual(await held(),expectedHeld);
  await set('[name=screenerEligibility]','ALL');await ready();
  // PILOT is bounded to ten members. Test-only fetch shim asks the REAL API for two
  // rows per page so Previous/Next can be exercised without expanding the universe.
  await evaluate(`(()=>{const fetch=window.fetch;window.__screenPending=[];window.__screenDelay=false;window.__screenPageSize=2;window.fetch=(input,options)=>{
   if(typeof input!=='string'||!input.startsWith('/api/screener?'))return fetch(input,options);
   const url=new URL(input,location.origin);url.searchParams.set('limit',window.__screenPageSize);const init={...options};
   if(!window.__screenDelay)return fetch(url,init);
   delete init.signal;return new Promise((resolve,reject)=>{const pending={resolve,reject,response:null};window.__screenPending.push(pending);fetch(url,init).then(response=>pending.response=response,reject);});
  };})()`);
  await buttonText('Refresh evaluation');await ready();assert.equal(query().get('limit'),'2');assert.equal(query().has('inputHash'),false);
  const pagePin=query().get('cutoff');await buttonText('Next');await ready();assert.equal(query().get('offset'),'2');assert.equal(Date.parse(query().get('cutoff')),Date.parse(pagePin));const nextPin=query().get('inputHash');assert.deepEqual(await held(),expectedHeld);
  await buttonText('Previous');await ready();assert.equal(query().get('offset'),'0');assert.equal(query().get('inputHash'),nextPin);assert.deepEqual(await held(),expectedHeld);
  // Hold two real HTTP responses; deliberately ignore cancellation, release newest first.
  await evaluate('window.__screenDelay=true');await set('[name=screenerSetup]','WATCH');await set('[name=screenerSetup]','NONE');
  await until('window.__screenPending.length===2 && window.__screenPending.every(p=>p.response)');
  assert.deepEqual(await held(),expectedHeld,'Held rows remain visible while pages load');
  await evaluate('window.__screenPending[1].resolve(window.__screenPending[1].response)');await ready();
  const latest=await evaluate('document.querySelector("[aria-label=\\\"Discovery results\\\"]").innerText');assert.match(latest,/NONE \/ No setup/);
  await evaluate('window.__screenPending[0].resolve(window.__screenPending[0].response);window.__screenDelay=false');
  await evaluate('new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))');
  assert.equal(await evaluate('document.querySelector("[aria-label=\\\"Discovery results\\\"]").innerText'),latest,'Late ignored-abort result cannot overwrite current filter');
  await set('[name=screenerSetup]','ALL');await ready();
  await evaluate(`document.querySelector('[aria-label="All Held Positions"] [data-instrument-id="${id(1)}"] button').focus()`);await click(`[aria-label="All Held Positions"] [data-instrument-id="${id(1)}"] button`);await until('!!document.querySelector("dialog[open]")');
  const detail=await evaluate('document.querySelector("dialog").innerText');for(const fact of ['Feature availability','WARMUP','Provenance','synthetic','referenceSnapshotIds','triggerPrice','knownAt','atrSeedStart'])assert.ok(detail.toLowerCase().includes(fact.toLowerCase()),fact);
  assert.match(detail,/RS20 · pp/);assert.match(detail,/RS60 · pp/);
  await size(390);await noOverflow();await shot('screener-details-390');await evaluate('document.querySelector("dialog").scrollTop=document.querySelector("dialog").scrollHeight');await shot('screener-details-bottom-390');
  const bounds=await evaluate('(()=>{const r=document.querySelector("dialog").getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom};})()');assert.ok(bounds.left>=0&&bounds.right<=390&&bounds.top>=0&&bounds.bottom<=1000);
  for(let i=0;i<32;i++){await key('Tab','Tab',9);assert.ok(await evaluate('document.querySelector("dialog").contains(document.activeElement)'));}
  await key('Escape','Escape',27);assert.equal(await evaluate('document.querySelectorAll("dialog[open]").length'),0);assert.ok(await evaluate('document.activeElement.matches("[aria-label^=\\\"Details for\\\"]")'));
  for(const width of [1440,1366,1280,1024,768,390]){await size(width);await noOverflow();assert.deepEqual(await held(),expectedHeld);await shot('screener-'+width);
   const reached=await evaluate(`Array.from(document.querySelectorAll('.table-scroll:has(.screener-table)')).every(scroll=>{scroll.scrollLeft=scroll.scrollWidth-scroll.clientWidth;const cell=scroll.querySelector('th:last-child').getBoundingClientRect(),frame=scroll.getBoundingClientRect();return cell.right<=frame.right+1 && cell.left>=frame.left;})`);assert.ok(reached,'Last headers reachable inside each scroll container at '+width);
   if(width===390||width===1280)await shot('screener-scrolled-'+width);
   await evaluate("document.querySelectorAll('.table-scroll:has(.screener-table)').forEach(scroll=>scroll.scrollLeft=0)");
  }
  await size(1440);await click('[aria-label="Switch to light theme"]');await shot('screener-light');await size(390);await noOverflow();await shot('screener-light-390');await click('[aria-label="Switch to dark theme"]');await size(1440);
  // Keyboard-reachable context, filters, paging and refresh; native focus outline exists.
  for(const selector of ['[name=screenerThrough]','[name=screenerCutoff]','[name=screenerSetup]','[name=screenerEligibility]','[aria-label="Discovery pagination"] button:not(:disabled)']){
   await evaluate(`document.querySelector(${JSON.stringify(selector)}).focus()`);await key('Tab','Tab',9);assert.ok(await evaluate('document.activeElement!==document.body'));}
  await send('Emulation.setEmulatedMedia',{features:[{name:'prefers-reduced-motion',value:'reduce'}]});assert.equal(await evaluate('getComputedStyle(document.querySelector(".button")).transitionDuration'),'0s');
  // A visible selected revision, in the owned DB only, must force a real 409.
  const sql=await readFile(join(fixtureRoot,'selected-change.sql'),'utf8');execFileSync('docker',['compose','exec','-T','postgres','psql','-X','-q','-v','ON_ERROR_STOP=1','-U','idx_stock','-d',process.env.IDX_TEST_SCREENER_DATABASE],{input:sql,stdio:['pipe','pipe','pipe'],timeout:30000});
  await buttonText('Next');await until('document.querySelector("main [role=alert]")?.innerText.includes("Underlying Screener inputs changed")');
  assert.equal(await evaluate('document.querySelectorAll(".screener-table").length'),0);assert.equal(query().get('inputHash'),nextPin);
  await shot('screener-input-changed');await buttonText('Refresh evaluation');await ready();assert.equal(query().has('inputHash'),false);assert.equal(query().get('offset'),'0');assert.deepEqual(await held(),expectedHeld);assert.match(await text(),/Selected: REJECTED/);
  // Selected portfolio change and date/cutoff drafts immediately invalidate the pin.
  await set('[name=screenerThrough]','2026-09-29');assert.equal(await evaluate('document.querySelectorAll(".screener-table").length'),0);await set('[name=screenerThrough]','2026-09-30');await submit('[aria-label="Screener context"]');await ready();assert.equal(query().has('inputHash'),false);
  await set('[name=screenerPortfolio]','');await ready();assert.equal(query().has('portfolioId'),false);assert.equal(query().has('inputHash'),false);assert.equal(await evaluate('document.querySelectorAll("[aria-label=\\\"All Held Positions\\\"]").length'),0);
  // These are temporary files beside the disposable API, never operational references.
  await writeFile(reference,JSON.stringify({schemaVersion:1,universes:[],instruments:[]}));await buttonText('Refresh evaluation');await ready();assert.match(await text(),/Screening could not be fully evaluated/);assert.match(await text(),/Trend · UNKNOWN/);assert.match(await text(),/Volatility · UNKNOWN/);assert.doesNotMatch(await text(),/No candidates met/);await shot('screener-blocked');
  await writeFile(reference,await readFile(join(fixtureRoot,'complete-empty-reference.json')));await buttonText('Refresh evaluation');await ready();assert.match(await text(),/Evaluation complete/);assert.match(await text(),/No candidates met this descriptive setup/);await shot('screener-complete-empty');
  await writeFile(reference,'{');await buttonText('Refresh evaluation');await until('document.querySelector("main [role=alert]")?.innerText.includes("Retry the evaluation")');assert.equal(await evaluate('document.querySelectorAll(".screener-table").length'),0);assert.doesNotMatch(await text(),/No candidates met/);
  await writeFile(reference,original);await buttonText('Refresh evaluation');await ready();assert.match(await text(),/Coverage is partial/);
  assert.equal(requests.slice(start).filter(r=>r.method!=='GET').length,0,'Screener browsing is read-only');
  assert.ok(requests.every(r=>r.url.startsWith(base+'/')||r.url.startsWith('data:')),'No external request');assert.equal(exceptions.length,0,JSON.stringify(exceptions));
  assert.deepEqual(await evaluate('Object.keys(localStorage).filter(key=>/screener/i.test(key))'),[],'No Screener persistence');
  console.log(JSON.stringify({status:'PASS',checks:'real HTTP discovery/optional portfolio/outside held/filter/paging pins; ignored-abort reverse responses; selected revision 409/explicit refresh; details/quality/availability; partial/blocked/complete-empty/503; keyboard/dialog focus/reduced motion/themes; no browsing writes or external requests',pagination:'test-only limit2 shim against real API; production default20 verified',widths:[1440,1366,1280,1024,768,390],screenshots:output},null,2));
 } finally {await writeFile(reference,original);}
}
