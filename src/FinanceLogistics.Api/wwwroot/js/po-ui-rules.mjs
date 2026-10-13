export function canConfirm(batch, warningsAccepted, stale = false) {
  return Boolean(batch && batch.status === 'READY' && batch.periodStatus === 'OPEN'
    && batch.totalRows > 0 && batch.errorCount === 0 && batch.jobStatus === 'SUCCEEDED'
    && (!batch.warningCount || warningsAccepted) && !stale);
}
export function needsPolling(batch) {
  return batch && ['RECEIVED', 'VALIDATING'].includes(batch.status)
    && !['FAILED', 'CANCELLED'].includes(batch.jobStatus);
}
export function headerKey(text) {
  const key = text.trim().replace(/^\uFEFF/, '').replace(/\s+/g, ' ').toUpperCase();
  return key === 'SOURCE' ? 'SOUCE' : key === 'INVOICE NO' ? 'INVOICE NO.' : key;
}
export function previewCells(row, headers, raw = false) {
  if (!raw && row.normalizedCells) return row.normalizedCells;
  return headers.map(header => row.rawCells.find(cell => headerKey(cell.header) === headerKey(header))?.text ?? null);
}
