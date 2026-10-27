import assert from 'node:assert/strict';
import { test } from 'node:test';
import { renderToStaticMarkup } from 'react-dom/server';
import { type Request, ApiError } from './api.js';
import { Navigation } from './ui.js';
import { PriceChart, StockLink, StockResults } from './StocksPage.js';
import { StockSession, emptyStockState, type HistoryRow, type StockHistory, type StockResult, type StockState, type TechnicalFeaturesReport, type PersonalTechnicalFeaturesReport } from './stocks.js';
import { type PersonalBars } from './personal.js';
import { type ScreenerResponse } from './screener.js';
const id='10000000-0000-4000-8000-000000000001';
const history:StockHistory={registry:{id,symbol:'SYN',name:'Synthetic',type:'UNKNOWN',currency:'UNKNOWN'},registryAsOf:'2026-10-03',through:'2026-09-30',cutoff:'2026-10-01T12:00:00Z',limit:60,order:'MARKET_DATE_ASCENDING',rows:[]};
const featuresReport=(overrides:Partial<TechnicalFeaturesReport>={}):TechnicalFeaturesReport=>({instrumentId:id,symbol:'SYN',name:'Synthetic',through:'2026-09-30',cutoff:'2026-10-01T12:00:00Z',marketDate:'2026-09-30',sessions:3,index:false,features:{changePercent:{availability:'AVAILABLE',value:1.25,reason:null,minimumSessions:2},ema50:{availability:'WARMUP',value:null,reason:'INSUFFICIENT_SESSIONS',minimumSessions:50}},...overrides});
const result=():StockResult=>({history:structuredClone(history),screener:null,screenerError:'',features:null,featuresError:'',personalFeatures:null,personalFeaturesError:'',portfolio:null,portfolioError:''});
test('Stocks navigation enabled and stable ID link is a real anchor',()=>{assert.doesNotMatch(renderToStaticMarkup(<Navigation page="stocks" onNavigate={()=>{}}/>),/Stocks · reserved/);assert.match(renderToStaticMarkup(<StockLink id={id}>SYN</StockLink>),new RegExp('href="/stocks/'+id+'"'));});
test('missing price, current metadata and outside scope remain honest',()=>{const html=renderToStaticMarkup(<StockResults result={result()} portfolioSelected={false}/>);for(const text of ['Current registry metadata','not fully knowledge-dated','Currency UNKNOWN','No visible canonical price','Insufficient usable history','Not evaluated by current Screener universe','No portfolio selected','Technical features were not read'])assert.ok(html.includes(text),text);assert.doesNotMatch(html,/NONE|0\.00/);});
test('available zero, WARMUP and DATA_BLOCKED preserve backend semantics',()=>{const r=result();r.screener={marketContext:{instrumentId:'other'},universeSnapshotId:'known',rows:[{instrumentId:id,eligibility:'DATA_BLOCKED',eligibilityReasons:['REFERENCE_MISSING'],setup:'NONE',setupEvaluated:false,setupReasons:[],trend:'UNKNOWN',ema20:0,ema50:null,fieldStates:{},provenance:{referenceSnapshotIds:[],priceBasis:'UNKNOWN'}}]} as unknown as ScreenerResponse;r.features=featuresReport({features:{changePercent:{availability:'AVAILABLE',value:0,reason:null,minimumSessions:2},ema50:{availability:'WARMUP',value:null,reason:'INSUFFICIENT_SESSIONS',minimumSessions:50},volumeRatio20:{availability:'UNAVAILABLE',value:null,reason:'VOLUME_BASIS_UNVERIFIED',minimumSessions:21}}});const html=renderToStaticMarkup(<StockResults result={r} portfolioSelected={true}/>);for(const text of ['DATA_BLOCKED','Not evaluated','WARMUP','warmup — needs 50 sessions','VOLUME_BASIS_UNVERIFIED','0.00','AVAILABLE','Not held in selected portfolio'])assert.ok(html.includes(text),text);assert.doesNotMatch(html,/NONE \/ No setup/);});
test('fewer than two usable observations never creates a chart; isolated observations remain visible',()=>{for(const rows of [[],[{date:'2026-09-30',close:1}]])assert.doesNotMatch(renderToStaticMarkup(<PriceChart rows={rows}/>),/<svg/);const html=renderToStaticMarkup(<PriceChart rows={[{date:'2026-09-28',close:1},{date:'2026-09-29',close:null},{date:'2026-09-30',close:2}]}/>);assert.equal((html.match(/<circle/g)??[]).length,2);});
function harness(){const calls:{path:string;signal?:AbortSignal;resolve:(v:unknown)=>void;reject:(e:unknown)=>void}[]=[];let state:StockState=emptyStockState;const request:Request=<T,>(path:string,body?:unknown,signal?:AbortSignal)=>{assert.equal(body,undefined);return new Promise<T>((resolve,reject)=>calls.push({path,signal,resolve:v=>resolve(v as T),reject}));};return {calls,at:(fragment:string)=>calls.filter(c=>c.path.includes(fragment)),session:new StockSession(s=>{state=s;},request),state:()=>state};}
test('resolved history cutoff pins Screener, technical-feature and portfolio reads',async()=>{const h=harness(),pending=h.session.load(id,{through:history.through,cutoff:'',portfolioId:'selected'});assert.equal(new URL(h.calls[0].path,'http://test').searchParams.has('cutoff'),false);h.calls[0].resolve(history);await Promise.resolve();assert.deepEqual(h.calls.map(c=>new URL(c.path,'http://test').pathname),['/instruments/'+id+'/history','/screener','/portfolios/selected','/instruments/'+id+'/technical-features']);for(const c of h.calls.slice(1)){const q=new URL(c.path,'http://test').searchParams;assert.equal(q.get('cutoff'),history.cutoff);assert.equal(q.get('through'),history.through);}h.at('/screener?')[0].resolve({rows:[]});h.at('/technical-features')[0].resolve(featuresReport());h.at('/portfolios/')[0].resolve({holdings:[]});await pending;assert.equal(h.state().result?.history.cutoff,history.cutoff);assert.equal(h.state().result?.features?.marketDate,'2026-09-30');});
test('draft edit aborts and late history cannot publish or start downstream reads',async()=>{const h=harness(),p=h.session.load(id,{through:history.through,cutoff:''});h.session.invalidate();assert.equal(h.calls[0].signal?.aborted,true);h.calls[0].resolve(history);await p;assert.deepEqual(h.state(),emptyStockState);assert.equal(h.calls.length,1);});
test('late downstream completion cannot overwrite a newer context',async()=>{const h=harness(),p=h.session.load(id,{through:history.through,cutoff:''});h.calls[0].resolve(history);await Promise.resolve();const newer=h.session.load(id,{through:'2026-09-29',cutoff:''});h.at('/history?').at(-1)!.resolve({...history,through:'2026-09-29'});await Promise.resolve();h.at('/screener?').at(-1)!.resolve({rows:[]});h.at('/technical-features').at(-1)!.resolve(featuresReport({marketDate:'2026-09-29'}));await newer;h.at('/screener?')[0].resolve({rows:[]});h.at('/technical-features')[0].resolve(featuresReport());await p;assert.equal(h.state().result?.history.through,'2026-09-29');});
test('invalid UUID does not request and unknown UUID reports not found',async()=>{const h=harness();await h.session.load('bad',{through:history.through,cutoff:''});assert.equal(h.calls.length,0);assert.match(h.state().error,/Invalid/);const p=h.session.load(id,{through:history.through,cutoff:''});h.calls[0].reject(new ApiError('not found',404));await p;assert.match(h.state().error,/Instrument not found/);});
test('Screener service failure leaves retained history and technical features available',async()=>{const h=harness(),p=h.session.load(id,{through:history.through,cutoff:''});h.calls[0].resolve(history);await Promise.resolve();h.at('/screener?')[0].reject(new ApiError('Unavailable',503));h.at('/technical-features')[0].resolve(featuresReport());await p;assert.equal(h.state().result?.history,history);assert.equal(h.state().result?.screener,null);assert.equal(h.state().result?.features?.sessions,3);assert.equal(h.state().error,'');});

