export { canConfirm, needsPolling } from './po-ui-rules.mjs';
export function previewCells(row,headers){return row.normalizedCells??headers.map(()=>row.rowKind==='DATA'?'Chưa chuẩn hóa':null);}
export function templatePresentation(template){return {
  label:`${template?.profile||'Chưa có mẫu'} · bản ${template?.versionNo??''}`,
  accept:'.xlsx',allowCsvDownload:false,
  hint:'Nộp XLSX nguyên bản đúng mẫu. Giữ sheet, dòng tiêu đề, subtotal và công thức; không đổi đuôi từ XLS sang XLSX. Mẫu DEV chỉ phục vụ kiểm thử; phạm vi ngày được cấu hình riêng cho từng nguồn.'
};}
export function coverageLabel(batch){
  if(!batch?.coverageStart||!batch?.coverageEnd)return '';
  const labels={SELECTED_MONTH:'Riêng tháng',YEAR_TO_PERIOD:'Lũy kế đến kỳ',EXPLICIT_RANGE:'Phạm vi ngày được cấu hình rõ'};
  return `${labels[batch.periodMode]||batch.periodMode} · Ngày giao dịch đã đọc: ${batch.coverageStart} → ${batch.coverageEnd}. Đây chưa phải xác nhận đủ dữ liệu hay đã đối chiếu các nguồn.`;
}
