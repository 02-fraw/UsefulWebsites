"use strict";

try {
    const savedTheme = localStorage.getItem("uw-theme");

    if (savedTheme === "light" || savedTheme === "dark") {
        document.documentElement.setAttribute("data-theme", savedTheme);
    }
} catch {
    document.documentElement.setAttribute("data-theme", "dark");
}
