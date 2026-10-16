(() => {
    const form = document.querySelector("[data-signup-form]");
    if (!form) return;

    const name = form.querySelector("[data-business-name]");
    const slug = form.querySelector("[data-business-slug]");
    const submit = form.querySelector("[data-signup-submit]");
    let slugEdited = false;

    const slugify = value => value
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "")
        .slice(0, 50);

    slug.addEventListener("input", () => {
        slugEdited = true;
        slug.value = slugify(slug.value);
    });

    name.addEventListener("input", () => {
        if (!slugEdited) slug.value = slugify(name.value);
    });

    form.addEventListener("submit", event => {
        const password = form.querySelector("#signup-password");
        const confirmation = form.querySelector("#signup-confirm-password");
        confirmation.setCustomValidity(
            password.value === confirmation.value ? "" : "Las contraseñas no coinciden.");
        if (!form.checkValidity()) {
            event.preventDefault();
            form.reportValidity();
            return;
        }
        submit.disabled = true;
        submit.textContent = "Creando tu cuenta…";
    });
})();
