import { createIdentityClient } from './api-client.mjs';

const api = createIdentityClient();
const status = document.getElementById('status');
const content = document.getElementById('account');
const reload = document.getElementById('reload');
const logout = document.getElementById('logout');

async function load() {
  content.hidden = true; status.textContent = 'Đang kiểm tra tài khoản và phạm vi truy cập…';
  reload.disabled = true;
  try {
    const user = await api.me();
    const sources = await api.sources();
    document.getElementById('display-name').textContent = user.displayName;
    document.getElementById('username-label').textContent = user.upn || user.username;
    document.getElementById('roles').textContent = user.roles.join(', ') || 'Chưa được cấp quyền';
    document.getElementById('sites').textContent = user.sites.join(', ') || 'Không có phạm vi dữ liệu';
    const body = document.getElementById('source-rows'); body.replaceChildren();
    for (const source of sources) {
      const row = document.createElement('tr');
      for (const value of [source.sourceCode, source.scopeCode, source.sites.join(', ')]) {
        const cell = document.createElement('td'); cell.textContent = value; row.append(cell);
      }
      body.append(row);
    }
    document.getElementById('empty-sources').hidden = sources.length > 0;
    content.hidden = false; status.textContent = '';
  } catch (error) {
    if (error.status === 401) {
      location.replace('/login.html?returnUrl=%2Findex.html'); return;
    }
    status.textContent = error.message + (error.correlationId ? ` Mã yêu cầu: ${error.correlationId}` : '');
  } finally { reload.disabled = false; }
}

reload.addEventListener('click', load);
logout.addEventListener('click', async () => {
  logout.disabled = true;
  try { await api.logout(); location.replace('/login.html'); }
  catch (error) { status.textContent = error.message; logout.disabled = false; }
});
await load();
