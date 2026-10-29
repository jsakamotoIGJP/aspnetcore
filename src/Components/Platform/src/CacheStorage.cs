// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents the named caches of the Cache API for the current page.
/// </summary>
public sealed class CacheStorage : IInternalAsyncDisposal
{
    private readonly IJSRuntime _jsRuntime;
    private Task<IJSObjectReference>? _referenceTask;
    private bool _disposed;

    internal CacheStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Determines whether a cache with the specified name exists.
    /// </summary>
    /// <param name="cacheName">The name of the cache.</param>
    /// <returns><see langword="true"/> if a cache named <paramref name="cacheName"/> exists; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cacheName"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The browser does not provide the Cache API for the current page.</exception>
    public async ValueTask<bool> HasAsync(string cacheName)
    {
        ArgumentNullException.ThrowIfNull(cacheName);

        var caches = await GetReferenceAsync().ConfigureAwait(false);

        return await caches.InvokeAsync<bool>("has", cacheName).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the cache with the specified name, creating it if it does not exist.
    /// The caller owns the returned <see cref="Cache"/> and disposes it.
    /// </summary>
    /// <param name="cacheName">The name of the cache to open.</param>
    /// <returns>The cache named <paramref name="cacheName"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cacheName"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The browser does not provide the Cache API for the current page.</exception>
    public async ValueTask<Cache> OpenAsync(string cacheName)
    {
        ArgumentNullException.ThrowIfNull(cacheName);

        var caches = await GetReferenceAsync().ConfigureAwait(false);
        var reference = await caches.InvokeAsync<IJSObjectReference>("open", cacheName).ConfigureAwait(false);

        return new Cache(reference);
    }

    /// <summary>
    /// Deletes the cache with the specified name and every response stored in it, if it exists.
    /// </summary>
    /// <param name="cacheName">The name of the cache to delete.</param>
    /// <returns><see langword="true"/> if a cache named <paramref name="cacheName"/> was deleted; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cacheName"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The browser does not provide the Cache API for the current page.</exception>
    public async ValueTask<bool> DeleteAsync(string cacheName)
    {
        ArgumentNullException.ThrowIfNull(cacheName);

        var caches = await GetReferenceAsync().ConfigureAwait(false);

        return await caches.InvokeAsync<bool>("delete", cacheName).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of all caches, in the order they were created.
    /// </summary>
    /// <returns>The names of all caches.</returns>
    /// <exception cref="NotSupportedException">The browser does not provide the Cache API for the current page.</exception>
    public async ValueTask<IReadOnlyList<string>> KeysAsync()
    {
        var caches = await GetReferenceAsync().ConfigureAwait(false);

        return await caches.InvokeAsync<string[]>("keys").ConfigureAwait(false);
    }

    async ValueTask IInternalAsyncDisposal.InternalDisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Don't wait for a pending acquisition. It could hold up scope disposal until the JS interop timeout.
        if (_referenceTask is { IsCompletedSuccessfully: true } referenceTask)
        {
            await referenceTask.Result.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Task<IJSObjectReference> GetReferenceAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_referenceTask is null || _referenceTask.IsFaulted || _referenceTask.IsCanceled)
        {
            _referenceTask = AcquireReferenceAsync();
        }

        return _referenceTask;
    }

    private async Task<IJSObjectReference> AcquireReferenceAsync()
    {
        const string NotAvailableMessage = "The Cache API is not available for the current page.";

        IJSObjectReference? reference;
        try
        {
            reference = await _jsRuntime.GetValueAsync<IJSObjectReference?>("caches").ConfigureAwait(false);
        }
        catch (JSException ex)
        {
            // Interop throws for a missing window.caches, such as on an insecure page or in an older browser.
            throw new NotSupportedException(NotAvailableMessage, ex);
        }

        return reference ?? throw new NotSupportedException(NotAvailableMessage);
    }
}
