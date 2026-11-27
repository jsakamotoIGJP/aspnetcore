// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Components.Platform;

/// <summary>
/// Represents a change to the cookies visible to the current page.
/// </summary>
public sealed class CookieChangeEvent
{
    /// <summary>
    /// Gets the cookies that were created or changed.
    /// </summary>
    public ImmutableArray<CookieListItem> Changed { get; init; } = [];

    /// <summary>
    /// Gets the cookies that were deleted. Their <see cref="CookieListItem.Value"/> is <see langword="null"/>.
    /// </summary>
    public ImmutableArray<CookieListItem> Deleted { get; init; } = [];
}
