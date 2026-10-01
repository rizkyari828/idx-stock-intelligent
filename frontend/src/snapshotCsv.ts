import type { ExpectedHolding } from './view';

export const snapshotFileLimit = 1024 * 1024;
export type SnapshotPreview = { rowsRead: number; validRows: number; sourceAccounts: number; duplicateGroups: number; errors: string[]; holdings: (ExpectedHolding & { rows: number[]; accounts: string[]; mandates: string[] })[] };
const scale = 10n ** 28n;

// Strict quoted CSV, including escaped quotes and embedded newlines. Rows use physical line numbers.
function csvRows(text: string) {
 const rows: { line: number; cells: string[] }[] = [];
 let cells: string[] = [], field = '', state = 'plain', line = 1, start = 1;
 for (let i = 0; i < text.length; i++) {
  const c = text[i];
  if (state === 'quoted') {
   if (c === '"') { if (text[i+1] === '"') { field += '"'; i++; } else state = 'closed'; }
   else { field += c; if (c === '\n' || c === '\r' && text[i+1] !== '\n') line++; }
   continue;
  }
  if (c === ',' || c === '\n' || c === '\r') {
   cells.push(field); field = ''; state = 'plain';
   if (c !== ',') { rows.push({line:start,cells}); cells = []; if (c === '\r' && text[i+1] === '\n') i++; start = ++line; }
  } else if (c === '"' && !field && state === 'plain') state = 'quoted';
  else if (c === '"' || state === 'closed') throw new Error(`Row ${line}: malformed quoted CSV.`);
  else field += c;
 }
 if (state === 'quoted') throw new Error(`Row ${start}: unterminated quoted field.`);
 if (cells.length || field || state === 'closed') rows.push({line:start,cells:[...cells,field]});
 return rows;
}

function cost(value: string) {
 if (!/^(?:\d+(?:\.\d*)?|\.\d+)$/.test(value)) throw new Error('average_cost must be a nonnegative decimal.');
 const [rawWhole, rawFraction = ''] = value.split('.');
 const whole = rawWhole || '0', fraction = rawFraction.replace(/0+$/, '');
 if (fraction.length > 28 || (whole + fraction).replace(/^0+/, '').length > 28) throw new Error('average_cost exceeds 28 decimal digits.');
 const scaled = BigInt(whole) * scale + BigInt(fraction.padEnd(28, '0'));
 if (scaled > 1_000_000_000_000n * scale) throw new Error('average_cost exceeds 1e12.');
 return scaled;
}

// Round repeating weighted averages to 28 significant digits (maximum 28 fractional digits).
function average(total: bigint, shares: bigint) {
 const integerDigits = (total / shares / scale).toString().length;
 const places = Math.min(28, 28 - (total / shares / scale === 0n ? 0 : integerDigits));
 const unit = 10n ** BigInt(28 - places), divisor = shares * unit;
 const rounded = (total + divisor / 2n) / divisor;
 const padded = rounded.toString().padStart(places + 1, '0');
 return places ? (padded.slice(0,-places) + '.' + padded.slice(-places)).replace(/\.?0+$/, '') : padded;
}

export function parseSnapshotCsv(text: string): SnapshotPreview {
 const preview: SnapshotPreview = {rowsRead:0,validRows:0,sourceAccounts:0,duplicateGroups:0,errors:[],holdings:[]};
 try {
  if (new TextEncoder().encode(text).length > snapshotFileLimit) throw new Error('Snapshot exceeds 1 MiB.');
  const rows = csvRows(text.replace(/^\uFEFF/, ''));
  const headers = rows.shift()?.cells.map(c=>c.trim().toLowerCase()) ?? [];
  const allowed = ['source_account','symbol','lots','shares','average_cost','mandate'];
  if (!headers.includes('symbol') || !headers.some(h=>h==='lots'||h==='shares') || new Set(headers).size !== headers.length || headers.some(h=>!allowed.includes(h))) throw new Error('Row 1: expected symbol and shares or lots headers; allowed columns: '+allowed.join(',')+'.');
  const groups = new Map<string, {shares:bigint; weighted:bigint; known:boolean; rows:number[]; accounts:Set<string>; mandates:Set<string>}>();
  const accounts = new Set<string>();
  for (const row of rows) {
   preview.rowsRead++;
   try {
    if (row.cells.length !== headers.length) throw new Error('column count does not match header.');
    const values = Object.fromEntries(headers.map((h,i)=>[h,row.cells[i].trim()]));
    const symbol = values.symbol.toUpperCase();
    if (!symbol || symbol.length > 50) throw new Error('symbol is required and must be at most 50 characters.');
    const integer = (v: string, name: string) => { if (!/^\d+$/.test(v) || BigInt(v) <= 0n || BigInt(v) > 1_000_000_000n) throw new Error(`${name} must be a positive whole number at most 1,000,000,000.`); return BigInt(v); };
    const lots = values.lots ? integer(values.lots,'lots') : null;
    const shares = values.shares ? integer(values.shares,'shares') : lots === null ? 0n : lots * 100n;
    if (!shares || shares > 1_000_000_000n) throw new Error('shares or convertible lots are required; maximum shares is 1,000,000,000.');
    if (lots !== null && shares !== lots * 100n) throw new Error('lots/shares mismatch: 1 LOT = 100 SHARES.');
    const expectedCost = values.average_cost ? cost(values.average_cost) : null;
    if (values.mandate && !['FAST_SWING','LONG_SWING','INVEST'].includes(values.mandate)) throw new Error('unknown mandate; use FAST_SWING, LONG_SWING or INVEST.');
    const group = groups.get(symbol) ?? {shares:0n,weighted:0n,known:true,rows:[],accounts:new Set<string>(),mandates:new Set<string>()};
    if (group.shares + shares > 1_000_000_000n) throw new Error(`${symbol}: consolidated shares exceed 1,000,000,000.`);
    group.shares += shares; group.weighted += shares * (expectedCost ?? 0n); group.known &&= expectedCost !== null; group.rows.push(row.line);
    if (values.source_account) { group.accounts.add(values.source_account); accounts.add(values.source_account); }
    if (values.mandate) group.mandates.add(values.mandate);
    groups.set(symbol,group); preview.validRows++;
   } catch (e) { preview.errors.push(`Row ${row.line}: ${(e as Error).message}`); }
  }
  if (!preview.rowsRead) preview.errors.push('No holding rows found.');
  if (groups.size > 200) preview.errors.push('At most 200 resulting holdings are allowed.');
  preview.sourceAccounts = accounts.size;
  for (const [symbol,g] of groups) {
   if (g.rows.length > 1) preview.duplicateGroups++;
   preview.holdings.push({instrumentId:null,symbol,shares:String(g.shares),averageCost:g.known?average(g.weighted,g.shares):null,rows:g.rows,accounts:[...g.accounts],mandates:[...g.mandates]});
  }
 } catch (e) { preview.errors.push((e as Error).message); }
 return preview;
}

export async function readSnapshotFile(file: File) {
 if (file.size > snapshotFileLimit) throw new Error('Snapshot exceeds 1 MiB.');
 return parseSnapshotCsv(new TextDecoder('utf-8',{fatal:true}).decode(await file.arrayBuffer()));
}
