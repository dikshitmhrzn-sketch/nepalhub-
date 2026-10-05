import { render, screen } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useFeaturedBusinesses } from '@/features/businesses/useBusinesses';
import { ApiError } from '@/lib/apiClient';

import { FeaturedStrip } from './FeaturedStrip';

vi.mock('@/features/businesses/useBusinesses', () => ({ useFeaturedBusinesses: vi.fn() }));

const state = vi.mocked(useFeaturedBusinesses);

function renderStrip() {
  const router = createMemoryRouter([{ path: '/businesses', element: <FeaturedStrip /> }], {
    initialEntries: ['/businesses'],
  });

  render(<RouterProvider router={router} />);
}

const listing = {
  id: 'business-1',
  slug: 'kath-cafe',
  name: 'Kath Cafe',
  category: 'Cafes' as const,
  districtName: 'Kathmandu',
  provinceName: 'Bagmati',
  address: null,
  phone: null,
  isVerified: true,
  isOpenNow: true,
  createdAt: '2026-09-01T00:00:00Z',
};

describe('FeaturedStrip', () => {
  beforeEach(() => {
    state.mockReset();
  });

  it('shows the paid listings with their labels, as the API returned them', () => {
    state.mockReturnValue({
      status: 'success',
      data: [listing, { ...listing, id: 'business-2', slug: 'mustang-lodge', name: 'Mustang Lodge' }],
      error: null,
      reload: vi.fn(),
    });

    renderStrip();

    expect(screen.getByRole('heading', { name: 'Featured listings' })).toBeTruthy();
    expect(screen.getByText('Paid placements, shown alphabetically.')).toBeTruthy();

    const links = screen.getAllByRole('link');
    expect(links.map((link) => link.getAttribute('href'))).toEqual([
      '/businesses/kath-cafe',
      '/businesses/mustang-lodge',
    ]);
    expect(screen.getAllByText('Featured')).toHaveLength(2);
  });

  it('renders nothing at all when nobody holds a slot, rather than an empty shelf', () => {
    state.mockReturnValue({ status: 'success', data: [], error: null, reload: vi.fn() });

    renderStrip();

    expect(screen.queryByRole('heading', { name: 'Featured listings' })).toBeNull();
  });

  it('keeps the directory untouched when the strip cannot be fetched', () => {
    state.mockReturnValue({
      status: 'error',
      data: null,
      error: new ApiError(0, 'network_error', 'Could not reach the server.'),
      reload: vi.fn(),
    });

    renderStrip();

    expect(screen.queryByRole('heading', { name: 'Featured listings' })).toBeNull();
  });

  it('stays out of the way while the strip is still loading', () => {
    state.mockReturnValue({ status: 'loading', data: null, error: null, reload: vi.fn() });

    renderStrip();

    expect(screen.queryByRole('heading', { name: 'Featured listings' })).toBeNull();
  });
});
