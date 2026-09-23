import { writeFileSync } from 'node:fs';

const apiBaseUrl = process.env.API_BASE_URL || '/api';
const enabled = process.env.GOOGLE_SIGN_IN_ENABLED === 'true';
const clientId = process.env.GOOGLE_CLIENT_ID || '';
if (apiBaseUrl !== '/api' && !/^https:\/\/[^/]+\/api\/?$/.test(apiBaseUrl)) {
  throw new Error('API_BASE_URL must be /api or an explicit HTTPS API origin ending in /api.');
}
if (enabled && !/^[a-zA-Z0-9-]+\.apps\.googleusercontent\.com$/.test(clientId)) {
  throw new Error('Enabled Google Sign-In requires a valid public Google Client ID.');
}
writeFileSync('src/environments/environment.ts', `export const environment = ${JSON.stringify({
  production: true, apiBaseUrl, googleSignIn: { enabled, clientId }
}, null, 2)};\n`);
