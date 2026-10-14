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
  return key === 'SOURCE' ? 'SOUCE' : key === 'INVOICE NO' ? 'INVOICE NO.' : key === 'PO CURR' ? 'PO CURRENCY' : key;
}
export function previewCells(row, headers, raw = false) {
  if (!raw && row.normalizedCells) return row.normalizedCells;
  return headers.map(header => row.rawCells.find(cell => headerKey(cell.header) === headerKey(header))?.text ?? null);
}

export function templatePresentation(template) {
  const finance = template?.profile === 'PO_FINANCE_XLSX_V1';
  return {
    label: `${finance ? 'PO Finance XLSX' : 'Mẫu thực hành DEV'} · phiên bản ${template?.versionNo ?? ''}`,
    accept: finance ? '.xlsx' : '.csv,.xlsx,.xls',
    allowCsvDownload: Boolean(template) && !finance,
    hint: finance
      ? 'File PO Finance nguyên bản: một sheet PO update, tiêu đề gộp hàng 1–2, dữ liệu từ hàng 3, tối đa 10 MiB. Không đổi tên sheet, không xóa công thức. Kỳ nộp phải được cấu hình theo tháng hoặc lũy kế.'
      : 'Mẫu thực hành: 17 cột, header ở dòng 1. CSV UTF-8; Excel có một sheet PO_UPDATED. Ngày dạng yyyy-MM-dd, dữ liệu riêng tháng, tối đa 10 MiB.'
  };
}
export function coverageLabel(batch) {
  if (!batch?.coverageStart || !batch?.coverageEnd) return '';
  const mode = batch.periodMode === 'YEAR_TO_PERIOD' ? 'Snapshot từ đầu năm đến kỳ đã chọn' : 'Dữ liệu riêng tháng';
  return `${mode} · Trx date của các dòng đã chuẩn hóa: ${batch.coverageStart} → ${batch.coverageEnd}. Phạm vi này chưa chứng minh file đã đủ dữ liệu.`;
}
