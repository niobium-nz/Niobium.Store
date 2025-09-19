import path from 'node:path';
import { describe, it, expect } from 'vitest';

// Load helper scripts
await import(path.resolve(process.cwd(), '../assets/quote.js'));
await import(path.resolve(process.cwd(), '../assets/order.js'));

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
    const shippingCountry = process.env.COUNTRY || 'US';
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

// Helpers for order test
function getOrderDetailsFromEnv() {
  const cart = getCartFromEnv();
  return {
    shippingId: Number(process.env.SHIPPING_ID || 10),
    shippingCountry: process.env.COUNTRY || 'US',
    consignee: process.env.CONSIGNEE || 'John Doe',
    email: process.env.EMAIL || 'hcp5he11@gmail.com',
    shippingAddressLine1: process.env.SHIP_ADDR1 || '123 Main St',
    shippingCity: process.env.SHIP_CITY || 'City',
    shippingPostcode: process.env.SHIP_POSTCODE || '10001',
    shippingState: process.env.SHIP_STATE || undefined,
    billingName: process.env.BILL_NAME || 'John Doe',
    billingAddressLine1: process.env.BILL_ADDR1 || '123 Main St',
    billingCity: process.env.BILL_CITY || 'City',
    billingCountry: process.env.BILL_COUNTRY || 'US',
    billingPostcode: process.env.BILL_POSTCODE || '10001',
    cart,
    culture: process.env.CULTURE || 'en-US',
    timeZone: process.env.TIMEZONE || 'UTC',
  };
}

describe('Store order end-to-end tests', () => {
  it('order.js posts order data and receives a successful response', async () => {
    const apiUrl = process.env.API_URL;
    if (!apiUrl) {
      throw new Error('API_URL environment variable is required.');
    }

    const recaptchaKey = process.env.RECAPTCHA_KEY || 'test-recaptcha-key';
    const tenant = process.env.TENANT || 'de8b3e50-abf6-4cdd-9269-f2472a1020ad';
    const details = getOrderDetailsFromEnv();

    const response = await globalThis.niobium.store.makeOrder(
      recaptchaKey,
      tenant,
      details,
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
