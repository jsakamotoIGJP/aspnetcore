// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.AspNetCore.Components.Platform;

// The change event as it crosses from JavaScript. Trimmed WebAssembly apps cannot deserialize ImmutableArray<T> directly.
internal sealed class CookieChangeEventPayload
{
    public CookieListItem[] Changed { get; init; } = [];

    public CookieListItem[] Deleted { get; init; } = [];

    public CookieChangeEvent ToCookieChangeEvent() => new()
    {
        Changed = ImmutableCollectionsMarshal.AsImmutableArray(Changed),
        Deleted = ImmutableCollectionsMarshal.AsImmutableArray(Deleted),
    };
}
