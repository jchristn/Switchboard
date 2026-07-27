import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ApiClient, ApiError } from './api';

describe('ApiClient', () => {
  beforeEach(() => {
    global.fetch = vi.fn();
  });

  it('builds the URL with basePath + query and drops empty params', async () => {
    global.fetch.mockResolvedValue({ ok: true, status: 200, text: async () => '{"FooBar":1}' });
    const c = new ApiClient('http://x:8000/', 'tok');
    const r = await c.getOrigins({ take: 5, skip: 0, search: '' });
    const url = global.fetch.mock.calls[0][0];
    expect(url).toBe('http://x:8000/_sb/v1.0/origins?take=5&skip=0');
    expect(r).toEqual({ fooBar: 1 }); // response keys camelCased
  });

  it('sends the bearer token and PascalCases request bodies', async () => {
    global.fetch.mockResolvedValue({ ok: true, status: 200, text: async () => '{}' });
    const c = new ApiClient('http://x:8000', 'tok');
    await c.createOrigin({ hostName: 'h', portNumber: 1 });
    const [, opts] = global.fetch.mock.calls[0];
    expect(opts.headers.Authorization).toBe('Bearer tok');
    expect(JSON.parse(opts.body)).toEqual({ HostName: 'h', PortNumber: 1 });
  });

  it('returns null for 204', async () => {
    global.fetch.mockResolvedValue({ ok: true, status: 204, text: async () => '' });
    const c = new ApiClient('http://x:8000', 'tok');
    expect(await c.deleteOrigin('g')).toBeNull();
  });

  it('throws ApiError with status and body on non-2xx', async () => {
    global.fetch.mockResolvedValue({
      ok: false,
      status: 400,
      text: async () => '{"Description":"bad"}',
    });
    const c = new ApiClient('http://x:8000', 'tok');
    await expect(c.getOrigins()).rejects.toMatchObject({ status: 400 });
    await expect(c.getOrigins()).rejects.toBeInstanceOf(ApiError);
  });

  it('dispatches auth:unauthorized on 401', async () => {
    global.fetch.mockResolvedValue({ ok: false, status: 401, text: async () => '' });
    const spy = vi.fn();
    window.addEventListener('auth:unauthorized', spy);
    const c = new ApiClient('http://x:8000', 'tok');
    await c.getOrigins().catch(() => {});
    expect(spy).toHaveBeenCalled();
    window.removeEventListener('auth:unauthorized', spy);
  });
});
