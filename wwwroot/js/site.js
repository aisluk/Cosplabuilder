/* ==========================================================================
   Cosplabuilder — HCJ Client-Side Interactive Controller (site.js)
   Handles REST API calls (GET, POST, PUT, DELETE), ETag 304 checks,
   progress bar widths, and interactive status code testing.
   ========================================================================== */

document.addEventListener("DOMContentLoaded", () => {
    // 1. Apply progress bar widths from data-progress attributes (avoids inline styles)
    document.querySelectorAll("[data-progress]").forEach((el) => {
        const val = Math.max(0, Math.min(100, parseInt(el.getAttribute("data-progress") || "0", 10)));
        el.style.width = val + "%";
    });

    // 1b. Ensure images load cleanly with a fallback SVG placeholder if a file/URL is missing
    const fallbackSvg = "data:image/svg+xml;utf8," + encodeURIComponent(
        '<svg xmlns="http://www.w3.org/2000/svg" width="320" height="185" viewBox="0 0 320 185">' +
        '<rect width="100%" height="100%" fill="#1e1b4b"/>' +
        '<text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#a5b4fc" font-family="Segoe UI, sans-serif" font-size="14" font-weight="bold">Image Preview</text>' +
        '</svg>'
    );
    document.querySelectorAll("img").forEach((img) => {
        img.addEventListener("error", () => {
            if (img.dataset.fallbackApplied === "true") return;
            img.dataset.fallbackApplied = "true";
            img.src = fallbackSvg;
        });
    });

    // 2. Toggle collapsible forms via data-toggle-target
    document.querySelectorAll("[data-toggle-target]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const targetId = btn.getAttribute("data-toggle-target");
            const target = targetId ? document.getElementById(targetId) : null;
            if (target) {
                target.classList.toggle("cb-hidden");
            }
        });
    });

    // 3. REST DELETE button handler (calls DELETE endpoints and reloads on 204 No Content)
    document.querySelectorAll("[data-rest-delete]").forEach((btn) => {
        btn.addEventListener("click", async () => {
            const endpoint = btn.getAttribute("data-rest-delete");
            const redirectUrl = btn.getAttribute("data-redirect");
            if (!endpoint) return;

            if (!confirm(`Execute REST DELETE ${endpoint}?`)) return;

            try {
                const res = await fetch(endpoint, { method: "DELETE" });
                if (res.status === 204 || res.ok) {
                    if (redirectUrl) {
                        window.location.href = redirectUrl;
                    } else {
                        window.location.reload();
                    }
                } else {
                    const err = await res.json().catch(() => ({ message: res.statusText }));
                    alert(`HTTP ${res.status}: ${err.message || err.error || "Request failed"}`);
                }
            } catch (e) {
                alert("Network error while calling DELETE endpoint.");
            }
        });
    });

    // 4. Interactive REST API & Status Code Console on Home / Explorer pages
    const apiMethodSelect = document.getElementById("cb-api-method");
    const apiUrlInput = document.getElementById("cb-api-url");
    const apiBodyInput = document.getElementById("cb-api-body");
    const apiSendBtn = document.getElementById("cb-api-send");
    const apiOutput = document.getElementById("cb-api-output");
    let lastEtag = null;

    async function runApiRequest(method, url, bodyText, useIfNoneMatch = false) {
        if (!apiOutput) return;
        apiOutput.textContent = `Sending ${method} ${url} ...`;

        const headers = { "Accept": "application/json" };
        if (useIfNoneMatch && lastEtag) {
            headers["If-None-Match"] = lastEtag;
        }

        const options = { method, headers, redirect: "manual" };
        if ((method === "POST" || method === "PUT") && bodyText && bodyText.trim().length > 0) {
            headers["Content-Type"] = "application/json";
            options.body = bodyText;
        }

        try {
            const response = await fetch(url, options);
            const etag = response.headers.get("ETag");
            if (etag) lastEtag = etag;

            const locationHeader = response.headers.get("Location");
            let text = "";
            if (response.status !== 204 && response.status !== 304) {
                text = await response.text();
                try {
                    text = JSON.stringify(JSON.parse(text), null, 2);
                } catch {
                    // keep plain text
                }
            }

            const headerSummary = [
                `HTTP Status: ${response.status} ${response.statusText}`,
                etag ? `ETag: ${etag}` : null,
                locationHeader ? `Location: ${locationHeader}` : null
            ].filter(Boolean).join(" | ");

            apiOutput.textContent = `${headerSummary}\n\n${text || "(Empty body for 204/302/304)"}`;
        } catch (err) {
            apiOutput.textContent = `Request Error: ${err}`;
        }
    }

    if (apiSendBtn && apiMethodSelect && apiUrlInput) {
        apiSendBtn.addEventListener("click", () => {
            runApiRequest(
                apiMethodSelect.value,
                apiUrlInput.value,
                apiBodyInput ? apiBodyInput.value : ""
            );
        });
    }

    document.querySelectorAll("[data-api-preset]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const method = btn.getAttribute("data-api-method") || "GET";
            const url = btn.getAttribute("data-api-preset") || "/api/projects";
            const body = btn.getAttribute("data-api-body") || "";
            const checkEtag = btn.getAttribute("data-api-etag") === "true";

            if (apiMethodSelect) apiMethodSelect.value = method;
            if (apiUrlInput) apiUrlInput.value = url;
            if (apiBodyInput) apiBodyInput.value = body;

            runApiRequest(method, url, body, checkEtag);
        });
    });
});
