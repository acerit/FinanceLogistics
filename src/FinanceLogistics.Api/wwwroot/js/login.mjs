import { createIdentityClient } from './api-client.mjs';
import { credentialsFromInputs, safeReturnPath } from './identity-utils.mjs';

const api = createIdentityClient();
const form = document.getElementById('login-form');
const status = document.getElementById('status');
const button = document.getElementById('submit');

form.addEventListener('submit', async event => {
  event.preventDefault();
  if (button.disabled) return;
  button.disabled = true; status.textContent = 'Đang xác thực tài khoản…';
  const password = document.getElementById('password');
  try {
    await api.login(credentialsFromInputs(document.getElementById('username').value, password.value));
    password.value = '';
    const next = new URLSearchParams(location.search).get('returnUrl');
    location.replace(safeReturnPath(next, location.origin));
  } catch (error) {
    password.value = '';
    status.textContent = error.message + (error.correlationId ? ` Mã yêu cầu: ${error.correlationId}` : '');
    button.disabled = false;
  }
});
