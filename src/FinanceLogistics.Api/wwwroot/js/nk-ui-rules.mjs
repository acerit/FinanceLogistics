export { canConfirm, needsPolling } from './po-ui-rules.mjs';
export function previewCells(row,headers){return row.normalizedCells??headers.map(()=>row.rowKind==='DATA'?'Chưa chuẩn hóa':null);}
export function templatePresentation(template){return {
  label:`${template?.profile||'Chưa có mẫu'} · bản ${template?.versionNo??''}`,
  accept:'.xlsx',allowCsvDownload:false,
  hint:template?.profile==='NK_1000_HEADER1_44_V5' ? 'NK_1000 bản 44 cột: header dòng 1, dữ liệu từ dòng 2; đã bỏ 12 cột của mẫu 56 cột. Không đổi tên, thứ tự hoặc tự thêm 12 cột khi upload. Các trường bị bỏ không tự được cập nhật; mapping kết quả sẽ xử lý riêng sau khi Finance/Logistics duyệt.' : template?.profile?.endsWith('_HEADER1_V4') ? 'NK_6000 bản 30 cột: header dòng 1, dữ liệu từ dòng 2. Không gộp ô header; chưa tự lấy dữ liệu PO Finance.' : template?.profile?.endsWith('_WORKBOOK_V2') ? 'Mẫu NK V2: đọc công thức bằng kết quả cache đã lưu, không tính lại hoặc tải liên kết ngoài. Chủ nguồn cần kiểm tra cache trước khi nộp snapshot. Giữ đúng sheet và 56 cột.' : 'Nộp XLSX nguyên bản đúng mẫu. Giữ sheet, dòng tiêu đề và đủ 56 cột; không đổi đuôi từ XLS sang XLSX. Mẫu DEV chỉ phục vụ kiểm thử; phạm vi ngày được cấu hình riêng cho từng nguồn.'
};}
export function coverageLabel(batch){
  if(!batch?.coverageStart||!batch?.coverageEnd)return '';
  const labels={SELECTED_MONTH:'Riêng tháng',YEAR_TO_PERIOD:'Lũy kế đến kỳ',EXPLICIT_RANGE:'Phạm vi ngày được cấu hình rõ'};
  return `${labels[batch.periodMode]||batch.periodMode} · Trx Date đã chuẩn hóa: ${batch.coverageStart} → ${batch.coverageEnd}. Đây chưa phải xác nhận đủ dữ liệu hay đã đối chiếu các nguồn.`;
}
