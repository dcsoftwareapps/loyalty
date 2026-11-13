(() => {
    const form = document.querySelector("[data-signup-form]");
    if (!form) return;

    const name = form.querySelector("[data-business-name]");
    const slug = form.querySelector("[data-business-slug]");
    const submit = form.querySelector("[data-signup-submit]");
    const password = form.querySelector("#signup-password");
    const confirmation = form.querySelector("#signup-confirm-password");
    let slugEdited = false;

    const requiredMessages = {
        "signup-email": "Escribe tu correo electrónico.",
        "signup-password": "Crea una contraseña.",
        "signup-confirm-password": "Confirma tu contraseña.",
        "signup-business": "Escribe el nombre de tu negocio.",
        "signup-slug": "Elige una dirección para tu negocio.",
        "signup-timezone": "Selecciona una zona horaria."
    };

    form.addEventListener("invalid", event => {
        const field = event.target;
        if (!(field instanceof HTMLInputElement || field instanceof HTMLSelectElement)) return;

        // Clear an earlier custom error before reading the current native validity state.
        field.setCustomValidity("");
        if (field.validity.valueMissing) {
            field.setCustomValidity(requiredMessages[field.id] || "Completa este campo.");
        } else if (field.validity.typeMismatch && field.type === "email") {
            field.setCustomValidity("Escribe un correo válido, por ejemplo nombre@negocio.com.");
        } else if (field.validity.tooShort && field.type === "password") {
            field.setCustomValidity("Usa al menos 8 caracteres para la contraseña.");
        } else if (field.validity.patternMismatch && field === slug) {
            field.setCustomValidity("Usa solo letras minúsculas, números y guiones entre palabras.");
        }
    }, true);

    form.addEventListener("input", event => {
        if (event.target instanceof HTMLInputElement) event.target.setCustomValidity("");
        if (event.target === password) confirmation.setCustomValidity("");
    });

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
