import test from 'node:test';
import assert from 'node:assert/strict';
import { credentialsFromInputs, safeReturnPath, readResponse } from '../../src/FinanceLogistics.Api/wwwroot/js/identity-utils.mjs';
import { createIdentityClient } from '../../src/FinanceLogistics.Api/wwwroot/js/api-client.mjs';

const fixedNow = Date.parse('2026-09-23T10:00:00Z');
const user = { userId: '7', username: 'sample', displayName: 'Sample User', roles: ['LOGISTICS'], sites: ['1000'], permissions: ['sources.read'] };
const auth = { token: 'test-access-token', tokenType: 'Bearer', expiresAtUtc: '2026-09-23T10:10:00Z', sessionExpiresAtUtc: '2026-09-23T12:00:00Z', user };
const csrf = { requestToken: 'test-csrf-token', headerName: 'X-CSRF-TOKEN' };
function response(data, status = 200) { return new Response(status === 204 ? null : JSON.stringify(data), { status }); }
function clientWithQueue(queue) {
  const calls = [];
  const client = createIdentityClient({ now: () => fixedNow, fetchImpl: async (path, options) => {
    calls.push({ path, options });
    const next = queue.shift();
    if (!next) throw new Error('Unexpected request');
    if (next instanceof Error) throw next;
    return next;
  } });
  return { client, calls };
}

test('username được trim; mật khẩu giữ nguyên cả khoảng trắng', () => {
  assert.deepEqual(credentialsFromInputs(' sample@example.test ', ' pass word '), { username: 'sample@example.test', password: ' pass word ' });
});
for (const candidate of ['https://other.example/steal', '//other.example', 'javascript:alert(1)', '/admin.html', '/login.html', 'https://user:pass@app.example/index.html'])
  test(`chặn return URL không được phép: ${candidate}`, () => {
    assert.equal(safeReturnPath(candidate, 'https://app.example'), '/index.html');
  });
test('return URL cùng origin hợp lệ được bỏ query và fragment', () => {
  assert.equal(safeReturnPath('/?untrusted=x#fragment', 'https://app.example'), '/');
});
test('HTML lỗi từ proxy không bị báo nhầm là lỗi kết nối mạng', async () => {
  await assert.rejects(readResponse(new Response('<html>Bad gateway</html>', { status: 502 })), e => e.code === 'INVALID_SERVER_RESPONSE' && e.status === 502);
});
test('401 giữ mã nghiệp vụ và correlationId', async () => {
  await assert.rejects(readResponse(response({ code: 'INVALID_CREDENTIALS', message: 'Invalid', correlationId: 'r1' }, 401)), e => e.status === 401 && e.correlationId === 'r1');
});
test('login gửi password chính xác và CSRF; không tự retry khi sai mật khẩu', async () => {
  const { client, calls } = clientWithQueue([response(csrf), response({ code: 'INVALID_CREDENTIALS', message: 'Invalid' }, 401)]);
  await assert.rejects(client.login({ username: 'sample', password: ' exact ' }), e => e.code === 'INVALID_CREDENTIALS');
  assert.equal(calls.length, 2);
  assert.equal(JSON.parse(calls[1].options.body).password, ' exact ');
  assert.equal(calls[1].options.headers['X-CSRF-TOKEN'], csrf.requestToken);
  assert.equal(calls[1].options.headers.Authorization, undefined);
});
test('khôi phục sau reload lấy token bằng cookie rồi gọi /me bằng bearer', async () => {
  const { client, calls } = clientWithQueue([response(csrf), response(auth), response(user)]);
  assert.deepEqual(await client.me(), user);
  assert.deepEqual(calls.map(c => c.path), ['/api/auth/csrf', '/api/auth/token', '/api/me']);
  assert.equal(calls[1].options.credentials, 'same-origin');
  assert.equal(calls[2].options.headers.Authorization, 'Bearer test-access-token');
});
test('hai request cùng lúc chỉ khôi phục phiên một lần', async () => {
  const { client, calls } = clientWithQueue([response(csrf), response(auth)]);
  await Promise.all([client.restore(), client.restore()]);
  assert.equal(calls.filter(c => c.path === '/api/auth/token').length, 1);
});
test('không tự retry thao tác ghi khi API trả 401', async () => {
  const { client, calls } = clientWithQueue([response(csrf), response(auth), response({ code: 'AUTH_REQUIRED', message: 'Login' }, 401)]);
  await client.restore();
  await assert.rejects(client.request('/api/future-imports', { method: 'POST', body: 'test' }), e => e.status === 401);
  assert.equal(calls.filter(c => c.path === '/api/future-imports').length, 1);
  assert.equal(calls.filter(c => c.path === '/api/auth/token').length, 1);
});
test('đọc bị 401 chỉ được thử lại sau khi khôi phục phiên', async () => {
  const { client, calls } = clientWithQueue([response(csrf), response(auth), response({ code: 'AUTH_REQUIRED' }, 401), response(auth), response(user)]);
  await client.restore(); assert.deepEqual(await client.me(), user);
  assert.equal(calls.filter(c => c.path === '/api/me').length, 2);
});
test('không âm thầm đổi sang user khác khi phiên ở tab khác đổi', async () => {
  const other = { ...auth, user: { ...user, userId: '99' } };
  const { client } = clientWithQueue([response(csrf), response(auth), response(other)]);
  await client.restore(); await assert.rejects(client.restore(), e => e.code === 'SESSION_CHANGED');
});
test('CSRF lỗi được lấy lại một lần trước khi gửi lại login', async () => {
  const { client, calls } = clientWithQueue([response(csrf), response({ code: 'CSRF_INVALID' }, 400), response(csrf), response(auth)]);
  await client.login({ username: 'sample', password: 'example' });
  assert.equal(calls.filter(c => c.path === '/api/auth/csrf').length, 2);
});
test('lỗi mạng được phân biệt với sai tài khoản', async () => {
  const { client } = clientWithQueue([new TypeError('network')]);
  await assert.rejects(client.restore(), e => e.code === 'NETWORK_ERROR' && e.status === 0);
});
test('phản hồi login thiếu token bị từ chối', async () => {
  const { client } = clientWithQueue([response(csrf), response({ user })]);
  await assert.rejects(client.login({ username: 'sample', password: 'example' }), e => e.code === 'INVALID_AUTH_RESPONSE');
});
test('logout 204 xóa token trong bộ nhớ; lần sau phải khôi phục lại', async () => {
  const { client, calls } = clientWithQueue([response(csrf), response(auth), response(null, 204), response(csrf), response({ code: 'SESSION_EXPIRED' }, 401)]);
  await client.restore(); await client.logout();
  await assert.rejects(client.me(), e => e.code === 'SESSION_EXPIRED');
  assert.equal(calls.filter(c => c.path === '/api/auth/token').length, 2);
});
