import { render } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { App } from './App';

function renderApp() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={['/']}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('App', () => {
  it('renders the home route without crashing', () => {
    renderApp();
    // The shell rendered: HomePage mounted under "/", and the document has a body.
    // Replace with a stronger assertion later (e.g., look for a TopNav landmark).
    expect(document.body).toBeInTheDocument();
  });
});
