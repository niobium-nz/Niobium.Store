import path from 'node:path';
import { describe, it, expect } from 'vitest';

// Load the helper script which attaches to global.niobium.store
await import(path.resolve(process.cwd(), '../assets/quote.js'));

// Ensure Node 18+ provides global fetch
if (typeof fetch !== 'function') {
  throw new Error('Global fetch is required (Node 18+).');
}

// Optional: allow cart to be passed via environment as JSON string
function getCartFromEnv() {
  const raw = process.env.CART;
  if (!raw) {
    return [
      { Listing: 1, Option: 'Default', Quantity: 2 },
      { Listing: 2, Option: 'Default', Quantity: 1 },
    ];
  }
  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) return parsed;
  } catch (_) {
    // ignore parse errors and fall back
  }
  return [
    { Listing: 1, Option: 'Default', Quantity: 2 },
    { Listing: 2, Option: 'Default', Quantity: 1 },
  ];
}

describe('Store quote end-to-end tests', () => {
  it('quote.js posts quote data and receives a successful response', async () => {
    const apiUrl = process.env.API_URL;
    if (!apiUrl) {
      throw new Error('API_URL environment variable is required.');
    }

    const recaptchaKey = process.env.RECAPTCHA_KEY || 'test-recaptcha-key';
    const tenant = process.env.TENANT || 'de8b3e50-abf6-4cdd-9269-f2472a1020ad';
    const shippingId = Number(process.env.SHIPPING_ID || 1);
    const shippingCountry = process.env.COUNTRY || 'NZL';
    const coupon = process.env.COUPON || undefined;
    const cart = getCartFromEnv();

    const response = await globalThis.niobium.store.getQuote(
      recaptchaKey,
      tenant,
      shippingId,
      shippingCountry,
      cart,
      coupon,
      apiUrl,
      true
    );

    // allow common success codes; adjust as needed for your API
    expect([200, 201, 202, 204]).toContain(response.status);

    // Try to parse JSON response if present
    const text = await response.text();
    if (text) {
      try {
        const json = JSON.parse(text);
        expect(json).toBeDefined();
      } catch (_) {
        // Not JSON; ignore
      }
    }
  });
});
