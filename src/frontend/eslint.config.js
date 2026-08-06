import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'

// The rule that earns this config its keep is react-hooks/rules-of-hooks: LonglistTable shipped an
// early return in front of five useMemo calls, which changes the hook count between renders and
// throws once the guard's condition flips. Nothing in the toolchain would have caught it — tsc
// can't see it and the crash only happens on a specific data transition.
export default tseslint.config(
  { ignores: ['dist', 'coverage', 'node_modules'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs['recommended-latest'].rules,
      // rules-of-hooks stays an error — it is the guard this config exists for, and the tree is clean.
      'react-hooks/rules-of-hooks': 'error',
      // Warn, not error: seven call sites mirror a server value into local state from an effect
      // (MarkButton/StatusSelect optimistic state, I18nProvider's pinned locale, form seeding). The
      // pattern predates this config and unwinding it is a behavioural change, not a lint fix — see
      // the todo.md entry. New code should derive state or key the component instead.
      'react-hooks/set-state-in-effect': 'warn',
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      // The codebase leans on inferred types and uses `_`-prefixed names for intentional discards.
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
    },
  },
  {
    // Build/test tooling runs in Node, not the browser.
    files: ['*.config.{ts,js}', 'src/test-setup.ts'],
    languageOptions: { globals: globals.node },
  },
)
