// Public-only enhancement. No network, storage, analytics, uploads or account actions.
function initializeLanding() {
    const root = document.querySelector(".lc-public");
    if (!root || root.dataset.enhanced) return;
    root.dataset.enhanced = "true";
    root.querySelectorAll("[data-faq-toggle]").forEach(button => {
        button.addEventListener("click", () => {
            const expanded = button.getAttribute("aria-expanded") === "true";
            button.setAttribute("aria-expanded", String(!expanded));
            document.getElementById(button.getAttribute("aria-controls")).hidden = expanded;
            button.querySelector("span").textContent = expanded ? "+" : "−";
        });
    });
    root.querySelectorAll(".lp-mobile-nav a").forEach(link => link.addEventListener("click", () => {
        const menu = link.closest("details");
        menu.open = false;
        const target = link.hash && document.getElementById(link.hash.slice(1));
        if (target) { target.setAttribute("tabindex", "-1"); target.focus({ preventScroll: true }); }
    }));
}
initializeLanding();
document.addEventListener("enhancedload", initializeLanding);
if (window.Blazor) window.Blazor.addEventListener("enhancedload", initializeLanding);
