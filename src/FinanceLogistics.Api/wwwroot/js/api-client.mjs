import { ApiError, readResponse } from './identity-utils.mjs';

export function createIdentityClient({ fetchImpl = globalThis.fetch.bind(globalThis), now = () => Date.now() } = {}) {
  let token = null, expiresAt = 0, user = null, csrfToken = null, renewal = null;

  async function raw(path, options = {}) {
    if (!path.startsWith('/api/')) throw new Error('Only relative application API paths are supported.');
    let response;
    try { response = await fetchImpl(path, { credentials: 'same-origin', cache: 'no-store', ...options }); }
    catch { throw new ApiError('NETWORK_ERROR', 'Không thể kết nối đến máy chủ.'); }
    return readResponse(response);
  }

  function clearMemory() { token = null; expiresAt = 0; user = null; }
  function accept(data, expectedUserId = null) {
    if (!data || typeof data.token !== 'string' || !data.token || data.tokenType !== 'Bearer'
        || !data.user || typeof data.user.userId !== 'string' || !Array.isArray(data.user.roles)
        || !Array.isArray(data.user.sites) || !Array.isArray(data.user.permissions)
        || !Number.isFinite(Date.parse(data.expiresAtUtc)) || Date.parse(data.expiresAtUtc) <= now()) {
      clearMemory(); throw new ApiError('INVALID_AUTH_RESPONSE', 'Phản hồi đăng nhập không đúng hợp đồng.');
    }
    if (expectedUserId && expectedUserId !== data.user.userId) {
      clearMemory(); throw new ApiError('SESSION_CHANGED', 'Tài khoản trong trình duyệt đã thay đổi. Vui lòng tải lại trang.', 401);
    }
    token = data.token; expiresAt = Date.parse(data.expiresAtUtc); user = data.user;
    return user;
  }

  async function sessionRequest(action, body, retryCsrf = true) {
    if (!csrfToken) {
      const result = await raw('/api/auth/csrf');
      if (!result?.requestToken || result.headerName !== 'X-CSRF-TOKEN')
        throw new ApiError('INVALID_CSRF_RESPONSE', 'Không thể khởi tạo phiên đăng nhập.');
      csrfToken = result.requestToken;
    }
    try {
      return await raw(`/api/auth/${action}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken },
        ...(body === undefined ? {} : { body: JSON.stringify(body) })
      });
    } catch (error) {
      // The CSRF filter rejects before invoking the action; this specific retry cannot repeat a login operation.
      if (error.code === 'CSRF_INVALID' && retryCsrf) {
        csrfToken = null; return sessionRequest(action, body, false);
      }
      throw error;
    }
  }

  async function restore() {
    if (!renewal) {
      const expectedUserId = user?.userId;
      renewal = sessionRequest('token').then(data => accept(data, expectedUserId)).catch(error => {
        if (error.status === 401 || error.status === 403) clearMemory();
        throw error;
      }).finally(() => { renewal = null; });
    }
    return renewal;
  }

  async function request(path, options = {}) {
    if (!token || expiresAt - now() < 30000) await restore();
    const execute = () => raw(path, {
      ...options, headers: { ...(options.headers || {}), Authorization: `Bearer ${token}` }
    });
    try { return await execute(); }
    catch (error) {
      // Only reads are retried. Upload/confirm operations will need their own idempotency contract.
      if (error.status === 401 && (!options.method || options.method.toUpperCase() === 'GET')) {
        await restore(); return execute();
      }
      throw error;
    }
  }

  return {
    async login(credentials) { return accept(await sessionRequest('login', credentials)); },
    restore,
    request,
    async me() { const current = await request('/api/me'); user = current; return current; },
    async sources() { return request('/api/me/source-access'); },
    async logout() { await sessionRequest('logout'); clearMemory(); csrfToken = null; }
  };
}
