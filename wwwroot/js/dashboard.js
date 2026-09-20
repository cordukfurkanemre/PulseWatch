"use strict";

const REFRESH_INTERVAL_MS = 30_000;

const elements = {
    totalSites: document.querySelector("#total-sites"),
    onlineSites: document.querySelector("#online-sites"),
    offlineSites: document.querySelector("#offline-sites"),
    averageUptime: document.querySelector("#average-uptime"),
    sitesList: document.querySelector("#sites-list"),
    systemState: document.querySelector("#system-state"),
    systemStateText: document.querySelector("#system-state-text"),
    lastUpdated: document.querySelector("#last-updated"),
    refreshButton: document.querySelector("#refresh-button"),
    errorBanner: document.querySelector("#error-banner"),
    errorMessage: document.querySelector("#error-message"),
    errorRetry: document.querySelector("#error-retry")
};

let activeController = null;

const numberFormatter = new Intl.NumberFormat(undefined, {
    maximumFractionDigits: 2
});

const dateFormatter = new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
});

function setText(element, value) {
    element.textContent = value;
    element.classList.remove("loading-value");
}

function setSystemState(mode, text) {
    elements.systemState.classList.remove("operational", "degraded");

    if (mode) {
        elements.systemState.classList.add(mode);
    }

    elements.systemStateText.textContent = text;
}

function setRefreshing(isRefreshing) {
    elements.refreshButton.disabled = isRefreshing;
    elements.refreshButton.classList.toggle("is-refreshing", isRefreshing);
    elements.sitesList.setAttribute("aria-busy", String(isRefreshing));

    if (isRefreshing) {
        setSystemState(null, "Updating");
    }
}

async function fetchJson(url, signal) {
    const response = await fetch(url, {
        method: "GET",
        headers: { Accept: "application/json" },
        cache: "no-store",
        signal
    });

    if (!response.ok) {
        throw new Error(`${response.status} ${response.statusText}`);
    }

    return response.json();
}

function renderSummary(summary) {
    setText(elements.totalSites, numberFormatter.format(summary.totalSites ?? 0));
    setText(elements.onlineSites, numberFormatter.format(summary.onlineSites ?? 0));
    setText(elements.offlineSites, numberFormatter.format(summary.offlineSites ?? 0));
    setText(elements.averageUptime, `${numberFormatter.format(summary.averageUptime ?? 0)}%`);
}

function createCell(value, label, extraClass = "") {
    const cell = document.createElement("span");
    cell.className = `data-cell ${extraClass}`.trim();
    cell.dataset.label = label;
    cell.textContent = value;
    return cell;
}

function createWebsiteIdentity(site) {
    const identity = document.createElement("div");
    identity.className = "site-identity";

    const name = document.createElement("span");
    name.className = "site-name";
    name.textContent = site.name || "Unnamed website";
    identity.append(name);

    let parsedUrl = null;
    try {
        const candidate = new URL(site.url);
        if (candidate.protocol === "http:" || candidate.protocol === "https:") {
            parsedUrl = candidate;
        }
    } catch {
        parsedUrl = null;
    }

    const urlElement = document.createElement(parsedUrl ? "a" : "span");
    urlElement.className = "site-url";
    urlElement.textContent = site.url || "No URL configured";

    if (parsedUrl) {
        urlElement.href = parsedUrl.href;
        urlElement.target = "_blank";
        urlElement.rel = "noopener noreferrer";
    }

    identity.append(urlElement);
    return identity;
}

function parseUtcTimestamp(value) {
    if (typeof value !== "string") {
        return null;
    }

    const timestamp = value.trim();
    if (!timestamp) {
        return null;
    }

    const hasTimezone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(timestamp);
    const normalizedTimestamp = hasTimezone ? timestamp : `${timestamp}Z`;
    const date = new Date(normalizedTimestamp);

    return Number.isNaN(date.getTime()) ? null : date;
}

function formatLastChecked(value) {
    if (!value) {
        return "Never checked";
    }

    const date = parseUtcTimestamp(value);
    return date ? dateFormatter.format(date) : "Unknown";
}

