// Browser acceptance with Chrome's native DevTools protocol; Node 22+, no packages.
// Start the static server and isolated Chrome as described in README.md first.
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
const base = 'http://127.0.0.1:5081';
const output = process.argv[2] || '/tmp/idx-design-previews';
const pages = await (await fetch('http://127.0.0.1:9224/json')).json();
const socket = new WebSocket(pages.find(p => p.type === 'page').webSocketDebuggerUrl);
await new Promise((resolve,reject) => { socket.onopen=resolve;socket.onerror=reject; });
let sequence=0;const pending=new Map(), exceptions=[], requests=[];
socket.onmessage=event=>{
 const message=JSON.parse(event.data);
 if(message.method==='Runtime.exceptionThrown')exceptions.push(message.params.exceptionDetails);
 if(message.method==='Network.requestWillBeSent')requests.push(message.params.request.url);
 if(message.id){const callback=pending.get(message.id);pending.delete(message.id);message.error?callback.reject(message.error):callback.resolve(message.result);}
};
function send(method,params={}){return new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});socket.send(JSON.stringify({id,method,params}));});}
async function evaluate(expression){const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw new Error(JSON.stringify(r.exceptionDetails));return r.result.value;}
const click=selector=>evaluate(`document.querySelector(${JSON.stringify(selector)}).click()`);
const demo=async(kind,value)=>{await click('[data-action="design-system"]');await click(`[data-demo-${kind}="${value}"]`);};
const text=()=>evaluate('document.querySelector("main").innerText');
const set=async(selector,value)=>evaluate(`(()=>{const e=document.querySelector(${JSON.stringify(selector)});e.value=${JSON.stringify(value)};e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));})()`);
async function size(width,height){await send('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile:false});}
async function shot(name){await evaluate('document.fonts.ready');await evaluate('document.querySelector("#toast").hidden=true');const r=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:true});await writeFile(`${output}/${name}.png`,Buffer.from(r.data,'base64'));}
async function noOverflow(){const dimensions=await evaluate('({page:state.page,width:innerWidth,scroll:document.documentElement.scrollWidth})');assert.ok(dimensions.scroll<=dimensions.width+1,JSON.stringify(dimensions));}
try {
 await mkdir(output,{recursive:true});await send('Page.enable');await send('Runtime.enable');await send('Network.enable');await send('Network.setCacheDisabled',{cacheDisabled:true});
 await size(1440,1120);const marker='?check='+Date.now();await send('Page.navigate',{url:base+'/'+marker});
 for(let i=0;i<100;i++){if(await evaluate(`location.search===${JSON.stringify(marker)} && document.readyState==='complete' && !!document.querySelector('.reconcile-table')`))break;await new Promise(r=>setTimeout(r,50));}
 assert.match(await text(),/Portfolio reconciliation/);assert.equal(await evaluate('document.querySelectorAll(".reconcile-table tbody tr").length'),8);await noOverflow();await shot('reconcile-desktop');
 await click('[data-result-filter="review"]');assert.equal(await evaluate('document.querySelectorAll(".reconcile-table tbody tr").length'),4);
 await demo('reconcile','match');assert.match(await text(),/Your snapshot matches the ledger/);assert.equal(await evaluate('document.querySelectorAll(".reconcile-table tbody tr").length'),6);await shot('reconcile-match');
 await click('[data-action="toggle-snapshot"]');await set('#expected-cash','8750001');assert.match(await text(),/Snapshot changed/);await evaluate('document.querySelector("#snapshot-form").requestSubmit()');assert.match(await text(),/cash balance differ/);
 await set('#cutoff','2026-09-29T08:00');await evaluate('document.querySelector("#snapshot-form").requestSubmit()');assert.match(await text(),/contains one historical fixture/);
 await click('[data-action="reset-snapshot"]');await click('[data-action="toggle-snapshot"]');await evaluate('document.querySelector("#expected-details").open=true');await click('[data-action="add-holding"]');
 await set('[data-row="7"][data-field="symbol"]','BBCA');await set('[data-row="7"][data-field="shares"]','100');await set('[data-row="7"][data-field="avg"]','100');await evaluate('document.querySelector("#snapshot-form").requestSubmit()');assert.match(await text(),/entered twice/);
 await click('[data-action="remove-holding"][data-row="7"]');await evaluate('document.querySelector("#snapshot-form").requestSubmit()');assert.match(await text(),/A few differences/);
 await shot('reconcile-editor');
 for(const scenario of ['empty','loading','error']){await demo('reconcile',scenario);assert.equal(await evaluate('document.querySelectorAll(".reconcile-table").length'),0);await shot(`reconcile-${scenario}`);}
 await click('[data-action="retry-compare"]');assert.match(await text(),/A few differences/);
 await click('[data-page="portfolio"]');assert.match(await text(),/Portfolio value\nUnavailable/);assert.match(await text(),/ASII has no market price/);assert.match(await text(),/37,255,000/);await noOverflow();await shot('portfolio');
 await click('[data-action="holding"][data-symbol="ASII"]');assert.match(await evaluate('document.querySelector("dialog").innerText'),/No current market price/);await click('[data-action="close-dialog"]');
 await click('[data-page="transactions"]');await shot('transactions');await click('[data-action="correct"][data-event="evt-007"]');assert.ok(await evaluate('document.querySelector("dialog").open'));assert.match(await evaluate('document.querySelector("dialog").innerText'),/Original event ID · immutable/);
 assert.equal(await evaluate('document.querySelectorAll("dialog input[name=knownAt],dialog input[name=eventId]").length'),0);await shot('correct-transaction');await set('dialog [name="price"]','3460');await evaluate('document.querySelector("#correction-form").requestSubmit()');assert.match(await text(),/3,460/);assert.equal(await evaluate('document.querySelectorAll(".transaction-table tbody tr").length'),9);
 await click('[data-page="thesis"]');await shot('thesis');await click('[data-action="invalidate-thesis"]');await set('dialog [name="invalidation"]','Synthetic review condition was met.');await evaluate('document.querySelector("#thesis-form").requestSubmit()');assert.match(await text(),/Inactive thesis/);assert.match(await text(),/v3/);assert.match(await text(),/v1/);await shot('thesis-history');
 await click('[data-thesis-symbol="ASII"]');assert.match(await text(),/No thesis recorded/);await click('[data-action="new-thesis"]');await set('dialog [name="text"]','Synthetic owner-authored thesis.');await set('dialog [name="invalidation"]','Synthetic condition.');await evaluate('document.querySelector("#thesis-form").requestSubmit()');assert.match(await text(),/Synthetic owner-authored thesis/);
 await click('[data-page="import"]');await shot('import-select');assert.ok(await evaluate('document.querySelector("[data-action=preview-import]").disabled'));await click('[data-action="sample-file"]');await click('[data-action="preview-import"]');assert.match(await text(),/13 LOTS → 1,300 SHARES/);await shot('import-preview');
 await demo('import','csv-invalid');assert.ok(await evaluate('document.querySelector("[data-action=confirm-import]").disabled'));await shot('import-invalid');await demo('import','csv-ready');await click('[data-action="confirm-import"]');assert.match(await text(),/Nothing was written/);
 await click('[data-action="restart-import"]');await click('[data-import-format="JSON"]');await set('#restore-mode','RESTORE_EXISTING_EMPTY');await click('[data-action="sample-file"]');await click('[data-action="preview-import"]');assert.match(await text(),/original empty portfolio/);assert.doesNotMatch(await text(),/13 LOTS/);await shot('json-restore');
 await demo('import','json-invalid');assert.ok(await evaluate('document.querySelector("[data-action=confirm-import]").disabled'));
 await demo('import','csv-ready');await evaluate('document.querySelector("#portfolio-select").dispatchEvent(new Event("change",{bubbles:true}))');assert.equal(await evaluate('state.importStep'),1);assert.equal(await evaluate('document.querySelectorAll("[data-action=confirm-import]").length'),0);
 await demo('import','csv-ready');await click('[data-action="back-import"]');await click('[data-import-format="JSON"]');assert.equal(await evaluate('state.importStep'),1);
 await click('[data-action="sample-file"]');await click('[data-action="preview-import"]');await click('[data-action="back-import"]');await set('#restore-mode','CREATE_NEW');assert.equal(await evaluate('state.importStep'),1);
 await evaluate('chooseFile(new File(["synthetic"],"example.json"))');assert.equal(await evaluate('state.importStep'),1);await click('[data-action="preview-import"]');assert.ok(!await evaluate('document.querySelector("[data-action=confirm-import]").disabled'));
 // All surfaces are usable at laptop, tablet and narrow widths. Tables own their overflow.
 for(const width of [1366,1280,1024,768,390]){
  await size(width,1000);
  for(const page of ['reconcile','portfolio','transactions','thesis','import','system']){
   await evaluate(`navigate('${page}')`);await noOverflow();
   if(page==='reconcile'){await shot(`reconcile-${width}`);if(width===768||width===390){assert.ok(await evaluate('(()=>{const e=document.querySelector(".reconcile-table").parentElement;e.scrollLeft=e.scrollWidth;return e.scrollLeft>0;})()'));await shot(`reconcile-scrolled-${width}`);}}
   if(page==='transactions'&&width===390){await click('[data-action="correct"][data-event="evt-demo-9"]');const bounds=await evaluate('(()=>{const r=document.querySelector("dialog").getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom};})()');assert.ok(bounds.left>=0&&bounds.right<=width&&bounds.top>=0&&bounds.bottom<=1000);await shot('correction-390');await click('[data-action="close-dialog"]');}
  }
 }
 await size(1440,1120);await evaluate('resetSnapshot();navigate("reconcile")');await click('[data-action="theme"]');await shot('reconcile-light');await noOverflow();await click('[data-action="theme"]');
 const contrast=await evaluate(`(()=>{const style=getComputedStyle(document.documentElement);const rgb=name=>style.getPropertyValue(name).trim().match(/[a-f\\d]{2}/gi).map(n=>parseInt(n,16)/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4);const lum=name=>rgb(name).reduce((s,v,i)=>s+v*[.2126,.7152,.0722][i],0);return ['--text','--text-secondary','--text-muted','--positive','--negative','--warning','--missing','--info'].map(name=>({name,ratio:(lum(name)+.05)/(lum('--surface')+.05)}));})()`);
 for(const color of contrast)assert.ok(color.ratio>=4.5,`${color.name} contrast ${color.ratio}`);
 await click('[data-action="quality"]');
 for(let i=0;i<8;i++){await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});assert.ok(await evaluate('document.querySelector("dialog").contains(document.activeElement)'));}
 await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});assert.ok(!await evaluate('document.querySelector("dialog").open'));
 const standalone=new URL('./index.html',import.meta.url).href;await send('Page.navigate',{url:standalone});for(let i=0;i<100;i++){if(await evaluate(`location.protocol==='file:' && document.readyState==='complete' && !!document.querySelector('.reconcile-table')`))break;await new Promise(r=>setTimeout(r,50));}assert.match(await text(),/Portfolio reconciliation/);
 assert.equal(exceptions.length,0,JSON.stringify(exceptions));assert.ok(requests.every(url=>url.startsWith(base+'/')||url.startsWith('data:')||url.startsWith(new URL('./',import.meta.url).href)),'Unexpected external request');assert.ok(!requests.some(url=>url.includes('/api/')),'Prototype must never call an API');
 console.log(JSON.stringify({status:'PASS',checks:'reconciliation states, input validation, immutable corrections, thesis versions, import confirmation, JSON restore, viewport overflow, focus containment, contrast, zero backend calls',widths:[1440,1366,1280,1024,768,390],screenshots:output,minimumDarkTextContrast:Math.min(...contrast.map(c=>c.ratio)).toFixed(2)},null,2));
} finally { socket.close(); }
