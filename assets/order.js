/**
 * @typedef {Object} OrderCartItem
 * @property {number} Listing
 * @property {string=} Option
 * @property {number} Quantity
 */

/**
 * @typedef {Object} OrderRequestData
 * @property {string} ID
 * @property {string} Tenant
 * @property {number} Shipping
 * @property {string} ShippingCountry
 * @property {string=} Coupon
 * @property {number} Timestamp
 * @property {string} Culture
 * @property {string} TimeZone
 * @property {string} Consignee
 * @property {string} Email
 * @property {string=} Phone
 * @property {string} ShippingAddressLine1
 * @property {string=} ShippingAddressLine2
 * @property {string=} ShippingSuburb
 * @property {string} ShippingCity
 * @property {string=} ShippingState
 * @property {string} ShippingPostcode
 * @property {string} BillingName
 * @property {string=} BillingBusiness
 * @property {string} BillingAddressLine1
 * @property {string=} BillingAddressLine2
 * @property {string=} BillingSuburb
 * @property {string} BillingCity
 * @property {string=} BillingState
 * @property {string} BillingCountry
 * @property {string} BillingPostcode
 * @property {boolean=} MarketingSubscription
 * @property {string=} Notes
 * @property {string=} Track
 * @property {OrderCartItem[]} Cart
 * @property {string} Captcha
 */

/*
    * Consumer Example

    async function handleMakeOrder() {
      try {
        const response = await niobium.store.makeOrder(
          "your-recaptcha-key",
          "your-tenant-guid",
          {
            // Required
            shippingId: 10, // or: shipping: 10
            shippingCountry: "US",
            consignee: "John Doe",
            email: "john@example.com",
            shippingAddressLine1: "123 Main St",
            shippingCity: "City",
            shippingPostcode: "10001",
            billingName: "John Doe",
            billingAddressLine1: "123 Main St",
            billingCity: "City",
            billingCountry: "US",
            billingPostcode: "10001",
            cart: [
              { Listing: 1, Option: "Default", Quantity: 2 },
              { Listing: 2, Option: "Default", Quantity: 1 }
            ],

            // Optional
            coupon: "WELCOME10",
            notes: "Leave at the front door.",
            phone: "+11234567890",
            shippingAddressLine2: "Unit 4",
            shippingSuburb: "Downtown",
            shippingState: "NY",
            billingBusiness: "ACME Inc.",
            billingAddressLine2: "Suite 5",
            billingSuburb: "Midtown",
            billingState: "NY",
            marketingSubscription: true,
            track: "adSrc01",

            // Optional overrides (defaults provided if omitted)
            culture: navigator.language || 'en-US',
            timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC',
          }
        );

        if (!response.ok) {
          console.error("Order request failed", response.status, await response.text());
          return;
        }

        const orderResponse = await response.json();
        console.log("Order response", orderResponse);
      } catch (error) {
        console.error("An error occurred while placing order.", error);
      }
    }
 */

