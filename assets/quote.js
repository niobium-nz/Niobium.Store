/**
 * @typedef {Object} QuoteCartItem
 * @property {number} Listing
 * @property {string=} Option
 * @property {number} Quantity
 */

/**
 * @typedef {Object} QuoteRequestData
 * @property {string} ID
 * @property {string} Tenant
 * @property {number} Shipping
 * @property {string} ShippingCountry
 * @property {string=} Coupon
 * @property {QuoteCartItem[]} Cart
 * @property {string} Captcha
 */

/*
    * Consumer Example

    async function handleQuoteRequest() {
      try {
        const response = await niobium.store.getQuote(
          "your-recaptcha-key",
          "your-tenant-guid",
          10,                 // shipping option ID
          "US",              // shipping country code
          [                   // cart items
            { Listing: 1, Option: "Default", Quantity: 2 },
            { Listing: 2, Option: "Default", Quantity: 1 },
          ],
          "BUY1GET1FREE"    // optional coupon (or undefined/null)
        );

        if (!response.ok) {
          console.error("Quote request failed", response.status, await response.text());
          return;
        }

        const quote = await response.json();
        console.log("Quote response", quote);
      } catch (error) {
        console.error("An error occurred during quote request.", error);
      } finally {
        // cleanup or final actions
      }
    }
 */

(function (global) {
    "use strict";

    // Create/resolve namespace: niobium.store
    const niobium = (global.niobium = global.niobium || {});
    const storeNS = (niobium.store = niobium.store || {});

    // --- Helpers moved to global.niobium namespace ---

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
     * Allows passing a function that will be invoked on each attempt to produce fresh options (e.g., for reCAPTCHA tokens).
     * @param {string} url The URL to send the request to.
     * @param {RequestInit|(() => Promise<RequestInit>|RequestInit)} options The fetch options or a factory returning options per attempt.
     * @param {number} [retries=3] The maximum number of retry attempts.
     * @returns {Promise<Response>} The fetch response.
     */
    niobium.fetchWithRetry = niobium.fetchWithRetry || async function fetchWithRetry(url, options, retries = 3) {
        const resolveOptions = async () => (typeof options === "function" ? await /** @type {any} */ (options)() : options);

        try {
            const currentOptions = await resolveOptions();
            const response = await fetch(url, currentOptions);

            if (!response.ok && retries > 0) {
                console.warn(`Fetch failed with status ${response.status}. Retrying...`);
                const delay = 1000 * (4 - retries);
                await new Promise((resolve) => setTimeout(resolve, delay));
                // Recurse with the original options reference so a factory can produce fresh values on each attempt
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
     * Requests a quote from the server after executing reCAPTCHA.
     * Ensures a fresh reCAPTCHA token is generated for every retry attempt.
     * @param {string} reCaptchaPublicKey The reCAPTCHA public key.
     * @param {string} tenant The tenant GUID.
     * @param {number} shippingId The shipping option ID.
     * @param {string} shippingCountry The destination country code.
     * @param {QuoteCartItem[]} cart The cart items.
     * @param {string=} coupon Optional coupon code.
     * @param {string=} baseUrl The API base URL (defaults to "/api").
     * @param {boolean=} localTest Whether testing locally (adds Referer header for some tunnels).
     * @returns {Promise<Response>} The fetch response promise.
     */
    async function getQuote(
        reCaptchaPublicKey,
        tenant,
        shippingId,
        shippingCountry,
        cart,
        coupon,
        baseUrl,
        localTest = false
    ) {
        if (!Array.isArray(cart) || cart.length <= 0)
            throw new Error("Cart must be a non-empty array.");

        // Keep request identity and payload stable across retries except for Captcha token
        const stableId = niobium.generateGUID();
        const headers = { "Content-Type": "application/json" };
        if (localTest) {
            // Note: Some environments forbid setting Referer; browsers will ignore this header.
            headers["Referer"] = "http://127.0.0.1:3000/";
        }

        /**
         * Build fresh RequestInit with a new reCAPTCHA token on every attempt
         * @returns {Promise<RequestInit>}
         */
        const buildOptions = async () => {
            let token;
            try {
                token = await niobium.getRecaptchaToken(reCaptchaPublicKey, "quote");
            } catch (error) {
                // Surface a consistent error when reCAPTCHA fails before contacting server
                throw new Error("reCAPTCHA execution failed.");
            }

            /** @type {QuoteRequestData} */
            const data = {
                ID: stableId,
                Tenant: tenant,
                Shipping: shippingId,
                ShippingCountry: shippingCountry,
                Coupon: coupon ?? undefined,
                Cart: cart,
                Captcha: token,
            };

            return {
                method: "POST",
                headers: headers,
                body: JSON.stringify(data),
            };
        };

        const url = (baseUrl || "/api/store") + "/quote";
        // Pass the options factory so each retry gets a fresh token
        return await niobium.fetchWithRetry(url, buildOptions);
    }

    // Public API
    storeNS.getQuote = getQuote;
})(typeof window !== "undefined" ? window : globalThis);
