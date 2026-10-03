import assert from 'node:assert/strict';

export async function checkVerification({base,send,evaluate,until,click,size,noOverflow,shot,requests,exceptions}) {
 const runs=JSON.parse(process.env.IDX_TEST_VERIFICATION_RUNS);
 const panel='[aria-label="Audit / Verification"]';
 const waitDetail=()=>until('!!document.querySelector("[aria-label=\\\"Captured evidence linkage\\\"]")');
 const captured=()=>evaluate('JSON.stringify({table:document.querySelector(".decision-rows-table")?.innerText,context:document.querySelector("[aria-label=\\\"Immutable capture context\\\"]").innerText,hashes:document.querySelector("[aria-label=\\\"Captured evidence linkage\\\"]").innerText})');
 const verifyCalls=()=>requests.filter(r=>r.method==='POST'&&new URL(r.url).pathname.endsWith('/verify'));
 const states=runs.real?[[runs.real,runs.state]]:[[runs.match,'MATCH'],[runs.blocked,'MATCH'],[runs.unavailable,'INPUT_NOT_AVAILABLE'],[runs.policy,'POLICY_VERSION_UNAVAILABLE'],[runs.different,'DIFFERENT_RESULT']];
 for(const [id,state] of states){
  const before=verifyCalls().length;
  await send('Page.navigate',{url:base+'/decisions/'+id});await waitDetail();
  assert.equal(verifyCalls().length,before,'No automatic verification on load');
  assert.equal(await evaluate(`document.querySelector('${panel}').querySelector('[role=status]')===null`),true);
  const immutable=await captured();
  // Make progress observable while preserving the real response and evidence.
  await send('Network.emulateNetworkConditions',{offline:false,latency:200,downloadThroughput:-1,uploadThroughput:-1});
  await click(panel+' button');await until(`document.querySelector('${panel} button').disabled`);
  await until(`document.querySelector('${panel} [role=status]')?.innerText==='${state}'`);
  await send('Network.emulateNetworkConditions',{offline:false,latency:0,downloadThroughput:-1,uploadThroughput:-1});
  assert.equal(verifyCalls().length,before+1);assert.deepEqual(JSON.parse(verifyCalls().at(-1).postData),{});
  assert.equal(await captured(),immutable,'Verification must not replace captured values');
  assert.equal(await evaluate(`document.querySelectorAll('${panel} [role=alert]').length`),0,'Unavailable/different are successful outcomes');
  const text=await evaluate(`document.querySelector('${panel}').innerText`);
  assert.match(text,/Verified at/);assert.match(text,/Captured at/);assert.match(text,/original capture chronology/);
  if(state==='DIFFERENT_RESULT'){assert.match(text,/immutable captured result remains unchanged/);assert.match(text,/rs20Pp/);}
  for(const width of [1440,1366,1280,1024,768,390]){await size(width);await noOverflow();await shot('verification-'+state+'-'+width);}
  await size(1440);
 }
 const id=states[0][0];await send('Page.navigate',{url:base+'/decisions/'+id});await waitDetail();
 const immutable=await captured();
 await send('Network.setBlockedURLs',{urls:[base+'/api/screener/decision-snapshots/*/verify']});
 await click(panel+' button');await until(`!!document.querySelector('${panel} [role=alert]')`);
 assert.equal(await captured(),immutable);await send('Network.setBlockedURLs',{urls:[]});
 await send('Emulation.setFocusEmulationEnabled',{enabled:true});await evaluate(`document.querySelector('${panel} button').focus()`);
 assert.ok(await evaluate('getComputedStyle(document.activeElement).outlineWidth!=="0px"'));
 await send('Input.dispatchKeyEvent',{type:'keyDown',key:' ',code:'Space',windowsVirtualKeyCode:32,text:' '});
 await send('Input.dispatchKeyEvent',{type:'keyUp',key:' ',code:'Space',windowsVirtualKeyCode:32});
 await until(`document.querySelector('${panel} [role=status]')?.innerText==='${states[0][1]}'`);
 await send('Page.reload',{ignoreCache:true});await waitDetail();
 assert.equal(await evaluate(`document.querySelector('${panel} [role=status]')===null`),true,'Verification is on demand, not persisted');
 assert.ok(requests.filter(r=>r.method!=='GET').every(r=>r.method==='POST'&&/^\/api\/screener\/decision-snapshots\/[a-f0-9-]+\/verify$/.test(new URL(r.url).pathname)),'Only explicit compute-only verification POSTs');
 assert.ok(requests.every(r=>r.url.startsWith(base+'/')||r.url.startsWith('data:')));assert.equal(exceptions.length,0,JSON.stringify(exceptions));
 console.log(JSON.stringify({status:'PASS',states:states.map(s=>s[1]),widths:[1440,1366,1280,1024,768,390],verificationRequests:verifyCalls().length,checks:'explicit action; observable loading; four outcomes; separate chronology/hashes; bounded diagnostics; immutable captured table; HTTP error/retry; keyboard/focus; reload clears on-demand result; no capture/provider/external requests'}));
}