function createSiteRow(site) {
    const row = document.createElement("article");
    row.className = "site-row";

    const status = document.createElement("span");
    status.className = `status-badge ${site.isOnline ? "online" : "offline"}`;
    status.textContent = site.isOnline ? "Online" : "Offline";
    status.setAttribute("aria-label", `Status: ${status.textContent}`);

    const statusContainer = document.createElement("div");
    statusContainer.className = "data-cell";
    statusContainer.dataset.label = "Status";
    statusContainer.append(status);

    const statusCode = site.statusCode == null ? "—" : String(site.statusCode);
    const responseTime = site.responseTimeMs == null ? "—" : `${numberFormatter.format(site.responseTimeMs)} ms`;
    const uptime = `${numberFormatter.format(site.uptime ?? 0)}%`;

    row.append(
        createWebsiteIdentity(site),
        statusContainer,
        createCell(statusCode, "HTTP", site.statusCode == null ? "muted" : ""),
        createCell(responseTime, "Response", site.responseTimeMs == null ? "muted" : ""),
        createCell(uptime, "Uptime", "uptime-value"),
        createCell(formatLastChecked(site.lastCheckedAtUtc), "Last checked", site.lastCheckedAtUtc ? "" : "muted")
    );

    return row;
}

function renderSites(sites) {
    elements.sitesList.replaceChildren();

    if (!Array.isArray(sites) || sites.length === 0) {
        const emptyState = document.createElement("div");
        emptyState.className = "empty-state";

        const icon = document.createElement("span");
        icon.className = "empty-icon";
        icon.setAttribute("aria-hidden", "true");
        icon.textContent = "—";

        const title = document.createElement("strong");
        title.textContent = "No websites monitored yet";

        const message = document.createElement("p");
        message.textContent = "Add a website through the API to start tracking availability and response time.";

        emptyState.append(icon, title, message);
        elements.sitesList.append(emptyState);
        return;
    }

    const fragment = document.createDocumentFragment();
    sites.forEach(site => fragment.append(createSiteRow(site)));
    elements.sitesList.append(fragment);
}

function showError(messages) {
    elements.errorMessage.textContent = `${messages.join(" ")} Automatic refresh will retry.`;
    elements.errorBanner.hidden = false;
    setSystemState("degraded", "Data unavailable");
}

function hideError() {
    elements.errorBanner.hidden = true;
}

async function refreshDashboard() {
    if (activeController) {
        activeController.abort();
    }

    activeController = new AbortController();
    const currentController = activeController;
    setRefreshing(true);

    const [summaryResult, sitesResult] = await Promise.allSettled([
        fetchJson("/api/dashboard/summary", currentController.signal),
        fetchJson("/api/dashboard/sites", currentController.signal)
    ]);

    if (currentController.signal.aborted) {
        return;
    }

    const errors = [];

    if (summaryResult.status === "fulfilled") {
        renderSummary(summaryResult.value);
    } else {
        errors.push(`Summary request failed: ${summaryResult.reason.message}.`);
    }

    if (sitesResult.status === "fulfilled") {
        renderSites(sitesResult.value);
    } else {
        errors.push(`Sites request failed: ${sitesResult.reason.message}.`);
    }

    setRefreshing(false);
    elements.lastUpdated.textContent = `Updated ${dateFormatter.format(new Date())}`;

    if (errors.length > 0) {
        showError(errors);
    } else {
        hideError();
        setSystemState("operational", "System operational");
    }
}

elements.refreshButton.addEventListener("click", refreshDashboard);
elements.errorRetry.addEventListener("click", refreshDashboard);

refreshDashboard().catch(error => {
    setRefreshing(false);
    showError([`Dashboard refresh failed: ${error.message}.`]);
});

window.setInterval(() => {
    refreshDashboard().catch(error => {
        setRefreshing(false);
        showError([`Dashboard refresh failed: ${error.message}.`]);
    });
}, REFRESH_INTERVAL_MS);
