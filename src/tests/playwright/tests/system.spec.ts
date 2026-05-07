import { test, expect } from '@playwright/test';

test.describe('@smoke system', () => {
  test('GET /api/system/ping returns 200', async ({ request }) => {
    const response = await request.get('/api/system/ping');
    expect(response.status()).toBe(200);
  });

  test('SPA root mounts', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('#root')).toBeVisible();
  });
});
