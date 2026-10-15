import js from '@eslint/js';
import boundaries from 'eslint-plugin-boundaries';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import { defineConfig, globalIgnores } from 'eslint/config';
import globals from 'globals';
import tseslint from 'typescript-eslint';

// facility-0065: MenuNest's eslint 9 config plus eslint-plugin-boundaries for the folder rules of facility-0025.
export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat['recommended-latest'],
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
    },
    rules: {
      // facility-0066: text people type is rendered as React text only
      'no-restricted-syntax': [
        'error',
        {
          selector: "JSXAttribute[name.name='dangerouslySetInnerHTML']",
          message: 'facility-0066: render text people type as React text, never as HTML.',
        },
      ],
    },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/**/*.test.{ts,tsx}', 'src/test/**'],
    plugins: { boundaries },
    settings: {
      'import/resolver': { node: { extensions: ['.ts', '.tsx'] } },
      'boundaries/elements': [
        { type: 'pages', pattern: 'src/pages/*', capture: ['page'] },
        { type: 'shared', pattern: 'src/shared' },
        { type: 'store', pattern: 'src/store' },
      ],
      // Element descriptors match folders only, so the three app files are classified as files
      'boundaries/files': [{ category: 'app', pattern: 'src/{main,App,router}.tsx' }],
    },
    rules: {
      'boundaries/no-unknown-files': 'error',
      'boundaries/dependencies': [
        'error',
        {
          default: 'disallow',
          policies: [
            { from: { file: { categories: 'app' } }, allow: { to: { element: { types: { anyOf: ['pages', 'shared', 'store'] } } } } },
            { from: { file: { categories: 'app' } }, allow: { to: { file: { categories: 'app' } } } },
            { from: { element: { type: 'pages' } }, allow: { to: { element: { types: { anyOf: ['shared', 'store'] } } } } },
            { from: { element: { type: 'store' } }, allow: { to: { element: { types: { anyOf: ['shared', 'pages'] } } } } },
            { from: { element: { type: 'shared' } }, allow: { to: { element: { type: 'store' } } } },
          ],
        },
      ],
    },
  },
]);
