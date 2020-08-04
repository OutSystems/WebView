using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tests.ReactView {

    public class ReactViewPreRenderCacheTests : TestBase<TestReactView> {

        protected override TestReactView CreateView() {
            return null;
        }

        protected override void ShowDebugConsole() { }

        private static Task WithCacheSize(int size, Func<Task> func) {
            var previousCacheSize = TestReactView.PreloadedCacheEntriesSize;
            try {
                TestReactView.PreloadedCacheEntriesSize = size;
                return func.Invoke();
            } finally {
                TestReactView.PreloadedCacheEntriesSize = previousCacheSize;
            }
        }

        private static void AssertContains(string obtained, string substring, string message) {
            Assert.IsTrue(obtained.Contains(substring), $"{message}{Environment.NewLine}'{substring}'{Environment.NewLine} not found in {Environment.NewLine}'{obtained}'");
        }

        [Test(Description = "Tests that cache size does not grow beyond limit")]
        public async Task CacheSizeDoesNotGrowBeyondLimit() {
            await Run(async () => {
                await WithCacheSize(2, async () => {
                    var firstRenders = new List<string>();
                    for (var i = 0; i < 3; i++) {
                        using var sandbox = new Sandbox(Window, CurrentTestName + i);
                        await sandbox.Ready();

                        var firstRenderHtml = sandbox.GetFirstRenderHtml();
                        Assert.IsEmpty(firstRenderHtml);

                        var currentHtml = sandbox.GetHtml();
                        Assert.IsNotEmpty(currentHtml);
                        firstRenders.Add(currentHtml);
                    }

                    var secondRenders = new List<string>();
                    for (var i = 2; i >= 0; i--) {
                        using var sandbox = new Sandbox(Window, CurrentTestName + i);
                        await sandbox.Ready();

                        var firstRenderHtml = sandbox.GetFirstRenderHtml();
                        secondRenders.Insert(0, firstRenderHtml);
                    }

                    Assert.IsEmpty(secondRenders[0], "First screen cache entry should not exist"); // property 1 - second render
                    AssertContains(secondRenders[1], firstRenders[1], "Second screen cache entry must exist"); // property 2 - second render
                    AssertContains(secondRenders[2], firstRenders[2], "Third screen cache entry must exist"); // property 3 - second render
                });
            });
        }

        [Test(Description="Tests that a component is rendered from cache")]
        public async Task ComponentIsRenderedFromCache() {
            await Run(async () => {
                var propertyName = CurrentTestName + "1";
                await WithCacheSize(2, async () => {
                    string firstRenderedHtml;
                    using (var sandbox = new Sandbox(Window, propertyName)) {
                        await sandbox.Ready();

                        firstRenderedHtml = sandbox.GetFirstRenderHtml();
                        Assert.IsEmpty(firstRenderedHtml);

                        firstRenderedHtml = sandbox.GetHtml();
                        Assert.IsNotEmpty(firstRenderedHtml);
                    }

                    using (var sandbox = new Sandbox(Window, propertyName)) {
                        await sandbox.Ready();

                        var currentRenderedHtml = sandbox.GetFirstRenderHtml();
                        AssertContains(currentRenderedHtml, firstRenderedHtml, "Component should have been rendered from cache");
                    } 
                });
            });
        }

        [Test(Description = "Test that cache content contains html and stylesheets")]
        public async Task HtmlAndStylesheetsAreStoredInCache() {
            await Run(async () => {
                var propertyName = CurrentTestName + "1";
                await WithCacheSize(2, async () => {
                    using (var sandbox = new Sandbox(Window, propertyName)) {
                        await sandbox.Ready();

                        var firstRenderHtml = sandbox.GetFirstRenderHtml();
                        Assert.IsEmpty(firstRenderHtml);
                    }

                    using (var sandbox = new Sandbox(Window, propertyName)) {
                        await sandbox.Ready();

                        var firstRenderHtml = sandbox.GetFirstRenderHtml();
                        AssertContains(firstRenderHtml, "<link", "Cache should contain stylesheets");
                        AssertContains(firstRenderHtml, "<div", "Cache should contain html");
                    }
                });
            });
        }

        [Test(Description = "Tests that component is rendered after first render from cache")]
        public async Task ComponentIsRenderedAfterPreRender() {
            await Run(async () => {
                var propertyName = CurrentTestName + "1";
                await WithCacheSize(2, async () => {
                    using (var sandbox = new Sandbox(Window, propertyName)) {
                        await sandbox.Ready();
                    }

                    using (var sandbox = new Sandbox(Window, propertyName)) {
                        await sandbox.Ready();

                        var firstRenderHtml = sandbox.GetFirstRenderHtml();
                        Assert.IsNotEmpty(firstRenderHtml);

                        var currentHtml = sandbox.GetHtml();
                        Assert.AreEqual(0, Regex.Matches(currentHtml, "<link").Count, "Rendered html: " + firstRenderHtml);
                        Assert.AreEqual(1, Regex.Matches(currentHtml, "Cache timestamp").Count, "Rendered html: " + firstRenderHtml);
                    }
                });
            });
        }

        [Test(Description = "Tests that different property values does not use cache")]
        public async Task DifferentPropertyValueDoesNotUseCache() {
            await Run(async () => {
                await WithCacheSize(2, async () => {
                    using (var sandbox = new Sandbox(Window, CurrentTestName + "1")) {
                        await sandbox.Ready();
                        var firstRenderHtml = sandbox.GetFirstRenderHtml();
                        Assert.IsEmpty(firstRenderHtml);
                    }

                    using (var sandbox = new Sandbox(Window, CurrentTestName + "2")) {
                        await sandbox.Ready();
                        var firstRenderHtml = sandbox.GetFirstRenderHtml();
                        Assert.IsEmpty(firstRenderHtml);
                    }
                });
            });
        }
    }
}
