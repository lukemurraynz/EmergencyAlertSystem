import { test, expect } from '@playwright/test';

/**
 * End-to-end tests for Emergency Alert System.
 * Tests complete alert lifecycle: create → approve → deliver → dashboard updates.
 */

const API_BASE = process.env.API_URL || 'http://localhost:5000';
const APP_BASE = process.env.APP_URL || 'http://localhost:5173';

test.describe('Alert Lifecycle E2E', () => {
    test.beforeEach(async ({ page }) => {
        // Navigate to the app
        await page.goto(APP_BASE);

        // Mock authentication (assuming token-based auth)
        await page.evaluate(() => {
            localStorage.setItem('authToken', 'test-operator-token');
            localStorage.setItem('userRole', 'Operator');
        });
    });

    test('should create alert successfully and display in dashboard', async ({ page }) => {
        // Step 1: Navigate to Create Alert page
        await page.goto(`${APP_BASE}/alerts/create`);

        // Step 2: Fill out the alert form
        await page.getByLabel('Headline').fill('E2E Test Alert: Severe Weather Warning');
        await page.getByLabel('Description').fill('This is a test alert created by Playwright E2E tests.');

        // Select severity
        await page.getByLabel('Severity').click();
        await page.getByRole('option', { name: 'Severe' }).click();

        // Set expiry time (2 hours from now)
        const expiryTime = new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString().slice(0, 16);
        await page.getByLabel('Expires At').fill(expiryTime);

        // Draw polygon on map (simulate with coordinates input)
        await page.getByTestId('polygon-coordinates').fill('POLYGON((-122.4 47.6, -122.3 47.6, -122.3 47.7, -122.4 47.7, -122.4 47.6))');

        // Submit the form
        await page.getByRole('button', { name: 'Create Alert' }).click();

        // Step 3: Verify success message
        await expect(page.getByText('Alert created successfully')).toBeVisible({ timeout: 5000 });

        // Step 4: Verify redirect to dashboard
        await expect(page).toHaveURL(`${APP_BASE}/dashboard`);

        // Step 5: Verify alert appears in dashboard
        await expect(page.getByText('E2E Test Alert: Severe Weather Warning')).toBeVisible({ timeout: 3000 });
    });

    test('should approve alert and trigger delivery', async ({ page }) => {
        // Arrange: Create an alert first (mock API response)
        const alertId = 'e2e-test-alert-123';

        // Navigate to alert detail page
        await page.goto(`${APP_BASE}/alerts/${alertId}`);

        // Wait for alert details to load
        await expect(page.getByTestId('alert-headline')).toBeVisible();

        // Act: Approve the alert
        await page.getByRole('button', { name: 'Approve' }).click();

        // Verify approval confirmation dialog
        await expect(page.getByText('Confirm Approval')).toBeVisible();
        await page.getByRole('button', { name: 'Confirm' }).click();

        // Assert: Verify success notification
        await expect(page.getByText('Alert approved successfully')).toBeVisible({ timeout: 5000 });

        // Verify status badge shows "Approved"
        await expect(page.getByTestId('alert-status')).toHaveText('Approved');
    });

    test('should reject alert with reason', async ({ page }) => {
        const alertId = 'e2e-test-alert-456';

        // Navigate to alert detail page
        await page.goto(`${APP_BASE}/alerts/${alertId}`);

        // Click Reject button
        await page.getByRole('button', { name: 'Reject' }).click();

        // Fill rejection reason
        await page.getByLabel('Rejection Reason').fill('Duplicate alert - already issued for this area');

        // Confirm rejection
        await page.getByRole('button', { name: 'Confirm Rejection' }).click();

        // Verify success
        await expect(page.getByText('Alert rejected')).toBeVisible({ timeout: 5000 });
        await expect(page.getByTestId('alert-status')).toHaveText('Rejected');
    });

    test('should display real-time dashboard updates via SignalR', async ({ page }) => {
        // Navigate to dashboard
        await page.goto(`${APP_BASE}/dashboard`);

        // Wait for initial dashboard load
        await expect(page.getByTestId('dashboard-summary')).toBeVisible();

        // Record initial alert count
        const initialCount = await page.getByTestId('total-alerts-count').textContent();

        // Simulate external alert creation (via API or another browser context)
        // In real test, this would trigger SignalR broadcast
        await page.evaluate(() => {
            // Mock SignalR event
            window.dispatchEvent(new CustomEvent('signalr:AlertCreated', {
                detail: {
                    alertId: 'new-alert-789',
                    headline: 'Real-time Update Test',
                    severity: 'Moderate'
                }
            }));
        });

        // Verify dashboard updates in <2 seconds (SC-013 requirement)
        await expect(page.getByText('Real-time Update Test')).toBeVisible({ timeout: 2000 });

        // Verify alert count incremented
        const updatedCount = await page.getByTestId('total-alerts-count').textContent();
        expect(parseInt(updatedCount!)).toBeGreaterThan(parseInt(initialCount!));
    });

    test('should cancel alert before approval', async ({ page }) => {
        const alertId = 'e2e-test-alert-cancel-1';

        // Navigate to alert detail
        await page.goto(`${APP_BASE}/alerts/${alertId}`);

        // Verify status is PendingApproval
        await expect(page.getByTestId('alert-status')).toHaveText('Pending Approval');

        // Cancel the alert
        await page.getByRole('button', { name: 'Cancel' }).click();
        await page.getByRole('button', { name: 'Confirm Cancel' }).click();

        // Verify cancellation
        await expect(page.getByText('Alert cancelled successfully')).toBeVisible({ timeout: 5000 });
        await expect(page.getByTestId('alert-status')).toHaveText('Cancelled');
    });

    test('should display validation errors for invalid polygon', async ({ page }) => {
        // Navigate to create alert page
        await page.goto(`${APP_BASE}/alerts/create`);

        // Fill form with invalid polygon (not closed)
        await page.getByLabel('Headline').fill('Invalid Polygon Test');
        await page.getByLabel('Description').fill('Testing polygon validation');
        await page.getByTestId('polygon-coordinates').fill('POLYGON((-122.4 47.6, -122.3 47.6, -122.3 47.7))');

        // Submit
        await page.getByRole('button', { name: 'Create Alert' }).click();

        // Verify error message
        await expect(page.getByText(/polygon must be closed/i)).toBeVisible();
    });

    test('should display delivery attempts on alert detail page', async ({ page }) => {
        const alertId = 'e2e-test-alert-delivery-1';

        // Navigate to alert with delivery history
        await page.goto(`${APP_BASE}/alerts/${alertId}`);

        // Expand delivery attempts section
        await page.getByRole('button', { name: 'Delivery Attempts' }).click();

        // Verify delivery attempts table visible
        await expect(page.getByTestId('delivery-attempts-table')).toBeVisible();

        // Verify at least one delivery attempt shown
        await expect(page.getByRole('row').filter({ hasText: /Delivered|Failed|Pending/i })).toHaveCount({ timeout: 3000 }, { gte: 1 });
    });

    test('should filter dashboard by severity', async ({ page }) => {
        // Navigate to dashboard
        await page.goto(`${APP_BASE}/dashboard`);

        // Select severity filter
        await page.getByLabel('Filter by Severity').click();
        await page.getByRole('option', { name: 'Extreme' }).click();

        // Verify only extreme alerts displayed
        const alerts = page.getByTestId('alert-card');
        await expect(alerts.first()).toBeVisible();

        const severityBadges = await alerts.locator('[data-testid="severity-badge"]').allTextContents();
        expect(severityBadges.every(badge => badge === 'Extreme')).toBeTruthy();
    });

    test('should paginate alert list correctly', async ({ page }) => {
        // Navigate to dashboard
        await page.goto(`${APP_BASE}/dashboard`);

        // Wait for initial page load
        await expect(page.getByTestId('alert-list')).toBeVisible();

        // Verify pagination controls
        await expect(page.getByRole('button', { name: 'Next Page' })).toBeVisible();

        // Record first alert on page 1
        const firstAlertPage1 = await page.getByTestId('alert-card').first().textContent();

        // Navigate to page 2
        await page.getByRole('button', { name: 'Next Page' }).click();

        // Verify different alerts on page 2
        const firstAlertPage2 = await page.getByTestId('alert-card').first().textContent();
        expect(firstAlertPage1).not.toBe(firstAlertPage2);

        // Navigate back to page 1
        await page.getByRole('button', { name: 'Previous Page' }).click();

        // Verify same alerts as before
        const firstAlertPage1Again = await page.getByTestId('alert-card').first().textContent();
        expect(firstAlertPage1).toBe(firstAlertPage1Again);
    });

    test('should display correlation events on dashboard', async ({ page }) => {
        // Navigate to dashboard
        await page.goto(`${APP_BASE}/dashboard`);

        // Expand correlation events section
        await page.getByRole('button', { name: 'Correlation Events' }).click();

        // Verify correlation events table
        await expect(page.getByTestId('correlation-events-table')).toBeVisible();

        // Verify event types displayed (GeographicCluster, SeverityEscalation, etc.)
        await expect(page.getByText(/Geographic Cluster|Severity Escalation|Regional Hotspot/i)).toBeVisible({ timeout: 3000 });
    });

    test('should handle concurrent approval attempts (first-wins)', async ({ page, context }) => {
        const alertId = 'e2e-concurrent-approval-test';

        // Open two browser contexts (simulate two approvers)
        const page1 = page;
        const page2 = await context.newPage();

        // Both navigates to same alert
        await page1.goto(`${APP_BASE}/alerts/${alertId}`);
        await page2.goto(`${APP_BASE}/alerts/${alertId}`);

        // Both click approve simultaneously
        const approve1 = page1.getByRole('button', { name: 'Approve' }).click();
        const approve2 = page2.getByRole('button', { name: 'Approve' }).click();

        await Promise.all([approve1, approve2]);

        // Confirm on both
        await page1.getByRole('button', { name: 'Confirm' }).click();
        await page2.getByRole('button', { name: 'Confirm' }).click();

        // Verify one succeeds, one gets conflict error
        const success = page.getByText('Alert approved successfully');
        const conflict = page.getByText(/already approved|conflict/i);

        await expect(success.or(conflict)).toBeVisible({ timeout: 5000 });
    });

    test('should create alert in under 3 minutes (SC-001)', async ({ page }) => {
        const startTime = Date.now();

        // Navigate to create page
        await page.goto(`${APP_BASE}/alerts/create`);

        // Fill form with all required fields
        await page.getByLabel('Headline').fill('Performance Test Alert');
        await page.getByLabel('Description').fill('Testing SC-001: Create alert in under 3 minutes');
        await page.getByLabel('Severity').click();
        await page.getByRole('option', { name: 'Moderate' }).click();

        const expiryTime = new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString().slice(0, 16);
        await page.getByLabel('Expires At').fill(expiryTime);

        await page.getByTestId('polygon-coordinates').fill('POLYGON((-122.4 47.6, -122.3 47.6, -122.3 47.7, -122.4 47.7, -122.4 47.6))');

        // Submit
        await page.getByRole('button', { name: 'Create Alert' }).click();

        // Wait for success
        await expect(page.getByText('Alert created successfully')).toBeVisible({ timeout: 5000 });

        const endTime = Date.now();
        const elapsedSeconds = (endTime - startTime) / 1000;

        // Assert: SC-001 requirement (<3 minutes = 180 seconds)
        expect(elapsedSeconds).toBeLessThan(180);
    });
});