test('benchmark reference provenance is visible without inventing a stock setup',()=>{const r=result();r.screener={universeSnapshotId:'known',rows:[],marketContext:{instrumentId:id,trend:'UNKNOWN',volatility:'UNKNOWN',fieldStates:{},provenance:{referenceSnapshotIds:['index-reference'],priceBasis:'RAW_AS_TRADED'}}} as unknown as ScreenerResponse;const html=renderToStaticMarkup(<StockResults result={r} portfolioSelected={false}/>);assert.match(html,/Retained knowledge-dated evidence/);assert.match(html,/index-reference/);assert.doesNotMatch(html,/NONE \/ No setup/);});

test('a warmup feature is shown as warmup with its window, never as a blank or zero',()=>{const r=result();r.features=featuresReport({sessions:3,features:{changePercent:{availability:'AVAILABLE',value:0,reason:null,minimumSessions:2},ema50:{availability:'WARMUP',value:null,reason:'INSUFFICIENT_SESSIONS',minimumSessions:50},volumeRatio20:{availability:'UNAVAILABLE',value:null,reason:'VOLUME_BASIS_UNVERIFIED',minimumSessions:21}}});const html=renderToStaticMarkup(<StockResults result={r} portfolioSelected={false}/>);for(const text of ['warmup — needs 50 sessions; 3 continuous so far','WARMUP','VOLUME_BASIS_UNVERIFIED','As of 2026-09-30','Warmup is correct, not broken'])assert.ok(html.includes(text),text);const cell=(key:string)=>new RegExp(`<div class="metric" data-feature="${key}">(.*?)</div>`,'s').exec(html)?.[1]??'';assert.match(cell('ema50'),/<strong>Unavailable<\/strong>/);assert.doesNotMatch(cell('ema50'),/<strong>[0-9]/);assert.doesNotMatch(cell('volumeRatio20'),/<strong>[0-9]/);assert.match(cell('changePercent'),/<strong>0\.00<\/strong>/);});
test('an index instrument reports share-volume features as not applicable, not zero',()=>{const r=result();r.features=featuresReport({index:true,features:{ema20:{availability:'WARMUP',value:null,reason:'INSUFFICIENT_SESSIONS',minimumSessions:20},volumeRatio20:{availability:'UNAVAILABLE',value:null,reason:'NOT_APPLICABLE_TO_INDEX',minimumSessions:21}}});const html=renderToStaticMarkup(<StockResults result={r} portfolioSelected={false}/>);assert.match(html,/Index instrument · share-volume and stock-relative features are not applicable/);assert.match(html,/NOT_APPLICABLE_TO_INDEX/);assert.ok(html.includes('warmup — needs 20 sessions; 3 continuous so far'));});
const personalBars:PersonalBars={source:'PERSONAL_FILE_STORE',generatedAt:'2026-10-27T02:00:00Z',sessions:[{sessionDate:'2026-10-26',sessionProof:'UNPROVEN_PERSONAL',capturedAt:'2026-10-26T11:00:00+00:00',rows:[{symbol:'SYN',open:'100.25',high:'101.5',low:'99.75',close:'100.50',adjustedClose:'100.50',volume:1234567,qualityStatus:'UNPROVEN_PERSONAL'}]}]};
const canonicalRow=(date:string):HistoryRow=>({date,open:110,high:112,low:109,close:111.25,volume:900,availability:'AVAILABLE',reason:null,evidence:{revisionNumber:1,knownAt:'2026-10-26T11:00:00Z',retrievedAt:null,sourceId:'synthetic',canonicalQuality:'VALID',volumeUnit:'SHARES',volumeBasis:'RAW_AS_TRADED',marketSegment:'MAIN',contentHash:'hash',sessionReference:'synthetic-session',sessionKnownAt:'2026-10-26T11:00:00Z',open:'110',high:'112',low:'109',close:'111.25'}});
const withCanonical=(dates:string[],cutoff='2026-10-27T00:00:00Z'):StockResult=>{const r=result();r.history={...structuredClone(history),cutoff,rows:dates.map(canonicalRow)};return r;};

