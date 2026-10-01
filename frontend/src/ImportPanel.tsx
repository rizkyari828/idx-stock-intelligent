import { useState } from 'react';
import { canConfirmImport, type ImportPreview } from './view.js';
export function ImportPanel({ portfolioId, request, onImported }: {
  portfolioId?: string;
  request: <T>(path: string, body?: unknown) => Promise<T>;
  onImported: (id: string) => Promise<void>;
}) {
  const [format, setFormat] = useState('JSON');
  const [mode, setMode] = useState('CREATE_NEW');
  const [content, setContent] = useState('');
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState('');
  const body = () => ({ format, content, mode: format === 'CSV' ? 'CREATE_NEW' : mode, portfolioId: format === 'CSV' ? portfolioId : null });
  async function act(work: () => Promise<void>) {
    setBusy(true); setError('');
    try { await work(); } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }
  return <section aria-label="Portfolio import">
    <h2>Import portfolio</h2>
    <p>JSON restores a complete portfolio with its original ID and timestamps. CSV appends generic transactions to the open portfolio. Preview writes nothing; Confirm Import writes atomically.</p>
    <label>Format<select disabled={busy} value={format} onChange={e => { setFormat(e.target.value); setPreview(null); setResult(''); }}><option>JSON</option><option>CSV</option></select></label>
    {format === 'JSON' ? <label>Mode<select disabled={busy} value={mode} onChange={e => { setMode(e.target.value); setPreview(null); setResult(''); }}><option>CREATE_NEW</option><option>RESTORE_EXISTING_EMPTY</option></select></label>
      : <p>Target portfolio: {portfolioId ?? 'Open or create a portfolio before CSV import.'}</p>}
    <label>Select file (UTF-8, max 8 MiB)<input type="file" accept=".json,.csv" disabled={busy} onChange={e => {
      const file = e.target.files?.[0]; setPreview(null); setResult(''); setContent('');
      if (file) void act(async () => { if (file.size > 8 * 1024 * 1024) throw new Error('File exceeds 8 MiB.'); setContent(new TextDecoder('utf-8', { fatal: true }).decode(await file.arrayBuffer())); });
    }} /></label>
    <button type="button" disabled={busy || !content || format === 'CSV' && !portfolioId} onClick={() => void act(async () => { setResult(''); setPreview(await request<ImportPreview>('/portfolio-imports/preview', body())); })}>Preview</button>
    {preview && <div role="status"><p>{preview.status} · Rows read {preview.rowsRead} · Valid {preview.validRows} · Invalid {preview.invalidRows} · Duplicates {preview.duplicates} · Resulting events {preview.estimatedResultingEvents}</p>
      <p>Unknown instruments: {preview.unknownInstruments.length}</p><ul>{preview.errors.map((e, i) => <li key={i}>{e}</li>)}</ul>
      <details><summary>Normalized quantities / row validation</summary><ol>{preview.rows.map(r => <li key={r.row}>Row {r.row}: {r.error ?? `${r.event?.type} · ${r.event?.quantity} SHARES`}{r.duplicate && ' · DUPLICATE'}</li>)}</ol></details>
    </div>}
    <button type="button" disabled={busy || !canConfirmImport(preview)} onClick={() => void act(async () => {
      const response = await request<{ status: string; portfolioId: string | null; eventsAdded: number; thesesAdded: number; errors: string[] }>('/portfolio-imports', body());
      setResult(`${response.status} · ${response.eventsAdded} events added · ${response.thesesAdded} thesis versions added`); setPreview(null);
      if (response.errors.length) setError(response.errors.join('; '));
      if (['IMPORTED', 'ALREADY_PRESENT'].includes(response.status) && response.portfolioId) await onImported(response.portfolioId);
    })}>Confirm Import</button>
    {result && <p role="status">{result}</p>}{error && <p role="alert" className="error">{error}</p>}
  </section>;
}
