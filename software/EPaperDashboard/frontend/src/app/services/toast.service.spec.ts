import { afterEach, describe, expect, it, vi } from 'vitest';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  afterEach(() => vi.useRealTimers());

  it('pauses and resumes automatic dismissal', () => {
    vi.useFakeTimers();
    const service = new ToastService();
    const id = service.info('Saved', 1000);

    vi.advanceTimersByTime(400);
    service.pause(id);
    vi.advanceTimersByTime(2000);
    expect(service.toasts()).toHaveLength(1);

    service.resume(id);
    vi.advanceTimersByTime(599);
    expect(service.toasts()).toHaveLength(1);
    vi.advanceTimersByTime(1);
    expect(service.toasts()).toHaveLength(0);
  });
});
