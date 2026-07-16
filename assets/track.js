/**
 * @typedef {Object} TrackRequestData
 * @property {string} ID
 * @property {string} Email
 * @property {string} Captcha
 * @property {number} Order
 */

/*
  * Consumer Example

  async function handleTrackOrder() {
    try {
      const response = await niobium.store.trackOrder(
        "your-recaptcha-key",
        {
          email: "john@example.com",
          order: 1234567890
        }
      );

      if (!response.ok) {
        console.error("Track request failed", response.status, await response.text());
        return;
      }

      const trackInfo = await response.json();
      console.log("Tracking details", trackInfo);
    } catch (error) {
      console.error("An error occurred while tracking order.", error);
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
     * Reads the reCAPTCHA site key from the track.js query string.
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
     * Tracks an order after executing reCAPTCHA.
     * @param {string} reCaptchaPublicKey The reCAPTCHA public key.
     * @param {{ email: string, order: number }} details The tracking input details.
     * @param {string=} baseUrl The API base URL (defaults to "/api").
     * @param {boolean=} localTest Whether testing locally (adds Referer header for some tunnels).
     * @returns {Promise<Response>} The fetch response promise.
     */
    async function trackOrder(reCaptchaPublicKey, details, baseUrl, localTest = false) {
        if (!details || !details.email || !details.order) {
            throw new Error("Email and order are required.");
        }

        const resolvedSiteKey = (reCaptchaPublicKey || "").trim() || configuredSiteKey;

        // Stable identity per attempt sequence
        const stableId = niobium.generateGUID();

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
                token = await niobium.getRecaptchaToken(resolvedSiteKey, "track");
            } catch (error) {
                throw new Error("reCAPTCHA execution failed.");
            }

            /** @type {TrackRequestData} */
            const data = {
                ID: stableId,
                Email: details.email,
                Captcha: token,
                Order: details.order,
            };

            return {
                method: "POST",
                headers: headers,
                body: JSON.stringify(data),
            };
        };

        const url = (baseUrl || "/api/store") + "/track";
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
    storeNS.trackOrder = trackOrder;
})(typeof window !== "undefined" ? window : globalThis);
