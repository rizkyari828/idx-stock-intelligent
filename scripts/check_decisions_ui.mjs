// Production React read-only acceptance against the owned Decision Snapshot fixture.
import assert from 'node:assert/strict';
export async function checkDecisions({base,send,evaluate,until,click,nav,size,noOverflow,shot,requests,exceptions}) {
 const runs=JSON.parse(process.env.IDX_TEST_DECISION_RUNS);
 if(runs.real){await checkRetainedReal({base,send,evaluate,until,nav,size,noOverflow,shot,requests,exceptions},runs.real);return;}
 const id=n=>`10000000-0000-4000-8000-${String(n).padStart(12,'0')}`;
 const text=()=>evaluate('document.querySelector("main").innerText');
 const detail=()=>until('!!document.querySelector("[aria-label=\\\"Immutable capture context\\\"]") && !document.querySelector("main [aria-busy=true]")');
 await nav('Decision History');await until('document.querySelectorAll(".decision-list-table tbody tr").length===20');
 assert.equal(await evaluate('location.pathname'),'/decisions');
 assert.ok(requests.some(r=>new URL(r.url).pathname==='/api/screener/decision-snapshots'&&!new URL(r.url).search),'Use API default page size');
 for(const width of [1440,1366,1280,1024,768,390]){await size(width);await noOverflow();await shot('decisions-list-'+width);}
 await size(1440);await click('[aria-label="Decision history pagination"] button');await until('document.querySelectorAll(".decision-list-table tbody tr").length===3');
 assert.match(await text(),/BLOCKED/);assert.match(await text(),/PARTIAL/);assert.equal(await evaluate('document.querySelectorAll("main [role=alert]").length'),0);
 const cursors=requests.filter(r=>r.url.includes('/decision-snapshots?'));assert.ok(cursors.some(r=>new URL(r.url).searchParams.has('cursor')));
 await click(`a[href="/decisions/${runs.partial}"]`);await detail();assert.equal(await evaluate('location.pathname'),'/decisions/'+runs.partial);
 assert.equal(await evaluate('document.querySelectorAll(".decision-rows-table tbody tr").length'),6);
 for(const token of ['Captured at','Known by','Through','Target market session','Recorded at','Portfolio','PARTIAL','DATA_BLOCKED','WATCH','CONFIRMED','NONE / No setup','Outside configured scope'])assert.ok((await text()).includes(token),token);
 const hashes=await evaluate('Array.from(document.querySelectorAll(".decision-hash")).map(e=>e.textContent)');assert.equal(hashes.length,2);assert.ok(hashes.every(h=>/^[a-f0-9]{64}$/.test(h)));
 await click(`[data-instrument-id="${id(1)}"] button`);await until('!!document.querySelector("#captured-row-detail")');assert.equal(await evaluate(`document.querySelector('[data-instrument-id="${id(1)}"] button').getAttribute('aria-expanded')`),'true');
 for(const token of ['WARMUP','UNAVAILABLE','Captured episode','Captured portfolio facts','FAST_SWING'])assert.ok((await text()).includes(token),token);
 for(const width of [1440,1366,1280,1024,768,390]){await size(width);await noOverflow();const bounds=await evaluate('(()=>{const r=document.querySelector("#captured-row-detail").getBoundingClientRect();return {left:r.left,right:r.right};})()');assert.ok(bounds.left>=0&&bounds.right<=width+1,JSON.stringify(bounds));await shot('decisions-detail-expanded-'+width);}
 await size(1440);await click(`[data-instrument-id="${id(6)}"] button`);await until('document.querySelector("#captured-row-detail")?.innerText.includes("Outside configured Screener scope")');
 assert.match(await evaluate('document.querySelector("#captured-row-detail").innerText'),/Not evaluated/);assert.doesNotMatch(await evaluate('document.querySelector("#captured-row-detail").innerText'),/NONE \/ No setup/);
 // No live Screener/market/portfolio lookup is used to populate capture facts.
 assert.equal(requests.filter(r=>new URL(r.url).pathname==='/api/screener').length,0);
 assert.equal(requests.filter(r=>/\/api\/instruments\/[^/]+\/history$/.test(new URL(r.url).pathname)).length,0);
 await click('#captured-row-detail a.stock-link');await until('!!document.querySelector("[aria-label=\\\"Current registry metadata\\\"]")');assert.equal(await evaluate('location.pathname'),'/stocks/'+id(6));
 await until('document.querySelector("[aria-label=\\\"Instrument decision history\\\"]")?.innerText.includes("Open capture")');
 assert.ok(requests.some(r=>new URL(r.url).pathname==='/api/instruments/'+id(6)+'/decision-snapshots'&&new URL(r.url).searchParams.get('limit')==='5'));
 await click('[aria-label="Instrument decision history"] a');await detail();assert.equal(await evaluate('location.pathname'),'/decisions/'+runs.partial);
 await evaluate('history.back()');await until('!!document.querySelector("[aria-label=\\\"Current registry metadata\\\"]")');await evaluate('history.back()');await detail();
 await send('Page.navigate',{url:base+'/decisions/'+runs.blocked});await detail();assert.match(await text(),/BLOCKED/);assert.match(await text(),/Discovery only · no portfolio context/);assert.match(await text(),/No instruments were captured/);assert.equal(await evaluate('document.querySelectorAll("main [role=alert]").length'),0);await noOverflow();await shot('decisions-blocked');
 await send('Page.reload',{ignoreCache:true});await detail();assert.equal(await evaluate('location.pathname'),'/decisions/'+runs.blocked);
 await send('Page.navigate',{url:base+'/decisions/bad'});await until('document.querySelector("main [role=alert]")?.innerText.includes("Invalid decision")');await noOverflow();
 await send('Page.navigate',{url:base+'/decisions/'+id(98)});await until('document.querySelector("main [role=alert]")?.innerText.includes("Decision capture not found")');
 await send('Network.setBlockedURLs',{urls:[base+'/api/screener/decision-snapshots*']});await send('Page.navigate',{url:base+'/decisions'});await until('document.querySelector("main [role=alert]")?.innerText.includes("Decision history unavailable")');
 await send('Network.setBlockedURLs',{urls:[]});await click('[aria-label="Decision history pagination"] button:last-child');await until('document.querySelectorAll(".decision-list-table tbody tr").length===20');
 await send('Page.navigate',{url:base+'/decisions/'+runs.partial});await detail();await send('Emulation.setFocusEmulationEnabled',{enabled:true});await evaluate(`document.querySelector('[data-instrument-id="${id(1)}"] button').focus()`);
 assert.ok(await evaluate('document.activeElement.matches(".decision-rows-table button")'),'Expansion button focused');
 await send('Input.dispatchKeyEvent',{type:'keyDown',key:' ',code:'Space',windowsVirtualKeyCode:32,text:' '});await send('Input.dispatchKeyEvent',{type:'keyUp',key:' ',code:'Space',windowsVirtualKeyCode:32});await until('!!document.querySelector("#captured-row-detail")');
 assert.ok(await evaluate('getComputedStyle(document.activeElement).outlineWidth!=="0px"'),'Visible keyboard focus');
 assert.equal(await evaluate('document.activeElement.getAttribute("aria-expanded")'),'true');
 const key=async(key,code,number,modifiers=0)=>{await send('Input.dispatchKeyEvent',{type:'keyDown',key,code,windowsVirtualKeyCode:number,modifiers});await send('Input.dispatchKeyEvent',{type:'keyUp',key,code,windowsVirtualKeyCode:number,modifiers});};
 await key(' ','Space',32);await until('!document.querySelector("#captured-row-detail")');assert.equal(await evaluate('document.activeElement.getAttribute("aria-expanded")'),'false');
 const focused=await evaluate('document.activeElement.closest("tr").dataset.instrumentId');await key('Tab','Tab',9);assert.notEqual(await evaluate('document.activeElement.closest("tr")?.dataset.instrumentId'),focused,'Tab leaves expansion control');
 await evaluate('document.querySelector("main").focus()');await key('Tab','Tab',9);assert.equal(await evaluate('document.activeElement.getAttribute("href")'),'/decisions','Back link is keyboard reachable');
 await evaluate('document.querySelector(".brand").focus()');
 for(let n=0;n<12&&!(await evaluate('document.activeElement.title==="Decision History"'));n++)await key('Tab','Tab',9);
 assert.equal(await evaluate('document.activeElement.title'),'Decision History','Navigation is keyboard reachable');
 assert.ok(await evaluate('getComputedStyle(document.activeElement).outlineWidth!=="0px"'));await key(' ','Space',32);await until('!!document.querySelector(".decision-list-table")');
 await evaluate('history.back()');await detail();await evaluate('history.forward()');await until('!!document.querySelector(".decision-list-table")');

 assert.equal(requests.filter(r=>r.method!=='GET').length,0,'History browsing must never write');assert.ok(requests.every(r=>r.url.startsWith(base+'/')||r.url.startsWith('data:')),'External request');assert.equal(exceptions.length,0,JSON.stringify(exceptions));
 console.log(JSON.stringify({status:'PASS',widths:[1440,1366,1280,1024,768,390],checks:'default bounded list; opaque cursor/reset; direct/reload/back routes; chronology/hashes; full population; PARTIAL/BLOCKED; WARMUP/unavailable; evaluated NONE/WATCH/CONFIRMED; outside held not evaluated; expansion/mobile/keyboard; stable current Stock Detail and latest-five history; invalid/404/service error; no live lookups for captured facts; GET only; no external requests'},null,2));
}

