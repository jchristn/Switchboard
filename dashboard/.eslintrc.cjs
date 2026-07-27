module.exports = {
  root: true,
  env: { browser: true, es2021: true, node: true },
  extends: [
    'eslint:recommended',
    'plugin:react/recommended',
    'plugin:react/jsx-runtime',
    'plugin:react-hooks/recommended',
  ],
  parserOptions: { ecmaVersion: 'latest', sourceType: 'module', ecmaFeatures: { jsx: true } },
  settings: { react: { version: 'detect' } },
  plugins: ['react-refresh'],
  ignorePatterns: ['dist', 'node_modules', 'coverage', 'playwright-report'],
  rules: {
    // Dev-only HMR hint; several modules intentionally export helpers/hooks alongside a component
    // (chart range constants, table utilities, context hooks). Not a correctness rule.
    'react-refresh/only-export-components': 'off',
    'no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
    // PropTypes are used on shared primitives but not enforced on route views.
    'react/prop-types': 'off',
  },
};
