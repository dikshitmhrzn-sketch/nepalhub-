import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

/**
 * Testing Library only registers its own cleanup when a global `afterEach` exists, and this
 * project runs with Vitest globals off so every test imports what it uses. Without this, each
 * test would render on top of the previous one's DOM and a query would find the earlier page's
 * elements alongside its own.
 */
afterEach(() => {
  cleanup();
});