test('a personal-only instrument shows its price with the PERSONAL badge and session date',()=>{
 const html=renderToStaticMarkup(<StockResults result={withCanonical([])} portfolioSelected={false} personal={personalBars}/>);
 for(const text of ['Latest personal capture','PERSONAL','100.50','2026-10-26','No visible canonical price','no independent session proof','not a canonical bar'])assert.ok(html.includes(text),text);
 assert.match(html,/badge personal/);
});

test('canonical bars win: no personal price or badge for a session a canonical bar covers',()=>{
 const sameDate=renderToStaticMarkup(<StockResults result={withCanonical(['2026-10-26'])} portfolioSelected={false} personal={personalBars}/>);
 assert.doesNotMatch(sameDate,/badge personal/);
 assert.doesNotMatch(sameDate,/100\.50/);
 assert.match(sameDate,/111\.25/);
 const newerCanonical=renderToStaticMarkup(<StockResults result={withCanonical(['2026-10-26','2026-10-27'])} portfolioSelected={false} personal={personalBars}/>);
 assert.doesNotMatch(newerCanonical,/badge personal/);
 assert.doesNotMatch(newerCanonical,/100\.50/);
});

test('a personal capture fills only the session after the newest canonical bar',()=>{
 const html=renderToStaticMarkup(<StockResults result={withCanonical(['2026-10-23'])} portfolioSelected={false} personal={personalBars}/>);
 assert.match(html,/badge personal/);
 assert.match(html,/100\.50/);
 assert.match(html,/No canonical bar exists for 2026-10-26/);
 assert.match(html,/111\.25/);
});

