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

    const RECAPTCHA_SCRIPT_BASE_URL = "https://www.google.com/recaptcha/api.js";
    const RECAPTCHA_LOAD_TIMEOUT_MS = 10000;
    const RECAPTCHA_READY_TIMEOUT_MS = 10000;
    let reCaptchaScriptLoadPromise = null;
    let reCaptchaScriptLoadSiteKey = null;

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
     * Reads the reCAPTCHA site key from the order.js query string.
     * @returns {string}
     */
    function getConfiguredSiteKey() {
        if (typeof document === "undefined" || !document.currentScript || !document.currentScript.src) {
            return "";
        }

        try {
            const scriptUrl = document.currentScript.src;
            const urlParams = new URLSearchParams(new URL(scriptUrl).search);
            return (urlParams.get("siteKey") || "").trim();
        } catch (error) {
            return "";
        }
    }

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
     * Wraps a promise with a timeout.
     * @template T
     * @param {Promise<T>} promise The promise to wrap.
     * @param {number} timeoutMs The timeout in milliseconds.
     * @param {string} message The timeout error message.
     * @returns {Promise<T>}
     */
    niobium.withTimeout = niobium.withTimeout || function withTimeout(promise, timeoutMs, message) {
        return new Promise((resolve, reject) => {
            const timeoutId = setTimeout(() => reject(new Error(message)), timeoutMs);

            promise.then(
                (value) => {
                    clearTimeout(timeoutId);
                    resolve(value);
                },
                (error) => {
                    clearTimeout(timeoutId);
                    reject(error);
                }
            );
        });
    };

    /**
     * Ensures the Google reCAPTCHA v3 script is loaded.
     * @param {string} siteKey Your reCAPTCHA site key.
     * @returns {Promise<void>}
     */
    niobium.ensureRecaptchaScript = niobium.ensureRecaptchaScript || function ensureRecaptchaScript(siteKey) {
        if (typeof global.grecaptcha !== "undefined" && global.grecaptcha.ready) {
            return Promise.resolve();
        }

        if (!siteKey) {
            return Promise.reject(new Error("A reCAPTCHA site key is required to load the reCAPTCHA script."));
        }

        if (reCaptchaScriptLoadPromise) {
            if (reCaptchaScriptLoadSiteKey && reCaptchaScriptLoadSiteKey !== siteKey) {
                return Promise.reject(new Error("A different reCAPTCHA site key is already being used on this page."));
            }

            return reCaptchaScriptLoadPromise;
        }

        reCaptchaScriptLoadSiteKey = siteKey;

        reCaptchaScriptLoadPromise = niobium.withTimeout(new Promise((resolve, reject) => {
            if (typeof document === "undefined") {
                reject(new Error("Document is unavailable to load the reCAPTCHA script."));
                return;
            }

            const scriptUrl = `${RECAPTCHA_SCRIPT_BASE_URL}?render=${encodeURIComponent(siteKey)}`;
            const existingScript = document.querySelector(`script[src="${scriptUrl}"]`);

            if (existingScript) {
                if (typeof global.grecaptcha !== "undefined" && global.grecaptcha.ready) {
                    resolve();
                    return;
                }

                existingScript.addEventListener("load", () => resolve(), { once: true });
                existingScript.addEventListener("error", () => reject(new Error("Failed to load the reCAPTCHA script.")), { once: true });
                return;
            }

            const script = document.createElement("script");
            script.src = scriptUrl;
            script.async = true;
            script.defer = true;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error("Failed to load the reCAPTCHA script."));
            document.head.appendChild(script);
        }), RECAPTCHA_LOAD_TIMEOUT_MS, "Timed out while loading the reCAPTCHA script.").catch((error) => {
            reCaptchaScriptLoadPromise = null;
            reCaptchaScriptLoadSiteKey = null;
            throw error;
        });

        return reCaptchaScriptLoadPromise;
    };

    /**
     * Wraps grecaptcha.ready() in a Promise.
     * @returns {Promise<void>}
     */
    niobium.reCaptchaReady = niobium.reCaptchaReady || function reCaptchaReady() {
        return niobium.withTimeout(new Promise((resolve) => {
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
        }), RECAPTCHA_READY_TIMEOUT_MS, "Timed out while waiting for reCAPTCHA to become ready.");
    };

    /**
     * Generates a reCAPTCHA v3 token using async/await.
     * @param {string} siteKey - Your reCAPTCHA site key.
     * @param {string} action - The action name for this request.
     * @returns {Promise<string>} The reCAPTCHA token.
     */
    niobium.getRecaptchaToken = niobium.getRecaptchaToken || async function getRecaptchaToken(siteKey, action) {
        if (siteKey) {
            await niobium.ensureRecaptchaScript(siteKey);
        }
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

        const resolvedSiteKey = (reCaptchaPublicKey || "").trim() || configuredSiteKey;

        // Prepare stable parts of the payload to keep idempotency across retries
        const stableId = niobium.generateGUID();
        const stableTimestamp = Date.now();
        const stableCulture = details.culture || (navigator.language || 'en-US');
        const stableTimeZone = details.timeZone || (Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC');

        const headers = { "Content-Type": "application/json" };
        if (localTest) {
            headers["Referer"] = "http://127.0.0.1:3000/";
        }

        /**
         * Build fresh RequestInit with a new reCAPTCHA token on every attempt
         * @returns {Promise<RequestInit>}
         */
        const buildOptions = async () => {
            let token;
            try {
                token = await niobium.getRecaptchaToken(resolvedSiteKey, "order");
            } catch (error) {
                throw new Error("reCAPTCHA execution failed.");
            }

            /** @type {OrderRequestData} */
            const data = {
                ID: stableId,
                Tenant: tenant,
                Shipping: (details.shipping ?? details.shippingId),
                ShippingCountry: details.shippingCountry,
                Coupon: details.coupon,
                Captcha: token,
                Timestamp: stableTimestamp,
                Culture: stableCulture,
                TimeZone: stableTimeZone,
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

            return {
                method: "POST",
                headers: headers,
                body: JSON.stringify(data),
            };
        };

        const url = (baseUrl || "/api/store") + "/orders";
        // Pass the options factory so each retry gets a fresh token
        return await niobium.fetchWithRetry(url, buildOptions);
    }

    const configuredSiteKey = getConfiguredSiteKey();
    if (configuredSiteKey) {
        niobium.ensureRecaptchaScript(configuredSiteKey).catch(() => {
            reCaptchaScriptLoadPromise = null;
            reCaptchaScriptLoadSiteKey = null;
        });
    }

    // Public API
    storeNS.makeOrder = makeOrder;
})(typeof window !== "undefined" ? window : globalThis);