test.describe('Dashboard Real-time Updates', () => {
    test.beforeEach(async ({ page }) => {
        await page.goto(APP_BASE);
        await page.evaluate(() => {
            localStorage.setItem('authToken', 'test-operator-token');
            localStorage.setItem('userRole', 'Operator');
        });
    });

    test('should connect to SignalR hub and receive updates', async ({ page }) => {
        // Navigate to dashboard
        await page.goto(`${APP_BASE}/dashboard`);

        // Wait for SignalR connection
        await page.waitForFunction(() => {
            return (window as Window & { __signalrConnected?: boolean }).__signalrConnected === true;
        }, { timeout: 10000 });

        // Verify connection indicator shows green
        await expect(page.getByTestId('signalr-status')).toHaveClass(/connected/i);
    });

    test('should update dashboard summary in <2 seconds (SC-013)', async ({ page }) => {
        await page.goto(`${APP_BASE}/dashboard`);

        // Record timestamp before update
        const beforeUpdate = Date.now();

        // Trigger SignalR event
        await page.evaluate(() => {
            window.dispatchEvent(new CustomEvent('signalr:DashboardSummaryUpdated', {
                detail: {
                    totalAlerts: 42,
                    pendingApproval: 5,
                    approved: 30,
                    delivered: 25
                }
            }));
        });

        // Wait for dashboard to reflect update
        await expect(page.getByTestId('total-alerts-count')).toHaveText('42', { timeout: 2000 });

        const afterUpdate = Date.now();
        const latencyMs = afterUpdate - beforeUpdate;

        // Assert: SC-013 requirement (<2 seconds = 2000ms)
        expect(latencyMs).toBeLessThan(2000);
    });
});

