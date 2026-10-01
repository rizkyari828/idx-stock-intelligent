import assert from 'node:assert/strict';
import { test } from 'node:test';
import { readDraft, saveDraft, clearDraft, draftKey, type ReconciliationDraft } from './reconciliationDraft.js';
import { loadInstruments, registryCheck, registerMissing } from './instrumentRegistration.js';
import type { Request } from './api.js';
import type { Instrument } from './view.js';
import { parseSnapshotCsv, readSnapshotFile } from './snapshotCsv.js';
import { money, valuationLabel, coverageLabel, canConfirmImport, cutoffIso, difference, type ImportPreview, type Holding, type PortfolioView } from './view.js';
test('missing prices and incomplete totals stay visible; zero cash is a number', () => {
  assert.equal(money(null), 'UNAVAILABLE');
  assert.equal(money(0), '0');
  assert.equal(valuationLabel({ valuation: { availability: 'UNAVAILABLE', unavailableReason: 'NO_CURRENT_MARKET_PRICE' } } as Holding), 'UNAVAILABLE · NO_CURRENT_MARKET_PRICE');
  assert.equal(coverageLabel({ valuationCoverage: 'PARTIAL', pricedHoldings: 1, holdingCount: 2 } as PortfolioView), 'PARTIAL · 1/2 holdings priced');
  assert.equal(valuationLabel({ valuation: { availability: 'AVAILABLE' }, market: { freshness: 'STALE', quality: 'DEGRADED' } } as Holding), 'STALE · DEGRADED');
});

test('only a valid preview enables explicit import confirmation', () => {
  assert.equal(canConfirmImport(null), false);
  const valid = { status: 'READY', canImport: true, invalidRows: 0, errors: [] } as unknown as ImportPreview;
  assert.equal(canConfirmImport(valid), true);
  assert.equal(canConfirmImport({ ...valid, status: 'INVALID', invalidRows: 1 }), false);
  assert.equal(canConfirmImport({ ...valid, status: 'CONFLICT', errors: ['conflict'] }), false);
  assert.equal(canConfirmImport({ ...valid, status: 'ALREADY_PRESENT' }), true);
});

test('WIB cutoff is independent of trade date and nonzero differences stay visible', () => {
 assert.equal(cutoffIso('2026-10-01T09:00:00'),'2026-10-01T02:00:00.000Z');
 assert.equal(difference(null),'—');assert.equal(difference(0),'0');assert.equal(difference(-100),'−100');assert.equal(difference(0.00000001),'+<0.000001');
});

