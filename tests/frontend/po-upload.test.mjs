import test from 'node:test';
import assert from 'node:assert/strict';
import { createIdentityClient } from '../../src/FinanceLogistics.Api/wwwroot/js/api-client.mjs';
import { canConfirm, needsPolling, previewCells } from '../../src/FinanceLogistics.Api/wwwroot/js/po-ui-rules.mjs';
const fixedNow = Date.parse('2026-10-06T08:00:00Z');
const user = { userId: '7', roles: ['FINANCE'], sites: ['1000', '6000'], permissions: ['sources.read', 'sources.upload', 'sources.confirm'] };
const auth = { token: 'fake-test-token', tokenType: 'Bearer', expiresAtUtc: '2026-10-06T09:00:00Z', user };
const json = (data, status = 200) => new Response(JSON.stringify(data), { status });
function queued(responses) {
  const calls = [];
  const client = createIdentityClient({ now: () => fixedNow, fetchImpl: async (path, options) => {
    calls.push({ path, options }); const value = responses.shift();
    if (!value) throw new Error('Unexpected request'); return value;
  } });
  return { client, calls };
}
const session = () => [json({ requestToken: 'fake-csrf', headerName: 'X-CSRF-TOKEN' }), json(auth)];
test('download bảo toàn bytes và vẫn gửi bearer', async () => {
  const bytes = new Uint8Array([0, 255, 13, 10, 65]);
  const { client, calls } = queued([...session(), new Response(bytes)]);
  const blob = await client.download('/api/po/batches/7/original');
  assert.deepEqual(new Uint8Array(await blob.arrayBuffer()), bytes);
  assert.equal(calls.at(-1).options.headers.Authorization, 'Bearer fake-test-token');
});
test('download bị từ chối trả lỗi API, không tải nhầm JSON thành Excel', async () => {
  const { client } = queued([...session(), json({ code: 'SOURCE_FORBIDDEN', message: 'No', correlationId: 'c1' }, 403)]);
  await assert.rejects(client.download('/api/po/batches/7/original'), e => e.status === 403 && e.code === 'SOURCE_FORBIDDEN');
});
test('download GET khôi phục phiên tối đa một lần sau 401', async () => {
  const { client, calls } = queued([...session(), json({ code: 'AUTH_REQUIRED' }, 401), json(auth), new Response('bytes')]);
  assert.equal(await (await client.download('/api/po/batches/7/original')).text(), 'bytes');
  assert.equal(calls.filter(x => x.path.endsWith('/original')).length, 2);
});
test('upload FormData giữ boundary cho trình duyệt và không tự retry POST', async () => {
  const form = new FormData(); form.append('file', new Blob(['a,b']), 'po.csv'); form.append('clientUploadId', 'one-logical-request');
  const { client, calls } = queued([...session(), json({ code: 'AUTH_REQUIRED' }, 401)]);
  await assert.rejects(client.request('/api/po/requirements/1/uploads', { method: 'POST', body: form }), e => e.status === 401);
  const uploads = calls.filter(x => x.path.endsWith('/uploads'));
  assert.equal(uploads.length, 1); assert.equal(uploads[0].options.body, form);
  assert.equal(uploads[0].options.headers['Content-Type'], undefined);
});
const ready = { status: 'READY', periodStatus: 'OPEN', totalRows: 2, errorCount: 0, warningCount: 0, jobStatus: 'SUCCEEDED' };
test('nút xác nhận chỉ bật cho READY đủ điều kiện', () => {
  assert.equal(canConfirm(ready, false), true);
  for (const change of [{ status: 'RECEIVED' }, { status: 'INVALID' }, { status: 'COMMITTED' }, { totalRows: 0 },
    { errorCount: 1 }, { periodStatus: 'CLOSED' }, { jobStatus: 'FAILED' }]) assert.equal(canConfirm({ ...ready, ...change }, true), false);
});
test('preview cũ phải tải lại sau xung đột; tick cảnh báo không bỏ qua xung đột', () => {
  assert.equal(canConfirm(ready, true, true), false);
  assert.equal(canConfirm({ ...ready, warningCount: 1 }, false), false);
  assert.equal(canConfirm({ ...ready, warningCount: 1 }, true), true);
});
test('polling dừng khi job đã FAILED hoặc file đã kiểm tra xong', () => {
  assert.equal(needsPolling({ status: 'VALIDATING', jobStatus: 'RUNNING' }), true);
  assert.equal(needsPolling({ status: 'VALIDATING', jobStatus: 'FAILED' }), false);
  assert.equal(needsPolling(ready), false);
});
test('preview raw theo đúng tên cột dù nguồn đổi vị trí; số 0 không thành trống', () => {
  const row = { rawCells: [{ header: 'PO Num', text: 'P1' }, { header: 'Receipt Num', text: '0001' }, { header: 'Receipt QTY', text: '0' }], normalizedCells: ['0001', 'P1', '0'] };
  assert.deepEqual(previewCells(row, ['Receipt Num', 'PO Num', 'Receipt QTY'], true), ['0001', 'P1', '0']);
  assert.deepEqual(previewCells(row, [], false), ['0001', 'P1', '0']);
});
