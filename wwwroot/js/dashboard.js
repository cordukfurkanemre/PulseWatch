"use strict";

const REFRESH_INTERVAL_MS = 30_000;

const elements = {
    totalSites: document.querySelector("#total-sites"),
    onlineSites: document.querySelector("#online-sites"),
    offlineSites: document.querySelector("#offline-sites"),
    averageUptime: document.querySelector("#average-uptime"),
    sitesList: document.querySelector("#sites-list"),
    responseSiteSelect: document.querySelector("#response-site-select"),
    responseChart: document.querySelector("#response-chart"),
    responseLatest: document.querySelector("#response-latest"),
    responseAverage: document.querySelector("#response-average"),
    systemState: document.querySelector("#system-state"),
    systemStateText: document.querySelector("#system-state-text"),
    lastUpdated: document.querySelector("#last-updated"),
    refreshButton: document.querySelector("#refresh-button"),
    errorBanner: document.querySelector("#error-banner"),
    errorMessage: document.querySelector("#error-message"),
    errorRetry: document.querySelector("#error-retry")
};

let activeController = null;
let chartController = null;
let selectedSiteId = null;

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

function syncResponseSiteSelector(sites) {
    const previousSelection = selectedSiteId;
    elements.responseSiteSelect.replaceChildren();

    if (!Array.isArray(sites) || sites.length === 0) {
        selectedSiteId = null;
        elements.responseSiteSelect.disabled = true;

        const option = document.createElement("option");
        option.textContent = "No websites available";
        elements.responseSiteSelect.append(option);
        renderChartState("No response-time data", "Add a website to start collecting measurements.");
        return;
    }

    sites.forEach(site => {
        const option = document.createElement("option");
        option.value = String(site.id);
        option.textContent = site.name || site.url || `Website ${site.id}`;
        elements.responseSiteSelect.append(option);
    });

    const selectionStillExists = sites.some(site => String(site.id) === String(previousSelection));
    selectedSiteId = selectionStillExists ? previousSelection : sites[0].id;
    elements.responseSiteSelect.value = String(selectedSiteId);
    elements.responseSiteSelect.disabled = false;
}

function renderChartState(title, message, isLoading = false) {
    elements.responseChart.replaceChildren();
    elements.responseChart.setAttribute("aria-busy", String(isLoading));
    elements.responseLatest.textContent = "—";
    elements.responseAverage.textContent = "—";

    const state = document.createElement("div");
    state.className = "chart-state";

    if (isLoading) {
        const spinner = document.createElement("span");
        spinner.className = "spinner";
        spinner.setAttribute("aria-hidden", "true");
        state.append(spinner);
    } else {
        const heading = document.createElement("strong");
        heading.textContent = title;
        state.append(heading);
    }

    const description = document.createElement("span");
    description.textContent = message;
    state.append(description);
    elements.responseChart.append(state);
}

function createSvgElement(name, attributes = {}) {
    const element = document.createElementNS("http://www.w3.org/2000/svg", name);
    Object.entries(attributes).forEach(([key, value]) => element.setAttribute(key, String(value)));
    return element;
}

function getNiceMaximum(value) {
    if (value <= 10) {
        return 10;
    }

    const magnitude = 10 ** Math.floor(Math.log10(value));
    const normalized = value / magnitude;
    const niceValue = normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
    return niceValue * magnitude;
}

function formatChartTime(value) {
    const date = parseUtcTimestamp(value);
    if (!date) {
        return "Unknown";
    }

    return new Intl.DateTimeFormat(undefined, {
        month: "short",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit"
    }).format(date);
}

