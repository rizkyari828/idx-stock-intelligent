import assert from 'node:assert/strict';
export async function checkOutcomes({base,send,evaluate,until,click,nav,size,noOverflow,shot,requests,exceptions}) {
 const runs=JSON.parse(process.env.IDX_TEST_OUTCOME_RUNS),writes=()=>requests.filter(r=>r.method==='POST'&&r.url.endsWith('/outcomes/evaluate'));
 const grid=()=>until('!!document.querySelector(".outcome-matrix") && document.querySelector("[aria-label=Outcomes]")?.getAttribute("aria-busy")==="false"');
 const text=()=>evaluate('document.querySelector("[aria-label=Outcomes]").innerText');
 const open=async rid=>{await send('Page.navigate',{url:base+'/decisions/'+rid});await grid();};
 const captured=()=>evaluate('JSON.stringify({rows:document.querySelector(".decision-rows-table").innerText,context:document.querySelector("[aria-label=\\\"Immutable capture context\\\"]").innerText,hashes:document.querySelector("[aria-label=\\\"Captured evidence linkage\\\"]").innerText})');
 const key=async(key,code,n)=>{await send('Input.dispatchKeyEvent',{type:'keyDown',key,code,windowsVirtualKeyCode:n,...(key==='Enter'?{text:'\r'}:{})});await send('Input.dispatchKeyEvent',{type:'keyUp',key,code,windowsVirtualKeyCode:n});};
 await nav('Decision History');await until('!!document.querySelector(".decision-list-table")');
 await click(`a[href="/decisions/${runs.real??runs.unresolved}"]`);await grid();
 assert.equal(writes().length,0,'Opening detail never evaluates');
 const expected=runs.real?10:2;
 assert.equal(await evaluate('document.querySelectorAll(".outcome-matrix tbody tr").length'),expected);
 assert.equal(await evaluate('document.querySelectorAll(".outcome-matrix details").length'),expected*4);
 assert.equal(await evaluate('Array.from(document.querySelectorAll(".outcome-state")).filter(e=>e.textContent==="UNRESOLVED").length'),expected*4);
 assert.match(await text(),/HORIZON_NOT_REACHED/);assert.doesNotMatch(await text(),/Price return 0\.00%/);
 const before=await captured();
 await click('[aria-label="Manual outcome evaluation"] button');await grid();await until('document.querySelector("[aria-label=Outcomes]").innerText.includes("No outcomes resolved")');
 assert.equal(writes().length,1);assert.deepEqual(JSON.parse(writes()[0].postData),{horizonSessions:1});assert.equal(await captured(),before);
 assert.equal(await evaluate('document.querySelectorAll("main [role=alert]").length'),0);
 if(!runs.real){
  await open(runs.positive);assert.equal(writes().length,1);
  for(const token of ['AVAILABLE','ANCHOR_UNAVAILABLE','DATA_UNAVAILABLE','BASIS_UNCERTAIN','Price return 10.00%','Preview · not recorded','Committed'])assert.ok((await text()).includes(token),token);
  const original=await captured();await evaluate('document.querySelectorAll("[aria-label=\\\"Manual outcome evaluation\\\"] button")[1].focus()');await key(' ','Space',32);
  await grid();await until('document.querySelector("[aria-label=Outcomes]").innerText.includes("2 new terminal")');
  assert.equal(await captured(),original);assert.equal(writes().length,2);assert.deepEqual(JSON.parse(writes()[1].postData),{horizonSessions:5});
  for(const [rid,label] of [[runs.negative,'Price return -10.00%'],[runs.zero,'Price return 0.00%']]){await open(rid);assert.ok((await text()).includes(label));}
  await open(runs.positive);
 }
 const selector='.outcome-matrix tbody tr:nth-child(2) td:first-of-type summary';
 await send('Emulation.setFocusEmulationEnabled',{enabled:true});await evaluate(`document.querySelector(${JSON.stringify(selector)}).focus()`);await key('Enter','Enter',13);
 await until('document.activeElement?.tagName==="SUMMARY" && document.activeElement.parentElement.open');
 assert.ok(await evaluate('getComputedStyle(document.activeElement).outlineWidth!=="0px"'));
 for(const token of ['Outcome policy','Anchor market date','Anchor close','Horizon market date','Outcome known at','Outcome recorded at','Captured at'])assert.ok((await text()).includes(token),token);
 for(const width of [1440,1366,1280,1024,768,390]){await size(width);await noOverflow();assert.equal(await evaluate('document.querySelectorAll(".outcome-matrix thead th").length'),5);await shot('outcomes-'+(runs.real?'real-':'mixed-')+width);}
 await evaluate(`document.querySelector(${JSON.stringify(selector)}).focus()`);await key('Enter','Enter',13);await until('document.activeElement?.tagName==="SUMMARY" && !document.activeElement.parentElement.open');
 await size(1440);await click('[aria-label=Outcomes] a[href="#captured-population"]');assert.equal(await evaluate('location.hash'),'#captured-population');
 await send('Network.setBlockedURLs',{urls:[base+'/api/screener/decision-snapshots/*/outcomes']});
 await click('[aria-label="Manual outcome evaluation"] button:last-child');await until('!!document.querySelector("[aria-label=Outcomes] [role=alert]")');
 assert.ok(await evaluate('!!document.querySelector(".decision-rows-table")'),'Capture remains available on outcome service failure');
 await send('Network.setBlockedURLs',{urls:[]});await click('[aria-label="Manual outcome evaluation"] button:last-child');await grid();
 await click('a[href="/decisions"]');await until('!!document.querySelector(".decision-list-table")');await evaluate('history.back()');await grid();
 assert.equal(writes().length,runs.real?1:2,'Browsing, reads and back do not evaluate');
 assert.ok(requests.filter(r=>r.method!=='GET').every(r=>r.method==='POST'&&r.url.endsWith('/outcomes/evaluate')));
 assert.equal(requests.filter(r=>new URL(r.url).pathname==='/api/screener').length,0);
 assert.ok(requests.every(r=>r.url.startsWith(base+'/')||r.url.startsWith('data:')));assert.equal(exceptions.length,0,JSON.stringify(exceptions));
 console.log(JSON.stringify({status:'PASS',real:!!runs.real,capturedRows:expected,horizons:4,widths:[1440,1366,1280,1024,768,390],explicitEvaluations:writes().length,externalRequests:0,checks:'full matrix; unresolved vs terminal; preview vs committed; explicit evaluation and refresh; unchanged capture; exact zero/positive/negative display; separate clocks; keyboard details/focus/return; responsive; API failure/retry; back navigation; no automatic evaluation'},null,2));
}
