const themeToggle = document.getElementById("themeToggle");
const fromStation = document.getElementById("fromStation");
const toStation = document.getElementById("toStation");
const swapBtn = document.getElementById("swapBtn");

// Dark Mode
const savedTheme = localStorage.getItem("metro-theme");

if (savedTheme === "dark") {
    document.body.classList.add("dark-mode");

    if (themeToggle) {
        themeToggle.textContent = "Light";
    }
}

if (themeToggle) {
    themeToggle.addEventListener("click", () => {
        document.body.classList.toggle("dark-mode");

        const isDark = document.body.classList.contains("dark-mode");
        themeToggle.textContent = isDark ? "Light" : "Dark";

        localStorage.setItem("metro-theme", isDark ? "dark" : "light");
    });
}

// Swap Stations
if (swapBtn && fromStation && toStation) {
    swapBtn.addEventListener("click", () => {
        const temp = fromStation.value;
        fromStation.value = toStation.value;
        toStation.value = temp;
    });
}

// Reveal Animation
const revealElements = document.querySelectorAll(".reveal");

const revealOnScroll = () => {
    revealElements.forEach((el) => {
        const top = el.getBoundingClientRect().top;

        if (top < window.innerHeight - 90) {
            el.classList.add("visible");
        }
    });
};

window.addEventListener("load", revealOnScroll);
window.addEventListener("scroll", revealOnScroll);