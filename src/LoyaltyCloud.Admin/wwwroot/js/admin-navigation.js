(() => {
    const openClass = "kb-app--menu-open";

    function getApp(element) {
        return element?.closest?.(".kb-app") ?? document.querySelector(".kb-app");
    }

    function setMenuOpen(app, open) {
        if (!app) return;

        app.classList.toggle(openClass, open);
        const toggle = app.querySelector("[data-admin-menu-toggle]");
        if (toggle) {
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
        }
    }

    function closeAllMenus() {
        document.querySelectorAll(`.${openClass}`).forEach(app => setMenuOpen(app, false));
    }

    document.addEventListener("click", event => {
        const toggle = event.target.closest("[data-admin-menu-toggle]");
        if (toggle) {
            event.preventDefault();
            const app = getApp(toggle);
            if (!app) return;
            setMenuOpen(app, !app.classList.contains(openClass));
            return;
        }

        const close = event.target.closest("[data-admin-menu-close]");
        if (close) {
            setMenuOpen(getApp(close), false);
            return;
        }

        const nav = event.target.closest("[data-admin-menu-nav]");
        if (nav && event.target.closest("a")) {
            setMenuOpen(getApp(nav), false);
        }
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            closeAllMenus();
        }
    });

    document.addEventListener("enhancedload", closeAllMenus);
})();
