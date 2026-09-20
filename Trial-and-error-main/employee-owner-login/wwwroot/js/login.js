// Add client-side feedback while keeping server-side validation authoritative.
(() => {
    const form = document.querySelector("#login-form");
    const password = document.querySelector("#Password");
    const toggle = document.querySelector("#password-toggle");
    const button = document.querySelector("#submit-button");

    if (!form || !password || !toggle || !button) return;

    toggle.addEventListener("click", () => {
        const showing = password.type === "text";
        password.type = showing ? "password" : "text";
        toggle.textContent = showing ? "Show" : "Hide";
        toggle.setAttribute("aria-label", showing ? "Show password" : "Hide password");
    });

    form.addEventListener("submit", (event) => {
        const username = document.querySelector("#Username");
        const usernameEmpty = !username.value.trim();
        const passwordEmpty = !password.value;

        username.setCustomValidity(usernameEmpty ? "Username is required." : "");
        password.setCustomValidity(passwordEmpty ? "Password is required." : "");

        if (!form.checkValidity()) {
            event.preventDefault();
            form.reportValidity();
            return;
        }

        button.disabled = true;
        button.classList.add("is-loading");
        button.querySelector(".button-label").textContent = "Signing in...";
    });
})();
