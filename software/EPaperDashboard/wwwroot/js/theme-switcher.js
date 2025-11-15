// Theme Switcher - Bootstrap 5.3+ with native dark mode support
console.log('=== Theme Switcher Loading ===');

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
    console.log('[setTheme] Called with theme:', theme);
    
    if (theme !== THEME_DARK && theme !== THEME_LIGHT) {
        theme = THEME_LIGHT;
    }
    
    // Get current theme before setting
    const currentBefore = document.documentElement.getAttribute('data-bs-theme');
    console.log('[setTheme] Current theme before:', currentBefore);
    
    // Set the attribute
    document.documentElement.setAttribute('data-bs-theme', theme);
    
    // Verify it was set
    const currentAfter = document.documentElement.getAttribute('data-bs-theme');
    console.log('[setTheme] Current theme after:', currentAfter);
    
    localStorage.setItem(THEME_STORAGE_KEY, theme);
    console.log('[setTheme] Saved to localStorage:', localStorage.getItem(THEME_STORAGE_KEY));
    
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
    console.log('[toggleTheme] Called');
    const currentTheme = document.documentElement.getAttribute('data-bs-theme');
    console.log('[toggleTheme] Current theme:', currentTheme);
    
    const newTheme = currentTheme === THEME_DARK ? THEME_LIGHT : THEME_DARK;
    console.log('[toggleTheme] New theme will be:', newTheme);
    
    setTheme(newTheme);
}

// Set up click handler as soon as possible
function setupClickHandler() {
    const toggleBtn = document.getElementById('themeToggleBtn');
    if (toggleBtn) {
        console.log('[setupClickHandler] Button found, setting onclick directly');
        
        // Watch for any changes to data-bs-theme to detect if it's being overwritten
        const originalSetAttribute = document.documentElement.setAttribute;
        let setAttributeCallCount = 0;
        document.documentElement.setAttribute = function(attr, value) {
            if (attr === 'data-bs-theme') {
                setAttributeCallCount++;
                console.log(`[setAttribute interceptor] Call #${setAttributeCallCount}: Setting data-bs-theme to ${value}`);
                console.trace('[setAttribute interceptor] Stack trace:');
            }
            return originalSetAttribute.call(this, attr, value);
        };
        
        toggleBtn.onclick = function(e) {
            console.log('[onclick] Button clicked, preventing default');
            if (e) {
                e.preventDefault();
                e.stopImmediatePropagation();
                e.returnValue = false;
            }
            toggleTheme();
            return false;
        };
        
        toggleBtn.addEventListener('click', function(e) {
            console.log('[addEventListener] Button clicked');
            if (e) {
                e.preventDefault();
                e.stopImmediatePropagation();
                e.returnValue = false;
            }
            toggleTheme();
            return false;
        });
        
        console.log('[setupClickHandler] Handler attached');
    } else {
        console.warn('[setupClickHandler] Button not found, will retry');
        setTimeout(setupClickHandler, 100);
    }
}

// Initialize theme when DOM is ready
function initialize() {
    console.log('[initialize] Starting initialization');
    
    const theme = getPreferredTheme();
    console.log('[initialize] Preferred theme:', theme);
    
    setTheme(theme);
    setupClickHandler();
    
    console.log('[initialize] Initialization complete');
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

console.log('=== Theme Switcher Loaded ===');