test('a personal capture is withheld until the knowledge cutoff reaches its capture time',()=>{
 const tooEarly=renderToStaticMarkup(<StockResults result={withCanonical([],'2026-10-26T10:00:00Z')} portfolioSelected={false} personal={personalBars}/>);
 assert.doesNotMatch(tooEarly,/badge personal/);
 assert.doesNotMatch(tooEarly,/100\.50/);
});

test('another instrument\'s personal capture is never attributed to this one',()=>{
 const other:PersonalBars={...personalBars,sessions:[{...personalBars.sessions[0],rows:[{...personalBars.sessions[0].rows[0],symbol:'OTHER.JK'}]}]};
 const html=renderToStaticMarkup(<StockResults result={withCanonical([])} portfolioSelected={false} personal={other}/>);
 assert.doesNotMatch(html,/badge personal/);
 assert.doesNotMatch(html,/100\.50/);
});

// --- personal fallback: the same thirteen features from unproven personal sessions, never merged ---
const personalReport=(overrides:Partial<PersonalTechnicalFeaturesReport>={}):PersonalTechnicalFeaturesReport=>({source:'PERSONAL_FILE_STORE',sessionProof:'UNPROVEN_PERSONAL',symbol:'SYN',through:'2026-09-30',cutoff:'2026-10-01T12:00:00Z',marketDate:'2026-09-30',runStart:'2026-08-03',sessions:22,warmupNote:'7 of 13 features AVAILABLE from 22 calendar-continuous UNPROVEN_PERSONAL sessions ending 2026-09-30; 2 still warming up. Personal capture is never canonical, verified or DATA_READY.',features:{changePercent:{availability:'AVAILABLE',value:0.5,reason:null,minimumSessions:2},ema20:{availability:'AVAILABLE',value:101.25,reason:null,minimumSessions:20},priorHigh20:{availability:'AVAILABLE',value:110.5,reason:null,minimumSessions:21},ema50:{availability:'WARMUP',value:null,reason:'INSUFFICIENT_SESSIONS',minimumSessions:50},volumeRatio20:{availability:'UNAVAILABLE',value:null,reason:'VOLUME_BASIS_UNVERIFIED',minimumSessions:21},rs20Pp:{availability:'UNAVAILABLE',value:null,reason:'BENCHMARK_MISSING',minimumSessions:21}},...overrides});
const warmupOnly=():TechnicalFeaturesReport=>featuresReport({sessions:1,features:{ema50:{availability:'WARMUP',value:null,reason:'INSUFFICIENT_SESSIONS',minimumSessions:50},volumeRatio20:{availability:'UNAVAILABLE',value:null,reason:'VOLUME_BASIS_UNVERIFIED',minimumSessions:21}}});
const settle=async()=>{for(let tick=0;tick<8;tick++)await Promise.resolve();};
const metric=(scope:string,key:string)=>new RegExp(`<div class="metric" data-feature="${key}">(.*?)</div>`,'s').exec(scope)?.[1]??'';

test('an all-warmup canonical profile shows the personal run in its own PERSONAL panel',()=>{
 const r=result();r.features=warmupOnly();r.personalFeatures=personalReport();
 const html=renderToStaticMarkup(<StockResults result={r} portfolioSelected={false}/>);
 const [canonical,personal]=html.split('<h2>Personal technical features');
 assert.doesNotMatch(html,/No personal capture/);
 for(const text of ['PERSONAL','22 calendar-continuous personal sessions','run from 2026-08-03','UNPROVEN_PERSONAL','never canonical, verified or DATA_READY','Available on the personal run'])assert.ok(html.includes(text),text);
 assert.match(html,/badge personal/);
 assert.match(html,/Personal technical features/);
 // Never mixed: the canonical table keeps its warmup/unavailable states and no personal value leaks into it.
 assert.match(canonical,/<h2>Technical features<\/h2>/);
 assert.match(canonical,/Warmup is correct, not broken/);
 assert.doesNotMatch(canonical,/<strong>101\.25<\/strong>/);
 assert.ok(personal.length>0,personal);
 assert.match(metric(personal,'ema20'),/<strong>101\.25<\/strong>/);
});

test('canonical wins as soon as it holds one usable feature: no personal panel and no merge',()=>{
 const r=result();r.features=featuresReport();r.personalFeatures=personalReport();
 const html=renderToStaticMarkup(<StockResults result={r} portfolioSelected={false}/>);
 assert.doesNotMatch(html,/Personal technical features/);
 assert.doesNotMatch(html,/badge personal/);
 assert.doesNotMatch(html,/101\.25/);
 assert.match(html,/1\.25/);
});

