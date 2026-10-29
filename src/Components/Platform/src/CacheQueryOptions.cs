// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents the options that control how a request is matched against the entries of a cache.
/// </summary>
public sealed class CacheQueryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to ignore the query string of the URL. The default is <see langword="false"/>.
    /// </summary>
    public bool IgnoreSearch { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to match requests with any HTTP method, not only GET and HEAD. The default is <see langword="false"/>.
    /// </summary>
    public bool IgnoreMethod { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to ignore the <c>Vary</c> header of the cached response. The default is <see langword="false"/>.
    /// </summary>
    public bool IgnoreVary { get; set; }
}
