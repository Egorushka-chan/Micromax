document.addEventListener("DOMContentLoaded", () => {
    const header = document.getElementById("landing-header");
    if (header) {
        const syncHeaderState = () => {
            header.classList.toggle("landing-header--scrolled", window.scrollY > 18);
        };

        syncHeaderState();
        window.addEventListener("scroll", syncHeaderState, { passive: true });
    }

    const revealItems = document.querySelectorAll("[data-reveal]");
    if (revealItems.length === 0) {
        return;
    }

    if (!("IntersectionObserver" in window)) {
        revealItems.forEach((item) => item.classList.add("is-visible"));
        return;
    }

    // Sections reveal once so the landing page stays calm instead of over-animating.
    const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
            if (!entry.isIntersecting) {
                return;
            }

            entry.target.classList.add("is-visible");
            observer.unobserve(entry.target);
        });
    }, {
        threshold: 0.16,
        rootMargin: "0px 0px -32px 0px"
    });

    revealItems.forEach((item) => observer.observe(item));
});
