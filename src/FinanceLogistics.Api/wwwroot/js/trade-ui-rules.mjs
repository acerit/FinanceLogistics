export {canConfirm,needsPolling} from './po-ui-rules.mjs';
export function previewCells(row,headers){return row.normalizedCells??headers.map(()=>row.rowKind==='DATA'?'Chưa chuẩn hóa':null);}
export function templatePresentation(template){return {label:`${template?.profile||'Chưa có mẫu'} · bản ${template?.versionNo??''}`,accept:'.xlsx',allowCsvDownload:false,hint:template?.profile?.endsWith('_HEADER1_V4')?'Chuẩn mới: hàng 1 là header, dữ liệu nhập từ hàng 2. Chọn đúng phiên bản V4 khi upload; không thêm dòng mô tả hoặc gộp ô.':template?.profile?.startsWith('PIN_TEST_')?'Pin Test: giữ nguyên dòng, ký tự; không khử trùng hoặc suy diễn ngày.':'XLSX đúng sheet và tên cột; header lạ hoặc trùng báo lỗi. Công thức chỉ đọc cache, không tính lại.'};}
export function coverageLabel(batch){if(batch?.periodMode==='AS_OF_SNAPSHOT')return 'Snapshot Pin Test; không lọc dòng theo ngày giấy ủy quyền.';return batch?.coverageStart&&batch?.coverageEnd?`Ngày dữ liệu: ${batch.coverageStart} → ${batch.coverageEnd}; chưa xác nhận đủ bộ nguồn lập báo cáo.`:'';}

export function policyLabel(policy){
 if(policy.periodMode==='AS_OF_SNAPSHOT')return 'Nộp đầy đủ danh mục Pin Test; không lọc dòng theo ngày giấy ủy quyền hoặc ngày hết hạn.';
 const modes={EXPLICIT_RANGE:'Phạm vi được cấu hình',YEAR_TO_PERIOD:'Lũy kế từ đầu năm',SELECTED_MONTH:'Riêng tháng'};
 const dates={DeclarationDate:'Ngày tờ khai',IssuedDate:'Ngày phát hành'};
 return `${modes[policy.periodMode]||'Phạm vi dữ liệu'}: ${dates[policy.coverageProperty]||'Ngày theo cấu hình'} từ ${policy.coverageStart} đến trước ${policy.coverageEndExclusive}. Nộp đầy đủ snapshot khi sửa.`;
}
