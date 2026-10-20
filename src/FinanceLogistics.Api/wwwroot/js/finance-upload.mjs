import { createIdentityClient } from './api-client.mjs';
import { canConfirm, needsPolling, previewCells, templatePresentation, coverageLabel } from './finance-ui-rules.mjs';
const api = createIdentityClient();
const $ = id => document.getElementById(id);
const sources={AP_REPORT:'AP · XXAPR017 Invoice Detail Report',SUPPLIER_PAYMENT_REGISTER:'Supplier · XXAPR015 Payment Register',EX_VND:'Chứng từ VND',EX_USD:'Chứng từ USD'};
const requested=new URL(location.href).searchParams.get('source')||'AP_REPORT';
const sourceCode=Object.hasOwn(sources,requested)?requested:'AP_REPORT';
const base=`/api/finance/${sourceCode}`;
$('source').replaceChildren(...Object.entries(sources).map(([code,name])=>new Option(name,code)));
$('source').value=sourceCode;$('sourceTitle').textContent=sources[sourceCode];
$('source').addEventListener('change',()=>{location.href=`/finance-upload.html?source=${encodeURIComponent($('source').value)}`;});
let user, requirements = [], templates = [], requirement = null, batch = null;
let timer, epoch = 0, previewEpoch = 0, expectedVersion = null, stale = false, busy = false;
let rowRequest=0, issueRequest=0, historyRequest=0;
let rowPage = 1, issuePage = 1, historyPage = 1, rowData = null;
const labels = { RECEIVED: 'Đã nhận file', VALIDATING: 'Đang kiểm tra', READY: 'Sẵn sàng xác nhận',
  INVALID: 'Cần sửa lỗi', COMMITTED: 'Đã xác nhận', DISCARDED: 'Đã bỏ', OPEN: 'Đang mở', REVIEW: 'Đang rà soát', CLOSED: 'Đã khóa' };
