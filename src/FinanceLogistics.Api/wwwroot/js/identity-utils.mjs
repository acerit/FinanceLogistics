export function credentialsFromInputs(username, password) {
  return { username: username.trim(), password }; // A password must retain spaces exactly.
}

export function safeReturnPath(candidate, origin) {
  if (typeof candidate !== 'string' || !candidate.trim()) return '/index.html';
  try {
    const url = new URL(candidate, origin);
    // Identity slice has one destination. Extend this explicit set when real screens are added.
    const allowedPaths = new Set(['/', '/index.html']);
    if (url.origin !== new URL(origin).origin || url.username || url.password || !allowedPaths.has(url.pathname))
      return '/index.html';
    return url.pathname; // Do not forward arbitrary query parameters or fragments.
  } catch { return '/index.html'; }
}

export class ApiError extends Error {
  constructor(code, message, status = 0, correlationId = '') {
    super(message); this.name = 'ApiError'; this.code = code;
    this.status = status; this.correlationId = correlationId;
  }
}

export async function readResponse(response) {
  if (response.status === 204) return null;
  const text = await response.text();
  let data;
  try { data = JSON.parse(text); }
  catch { throw new ApiError('INVALID_SERVER_RESPONSE', 'Máy chủ trả về dữ liệu không đúng định dạng.', response.status); }
  if (!response.ok) throw new ApiError(data?.code || 'HTTP_ERROR', data?.message || 'Yêu cầu chưa thực hiện được.',
    response.status, data?.correlationId || '');
  return data;
}
