// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.AspNetCore.Components.Testing.Infrastructure;
using Microsoft.AspNetCore.Components.Testing.Playwright;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PlatformTestApp.Components;
using PlatformTestApp.E2E.Tests.Fixtures;

namespace PlatformTestApp.E2E.Tests.Tests;

[UITest]
public partial class CacheStorageTests : BrowserTest
{
    private static readonly string[] CacheStoragePaths = ["/cachestorage-server", "/cachestorage-wasm"];

    private ServerInstance _server = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    protected override async Task InitializeCoreAsync()
    {
        await base.InitializeCoreAsync();
        _server = await StartServerAsync<App>(TestRoot.Servers);
        _context = await NewContext(new BrowserNewContextOptions().WithServerRouting(_server));
        _page = await _context.NewPageAsync();
    }

    [TestMethod]
    public async Task Caches_FindsListsAndDeletesCachesInBothRenderModes()
    {
        foreach (var path in CacheStoragePaths)
        {
            await NavigateAsync(path, "#cache-keys");

            // The projection cannot create a cache yet, so the page's own Cache API creates them.
            var prefix = path == "/cachestorage-server" ? "server" : "wasm";
            await _page.EvaluateAsync("async names => { for (const name of names) await caches.open(name); }", new[] { $"{prefix}-first", $"{prefix}-second" });

            CollectionAssert.IsSubsetOf(new[] { $"{prefix}-first", $"{prefix}-second" }, await GetBrowserCacheNamesAsync());
            await RunAsync("#cache-keys", await FormatBrowserCacheNamesAsync());
            await _page.Locator("#cache-name").FillAsync($"{prefix}-first");
            await RunAsync("#has-cache", "Has: True");

            await RunAsync("#delete-cache", "Delete: True");
            Assert.IsFalse(await _page.EvaluateAsync<bool>("name => caches.has(name)", $"{prefix}-first"));
            await RunAsync("#has-cache", "Has: False");
            await RunAsync("#delete-cache", "Delete: False");
            await RunAsync("#cache-keys", await FormatBrowserCacheNamesAsync());

            await _page.Locator("#cache-name").FillAsync($"{prefix}-second");
            await RunAsync("#delete-cache", "Delete: True");
            CollectionAssert.DoesNotContain(await GetBrowserCacheNamesAsync(), $"{prefix}-second");
            await RunAsync("#cache-keys", await FormatBrowserCacheNamesAsync());
        }
    }

    [TestMethod]
    public async Task Cache_AddsAndDeletesEntriesInBothRenderModes()
    {
        foreach (var path in CacheStoragePaths)
        {
            await NavigateAsync(path, "#open-cache");

            var name = path == "/cachestorage-server" ? "server-entries" : "wasm-entries";
            var request = $"{path}?entry=1";
            await _page.Locator("#cache-name").FillAsync(name);
            await RunAsync("#open-cache", $"Open: {name}");
            Assert.IsTrue(await _page.EvaluateAsync<bool>("name => caches.has(name)", name));

            await _page.Locator("#cache-request").FillAsync(request);
            await RunAsync("#add-entry", $"Add entry: {request}");
            Assert.IsTrue(await HasBrowserEntryAsync(name, request));

            // Another query string matches only when the option reaches the browser.
            await _page.Locator("#cache-request").FillAsync($"{path}?entry=2");
            await RunAsync("#delete-entry", "Delete entry: False");
            Assert.IsTrue(await HasBrowserEntryAsync(name, request));
            await RunAsync("#delete-entry-ignoring-search", "Delete entry ignoring search: True");
            Assert.IsFalse(await HasBrowserEntryAsync(name, request));

            await RunAsync("#close-cache", $"Close: {name}");
            Assert.IsTrue(await _page.EvaluateAsync<bool>("name => caches.has(name)", name));
            await RunAsync("#delete-cache", "Delete: True");
        }
    }

    private async Task NavigateAsync(string path, string interactiveSelector)
    {
        await _page.GotoAsync($"{_server.TestUrl}{path}");
        await _page.WaitForInteractiveAsync(interactiveSelector);
    }

    private async Task RunAsync(string buttonSelector, string expectedResult)
    {
        var result = _page.Locator("#cache-result");
        // Waiting for the next run keeps a step from passing on the text of the previous step.
        var nextRun = int.Parse(await result.GetAttributeAsync("data-run") ?? "", CultureInfo.InvariantCulture) + 1;

        await _page.Locator(buttonSelector).ClickAsync();
        await Expect(result).ToHaveAttributeAsync("data-run", nextRun.ToString(CultureInfo.InvariantCulture));
        await Expect(result).ToHaveTextAsync(expectedResult);
    }

    // Blazor WebAssembly can keep its own boot resources in Cache Storage, so compare with what the browser reports.
    private async Task<string[]> GetBrowserCacheNamesAsync()
        => await _page.EvaluateAsync<string[]>("() => caches.keys()");

    private async Task<bool> HasBrowserEntryAsync(string cacheName, string request)
        => await _page.EvaluateAsync<bool>(
            "async ([name, request]) => (await (await caches.open(name)).match(request)) !== undefined",
            new[] { cacheName, request });

    private async Task<string> FormatBrowserCacheNamesAsync()
    {
        var names = await GetBrowserCacheNamesAsync();

        return names.Length == 0 ? "Keys: No caches." : $"Keys: {string.Join(", ", names)}";
    }
}