function renderResponseChart(data) {
    elements.responseChart.replaceChildren();
    elements.responseChart.setAttribute("aria-busy", "false");

    if (!Array.isArray(data) || data.length === 0) {
        renderChartState("No measurements yet", "Response times will appear after the first health check.");
        return;
    }

    const values = data.map(item => Math.max(0, Number(item.responseTimeMs) || 0));
    const latest = values.at(-1);
    const average = values.reduce((sum, value) => sum + value, 0) / values.length;
    elements.responseLatest.textContent = `${numberFormatter.format(latest)} ms`;
    elements.responseAverage.textContent = `${numberFormatter.format(average)} ms`;

    const width = 1000;
    const height = 300;
    const padding = { top: 18, right: 22, bottom: 42, left: 58 };
    const plotWidth = width - padding.left - padding.right;
    const plotHeight = height - padding.top - padding.bottom;
    const yMaximum = getNiceMaximum(Math.max(...values));
    const pointStep = data.length === 1 ? 0 : plotWidth / (data.length - 1);

    const svg = createSvgElement("svg", {
        class: "response-chart-svg",
        viewBox: `0 0 ${width} ${height}`,
        role: "img",
        "aria-label": `Response time history with ${data.length} measurements`
    });

    const points = values.map((value, index) => ({
        x: data.length === 1 ? padding.left + plotWidth / 2 : padding.left + index * pointStep,
        y: padding.top + plotHeight - (value / yMaximum) * plotHeight,
        value,
        checkedAtUtc: data[index].checkedAtUtc
    }));

    for (let tick = 0; tick <= 4; tick += 1) {
        const ratio = tick / 4;
        const y = padding.top + plotHeight - ratio * plotHeight;
        const value = Math.round(yMaximum * ratio);

        svg.append(createSvgElement("line", {
            class: "chart-grid-line",
            x1: padding.left,
            x2: width - padding.right,
            y1: y,
            y2: y
        }));

        const label = createSvgElement("text", {
            class: "chart-axis-label",
            x: padding.left - 10,
            y: y + 4,
            "text-anchor": "end"
        });
        label.textContent = `${numberFormatter.format(value)} ms`;
        svg.append(label);
    }

    const linePoints = points.map(point => `${point.x},${point.y}`).join(" ");
    const areaPoints = `${padding.left},${padding.top + plotHeight} ${linePoints} ${width - padding.right},${padding.top + plotHeight}`;
    svg.append(createSvgElement("polygon", { class: "chart-area", points: areaPoints }));
    svg.append(createSvgElement("polyline", { class: "chart-line", points: linePoints }));

    points.forEach(point => {
        const circle = createSvgElement("circle", {
            class: "chart-point",
            cx: point.x,
            cy: point.y,
            r: 4,
            tabindex: 0
        });
        const title = createSvgElement("title");
        title.textContent = `${numberFormatter.format(point.value)} ms — ${formatChartTime(point.checkedAtUtc)}`;
        circle.append(title);
        svg.append(circle);
    });

    const labelIndexes = [...new Set([0, Math.floor((data.length - 1) / 2), data.length - 1])];
    labelIndexes.forEach((index, labelPosition) => {
        const point = points[index];
        const label = createSvgElement("text", {
            class: "chart-axis-label",
            x: point.x,
            y: height - 13,
            "text-anchor": labelPosition === 0 ? "start" : labelPosition === labelIndexes.length - 1 ? "end" : "middle"
        });
        label.textContent = formatChartTime(point.checkedAtUtc);
        svg.append(label);
    });

    elements.responseChart.append(svg);
}

async function loadResponseTimes() {
    if (selectedSiteId == null) {
        return null;
    }

    if (chartController) {
        chartController.abort();
    }

    chartController = new AbortController();
    const currentController = chartController;
    const requestedSiteId = selectedSiteId;
    renderChartState("", "Loading response times…", true);

    try {
        const data = await fetchJson(
            `/api/dashboard/sites/${encodeURIComponent(requestedSiteId)}/response-times?limit=30`,
            currentController.signal);

        if (!currentController.signal.aborted && String(selectedSiteId) === String(requestedSiteId)) {
            renderResponseChart(data);
        }

        return null;
    } catch (error) {
        if (currentController.signal.aborted) {
            return null;
        }

        renderChartState("Response times unavailable", "The chart will retry on the next refresh.");
        return `Response-time request failed: ${error.message}.`;
    }
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
        syncResponseSiteSelector(sitesResult.value);
    } else {
        errors.push(`Sites request failed: ${sitesResult.reason.message}.`);
    }

    const chartError = await loadResponseTimes();
    if (chartError) {
        errors.push(chartError);
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
elements.responseSiteSelect.addEventListener("change", () => {
    selectedSiteId = elements.responseSiteSelect.value;
    loadResponseTimes().then(error => {
        if (error) {
            showError([error]);
        }
    });
});

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
