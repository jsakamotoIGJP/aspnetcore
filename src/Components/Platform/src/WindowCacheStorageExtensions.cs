// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Provides access to the Cache API from a <see cref="Window"/>.
/// </summary>
public static class WindowCacheStorageExtensions
{
    extension(Window window)
    {
        /// <summary>
        /// Gets the named caches for the current page.
        /// </summary>
        public CacheStorage Caches
            => window.GetOrAddFeature(static jsRuntime => new CacheStorage(jsRuntime));
    }
}