const snapshotHeader = 'source_account,symbol,lots,shares,average_cost,mandate\n';
test('snapshot contract, quoted accounts, lots and unknown average cost', () => {
 const p=parseSnapshotCsv(snapshotHeader+'"Broker, One", bumi ,98,9800,208.37,LONG_SWING\r\nBroker_2,ANTM,10,,,INVEST\n');
 assert.deepEqual(p.errors,[]);assert.equal(p.rowsRead,2);assert.equal(p.validRows,2);assert.equal(p.sourceAccounts,2);
 assert.equal(p.holdings[0].symbol,'BUMI');assert.equal(p.holdings[0].averageCost,'208.37');assert.equal(p.holdings[1].shares,'1000');assert.equal(p.holdings[1].averageCost,null);
 assert.equal(parseSnapshotCsv('symbol,shares,average_cost\nA,1,.5').holdings[0].averageCost,'0.5');
 assert.deepEqual(parseSnapshotCsv('source_account,symbol,shares\n"A""B",A,1').holdings[0].accounts,['A"B']);
 assert.equal(p.holdings[0].instrumentId,null);assert.deepEqual(p.holdings[0].accounts,['Broker, One']);
});
test('snapshot validation reports physical rows and rejects invalid quantities, cost and mandate', () => {
 for(const [row,message] of [['A,BUMI,2,100,1,','mismatch'],['A,BUMI,,1.5,1,','whole'],['A,BUMI,,NaN,1,','whole'],['A,BUMI,10000001,,1,','maximum'],['A,BUMI,,1,-1,','nonnegative'],['A,BUMI,,1,1000000000001,','exceeds'],['A,BUMI,,1,1,UNKNOWN','unknown mandate'],['A,,1,,1,','symbol']]){
  const p=parseSnapshotCsv(snapshotHeader+row);assert.equal(p.validRows,0);assert.match(p.errors[0],new RegExp('Row 2:.*'+message));
 }
 assert.match(parseSnapshotCsv(snapshotHeader+'"A\nB",BUMI,,1,1,\nA,ANTM,,0,1,').errors[0],/Row 4/);
});
test('snapshot duplicates retain rows and metadata with exact weighted decimal average', () => {
 const p=parseSnapshotCsv(snapshotHeader+'A, bumi ,,100,0.1,INVEST\nB,BUMI,,300,0.2,FAST_SWING');
 assert.deepEqual(p.errors,[]);assert.equal(p.duplicateGroups,1);assert.equal(p.holdings.length,1);assert.equal(p.holdings[0].shares,'400');assert.equal(p.holdings[0].averageCost,'0.175');assert.deepEqual(p.holdings[0].rows,[2,3]);assert.deepEqual(p.holdings[0].mandates,['INVEST','FAST_SWING']);
 assert.equal(parseSnapshotCsv('symbol,shares,average_cost\nA,1,1\nA,2,2').holdings[0].averageCost,'1.666666666666666666666666667');
 assert.equal(parseSnapshotCsv('symbol,shares,average_cost\nA,1,1\nA,2,').holdings[0].averageCost,null);
 assert.match(parseSnapshotCsv('symbol,shares\nA,1000000000\nA,1').errors[0],/consolidated/);
});
test('snapshot bounds apply to resulting holdings; malformed CSV and headers fail explicitly', () => {
 assert.match(parseSnapshotCsv('symbol,shares\n'+Array.from({length:201},(_,i)=>`S${i},1`).join('\n')).errors[0],/200/);
 assert.deepEqual(parseSnapshotCsv('symbol,shares\n'+Array.from({length:201},()=>`A,1`).join('\n')).errors,[]);
 for(const text of ['symbol,shares\n"A,1','symbol,shares\n"A"oops,1','symbol,shares\nA"B,1','symbol,shares\nA,1,2','trade_date,type\n2026-01-01,BUY','symbol,symbol,shares\nA,A,1','symbol,cash,shares\nA,1,1','symbol,shares']) assert.ok(parseSnapshotCsv(text).errors.length,text);
 assert.match(parseSnapshotCsv('x'.repeat(1024*1024+1)).errors[0],/1 MiB/);
});
test('snapshot file is UTF-8 only and bounded before reading', async () => {
 await assert.rejects(readSnapshotFile(new File([new Uint8Array([0xff])],'bad.csv')));
 await assert.rejects(readSnapshotFile(new File(['x'.repeat(1024*1024+1)],'big.csv')),/1 MiB/);
 assert.deepEqual((await readSnapshotFile(new File(['\uFEFFsymbol,lots\nA,1'],'local.csv'))).errors,[]);
});