async function checkRetainedReal({base,send,evaluate,until,nav,size,noOverflow,shot,requests,exceptions},runId) {
 await nav('Decision History');await until(`!!document.querySelector('a[href="/decisions/${runId}"]')`);
 assert.equal(await evaluate('document.querySelectorAll("main [role=alert]").length'),0);
 await send('Page.navigate',{url:base+'/decisions/'+runId});
 await until('document.querySelectorAll(".decision-rows-table tbody tr").length===10');
 await send('Page.reload',{ignoreCache:true});await until('document.querySelectorAll(".decision-rows-table tbody tr").length===10');
 const text=await evaluate('document.querySelector("main").innerText');
 for(const value of ['BLOCKED','2026-10-03','2026-10-02','Discovery only · no portfolio context'])assert.ok(text.includes(value),value);
 assert.equal(await evaluate('document.querySelectorAll("main [role=alert]").length'),0);
 const hashes=await evaluate('Array.from(document.querySelectorAll(".decision-hash")).map(e=>e.textContent)');
 assert.deepEqual(hashes,['6c32994d484472bb08361905e29bf0f86d2a29c558c94ad91bf0d0e7380f9177','38ba96fba61bd32c803d6b9c2cca2e1a713a8611dfbb691496e44c4c457ed377']);
 for(const width of [1440,390]){await size(width);await noOverflow();await shot('decisions-real-'+width);}
 assert.equal(requests.filter(r=>r.method!=='GET').length,0);assert.equal(requests.filter(r=>new URL(r.url).pathname==='/api/screener').length,0);
 assert.ok(requests.every(r=>r.url.startsWith(base+'/')||r.url.startsWith('data:')));assert.equal(exceptions.length,0,JSON.stringify(exceptions));
 console.log(JSON.stringify({status:'PASS',runId,rows:10,captureStatus:'BLOCKED',through:'2026-10-03',targetSession:'2026-10-02',portfolio:null,hashes,widths:[1440,390],writes:0,externalRequests:0}));
}
