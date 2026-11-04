import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { reviewSubmission } from '@/features/submissions/api';
import type { AdminSubmissionRow } from '@/features/submissions/types';
import { ApiError } from '@/lib/apiClient';

import { SubmissionQueueCard } from './SubmissionQueueCard';

vi.mock('@/features/submissions/api', () => ({ reviewSubmission: vi.fn() }));

const row: AdminSubmissionRow = {
  id: 'submission-1',
  kind: 'Job',
  itemId: 'job-1',
  slug: 'junior-python-developer',
  title: 'Junior Python Developer',
  isPublished: false,
  status: 'Pending',
  note: 'My first posting',
  createdAt: '2026-10-01T12:00:00Z',
  reviewedAt: null,
  reviewNote: null,
  submitterName: 'Member One',
  submitterEmail: 'member@example.com',
};

function renderCard(
  overrides: Partial<AdminSubmissionRow> = {},
  onChanged = vi.fn(),
  onEdit = vi.fn(),
) {
  const router = createMemoryRouter(
    [
      {
        path: '/admin/submissions',
        element: (
          <SubmissionQueueCard
            row={{ ...row, ...overrides }}
            onChanged={onChanged}
            onEdit={onEdit}
          />
        ),
      },
    ],
    { initialEntries: ['/admin/submissions'] },
  );

  render(<RouterProvider router={router} />);

  return { onChanged, onEdit };
}

describe('SubmissionQueueCard', () => {
  beforeEach(() => {
    vi.mocked(reviewSubmission).mockReset();
  });

  it('approves an open submission with the note the moderator typed', async () => {
    vi.mocked(reviewSubmission).mockResolvedValue({ ...row, status: 'Approved' });
    const { onChanged } = renderCard();

    fireEvent.change(screen.getByLabelText('Decision note (optional)'), {
      target: { value: 'Reads well and the link works' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Approve and publish' }));

    await waitFor(() =>
      expect(reviewSubmission).toHaveBeenCalledWith(
        'submission-1',
        'approve',
        'Reads well and the link works',
      ),
    );
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('sends a blank note when none was written, because the note is optional', async () => {
    vi.mocked(reviewSubmission).mockResolvedValue({ ...row, status: 'Rejected' });
    renderCard();

    fireEvent.click(screen.getByRole('button', { name: 'Reject' }));

    await waitFor(() => expect(reviewSubmission).toHaveBeenCalledWith('submission-1', 'reject', null));
  });

  it('lists what is missing when approval finds a publish blocker', async () => {
    vi.mocked(reviewSubmission).mockRejectedValue(
      new ApiError(409, 'not_publishable', 'This draft does not satisfy the publish rules yet.', [
        { field: 'description', code: 'required', message: 'A description is required.' },
        { field: 'applicationUrl', code: 'invalid', message: 'Add a full https address.' },
      ]),
    );
    renderCard();

    fireEvent.click(screen.getByRole('button', { name: 'Approve and publish' }));

    expect(await screen.findByText(/cannot be approved as it stands/u)).toBeTruthy();
    expect(screen.getByText('A description is required.')).toBeTruthy();
    expect(screen.getByText('Add a full https address.')).toBeTruthy();
  });

  it('reloads the queue when another moderator got there first', async () => {
    vi.mocked(reviewSubmission).mockRejectedValue(
      new ApiError(409, 'submission_closed', 'That submission has already been decided.'),
    );
    const { onChanged } = renderCard();

    fireEvent.click(screen.getByRole('button', { name: 'Reject' }));

    expect(
      await screen.findByText('Another moderator has already decided this submission.'),
    ).toBeTruthy();
    expect(onChanged).toHaveBeenCalledTimes(1);
  });

  it('keeps the decision on screen once the submission is settled', () => {
    renderCard({
      status: 'Rejected',
      reviewedAt: '2026-10-02T12:00:00Z',
      reviewNote: 'The deadline had already passed',
    });

    expect(screen.getByText('Turned down')).toBeTruthy();
    expect(screen.getByText(/The deadline had already passed/u)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Approve and publish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull();
  });

  it('hands the row to the staff editor when Edit is pressed', () => {
    const { onEdit } = renderCard();

    fireEvent.click(screen.getByRole('button', { name: 'Edit' }));

    expect(onEdit).toHaveBeenCalledTimes(1);
    expect(onEdit).toHaveBeenCalledWith(row);
  });

  it('names who sent it and shows their note beside the draft', () => {
    renderCard();

    expect(screen.getByText(/sent by Member One/u)).toBeTruthy();
    expect(screen.getByText('“My first posting”')).toBeTruthy();
  });
});
