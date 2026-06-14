document.addEventListener("DOMContentLoaded", () => {
    const header = document.getElementById("landing-header");
    if (header) {
        const mobileHeaderBreakpoint = window.matchMedia("(max-width: 900px)");
        let lastScrollY = Math.max(window.scrollY, 0);

        const syncHeaderState = () => {
            const currentScrollY = Math.max(window.scrollY, 0);
            header.classList.toggle("landing-header--scrolled", currentScrollY > 18);

            if (mobileHeaderBreakpoint.matches) {
                header.classList.remove("landing-header--hidden");
                lastScrollY = currentScrollY;
                return;
            }

            const isScrollingDown = currentScrollY > lastScrollY + 4;
            const isScrollingUp = currentScrollY < lastScrollY - 4;

            if (currentScrollY <= 24) {
                header.classList.remove("landing-header--hidden");
            } else if (isScrollingDown && currentScrollY > 120) {
                header.classList.add("landing-header--hidden");
            } else if (isScrollingUp) {
                header.classList.remove("landing-header--hidden");
            }

            lastScrollY = currentScrollY;
        };

        syncHeaderState();
        window.addEventListener("scroll", syncHeaderState, { passive: true });
        mobileHeaderBreakpoint.addEventListener("change", syncHeaderState);
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
