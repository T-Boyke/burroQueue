import { defineConfig, presetWind3 } from 'unocss'

export default defineConfig({
  presets: [
    presetWind3({
      dark: 'media',
    }),
  ],
  theme: {
    colors: {
      fluent: {
        bg: 'var(--bg-page)',
        card: 'var(--bg-card)',
        subtle: 'var(--bg-subtle)',
        input: 'var(--bg-input)',
        border: 'var(--border)',
        focus: 'var(--border-focus)',
        text: 'var(--text-main)',
        muted: 'var(--text-secondary)',
        accent: 'var(--accent)',
        accentHover: 'var(--accent-hover)',
        accentActive: 'var(--accent-active)',
        onAccent: 'var(--text-on-accent)',
        toastBg: 'var(--success-bg)',
        toastBorder: 'var(--success-border)',
        toastText: 'var(--success-text)',
      },
    },
    fontFamily: {
      fluent: "'Segoe UI Variable Text', 'Segoe UI', system-ui, -apple-system, sans-serif",
    },
  },
  preflights: [
    {
      getCSS: () => `
:root {
  color-scheme: light dark;
  --bg-page: #f3f3f5;
  --bg-card: #ffffff;
  --bg-subtle: #f7f7f9;
  --bg-input: #ffffff;
  --border: rgba(0, 0, 0, 0.09);
  --border-focus: #005fb8;
  --text-main: #1a1a1c;
  --text-secondary: #5c5c63;
  --accent: #005fb8;
  --accent-hover: #00529e;
  --accent-active: #004585;
  --text-on-accent: #ffffff;
  --success-bg: rgba(15, 123, 62, 0.10);
  --success-border: rgba(15, 123, 62, 0.28);
  --success-text: #0f7b3e;
}
@media (prefers-color-scheme: dark) {
  :root {
    --bg-page: #141416;
    --bg-card: #202023;
    --bg-subtle: #27272b;
    --bg-input: #19191b;
    --border: rgba(255, 255, 255, 0.08);
    --border-focus: #60cdff;
    --text-main: #f5f5f7;
    --text-secondary: #9a9aa0;
    --accent: #60cdff;
    --accent-hover: #78d5ff;
    --accent-active: #4abce8;
    --text-on-accent: #0b0f14;
    --success-bg: rgba(56, 178, 107, 0.15);
    --success-border: rgba(56, 178, 107, 0.35);
    --success-text: #5fe09a;
  }
}
*, *::before, *::after { box-sizing: border-box; }
body {
  margin: 0;
  background-color: var(--bg-page);
  color: var(--text-main);
  font-family: 'Segoe UI Variable Text', 'Segoe UI', system-ui, -apple-system, sans-serif;
  -webkit-font-smoothing: antialiased;
}`,
    },
  ],
  shortcuts: {
    'bq-page': 'min-h-screen flex justify-center items-start px-4 py-6',
    'bq-shell': 'w-full max-w-[460px] bg-fluent-card border border-solid border-fluent-border rounded-xl px-6 py-7 shadow-xl',
    'bq-badge': 'text-[11px] font-semibold text-fluent-muted bg-fluent-subtle border border-solid border-fluent-border px-2 py-1 rounded-md',
    'bq-toast': 'bg-fluent-toastBg border border-solid border-fluent-toastBorder text-fluent-toastText text-[13px] font-medium px-3.5 py-2.5 rounded-lg mb-4 text-center',
    'bq-section': 'bg-fluent-subtle border border-solid border-fluent-border rounded-lg px-4 py-3.5 mb-4 text-left',
    'bq-section-label': 'text-[11px] font-bold uppercase tracking-[0.08em] text-fluent-accent mb-1',
    'bq-input': 'w-full bg-fluent-input border border-solid border-fluent-border rounded-md px-3 py-2.5 text-fluent-text text-sm outline-none transition-colors focus:border-fluent-focus',
    'bq-btn-primary': 'w-full bg-fluent-accent hover:bg-fluent-accentHover active:bg-fluent-accentActive text-fluent-onAccent border-none rounded-md p-3 text-sm font-semibold cursor-pointer transition-colors mt-1.5',
    'bq-btn-skip': 'bg-transparent text-fluent-muted hover:text-fluent-text border border-solid border-fluent-border rounded-md px-3 py-1.5 text-xs font-medium cursor-pointer transition-colors',
  },
})

