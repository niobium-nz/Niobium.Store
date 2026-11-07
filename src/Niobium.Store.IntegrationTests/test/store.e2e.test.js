import path from 'node:path';
import { describe, it, expect } from 'vitest';
import { approvedRecipient } from '../fixture-config.js';

// Validate before any request; the workflow supplies an approved test-only secret.
const testRecipient = approvedRecipient();

// Load helper scripts
await import(path.resolve(process.cwd(), '../../assets/quote.js'));
await import(path.resolve(process.cwd(), '../../assets/order.js'));
await import(path.resolve(process.cwd(), '../../assets/track.js'));

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
        const shippingCountry = process.env.COUNTRY || 'AUS';
        const coupon = process.env.COUPON || 'BUY1GET1FREE';
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

        const json = await requireJsonResponse(response, 'quote');
        const quote = json.quote ?? json.Quote;
        expect(Array.isArray(quote) && quote.length > 0).toBe(true);
    });
});

// Helpers for order test
function getOrderDetailsFromEnv() {
    const cart = getCartFromEnv();
    return {
        // required
        shippingId: Number(process.env.SHIPPING_ID || 1),
        shippingCountry: process.env.COUNTRY || 'AUS',
        consignee: process.env.CONSIGNEE || 'John Doe',
        email: testRecipient,
        shippingAddressLine1: process.env.SHIP_ADDR1 || '123 Queen St',
        shippingCity: process.env.SHIP_CITY || 'Sydney',
        shippingPostcode: process.env.SHIP_POSTCODE || '1010',
        billingName: process.env.BILL_NAME || 'John Doe',
        billingAddressLine1: process.env.BILL_ADDR1 || '123 Queen St',
        billingCity: process.env.BILL_CITY || 'Sydney',
        billingCountry: process.env.BILL_COUNTRY || 'AUS',
        billingPostcode: process.env.BILL_POSTCODE || '1010',
        cart,

        // optional
        coupon: process.env.COUPON || "BUY1GET1FREE",
        notes: process.env.NOTES || "Please left at front door.",
        phone: process.env.PHONE || "0216543210",
        shippingAddressLine2: process.env.SHIP_ADDR2 || undefined,
        shippingSuburb: process.env.SHIP_SUBURB || "City Centre",
        shippingState: process.env.SHIP_STATE || undefined,
        billingBusiness: process.env.BILL_BUSINESS || "Mlgb Limited",
        billingAddressLine2: process.env.BILL_ADDR2 || undefined,
        billingSuburb: process.env.BILL_SUBURB || "City Centre",
        billingState: process.env.BILL_STATE || undefined,
        marketingSubscription: false,
        track: process.env.TRACK || "fb-123",

        // Optional overrides (defaults exist in code if omitted)
        culture: process.env.CULTURE || 'en-AU',
        timeZone: process.env.TIMEZONE || 'Australia/Sydney',
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

        const json = await requireJsonResponse(response, 'order');
        const orderId = Number(json.order ?? json.Order);
        expect(Number.isSafeInteger(orderId) && orderId > 0).toBe(true);

        const trackResponse = await globalThis.niobium.store.trackOrder(
            recaptchaKey,
            { email: details.email, order: orderId },
            apiUrl,
            true
        );
        const tracked = await requireJsonResponse(trackResponse, 'tracking');
        const trackedCart = tracked.cart ?? tracked.Cart;
        expect(Array.isArray(trackedCart) && trackedCart.length > 0).toBe(true);
    });
});

async function requireJsonResponse(response, operation) {
    expect(response.status, `${operation}_status`).toBe(200);
    let json;
    try {
        json = await response.json();
    } catch {
        // Do not include response bodies or recipient information in failure logs.
        throw new Error(`${operation}_invalid_json`);
    }
    expect(json !== null && typeof json === 'object' && !Array.isArray(json), `${operation}_object`).toBe(true);
    return json;
}
