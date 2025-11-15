// Theme Switcher - Bootstrap 5.3+ with native dark mode support
const THEME_STORAGE_KEY = 'epaper-theme';
const THEME_DARK = 'dark';
const THEME_LIGHT = 'light';

function getPreferredTheme() {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored) {
        return stored;
    }
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
        return THEME_DARK;
    }
    return THEME_LIGHT;
}

function setTheme(theme) {
    if (theme !== THEME_DARK && theme !== THEME_LIGHT) {
        theme = THEME_LIGHT;
    }

    const currentBefore = document.documentElement.getAttribute('data-bs-theme');
    document.documentElement.setAttribute('data-bs-theme', theme);

    const currentAfter = document.documentElement.getAttribute('data-bs-theme');
    localStorage.setItem(THEME_STORAGE_KEY, theme);

    // Update button icon
    const toggleIcon = document.getElementById('themeToggleIcon');
    if (toggleIcon) {
        if (theme === THEME_DARK) {
            toggleIcon.classList.remove('fa-moon');
            toggleIcon.classList.add('fa-sun');
        } else {
            toggleIcon.classList.remove('fa-sun');
            toggleIcon.classList.add('fa-moon');
        }
    }
}

function toggleTheme() {
    const currentTheme = document.documentElement.getAttribute('data-bs-theme');
    const newTheme = currentTheme === THEME_DARK ? THEME_LIGHT : THEME_DARK;

    setTheme(newTheme);
}

// Set up click handler as soon as possible
function setupClickHandler() {
    const toggleBtn = document.getElementById('themeToggleBtn');
    if (toggleBtn) {
        const originalSetAttribute = document.documentElement.setAttribute;
        let setAttributeCallCount = 0;
        document.documentElement.setAttribute = function (attr, value) {
            if (attr === 'data-bs-theme') {
                setAttributeCallCount++;
            }
            return originalSetAttribute.call(this, attr, value);
        };

        toggleBtn.onclick = function (e) {
            if (e) {
                e.preventDefault();
                e.stopImmediatePropagation();
                e.returnValue = false;
            }
            toggleTheme();
            return false;
        };

        toggleBtn.addEventListener('click', function (e) {
            if (e) {
                e.preventDefault();
                e.stopImmediatePropagation();
                e.returnValue = false;
            }
            toggleTheme();
            return false;
        });
    } else {
        setTimeout(setupClickHandler, 100);
    }
}

// Initialize theme when DOM is ready
function initialize() {
    const theme = getPreferredTheme();
    setTheme(theme);
    setupClickHandler();
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize);
} else {
    initialize();
}

// Expose globally for manual testing
window.themeSwitcher = {
    toggle: toggleTheme,
    set: setTheme,
    get: () => document.documentElement.getAttribute('data-bs-theme'),
    init: initialize
};
