(() => {
    const chave = "sistema-chaves-aparencia";
    const raiz = document.documentElement;

    function lerPreferencias() {
        try {
            return JSON.parse(localStorage.getItem(chave) || "{}");
        } catch {
            return {};
        }
    }

    function aplicarPreferencias(modoEscuro, compacto, salvar) {
        raiz.dataset.theme = modoEscuro ? "dark" : "light";
        raiz.dataset.compact = compacto ? "true" : "false";

        if (salvar) {
            try {
                localStorage.setItem(chave, JSON.stringify({ dark: modoEscuro, compact: compacto }));
            } catch {
            }
        }
    }

    const preferencias = lerPreferencias();
    aplicarPreferencias(preferencias.dark === true, preferencias.compact === true, false);

    window.sitePreferences = {
        get: () => ({
            dark: raiz.dataset.theme === "dark",
            compact: raiz.dataset.compact === "true"
        }),
        apply: (modoEscuro, compacto) => aplicarPreferencias(modoEscuro, compacto, true)
    };
})();
