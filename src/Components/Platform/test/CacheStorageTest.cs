// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Xunit;

namespace Microsoft.AspNetCore.Components.Platform.Tests;

public class CacheStorageTest
{
    [Fact]
    public async Task Caches_IsStableAndNotAcquiredBeforeUse()
    {
        var jsRuntime = new RecordingJSRuntime();
        await using var provider = CreateServiceProvider(jsRuntime);
        var window = provider.GetRequiredService<IBrowserPlatform>().Window;

        Assert.Same(window.Caches, window.Caches);
        Assert.Equal(0, jsRuntime.GetValueCallCount);
    }

    [Fact]
    public async Task Caches_ForwardsOperationsAndAcquiresReferenceOnce()
    {
        var jsRuntime = new RecordingJSRuntime();
        await using var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;

        var has = await caches.HasAsync("v1");
        var deleted = await caches.DeleteAsync("v1");
        var keys = await caches.KeysAsync();

        Assert.True(has);
        Assert.True(deleted);
        Assert.Equal(["v1", "v2"], keys);
        Assert.Equal(1, jsRuntime.GetValueCallCount);
        Assert.Equal("caches", jsRuntime.RequestedProperty);
        Assert.Collection(
            jsRuntime.ObjectReference.Invocations,
            invocation => AssertInvocation(invocation, "has", "v1"),
            invocation => AssertInvocation(invocation, "delete", "v1"),
            invocation => AssertInvocation(invocation, "keys"));
    }

    [Fact]
    public async Task Caches_SharesPendingAcquisitionBetweenCallers()
    {
        var pending = new TaskCompletionSource<IJSObjectReference>();
        var jsRuntime = new RecordingJSRuntime { PendingNextGetValue = pending };
        await using var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;

        var first = caches.KeysAsync().AsTask();
        var second = caches.HasAsync("v1").AsTask();
        pending.SetResult(jsRuntime.ObjectReference);
        await Task.WhenAll(first, second);

        Assert.Equal(1, jsRuntime.GetValueCallCount);
        Assert.Equal(2, jsRuntime.ObjectReference.Invocations.Count);
    }

    [Fact]
    public async Task Caches_RejectsNullArguments()
    {
        var jsRuntime = new RecordingJSRuntime();
        await using var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;

        await Assert.ThrowsAsync<ArgumentNullException>("cacheName", () => caches.HasAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>("cacheName", () => caches.DeleteAsync(null!).AsTask());
        Assert.Equal(0, jsRuntime.GetValueCallCount);
    }

    [Fact]
    public async Task Caches_ReacquiresReferenceAfterAcquisitionIsCanceled()
    {
        var jsRuntime = new RecordingJSRuntime { CancelNextGetValue = true };
        await using var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => caches.KeysAsync().AsTask());
        var keys = await caches.KeysAsync();

        Assert.Equal(2, keys.Count);
        Assert.Equal(2, jsRuntime.GetValueCallCount);
    }

    [Fact]
    public async Task Caches_ThrowsNotSupportedExceptionWhenTheBrowserHasNoCaches()
    {
        // JS interop throws this for a missing window.caches, such as on a page that is not a secure context.
        var missingProperty = new JSException("The property 'caches' is not defined or is not readable.");
        var jsRuntime = new RecordingJSRuntime { GetValueException = missingProperty };
        var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;
        Func<Task>[] members =
        [
            () => caches.HasAsync("v1").AsTask(),
            () => caches.DeleteAsync("v1").AsTask(),
            () => caches.KeysAsync().AsTask(),
        ];

        foreach (var member in members)
        {
            var exception = await Assert.ThrowsAsync<NotSupportedException>(member);
            Assert.Same(missingProperty, exception.InnerException);
        }
        // Disposal must not throw for the failed acquisitions.
        await provider.DisposeAsync();

        Assert.Equal(members.Length, jsRuntime.GetValueCallCount);
        Assert.Empty(jsRuntime.ObjectReference.Invocations);
    }

    [Fact]
    public async Task Caches_ThrowsNotSupportedExceptionWhenCachesIsNull()
    {
        var jsRuntime = new RecordingJSRuntime { CachesNull = true };
        var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;

        await Assert.ThrowsAsync<NotSupportedException>(() => caches.KeysAsync().AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => caches.DeleteAsync("v1").AsTask());
        // Disposal must not throw for the failed acquisitions.
        await provider.DisposeAsync();

        Assert.Equal(2, jsRuntime.GetValueCallCount);
        Assert.Empty(jsRuntime.ObjectReference.Invocations);
    }