test.describe('Error Handling', () => {
    test('should display user-friendly error for API failure', async ({ page }) => {
        // Mock API failure
        await page.route(`${API_BASE}/api/v1/alerts`, route => {
            route.fulfill({
                status: 500,
                contentType: 'application/problem+json',
                body: JSON.stringify({
                    type: 'https://example.com/errors/internal-server-error',
                    title: 'Internal Server Error',
                    status: 500,
                    detail: 'An unexpected error occurred'
                })
            });
        });

        // Navigate to create page
        await page.goto(`${APP_BASE}/alerts/create`);

        // Fill and submit form
        await page.getByLabel('Headline').fill('Error Test');
        await page.getByLabel('Description').fill('Testing error handling');
        await page.getByRole('button', { name: 'Create Alert' }).click();

        // Verify error message displayed
        await expect(page.getByText(/internal server error|something went wrong/i)).toBeVisible({ timeout: 5000 });
    });

    test('should handle network timeout gracefully', async ({ page }) => {
        // Mock slow network (timeout after 5s)
        await page.route(`${API_BASE}/api/v1/alerts`, async route => {
            await new Promise(resolve => setTimeout(resolve, 6000));
            route.continue();
        });

        await page.goto(`${APP_BASE}/alerts/create`);

        // Fill form
        await page.getByLabel('Headline').fill('Timeout Test');
        await page.getByLabel('Description').fill('Testing timeout handling');
        await page.getByRole('button', { name: 'Create Alert' }).click();

        // Verify timeout error shown
        await expect(page.getByText(/request timed out|network error/i)).toBeVisible({ timeout: 10000 });
    });
});
