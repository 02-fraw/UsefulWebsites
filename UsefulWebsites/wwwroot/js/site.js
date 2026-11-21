"use strict";

document.addEventListener("DOMContentLoaded", () => {
    initializeTheme();
    initializeLogoFallbacks();
    initializeDescriptionToggles();
    initializeDeleteConfirmations();
    initializeBulkSelection();
    initializeSearchShortcut();
    initializeHomeLiveSearch();
    initializeAdminLiveSearch();
});

function initializeTheme() {
    const rootElement = document.documentElement;
    const themeToggle = document.getElementById("themeToggle");

    if (!themeToggle) {
        return;
    }

    updateThemeButton(themeToggle, rootElement.dataset.theme);

    themeToggle.addEventListener("click", () => {
        const currentTheme = rootElement.dataset.theme === "light" ? "light" : "dark";
        const newTheme = currentTheme === "dark" ? "light" : "dark";

        rootElement.dataset.theme = newTheme;

        try {
            localStorage.setItem("uw-theme", newTheme);
        } catch {
        }

        updateThemeButton(themeToggle, newTheme);
    });
}

function updateThemeButton(themeToggle, currentTheme) {
    const isLightTheme = currentTheme === "light";
    const label = isLightTheme ? "Switch to dark theme" : "Switch to light theme";

    themeToggle.setAttribute("aria-label", label);
    themeToggle.setAttribute("title", label);
    themeToggle.setAttribute("aria-pressed", isLightTheme.toString());
}

function initializeLogoFallbacks(scope = document) {
    const logoImages = scope.querySelectorAll("[data-site-logo]");

    logoImages.forEach(logoImage => {
        const showFallback = () => {
            const logoWrapper = logoImage.closest("[data-logo-wrapper]");
            const fallback = logoWrapper?.querySelector("[data-logo-fallback]");

            logoImage.classList.add("is-hidden");
            fallback?.classList.remove("is-hidden");
        };

        logoImage.addEventListener("error", showFallback);

        if (logoImage.complete && logoImage.naturalWidth === 0) {
            showFallback();
        }
    });
}

function initializeDescriptionToggles(scope = document) {
    const toggleButtons = scope.querySelectorAll("[data-description-toggle]");

    toggleButtons.forEach(toggleButton => {
        toggleButton.addEventListener("click", () => {
            const toolCard = toggleButton.closest(".tool-card");
            const description = toolCard?.querySelector("[data-description]");

            if (!description) {
                return;
            }

            const isExpanded = description.classList.toggle("is-expanded");
            toggleButton.textContent = isExpanded ? "Show less" : "Show more";
            toggleButton.setAttribute("aria-expanded", isExpanded.toString());
        });
    });
}

function initializeDeleteConfirmations(scope = document) {
    const confirmationButtons = scope.querySelectorAll("[data-confirm-message]");

    confirmationButtons.forEach(button => {
        button.addEventListener("click", event => {
            const requiresSelection = button.dataset.requiresSelection === "true";

            if (requiresSelection) {
                const parentForm = button.closest("form");
                const selectedCheckbox = parentForm?.querySelector(".website-checkbox:checked");

                if (!selectedCheckbox) {
                    event.preventDefault();
                    window.alert("Please select at least one website to delete.");
                    return;
                }
            }

            const confirmationMessage = button.dataset.confirmMessage;
            if (!confirmationMessage) {
                return;
            }

            if (!window.confirm(confirmationMessage)) {
                event.preventDefault();
            }
        });
    });
}

function initializeBulkSelection(scope = document) {
    const selectAllCheckbox = scope.querySelector("#selectAllWebsites");
    const websiteCheckboxes = Array.from(scope.querySelectorAll(".website-checkbox"));

    if (!selectAllCheckbox || websiteCheckboxes.length === 0) {
        return;
    }

    selectAllCheckbox.addEventListener("change", () => {
        websiteCheckboxes.forEach(checkbox => {
            checkbox.checked = selectAllCheckbox.checked;
        });

        updateSelectAllState(selectAllCheckbox, websiteCheckboxes);
    });

    websiteCheckboxes.forEach(checkbox => {
        checkbox.addEventListener("change", () => {
            updateSelectAllState(selectAllCheckbox, websiteCheckboxes);
        });
    });

    updateSelectAllState(selectAllCheckbox, websiteCheckboxes);
}

function updateSelectAllState(selectAllCheckbox, websiteCheckboxes) {
    const selectedCount = websiteCheckboxes.filter(checkbox => checkbox.checked).length;

    selectAllCheckbox.checked = selectedCount === websiteCheckboxes.length;
    selectAllCheckbox.indeterminate =
        selectedCount > 0 && selectedCount < websiteCheckboxes.length;
}

function initializeSearchShortcut() {
    const searchInput = document.querySelector("#searchInput, #adminSearchInput");

    if (!searchInput) {
        return;
    }

    document.addEventListener("keydown", event => {
        const isSearchShortcut =
            (event.ctrlKey || event.metaKey) &&
            event.key.toLowerCase() === "k";

        if (!isSearchShortcut) {
            return;
        }

        event.preventDefault();
        searchInput.focus();
        searchInput.select();
    });

    searchInput.addEventListener("keydown", event => {
        if (event.key !== "Escape") {
            return;
        }

        if (searchInput.value.length > 0) {
            searchInput.value = "";
            searchInput.dispatchEvent(new Event("input", { bubbles: true }));
        } else {
            searchInput.blur();
        }
    });
}