test('the personal panel never renders a number for a warmup or unavailable feature',()=>{
 const r=result();r.features=warmupOnly();r.personalFeatures=personalReport();
 const personal=renderToStaticMarkup(<StockResults result={r} portfolioSelected={false}/>).split('<h2>Personal technical features')[1]??'';
 assert.match(metric(personal,'ema50'),/<strong>Unavailable<\/strong>/);
 assert.doesNotMatch(metric(personal,'ema50'),/<strong>[0-9]/);
 assert.doesNotMatch(metric(personal,'volumeRatio20'),/<strong>[0-9]/);
 assert.match(metric(personal,'volumeRatio20'),/VOLUME_BASIS_UNVERIFIED/);
 assert.doesNotMatch(metric(personal,'rs20Pp'),/<strong>[0-9]/);
 assert.match(metric(personal,'rs20Pp'),/BENCHMARK_MISSING/);
 assert.match(metric(personal,'priorHigh20'),/<strong>110\.50<\/strong>/);
});

test('no personal run is an explicit empty state, never zeros or a fabricated panel',()=>{
 const r=result();r.features=warmupOnly();r.personalFeaturesError='No personal capture of SYN is visible through 2026-09-30 at this cutoff.';
 const html=renderToStaticMarkup(<StockResults result={r} portfolioSelected={false}/>);
 assert.doesNotMatch(html,/Personal technical features/);
 assert.doesNotMatch(html,/badge personal/);
 assert.match(html,/data-personal="MISSING"/);
 assert.ok(html.includes('No personal capture of SYN is visible through 2026-09-30'),html);
});

test('an all-warmup canonical profile triggers one personal read at the pinned through and cutoff',async()=>{
 const h=harness(),p=h.session.load(id,{through:history.through,cutoff:''});
 h.calls[0].resolve(history);await Promise.resolve();
 assert.deepEqual(h.calls.map(c=>new URL(c.path,'http://test').pathname),['/instruments/'+id+'/history','/screener','/instruments/'+id+'/technical-features']);
 h.at('/screener?')[0].resolve({rows:[]});
 h.at('/technical-features')[0].resolve(warmupOnly());
 await settle();
 assert.deepEqual(h.calls.map(c=>new URL(c.path,'http://test').pathname),['/instruments/'+id+'/history','/screener','/instruments/'+id+'/technical-features','/personal/technical-features/SYN']);
 const q=new URL(h.at('/personal/technical-features')[0].path,'http://test').searchParams;
 assert.equal(q.get('through'),history.through);
 assert.equal(q.get('cutoff'),history.cutoff);
 h.at('/personal/technical-features')[0].resolve(personalReport());
 await p;
 assert.equal(h.state().result?.personalFeatures?.sessions,22);
 assert.equal(h.state().error,'');
});

test('a failed personal fallback leaves the canonical panel and the page intact',async()=>{
 const h=harness(),p=h.session.load(id,{through:history.through,cutoff:''});
 h.calls[0].resolve(history);await Promise.resolve();
 h.at('/screener?')[0].resolve({rows:[]});
 h.at('/technical-features')[0].resolve(warmupOnly());
 await settle();
 h.at('/personal/technical-features')[0].reject(new ApiError('No personal capture of SYN is visible through 2026-09-30 at this cutoff.',404,'PERSONAL_CAPTURE_MISSING'));
 await p;
 assert.equal(h.state().error,'');
 assert.equal(h.state().result?.personalFeatures,null);
 assert.match(h.state().result?.personalFeaturesError??'',/No personal capture/);
 assert.equal(h.state().result?.features?.sessions,1);
});

test('personal features still appear outside the frozen canonical horizon where the canonical read is skipped',async()=>{
 const outside={...structuredClone(history),through:'2026-08-20',cutoff:'2026-10-01T12:00:00Z'};
 const h=harness(),p=h.session.load(id,{through:outside.through,cutoff:''});
 h.calls[0].resolve(outside);await settle();
 assert.deepEqual(h.calls.map(c=>new URL(c.path,'http://test').pathname),['/instruments/'+id+'/history','/personal/technical-features/SYN']);
 h.at('/personal/technical-features')[0].resolve(personalReport());
 await p;
 assert.match(h.state().result?.featuresError??'',/Outside the frozen Screener history horizon/);
 assert.equal(h.state().result?.personalFeatures?.sessions,22);
});
