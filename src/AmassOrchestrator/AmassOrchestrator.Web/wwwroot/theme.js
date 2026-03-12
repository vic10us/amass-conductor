window.themeInterop = {
    setTheme: function (themeName) {
        const link = document.getElementById('radzen-theme');
        if (link) {
            const css = themeName === 'dark' ? 'standard-dark-base.css' : 'standard-base.css';
            link.href = '_content/Radzen.Blazor/css/' + css;
        }
        localStorage.setItem('theme', themeName);
    },
    getTheme: function () {
        return localStorage.getItem('theme');
    },
    getSystemPreference: function () {
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
};
