import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { App } from './App'
import { SearchRunProvider } from './context/SearchRunContext'
import { I18nProvider } from './i18n'
import { LanguageSync } from './i18n/LanguageSync'
import './css/base.css'
import './css/components.css'
import './css/print.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
})

const rootEl = document.getElementById('root')
if (!rootEl) throw new Error('Root element not found')

createRoot(rootEl).render(
  <StrictMode>
    <I18nProvider>
      <QueryClientProvider client={queryClient}>
        <LanguageSync />
        <SearchRunProvider>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </SearchRunProvider>
      </QueryClientProvider>
    </I18nProvider>
  </StrictMode>,
)