(function (global) {
    "use strict";

    // Create/resolve namespace: niobium.store
    const niobium = (global.niobium = global.niobium || {});
    const storeNS = (niobium.store = niobium.store || {});

    // --- Helpers duplicated locally so this script can work standalone ---

    /**
     * Generates a compliant globally unique identifier (GUID).
     * @returns {string} The generated GUID.
     */
    niobium.generateGUID = niobium.generateGUID || function generateGUID() {
        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
            const r = (Math.random() * 16) | 0;
            const v = c === "x" ? r : (r & 0x3) | 0x8;
            return v.toString(16);
        });
    };

    /**
     * Executes a fetch request with a retry mechanism.
     * @param {string} url The URL to send the request to.
     * @param {RequestInit} options The fetch options.
     * @param {number} [retries=3] The maximum number of retry attempts.
     * @returns {Promise<Response>} The fetch response.
     */
    niobium.fetchWithRetry = niobium.fetchWithRetry || async function fetchWithRetry(url, options, retries = 3) {
        try {
            const response = await fetch(url, options);

            if (!response.ok && retries > 0) {
                console.warn(`Fetch failed with status ${response.status}. Retrying...`);
                const delay = 1000 * (4 - retries);
                await new Promise((resolve) => setTimeout(resolve, delay));
                return await niobium.fetchWithRetry(url, options, retries - 1);
            }
            return response;
        } catch (error) {
            if (retries > 0) {
                console.warn("Fetch failed due to network error. Retrying...", error);
                const delay = 1000 * (4 - retries);
                await new Promise((resolve) => setTimeout(resolve, delay));
                return await niobium.fetchWithRetry(url, options, retries - 1);
            }
            throw error;
        }
    };

    /**
     * Wraps grecaptcha.ready() in a Promise.
     * @returns {Promise<void>}
     */
    niobium.reCaptchaReady = niobium.reCaptchaReady || function reCaptchaReady() {
        return new Promise((resolve) => {
            if (typeof global.grecaptcha !== "undefined" && global.grecaptcha.ready) {
                global.grecaptcha.ready(resolve);
            } else {
                const interval = setInterval(() => {
                    if (typeof global.grecaptcha !== "undefined" && global.grecaptcha.ready) {
                        clearInterval(interval);
                        global.grecaptcha.ready(resolve);
                    }
                }, 50);
            }
        });
    };

    /**
     * Generates a reCAPTCHA v3 token using async/await.
     * @param {string} siteKey - Your reCAPTCHA site key.
     * @param {string} action - The action name for this request.
     * @returns {Promise<string>} The reCAPTCHA token.
     */
    niobium.getRecaptchaToken = niobium.getRecaptchaToken || async function getRecaptchaToken(siteKey, action) {
        await niobium.reCaptchaReady();
        const token = await global.grecaptcha.execute(siteKey, { action: action });
        return token;
    };

    /**
     * Places an order after executing reCAPTCHA.
     * @param {string} reCaptchaPublicKey The reCAPTCHA public key.
     * @param {string} tenant The tenant GUID.
     * @param {{
     *   // Required
     *   shippingId?: number,
     *   shipping?: number,
     *   shippingCountry: string,
     *   consignee: string,
     *   email: string,
     *   shippingAddressLine1: string,
     *   shippingCity: string,
     *   shippingPostcode: string,
     *   billingName: string,
     *   billingAddressLine1: string,
     *   billingCity: string,
     *   billingCountry: string,
     *   billingPostcode: string,
     *   cart: OrderCartItem[],
     *   // Optional
     *   coupon?: string,
     *   notes?: string,
     *   phone?: string,
     *   shippingAddressLine2?: string,
     *   shippingSuburb?: string,
     *   shippingState?: string,
     *   billingBusiness?: string,
     *   billingAddressLine2?: string,
     *   billingSuburb?: string,
     *   billingState?: string,
     *   marketingSubscription?: boolean,
     *   track?: string,
     *   culture?: string,
     *   timeZone?: string,
     * }} details Order details.
     * @param {string=} baseUrl The API base URL (defaults to "/api").
     * @param {boolean=} localTest Whether testing locally (adds Referer header for some tunnels).
     * @returns {Promise<Response>} The fetch response promise.
     */
    async function makeOrder(reCaptchaPublicKey, tenant, details, baseUrl, localTest = false) {
        if (!details || !Array.isArray(details.cart) || details.cart.length <= 0)
            throw new Error("Cart must be a non-empty array.");

        let token;
        try {
            token = await niobium.getRecaptchaToken(reCaptchaPublicKey, "order");
        } catch (error) {
            return Promise.reject(new Error("reCAPTCHA execution failed."));
        }

        /** @type {OrderRequestData} */
        const data = {
            ID: niobium.generateGUID(),
            Tenant: tenant,
            Shipping: (details.shipping ?? details.shippingId),
            ShippingCountry: details.shippingCountry,
            Coupon: details.coupon,
            Captcha: token,
            Timestamp: Date.now(),
            Culture: details.culture || (navigator.language || 'en-US'),
            TimeZone: details.timeZone || (Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'),
            Consignee: details.consignee,
            Email: details.email,
            Phone: details.phone,
            Notes: details.notes,
            ShippingAddressLine1: details.shippingAddressLine1,
            ShippingAddressLine2: details.shippingAddressLine2,
            ShippingSuburb: details.shippingSuburb,
            ShippingCity: details.shippingCity,
            ShippingState: details.shippingState,
            ShippingPostcode: details.shippingPostcode,
            BillingName: details.billingName,
            BillingBusiness: details.billingBusiness,
            BillingAddressLine1: details.billingAddressLine1,
            BillingAddressLine2: details.billingAddressLine2,
            BillingSuburb: details.billingSuburb,
            BillingCity: details.billingCity,
            BillingState: details.billingState,
            BillingCountry: details.billingCountry,
            BillingPostcode: details.billingPostcode,
            MarketingSubscription: !!details.marketingSubscription,
            Track: details.track,
            Cart: details.cart,
        };

        const headers = { "Content-Type": "application/json" };
        if (localTest) {
            headers["Referer"] = "http://127.0.0.1:3000/";
        }

        const options = {
            method: "POST",
            headers: headers,
            body: JSON.stringify(data),
        };

        const url = (baseUrl || "/api") + "/orders";
        return await niobium.fetchWithRetry(url, options);
    }

    // Public API
    storeNS.makeOrder = makeOrder;
})(typeof window !== "undefined" ? window : globalThis);