function browserStorage() {
 const entries=new Map<string,string>();
 return {getItem:(key:string)=>entries.get(key)??null,setItem:(key:string,value:string)=>{entries.set(key,value);},removeItem:(key:string)=>{entries.delete(key);}};
}
const draftFixture: ReconciliationDraft = {version:1,portfolioId:'p1',expectedCash:'123.45',through:'2026-09-30',cutoff:'2026-09-30T20:00:00',holdings:[{instrumentId:null,symbol:'BUMI',shares:'9800',averageCost:'208.37',accounts:['Stockbit'],mandates:['LONG_SWING']}]};
test('local draft reload restores inputs only, updates inputs and isolates portfolio keys', () => {
 const storage=browserStorage();assert.equal(saveDraft({...draftFixture,result:{status:'MATCH'},ledgerCash:999,fileContent:'secret CSV'} as ReconciliationDraft,storage),'');
 assert.deepEqual(readDraft('p1',storage),draftFixture);assert.equal(readDraft('p2',storage),null);
 const raw=JSON.parse(storage.getItem(draftKey('p1'))!);assert.equal(raw.result,undefined);assert.equal(raw.ledgerCash,undefined);assert.equal(raw.fileContent,undefined);
 saveDraft({...draftFixture,expectedCash:'50',through:'2026-10-01',cutoff:'',holdings:[{...draftFixture.holdings[0],shares:'100'}]},storage);
 assert.equal(readDraft('p1',storage)?.holdings[0].shares,'100');assert.equal(readDraft('p1',storage)?.expectedCash,'50');assert.equal(readDraft('p1',storage)?.through,'2026-10-01');assert.equal(readDraft('p1',storage)?.cutoff,'');
 saveDraft({...draftFixture,portfolioId:'p2'},storage);storage.setItem('idx-theme','dark');clearDraft('p1',storage);assert.equal(readDraft('p1',storage),null);assert.equal(readDraft('p2',storage)?.portfolioId,'p2');assert.equal(storage.getItem('idx-theme'),'dark');
});
test('malformed/version unsupported drafts and unavailable storage cannot break reconciliation', () => {
 const storage=browserStorage();
 for(const value of ['{','null',JSON.stringify({...draftFixture,version:2}),JSON.stringify({...draftFixture,portfolioId:'other'}),JSON.stringify({...draftFixture,holdings:[null]}),JSON.stringify({...draftFixture,holdings:[{...draftFixture.holdings[0],shares:100}]}),JSON.stringify({...draftFixture,through:'invalid'}),JSON.stringify({...draftFixture,cutoff:'bad'}),JSON.stringify({...draftFixture,holdings:[{...draftFixture.holdings[0],mandates:['UNKNOWN']}]} )]){
  storage.setItem(draftKey('p1'),value);assert.equal(readDraft('p1',storage),null,value);
 }
 const unavailable={getItem:()=>{throw new Error('blocked');},setItem:()=>{throw new Error('quota');},removeItem:()=>{throw new Error('blocked');}};
 assert.equal(readDraft('p1',unavailable),null);assert.match(saveDraft(draftFixture,unavailable),/could not be saved/);assert.match(clearDraft('p1',unavailable),/could not be removed/);
});
const equity=(symbol:string):Instrument=>({id:crypto.randomUUID(),symbol,name:symbol,type:'EQUITY',hasEffectiveSymbol:true});
test('registry detection normalizes symbols and reports non-equity/ambiguous conflicts', () => {
 assert.deepEqual(registryCheck([' a ','A','B','C','D','E'],[equity('A'),{...equity('C'),type:'INDEX'},equity('D'),equity('D'),{...equity('E'),hasEffectiveSymbol:false}]).map(c=>[c.symbol,c.status]),[['A','REGISTERED'],['B','MISSING'],['C','CONFLICT'],['D','CONFLICT'],['E','MISSING']]);
});
test('registration uses existing API only, skips existing identities and refreshes state', async () => {
 let registry=[equity('A')];const calls:{path:string;body?:unknown}[]=[];
 const request:Request=async <T,>(path:string,body?:unknown)=>{calls.push({path,body});if(body){assert.equal(path,'/instruments');const input=body as {id:string;symbol:string;name:string;type:string;validFrom:string};assert.equal(input.name,input.symbol);assert.equal(input.type,'EQUITY');assert.equal(input.validFrom,'2026-09-30');assert.match(input.id,/^[0-9a-f-]{36}$/);registry.push({...equity(input.symbol),id:input.id});return {} as T;}assert.match(path,/^\/instruments\?limit=200&offset=0&through=2026-09-30$/);return registry as T;};
 const inputs=structuredClone(draftFixture);const next=await registerMissing(['A','BUMI'],draftFixture.through,request);
 assert.deepEqual(next.outcomes.map(o=>o.status),['ALREADY_REGISTERED','REGISTERED']);assert.equal(calls.filter(c=>c.body).length,1);assert.equal(next.registry.length,2);assert.deepEqual(draftFixture,inputs);
 assert.ok(calls.every(c=>c.path.startsWith('/instruments')),'No portfolio, thesis, prices, snapshot or import requests');
});
test('registration reports partial conflict/failure, survives races and retries only missing symbols', async () => {
 let registry:Instrument[]=[];const writes:string[]=[];const reported:string[]=[];
 const request:Request=async <T,>(path:string,body?:unknown)=>{
  assert.ok(path.startsWith('/instruments'));
  if(!body)return [...registry] as T;
  const {symbol}=body as {symbol:string};writes.push(symbol);
  if(symbol==='B')throw new Error('Canonical symbol conflict');
  registry.push(equity(symbol));if(symbol==='C')throw new Error('Symbol already has a stable instrument ID');return {} as T;
 };
 const next=await registerMissing(['A','B','C'],'2026-09-30',request,o=>reported.push(o.symbol));
 assert.deepEqual(next.outcomes.map(o=>o.status),['REGISTERED','FAILED','ALREADY_REGISTERED']);assert.match(next.outcomes[1].message,/conflict/);assert.deepEqual(reported,['A','B','C']);
 const remaining=registryCheck(['A','B','C'],next.registry).filter(c=>c.status==='MISSING').map(c=>c.symbol);assert.deepEqual(remaining,['B']);
 await registerMissing(remaining,'2026-09-30',request);assert.deepEqual(writes,['A','B','C','B']);
});
test('registry pagination remains bounded and failed final refresh retains per-symbol outcomes', async () => {
 const paths:string[]=[];const request:Request=async <T,>(path:string)=>{paths.push(path);return (paths.length===1?Array.from({length:200},(_,i)=>equity('S'+i)):[equity('LAST')]) as T;};
 assert.equal((await loadInstruments('2026-09-30',undefined,request)).length,201);assert.match(paths[1],/offset=200/);
 let reads=0;const outcomes:string[]=[];const failsRefresh:Request=async <T,>(_path:string,body?:unknown)=>{if(body)return {} as T;if(++reads===1)return [] as T;throw new Error('Refresh offline');};
 await assert.rejects(registerMissing(['A'],'2026-09-30',failsRefresh,o=>outcomes.push(o.status)),/Refresh offline/);assert.deepEqual(outcomes,['REGISTERED']);
});