function bindClearButton(input, clearButton) {
    if (!input || !clearButton) {
        return;
    }

    const syncVisibility = () => {
        clearButton.hidden = input.value.length === 0;
    };

    syncVisibility();
    input.addEventListener("input", syncVisibility);

    clearButton.addEventListener("click", () => {
        input.value = "";
        syncVisibility();
        input.focus();
        input.dispatchEvent(new Event("input", { bubbles: true }));
    });
}

function initializeHomeLiveSearch() {
    const searchInput = document.getElementById("searchInput");
    const clearButton = document.getElementById("homeSearchClear");
    const searchForm = searchInput?.closest("form");
    const resultsBar = document.querySelector(".directory-main > .directory-results-bar");
    const resultsListSelector =
        ".directory-main > .tool-grid, .directory-main > .directory-empty-state";

    bindClearButton(searchInput, clearButton);

    if (!searchInput || !searchForm || !resultsBar || !document.querySelector(resultsListSelector)) {
        return;
    }

    let searchTimer;
    let activeRequest;
    let requestVersion = 0;

    searchInput.addEventListener("input", () => {
        const currentVersion = ++requestVersion;

        window.clearTimeout(searchTimer);
        activeRequest?.abort();

        searchTimer = window.setTimeout(async () => {
            const request = new AbortController();
            activeRequest = request;

            const url = new URL(searchForm.action, window.location.href);
            url.search = new URLSearchParams(new FormData(searchForm)).toString();

            try {
                const response = await fetch(url, {
                    signal: request.signal,
                    headers: { Accept: "text/html" }
                });

                if (!response.ok) {
                    throw new Error(`Search request failed: ${response.status}`);
                }

                const html = await response.text();
                if (currentVersion !== requestVersion) {
                    return;
                }

                const nextPage = new DOMParser().parseFromString(html, "text/html");
                const nextResultsBar = nextPage.querySelector(".directory-main > .directory-results-bar");
                const nextResultsList = nextPage.querySelector(resultsListSelector);
                const currentResultsBar = document.querySelector(".directory-main > .directory-results-bar");
                const currentResultsList = document.querySelector(resultsListSelector);

                if (!nextResultsBar || !nextResultsList || !currentResultsBar || !currentResultsList) {
                    return;
                }

                currentResultsBar.replaceWith(nextResultsBar);
                currentResultsList.replaceWith(nextResultsList);

                const currentCategoryLinks = document.querySelectorAll(".category-navigation-link");
                const nextCategoryLinks = nextPage.querySelectorAll(".category-navigation-link");

                if (currentCategoryLinks.length === nextCategoryLinks.length) {
                    currentCategoryLinks.forEach((link, index) => {
                        link.href = nextCategoryLinks[index].getAttribute("href");
                    });
                }

                initializeLogoFallbacks(nextResultsList);
                initializeDescriptionToggles(nextResultsList);

                window.history.replaceState(window.history.state, "", url);
            } catch (error) {
                if (error.name !== "AbortError") {
                    console.error("Live search failed:", error);
                }
            } finally {
                if (activeRequest === request) {
                    activeRequest = null;
                }
            }
        }, 180);
    });
}

function initializeAdminLiveSearch() {
    const searchInput = document.getElementById("adminSearchInput");
    const clearButton = document.getElementById("adminSearchClear");
    const searchForm = document.getElementById("adminSearchForm");
    const resultsContainer = document.getElementById("adminResultsContainer");

    bindClearButton(searchInput, clearButton);

    if (!searchInput || !searchForm || !resultsContainer) {
        return;
    }

    let searchTimer;
    let activeRequest;
    let requestVersion = 0;

    searchInput.addEventListener("input", () => {
        const currentVersion = ++requestVersion;

        window.clearTimeout(searchTimer);
        activeRequest?.abort();

        searchTimer = window.setTimeout(async () => {
            const request = new AbortController();
            activeRequest = request;

            const url = new URL(searchForm.action, window.location.href);
            url.search = new URLSearchParams(new FormData(searchForm)).toString();

            try {
                const response = await fetch(url, {
                    signal: request.signal,
                    headers: { Accept: "text/html" }
                });

                if (!response.ok) {
                    throw new Error(`Admin search request failed: ${response.status}`);
                }

                const html = await response.text();
                if (currentVersion !== requestVersion) {
                    return;
                }

                const nextPage = new DOMParser().parseFromString(html, "text/html");
                const nextContainer = nextPage.getElementById("adminResultsContainer");
                const currentContainer = document.getElementById("adminResultsContainer");

                if (!nextContainer || !currentContainer) {
                    return;
                }

                currentContainer.replaceWith(nextContainer);

                const nextListedCount = nextPage.getElementById("adminListedCount");
                const currentListedCount = document.getElementById("adminListedCount");
                if (nextListedCount && currentListedCount) {
                    currentListedCount.textContent = nextListedCount.textContent;
                }

                const currentCategoryLinks = document.querySelectorAll(".admin-category-filters a");
                const nextCategoryLinks = nextPage.querySelectorAll(".admin-category-filters a");

                if (currentCategoryLinks.length === nextCategoryLinks.length) {
                    currentCategoryLinks.forEach((link, index) => {
                        link.href = nextCategoryLinks[index].getAttribute("href");
                    });
                }

                initializeLogoFallbacks(nextContainer);
                initializeDeleteConfirmations(nextContainer);
                initializeBulkSelection(nextContainer);

                window.history.replaceState(window.history.state, "", url);
            } catch (error) {
                if (error.name !== "AbortError") {
                    console.error("Admin live search failed:", error);
                }
            } finally {
                if (activeRequest === request) {
                    activeRequest = null;
                }
            }
        }, 180);
    });
}