function message(text, kind = '') { $('message').textContent = text; $('message').className = kind; }
function stopPolling() { clearTimeout(timer); }
function selectedTemplate() { return templates.find(t => t.templateVersionId === $('template').value); }
function drawTemplate() {
  const view = templatePresentation(selectedTemplate());
  $('templateHint').textContent = view.hint;
  $('file').accept = view.accept;
}
function controls() {
  $('upload').disabled = busy || !requirement || requirement.periodStatus !== 'OPEN' || !templates.length;
  $('source').disabled=busy; $('period').disabled = busy; $('template').disabled = busy; $('file').disabled = busy;
  $('confirm').disabled = busy || !canConfirm(batch, $('warnings').checked, stale);
  $('refresh').disabled = busy || !batch; $('original').disabled = busy || !batch;
  $('warnings').disabled = busy || !batch || batch.status !== 'READY' || batch.warningCount === 0;
  $('retry').hidden = batch?.jobStatus !== 'FAILED'; $('retry').disabled = busy;
}
function errorMessage(error) {
  return `${error.message || 'Thao tác chưa hoàn thành.'}${error.code ? ` [${error.code}]` : ''}${error.correlationId ? ` · Mã tra cứu: ${error.correlationId}` : ''}`;
}
async function action(fn) {
  try { await fn(); }
  catch (error) {
    if (error.code === 'REQUIREMENT_CHANGED') stale = true;
    message(errorMessage(error), 'error');
    if (error.status === 401) {
      stopPolling(); stale = true;
      message(`${errorMessage(error)} Hãy đăng nhập lại ở trang chính, rồi mở lại trang Finance.`, 'error');
    }
    controls();
  }
}
function table(target, headers, rows) {
  const element = document.createElement('table');
  const head = element.createTHead().insertRow();
  headers.forEach(text => { const th = document.createElement('th'); th.scope = 'col'; th.textContent = text; head.append(th); });
  const body = element.createTBody();
  rows.forEach(values => {
    const tr = body.insertRow();
    values.forEach(value => {
      const td = tr.insertCell();
      if (value instanceof Node) td.append(value);
      else { td.textContent = value === null || value === undefined || value === '' ? '—' : String(value); if (td.textContent.length > 65) td.className = 'long'; }
    });
  });
  $(target).replaceChildren(element);
}
function pager(prefix, data) {
  $(prefix + 'Previous').disabled = data.page <= 1;
  $(prefix + 'Next').disabled = data.page * data.pageSize >= data.total;
  $(prefix + 'Page').textContent = `${data.total} mục · Trang ${data.page}`;
}
function drawRows() {
  if (!rowData) { $('previewTable').replaceChildren(); return; }
  const headers = batch?.headers || templates[0]?.headers || [];
  if($('rawToggle').checked){
    const ordinals=[...new Set(rowData.items.flatMap(r=>r.rawCells.map(c=>c.ordinal)))].sort((a,b)=>a-b);
    const names=ordinals.map(n=>rowData.items.flatMap(r=>r.rawCells).find(c=>c.ordinal===n)?.header||`Cột ${n}`);
    table('previewTable',['Dòng','Loại',...names],rowData.items.map(row=>[row.recordNumber,row.rowKind,...ordinals.map(n=>{
      const c=row.rawCells.find(x=>x.ordinal===n);if(!c)return null;
      return `${c.cellReference||''}: ${c.text??'∅'}`+(c.formula!==null&&c.formula!==undefined?`\nCông thức: ${c.formula}\nCache: ${c.cachedValue??'∅'}\nGốc: ${c.formulaOrigin||''}`:'');
    })]));
  } else table('previewTable',['Dòng','Loại',...headers],rowData.items.map(row=>[row.recordNumber,row.rowKind,...previewCells(row,headers,false)]));
  pager('rows', rowData);
}
async function loadRows() {
  if (!batch) return;
  const id = batch.batchId, version = previewEpoch;
  const request=++rowRequest;
  const data = await api.request(`${base}/batches/${id}/rows?page=${rowPage}`);
  if (batch?.batchId !== id || version !== previewEpoch) return;
  if(request!==rowRequest)return;
  rowData = data; drawRows();
}
async function loadIssues() {
  if (!batch) return;
  const id = batch.batchId, version = previewEpoch;
  const request=++issueRequest;
  const data = await api.request(`${base}/batches/${id}/issues?page=${issuePage}&issueCode=${encodeURIComponent($('issueCode').value)}`);
  if (batch?.batchId !== id || version !== previewEpoch) return;
  if(request!==issueRequest)return;
  table('issuesTable', ['Mức', 'Dòng', 'Cột', 'Nội dung', 'Mã'], data.items.map(x => [x.severity, x.recordNumber ?? 'Toàn file/header', x.columnName, x.message, x.issueCode]));
  pager('issues', data);
}
async function loadSummary(){
  if(!batch)return;const id=batch.batchId,version=previewEpoch;
  const data=await api.request(`${base}/batches/${id}/issues/summary`);
  if(batch?.batchId!==id||version!==previewEpoch)return;
  table('issueSummary',['Mức','Nhóm','Số cảnh báo/lỗi'],data.map(x=>[x.severity,x.issueCode,x.issueCount]));
  const selected=$('issueCode').value;
  $('issueCode').replaceChildren(new Option('Tất cả',''),...data.map(x=>new Option(`${x.issueCode} (${x.issueCount})`,x.issueCode)));
  if(data.some(x=>x.issueCode===selected))$('issueCode').value=selected;
}
async function history() {
  if (!requirement) return;
  const id = requirement.requirementId, stamp = epoch;
  const request=++historyRequest;
  const data = await api.request(`${base}/requirements/${id}/history?page=${historyPage}`);
  if (stamp !== epoch||request!==historyRequest) return;
  table('historyTable', ['Bản', 'File', 'Trạng thái', 'Đang dùng', 'Dòng / Lỗi / Cảnh báo', 'Nộp lúc', 'Xem'], data.items.map(item => {
    const button = document.createElement('button'); button.type = 'button'; button.textContent = 'Mở';
    button.addEventListener('click', () => action(() => showBatch(item.batchId)));
    return [item.versionNo, item.originalName, labels[item.status] || item.status, item.isCurrent ? 'Có' : '',
      `${item.totalRows} / ${item.errorCount} / ${item.warningCount}`, new Date(item.uploadedAtUtc).toLocaleString('vi-VN'), button];
  }));
  pager('history', data);
}
function drawBatch() {
  $('batchSummary').textContent = `Bản ${batch.versionNo} · ${batch.originalName} · ${labels[batch.status] || batch.status}${batch.isCurrent ? ' · ĐANG ĐƯỢC SỬ DỤNG' : ''}`;
  $('coverage').textContent = coverageLabel(batch);
  $('counts').textContent = `${batch.totalRows} giao dịch đã đọc · ${batch.errorCount} lỗi · ${batch.warningCount} cảnh báo · Mã lần nộp ${batch.batchId}`
    + ' · Preview raw bao gồm cả dòng Subtotal/footer nếu có.' + (batch.jobStatus === 'FAILED' ? ` · Xử lý chưa hoàn thành: ${batch.jobErrorCode}. Sửa lỗi kỹ thuật rồi bấm Thử lại xử lý.` : '');
  $('active').textContent = batch.activeBatchId ? `Mã lần nộp ${batch.activeBatchId}` : 'Chưa có bản được xác nhận';
  $('confirmHint').textContent = batch.isCurrent ? 'Đây là bản đang dùng. Bạn có thể nộp bản sửa ở phía trên.'
    : batch.status === 'COMMITTED' ? 'Đây là bản lịch sử; xem và tải lại được, không kích hoạt lại bằng nút xác nhận.'
    : batch.status === 'INVALID' ? 'Hãy sửa file ở máy của bạn và gửi thành một lần nộp mới.'
    : 'Xác nhận sẽ thay bản đang dùng của kỳ. File và dữ liệu bản trước vẫn được giữ.';
  controls();
}
async function showBatch(id) {
  if (busy) return;
  stopPolling(); const stamp = ++previewEpoch;
  expectedVersion = null; stale = false; batch = null; rowData = null; rowPage = 1; issuePage = 1;
  $('warnings').checked = false; $('issueCode').replaceChildren(new Option('Tất cả','')); $('issueSummary').replaceChildren(); controls();
  const data = await api.request(`${base}/batches/${id}`);
  if (stamp !== previewEpoch) return;
  batch = data; expectedVersion = data.requirementRowVersion; drawBatch();
  await Promise.all([loadRows(), loadIssues(), loadSummary()]);
  if (needsPolling(batch)) timer = setTimeout(() => action(() => poll(id, stamp)), 2000);
}
async function poll(id, stamp) {
  if (stamp !== previewEpoch) return;
  const data = await api.request(`${base}/batches/${id}`);
  if (stamp !== previewEpoch) return;
  // Only capture once the first usable preview is ready. Do not silently replace it afterwards.
  if (batch?.status !== 'READY' && data.status === 'READY') expectedVersion = data.requirementRowVersion;
  batch = data; drawBatch();
  if (needsPolling(data)) timer = setTimeout(() => action(() => poll(id, stamp)), 2000);
  else await Promise.all([loadRows(), loadIssues(), loadSummary(), history()]);
}
async function choosePeriod() {
  stopPolling(); const stamp = ++epoch; previewEpoch++;
  requirement = null; batch = null; expectedVersion = null; rowData = null; historyPage = 1;
  $('issueCode').replaceChildren(new Option('Tất cả',''));
  ['previewTable', 'issuesTable', 'historyTable', 'issueSummary'].forEach(id => $(id).replaceChildren());
  $('batchSummary').textContent = 'Chưa chọn lần nộp.'; $('counts').textContent = ''; $('coverage').textContent = '';
  $('warnings').checked = false; controls();
  const data = await api.request(`${base}/periods/${$('period').value}/requirements`);
  if (stamp !== epoch) return;
  requirements = data; requirement = data.find(x => x.sourceCode === sourceCode && x.scopeCode === 'FINANCE_SHARED');
  if (!requirement) throw new Error('Kỳ này chưa có yêu cầu nộp nguồn này.');
  $('active').textContent = requirement.activeBatchId ? `Bản ${requirement.activeVersionNo} · Mã ${requirement.activeBatchId}` : 'Chưa có bản được xác nhận';
  $('periodStatus').textContent = labels[requirement.periodStatus] || requirement.periodStatus;
  $('deadline').textContent = requirement.dueAtUtc ? `Hạn nộp: ${new Date(requirement.dueAtUtc).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })} (giờ Việt Nam).` : 'Chưa đặt hạn nộp.';
  controls(); await history();
}
function saveBlob(blob, name) {
  const link = document.createElement('a'); const url = URL.createObjectURL(blob);
  link.href = url; link.download = name; document.body.append(link); link.click(); link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
$('uploadForm').addEventListener('submit', event => { event.preventDefault(); action(async () => {
  if (busy || !requirement) return;
  const file = $('file').files[0]; if (!file) throw new Error('Hãy chọn file.');
  if (!file.name.toLowerCase().endsWith('.xlsx'))
    throw new Error('Mẫu Finance cần XLSX nguyên bản. Hãy chọn lại file hoặc mẫu nhập.');
  if (file.size > 10 * 1024 * 1024) throw new Error('File vượt 10 MiB.');
  busy = true; controls(); let uploaded;
  try {
    const hash = [...new Uint8Array(await crypto.subtle.digest('SHA-256', await file.arrayBuffer()))].map(x => x.toString(16).padStart(2, '0')).join('');
    const key = `finance4-pending:${user.userId}:${requirement.requirementId}:${$('template').value}:${file.name}:${hash}`;
    const clientId = sessionStorage.getItem(key) || crypto.randomUUID(); sessionStorage.setItem(key, clientId);
    const form = new FormData(); form.append('file', file); form.append('templateVersionId', $('template').value); form.append('clientUploadId', clientId);
    uploaded = await api.request(`${base}/requirements/${requirement.requirementId}/uploads`, { method: 'POST', body: form });
    sessionStorage.removeItem(key); // A received success ends this logical upload attempt.
    message(`Đã nhận bản ${uploaded.versionNo}. Bản đang dùng chưa thay đổi.`, 'success');
  } finally { busy = false; controls(); }
  await showBatch(uploaded.batchId); await history();
}); });
$('confirm').addEventListener('click', () => action(async () => {
  if (busy || !canConfirm(batch, $('warnings').checked, stale)) return;
  const id = batch.batchId; busy = true; controls();
  try {
    await api.request(`${base}/batches/${id}/confirm`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ expectedRequirementRowVersion: expectedVersion, acceptWarnings: $('warnings').checked }) });
    message('Đã xác nhận. Kỳ này sử dụng bản vừa chọn; các bản trước vẫn có trong lịch sử.', 'success');
  } finally { busy = false; controls(); }
  await showBatch(id); await history();
}));
$('retry').addEventListener('click', () => action(async () => {
  if (!batch || busy) return; const id = batch.batchId; busy = true; controls();
  try { await api.request(`${base}/batches/${id}/retry-validation`, { method: 'POST' }); }
  finally { busy = false; controls(); }
  await showBatch(id);
}));
$('original').addEventListener('click', () => action(async () => {
  if (!batch) return; const { batchId, originalName } = batch;
  saveBlob(await api.download(`${base}/batches/${batchId}/original`), originalName);
}));
$('refresh').addEventListener('click', () => { if (batch) action(() => showBatch(batch.batchId)); });
$('issueCode').addEventListener('change',()=>action(async()=>{issuePage=1;await loadIssues();}));
$('historyRefresh').addEventListener('click', () => action(history));
$('period').addEventListener('change', () => action(choosePeriod));
$('template').addEventListener('change', drawTemplate);
$('warnings').addEventListener('change', controls); $('rawToggle').addEventListener('change', drawRows);
for (const [prefix, getPage, setPage, load] of [
  ['rows', () => rowPage, x => rowPage = x, loadRows], ['issues', () => issuePage, x => issuePage = x, loadIssues],
  ['history', () => historyPage, x => historyPage = x, history]]) {
  $(prefix + 'Previous').addEventListener('click', () => action(async () => { setPage(Math.max(1, getPage() - 1)); await load(); }));
  $(prefix + 'Next').addEventListener('click', () => action(async () => { setPage(getPage() + 1); await load(); }));
}
$('logout').addEventListener('click', () => action(async () => { stopPolling(); await api.logout(); location.href = '/login.html'; }));
window.addEventListener('pagehide', stopPolling);
await action(async () => {
  user = await api.me();
  if (!user.roles.includes('FINANCE')) throw new Error('Trang Finance yêu cầu quyền FINANCE. SYSTEM_ADMIN riêng lẻ không thay cho quyền nghiệp vụ.');
  [requirements, templates] = await Promise.all([api.request(`${base}/periods`), api.request(`${base}/templates`)]);
  $('period').replaceChildren(...requirements.map(p => new Option(p.periodCode, p.periodId)));
  $('template').replaceChildren(...templates.map(t => new Option(templatePresentation(t).label, t.templateVersionId)));
  drawTemplate();
  if (!templates.length) message('Chưa có mẫu Finance được duyệt. Kiểm tra script F4_002 trong hướng dẫn.', 'warning');
  if (requirements.length) await choosePeriod(); else message('Chưa có kỳ Finance được cấu hình.', 'warning');
});
