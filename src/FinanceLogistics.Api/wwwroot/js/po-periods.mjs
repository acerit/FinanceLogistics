import { createIdentityClient } from './api-client.mjs';

const api = createIdentityClient();
const status = document.getElementById('status');
const workspace = document.getElementById('workspace-content');
const period = document.getElementById('period');
const periodStatus = document.getElementById('period-status');
const rows = document.getElementById('requirement-rows');
const empty = document.getElementById('empty');
const reload = document.getElementById('reload');
const logout = document.getElementById('logout');
const labels = { OPEN: 'Đang mở', REVIEW: 'Đang rà soát', CLOSED: 'Đã đóng' };
let periods = [];

function busy(value) {
  reload.disabled = value;
  period.disabled = value || periods.length === 0;
}
function fail(error) {
  rows.replaceChildren();
  workspace.hidden = true;
  if (error.status === 401) {
    // Existing Identity allow-list intentionally permits index.html only.
    location.replace('/login.html?returnUrl=%2Findex.html');
    return;
  }
  status.textContent = error.message +
    (error.correlationId ? ' Mã yêu cầu: ' + error.correlationId : '');
}
function dueLabel(value, timeZone) {
  if (value === null) return 'Chưa cấu hình';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Ngày không hợp lệ';
  try {
    return new Intl.DateTimeFormat('vi-VN', {
      timeZone, day:'2-digit', month:'2-digit', year:'numeric',
      hour:'2-digit', minute:'2-digit', hourCycle:'h23'
    }).format(date);
  } catch { return date.toISOString() + ' (UTC)'; }
}
async function showSelectedPeriod() {
  rows.replaceChildren();
  const selected = periods.find(x => x.periodId === period.value);
  if (!selected) { periodStatus.textContent = ''; return; }
  periodStatus.textContent = 'Trạng thái kỳ: ' + (labels[selected.status] || selected.status);
  const items = await api.request('/api/periods/' +
    encodeURIComponent(selected.periodId) + '/requirements');
  if (!Array.isArray(items)) throw new Error('Phản hồi yêu cầu nộp không hợp lệ.');
  for (const item of items) {
    const hasCurrent = item.activeBatchId !== null;
    const validCurrent = hasCurrent && item.activeBatchStatus === 'COMMITTED';
    const current = hasCurrent ? 'Phiên bản ' + item.activeVersionNo : 'Chưa có';
    const tr = document.createElement('tr');
    const values = [
      item.sourceCode,
      item.scopeCode === 'FINANCE_SHARED' ? 'Finance · 1000 và 6000' : item.scopeCode,
      dueLabel(item.dueAtUtc, item.timeZoneId),
      validCurrent ? 'Đã có bản xác nhận' :
        hasCurrent ? 'Cần kiểm tra bản đang dùng' : 'Chưa có bản xác nhận',
      current
    ];
    for (const value of values) {
      const td = document.createElement('td');
      td.textContent = value;
      tr.append(td);
    }
    rows.append(tr);
  }
}
async function load() {
  busy(true);
  workspace.hidden = true;
  rows.replaceChildren();
  status.textContent = 'Đang tải kỳ dữ liệu…';
  try {
    const user = await api.me();
    document.getElementById('identity-label').textContent =
      user.displayName + ' · ' + user.roles.join(', ');
    const data = await api.request('/api/periods');
    if (!Array.isArray(data)) throw new Error('Phản hồi danh sách kỳ không hợp lệ.');
    periods = data;
    period.replaceChildren();
    for (const item of periods) {
      const option = document.createElement('option');
      option.value = item.periodId;
      option.textContent = item.periodCode + ' — ' + (labels[item.status] || item.status);
      period.append(option);
    }
    empty.hidden = periods.length !== 0;
    await showSelectedPeriod();
    workspace.hidden = false;
    status.textContent = '';
  } catch (error) { fail(error); }
  finally { busy(false); }
}

reload.addEventListener('click', load);
period.addEventListener('change', async () => {
  busy(true);
  status.textContent = 'Đang tải yêu cầu nộp…';
  try { await showSelectedPeriod(); status.textContent = ''; }
  catch (error) { fail(error); }
  finally { busy(false); }
});
logout.addEventListener('click', async () => {
  logout.disabled = true;
  try { await api.logout(); location.replace('/login.html'); }
  catch (error) { fail(error); logout.disabled = false; }
});
await load();
