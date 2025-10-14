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

        let token;
        try {
            token = await niobium.getRecaptchaToken(reCaptchaPublicKey, "track");
        } catch (error) {
            return Promise.reject(new Error("reCAPTCHA execution failed."));
        }

        /** @type {TrackRequestData} */
        const data = {
            ID: niobium.generateGUID(),
            Email: details.email,
            Captcha: token,
            Order: details.order,
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

        const url = (baseUrl || "/api/store") + "/track";
        return await niobium.fetchWithRetry(url, options);
    }

    // Public API
    storeNS.trackOrder = trackOrder;
})(typeof window !== "undefined" ? window : globalThis);
