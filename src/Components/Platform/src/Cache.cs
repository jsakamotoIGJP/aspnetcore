// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Internal;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents one named cache of the Cache API, opened by <see cref="CacheStorage.OpenAsync(string)"/>.
/// The caller owns the instance and disposes it. Disposing it does not delete the cache or its entries.
/// </summary>
public sealed class Cache : IAsyncDisposable
{
    private readonly IJSObjectReference _reference;
    private bool _disposed;

    internal Cache(IJSObjectReference reference)
    {
        _reference = reference;
    }

    /// <summary>
    /// Fetches the specified URL and stores the response in the cache.
    /// </summary>
    /// <param name="request">The URL to fetch and store.</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public ValueTask AddAsync(string request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _reference.InvokeVoidAsync("add", request);
    }

    /// <summary>
    /// Deletes the entry for the specified URL, if it exists.
    /// </summary>
    /// <param name="request">The URL of the entry to delete.</param>
    /// <returns><see langword="true"/> if an entry was deleted; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public ValueTask<bool> DeleteAsync(string request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _reference.InvokeAsync<bool>("delete", request);
    }

    /// <summary>
    /// Deletes the entries that match the specified URL with the specified options, if any exist.
    /// </summary>
    /// <param name="request">The URL of the entries to delete.</param>
    /// <param name="options">The options that control how <paramref name="request"/> is matched.</param>
    /// <returns><see langword="true"/> if an entry was deleted; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    [DynamicDependency(LinkerFlags.JsonSerialized, typeof(CacheQueryOptions))]
    public ValueTask<bool> DeleteAsync(string request, CacheQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _reference.InvokeAsync<bool>("delete", request, options);
    }

    /// <summary>
    /// Releases the browser object that this instance represents. The cache and its entries stay in the browser.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;

        return _reference.DisposeAsync();
    }
}
