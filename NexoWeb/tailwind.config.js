/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Components/**/*.razor',
    './Components/**/*.razor.cs',
    './wwwroot/**/*.html',
  ],
  corePlugins: {
    preflight: false, // MudBlazor maneja su propio reset
  },
  theme: {
    extend: {
      colors: {
        'nexo': {
          purple:      '#7C5CFF',
          'purple-d':  '#5A3BFF',
          'purple-dd': '#4B2FE0',
          'purple-l':  '#9B7FFF',
          'purple-xl': '#EDE9FF',
          dark:        '#13131F',
          'dark-2':    '#1A1A2E',
          'dark-3':    '#22223A',
          gray:        '#F4F5F9',
          'gray-2':    '#EEEEF5',
          'gray-3':    '#D1D5E8',
          border:      '#E8E9EF',
          text:        '#1A1C2E',
          'text-2':    '#6B6B8A',
          'text-3':    '#9B9BB4',
        },
      },
      fontFamily: {
        sans: ['Mango Dream', 'Plus Jakarta Sans', 'Inter', 'system-ui', 'sans-serif'],
      },
      borderRadius: {
        'nexo': '14px',
        'nexo-sm': '8px',
        'nexo-lg': '20px',
        'nexo-xl': '24px',
      },
      boxShadow: {
        'nexo': '0 2px 8px rgba(26,28,46,0.07), 0 1px 2px rgba(26,28,46,0.04)',
        'nexo-md': '0 4px 16px rgba(26,28,46,0.08), 0 2px 4px rgba(26,28,46,0.04)',
        'nexo-lg': '0 8px 32px rgba(26,28,46,0.10), 0 2px 8px rgba(26,28,46,0.05)',
        'nexo-purple': '0 4px 16px rgba(124,92,255,0.28), 0 1px 4px rgba(124,92,255,0.16)',
      },
    },
  },
  plugins: [],
}