    [Fact]
    public async Task Caches_PassesThroughOtherAcquisitionFailuresAndReacquires()
    {
        var disconnected = new JSDisconnectedException("The circuit is disconnected.");
        var jsRuntime = new RecordingJSRuntime { GetValueException = disconnected };
        await using var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;

        var exception = await Assert.ThrowsAsync<JSDisconnectedException>(() => caches.KeysAsync().AsTask());
        jsRuntime.GetValueException = null;
        var keys = await caches.KeysAsync();

        Assert.Same(disconnected, exception);
        Assert.Equal(2, keys.Count);
        Assert.Equal(2, jsRuntime.GetValueCallCount);
    }

    [Fact]
    public async Task ScopeDisposal_DisposesTheAcquiredReference()
    {
        var jsRuntime = new RecordingJSRuntime();
        var provider = CreateServiceProvider(jsRuntime);

        await using (provider)
        {
            await provider.GetRequiredService<IBrowserPlatform>().Window.Caches.KeysAsync();
            Assert.False(jsRuntime.ObjectReference.Disposed);
        }

        Assert.True(jsRuntime.ObjectReference.Disposed);
    }

    [Fact]
    public async Task ScopeDisposal_MakesCachesUnusable()
    {
        var jsRuntime = new RecordingJSRuntime();
        var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;
        await caches.KeysAsync();

        await provider.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => caches.KeysAsync().AsTask());
        Assert.Equal(1, jsRuntime.GetValueCallCount);
    }

    [Fact]
    public async Task ScopeDisposal_DoesNotWaitForPendingAcquisition()
    {
        var neverCompletes = new TaskCompletionSource<IJSObjectReference>();
        var jsRuntime = new RecordingJSRuntime { PendingNextGetValue = neverCompletes };
        var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;

        _ = caches.KeysAsync();

        await provider.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(jsRuntime.ObjectReference.Disposed);
    }

    [Fact]
    public async Task OpenAsync_ReturnsACacheThatForwardsOperationsToTheOpenedObject()
    {
        var jsRuntime = new RecordingJSRuntime();
        await using var provider = CreateServiceProvider(jsRuntime);
        var options = new CacheQueryOptions { IgnoreSearch = true };

        await using var cache = await provider.GetRequiredService<IBrowserPlatform>().Window.Caches.OpenAsync("v1");
        await cache.AddAsync("/data");
        var deleted = await cache.DeleteAsync("/data");
        var deletedWithOptions = await cache.DeleteAsync("/data", options);

        Assert.True(deleted);
        Assert.True(deletedWithOptions);
        AssertInvocation(Assert.Single(jsRuntime.ObjectReference.Invocations), "open", "v1");
        Assert.Collection(
            jsRuntime.ObjectReference.OpenedCache!.Invocations,
            invocation => AssertInvocation(invocation, "add", "/data"),
            invocation => AssertInvocation(invocation, "delete", "/data"),
            invocation => AssertInvocation(invocation, "delete", "/data", options));
    }

    [Fact]
    public async Task Cache_DisposeReleasesTheObjectOnceAndMakesTheCacheUnusable()
    {
        var jsRuntime = new RecordingJSRuntime();
        await using var provider = CreateServiceProvider(jsRuntime);
        var cache = await provider.GetRequiredService<IBrowserPlatform>().Window.Caches.OpenAsync("v1");
        var reference = jsRuntime.ObjectReference.OpenedCache!;

        await cache.DisposeAsync();
        await cache.DisposeAsync();

        Assert.Equal(1, reference.DisposeCallCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.AddAsync("/data").AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.DeleteAsync("/data").AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => cache.DeleteAsync("/data", new CacheQueryOptions()).AsTask());
        Assert.Empty(reference.Invocations);
        // The scope does not own the cache, and the shared CacheStorage stays usable.
        Assert.False(jsRuntime.ObjectReference.Disposed);
    }

    [Fact]
    public async Task Cache_RejectsNullArguments()
    {
        var jsRuntime = new RecordingJSRuntime();
        await using var provider = CreateServiceProvider(jsRuntime);
        var caches = provider.GetRequiredService<IBrowserPlatform>().Window.Caches;
        await Assert.ThrowsAsync<ArgumentNullException>("cacheName", () => caches.OpenAsync(null!).AsTask());
        await using var cache = await caches.OpenAsync("v1");

        await Assert.ThrowsAsync<ArgumentNullException>("request", () => cache.AddAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>("request", () => cache.DeleteAsync(null!).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>("request", () => cache.DeleteAsync(null!, new CacheQueryOptions()).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>("options", () => cache.DeleteAsync("/data", null!).AsTask());
        Assert.Empty(jsRuntime.ObjectReference.OpenedCache!.Invocations);
    }

    // A mock IJSRuntime records arguments before serialization, so this test checks the JSON the browser receives.
    [Fact]
    public async Task DeleteAsync_SendsEveryCacheQueryOption()
    {
        var jsRuntime = new SerializingJSRuntime();
        await using var provider = CreateServiceProvider(jsRuntime);
        await using var cache = await provider.GetRequiredService<IBrowserPlatform>().Window.Caches.OpenAsync("v1");

        await cache.DeleteAsync("/data", new CacheQueryOptions());
        await cache.DeleteAsync("/data", new CacheQueryOptions { IgnoreSearch = true, IgnoreMethod = true, IgnoreVary = true });
        await cache.DeleteAsync("/data");

        Assert.Equal(
            [
                """["/data",{"ignoreSearch":false,"ignoreMethod":false,"ignoreVary":false}]""",
                """["/data",{"ignoreSearch":true,"ignoreMethod":true,"ignoreVary":true}]""",
                """["/data"]""",
            ],
            jsRuntime.ArgumentsSentTo("delete"));
    }

    private static ServiceProvider CreateServiceProvider(IJSRuntime jsRuntime)
    {
        var services = new ServiceCollection();
        services.AddSingleton(jsRuntime);
        services.AddBrowserPlatform();

        return services.BuildServiceProvider();
    }

    private static void AssertInvocation(Invocation invocation, string identifier, params object?[] arguments)
    {
        Assert.Equal(identifier, invocation.Identifier);
        Assert.Equal(arguments, invocation.Arguments);
    }

    private sealed class RecordingJSRuntime : IJSRuntime
    {
        public RecordingJSObjectReference ObjectReference { get; } = new();

        public int GetValueCallCount { get; private set; }

        public string? RequestedProperty { get; private set; }

        public bool CancelNextGetValue { get; set; }

        public bool CachesNull { get; set; }

        public Exception? GetValueException { get; set; }

        public TaskCompletionSource<IJSObjectReference>? PendingNextGetValue { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TValue> GetValueAsync<TValue>(string identifier)
        {
            GetValueCallCount++;
            RequestedProperty = identifier;

            if (CancelNextGetValue)
            {
                CancelNextGetValue = false;

                return ValueTask.FromCanceled<TValue>(new CancellationToken(canceled: true));
            }

            if (PendingNextGetValue is { } pending)
            {
                PendingNextGetValue = null;

                return new ValueTask<TValue>((Task<TValue>)(object)pending.Task);
            }

            if (GetValueException is { } exception)
            {
                return ValueTask.FromException<TValue>(exception);
            }

            // JS interop returns a null reference when window.caches exists and holds null or undefined.
            return ValueTask.FromResult(CachesNull ? default! : (TValue)(object)ObjectReference);
        }
    }

    private sealed class RecordingJSObjectReference : IJSObjectReference
    {
        public List<Invocation> Invocations { get; } = [];

        public RecordingJSObjectReference? OpenedCache { get; private set; }

        public int DisposeCallCount { get; private set; }

        public bool Disposed => DisposeCallCount > 0;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Invocations.Add(new Invocation(identifier, args ?? []));

            object? result = identifier switch
            {
                "open" => OpenedCache = new RecordingJSObjectReference(),
                "has" or "delete" => true,
                "keys" => new[] { "v1", "v2" },
                _ => default(TValue),
            };

            return ValueTask.FromResult((TValue?)result!);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }

    private sealed record Invocation(string Identifier, object?[] Arguments);

    // Runs the real JSRuntime serializer and answers each call as the browser would.
    private sealed class SerializingJSRuntime : JSRuntime
    {
        private readonly List<(string Identifier, string? ArgsJson)> _calls = [];

        public string[] ArgumentsSentTo(string identifier)
            => [.. _calls.Where(call => call.Identifier == identifier).Select(call => call.ArgsJson ?? string.Empty)];

        protected override void BeginInvokeJS(in JSInvocationInfo invocationInfo)
        {
            _calls.Add((invocationInfo.Identifier, invocationInfo.ArgsJson));

            var result = invocationInfo.Identifier switch
            {
                "caches" => """{"__jsObjectId":1}""",
                "open" => """{"__jsObjectId":2}""",
                "delete" => "true",
                _ => "null",
            };

            if (invocationInfo.AsyncHandle != 0)
            {
                DotNetDispatcher.EndInvokeJS(this, $"[{invocationInfo.AsyncHandle},true,{result}]");
            }
        }

        protected override void BeginInvokeJS(long taskId, string identifier, string? argsJson, JSCallResultType resultType, long targetInstanceId)
            => throw new NotSupportedException();

        protected override void EndInvokeDotNet(DotNetInvocationInfo invocationInfo, in DotNetInvocationResult invocationResult)
            => throw new NotSupportedException();
    }
}
