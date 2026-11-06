export { canConfirm, needsPolling } from './po-ui-rules.mjs';
export function previewCells(row,headers){return row.normalizedCells??headers.map(()=>row.rowKind==='DATA'?'Chưa chuẩn hóa':null);}
export function templatePresentation(template){return {
  label:`${template?.profile||'Chưa có mẫu'} · bản ${template?.versionNo??''}`,
  accept:'.xlsx',allowCsvDownload:false,
  hint:template?.profile?.endsWith('_HEADER1_V4') ? 'Chuẩn mới: chỉ một dòng header tại hàng 1; nhập giao dịch từ hàng 2. Không thêm trang trí, không gộp ô header. NK_1000 56 cột, NK_6000 30 cột. Đây là template input, chưa tự lấy PO Finance.' : template?.profile?.endsWith('_WORKBOOK_V2') ? 'Mẫu NK V2: đọc công thức bằng kết quả cache đã lưu, không tính lại hoặc tải liên kết ngoài. Chủ nguồn cần kiểm tra cache trước khi nộp snapshot. Giữ đúng sheet và 56 cột.' : 'Nộp XLSX nguyên bản đúng mẫu. Giữ sheet, dòng tiêu đề và đủ 56 cột; không đổi đuôi từ XLS sang XLSX. Mẫu DEV chỉ phục vụ kiểm thử; phạm vi ngày được cấu hình riêng cho từng nguồn.'
};}
export function coverageLabel(batch){
  if(!batch?.coverageStart||!batch?.coverageEnd)return '';
  const labels={SELECTED_MONTH:'Riêng tháng',YEAR_TO_PERIOD:'Lũy kế đến kỳ',EXPLICIT_RANGE:'Phạm vi ngày được cấu hình rõ'};
  return `${labels[batch.periodMode]||batch.periodMode} · Trx Date đã chuẩn hóa: ${batch.coverageStart} → ${batch.coverageEnd}. Đây chưa phải xác nhận đủ dữ liệu hay đã đối chiếu các nguồn.`;
}
