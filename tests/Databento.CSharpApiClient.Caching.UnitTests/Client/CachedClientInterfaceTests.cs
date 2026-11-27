// Ignore Spelling: Cbbo Bbo Tcbbo Ohlcv

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// <see cref="ICachedDatabentoClient"/> and its extensions offer every call of <see cref="CachedDatabentoClient"/>, each asking for its
    /// schema with the cache mode it is given, and the client's own <see cref="IDatabentoTimeseriesClient"/> calls read the cache.
    /// </summary>
    [TestClass]
    public sealed class CachedClientInterfaceTests
    {
        private static readonly string[] Symbols = { Call, Put };

        [TestMethod]
        public void Interface_HoldsAsyncCallsForSeveralSymbolsWithACacheMode()
        {
            MethodInfo[] calls = typeof(ICachedDatabentoClient).GetMethods().Where(TakesACacheMode).ToArray();
            Type[] parameters = { typeof(string), typeof(IReadOnlyList<string>), typeof(DateTimeOffset), typeof(DateTimeOffset), typeof(CacheMode), typeof(CancellationToken) };

            Assert.AreEqual(11, calls.Length);
            Assert.IsTrue(calls.All(call => call.Name.EndsWith("Async", StringComparison.Ordinal) && Parameters(call).SequenceEqual(parameters)), string.Join("; ", calls.Select(Signature)));
            Assert.IsTrue(typeof(ICachedDatabentoClient).IsAssignableFrom(typeof(CachedDatabentoClient)));
        }

        [TestMethod]
        public void InterfaceAndExtensions_OfferEveryPublicCall()
        {
            List<MethodInfo> calls = typeof(CachedDatabentoClient).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.DeclaringType == typeof(CachedDatabentoClient) && method.Name != nameof(IDisposable.Dispose) && !method.IsSpecialName)
                .ToList();
            IEnumerable<MethodInfo> otherCalls = typeof(ICachedDatabentoClient).GetMethods()
                .Concat(Extensions(typeof(CachedDatabentoClientExtensions)))
                .Where(method => !method.IsSpecialName && !TakesACacheMode(method));
            List<MethodInfo> offered = OfferedCachedCalls().Concat(otherCalls).ToList();

            List<string> missing = calls.Where(call => !offered.Any(member => member.Name == call.Name && OwnParameters(member).SequenceEqual(Parameters(call)))).Select(Signature).ToList();
            List<string> extra = offered.Where(member => !calls.Any(call => call.Name == member.Name && OwnParameters(member).SequenceEqual(Parameters(call))) && !IsSyncFormOfAClassCall(member, calls))
                .Select(Signature).ToList();

            Assert.AreEqual(61, calls.Count, "44 time-series calls and the batch calls, async and sync, EnsureCachedAsync, and 12 metadata and symbology calls, async");
            Assert.AreEqual(0, missing.Count, "Not offered: " + string.Join("; ", missing));
            Assert.AreEqual(0, extra.Count, "Not on the client: " + string.Join("; ", extra));
        }

        [TestMethod]
        public async Task EveryOfferedCall_AsksForItsSchemaAndSymbols_WithTheCacheModeItIsGiven()
        {
            List<string> wrong = new List<string>();
            foreach(MethodInfo call in OfferedCachedCalls())
            {
                using(CachedClientFixture fixture = new CachedClientFixture())
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
                {
                    await InvokeAsync(call, client, CacheMode.Use, CancellationToken.None);
                    await InvokeAsync(call, client, CacheMode.Use, CancellationToken.None);
                    int afterUse = fixture.Databento.TimeseriesRequests.Count;
                    await InvokeAsync(call, client, CacheMode.Bypass, CancellationToken.None);

                    IReadOnlyList<FakeRequest> requests = fixture.Databento.TimeseriesRequests;
                    string[] symbols = OwnParameters(call)[1] == typeof(string) ? new[] { Call } : Symbols;
                    if(afterUse != 1 || requests.Count != 2
                        || requests.Any(request => request.Fields["schema"] != SchemaOf(call) || !request.Symbols.OrderBy(symbol => symbol).SequenceEqual(symbols.OrderBy(symbol => symbol))))
                    {
                        wrong.Add(Signature(call) + ": " + string.Join(", ", requests.Select(request => request.Fields["schema"] + " " + string.Join("+", request.Symbols))) + " (after Use: " + afterUse + ")");
                    }
                }
            }

            Assert.AreEqual(0, wrong.Count, string.Join(Environment.NewLine, wrong));
        }

        [TestMethod]
        public async Task EveryCallAsTheClientsOwnInterface_ReadsTheCache()
        {
            List<string> wrong = new List<string>();
            foreach(MethodInfo call in OfferedClientCalls())
            {
                using(CachedClientFixture fixture = new CachedClientFixture())
                using(CachedDatabentoClient client = fixture.CreateClient(options => options.EmptyDays = EmptyDayPolicy.CacheAlways))
                {
                    await InvokeAsync(call, client, CacheMode.Use, CancellationToken.None);
                    await InvokeAsync(call, client, CacheMode.Use, CancellationToken.None);

                    IReadOnlyList<FakeRequest> requests = fixture.Databento.TimeseriesRequests;
                    if(requests.Count != 1 || requests[0].Fields["schema"] != SchemaOf(call))
                    {
                        wrong.Add(Signature(call) + ": " + string.Join(", ", requests.Select(request => request.Fields["schema"])));
                    }
                }
            }

            Assert.AreEqual(0, wrong.Count, string.Join(Environment.NewLine, wrong));
        }

        [TestMethod]
        public async Task EveryOfferedAsyncCall_PassesItsCancellationTokenOn()
        {
            List<string> notCanceled = new List<string>();
            using(CancellationTokenSource canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                foreach(MethodInfo call in OfferedCachedCalls().Concat(OfferedClientCalls()).Where(call => call.Name.EndsWith("Async", StringComparison.Ordinal)))
                {
                    using(CachedClientFixture fixture = new CachedClientFixture())
                    using(CachedDatabentoClient client = fixture.CreateClient())
                    {
                        try
                        {
                            await InvokeAsync(call, client, CacheMode.Use, canceled.Token);
                            notCanceled.Add(Signature(call));
                        }
                        catch(OperationCanceledException)
                        {
                            // the token reached the cache
                        }
                    }
                }
            }

            Assert.AreEqual(0, notCanceled.Count, "Ran to the end with a canceled token: " + string.Join("; ", notCanceled));
        }

        [TestMethod]
        public async Task GetCbbo1mAsync_AsTheClientsOwnInterface_ReturnsANewArrayEachCall()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 2));
                IDatabentoTimeseriesClient timeseries = client;

                ICbboRecord[] first = await timeseries.GetCbbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd);
                first[0] = null;
                ICbboRecord[] second = await timeseries.GetCbbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd);

                Assert.AreEqual(2, second.Length);
                Assert.IsNotNull(second[0], "a caller's change to one answer is not seen in the next");
                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public async Task CallsWrittenAgainstTheInterface_WithoutACacheMode_ReadTheCache()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Add(Schema.ConsolidatedBBO1Min, Cbbo(Call, Day, 1), Cbbo(Put, Day, 2));
                ICachedDatabentoClient cached = client;
                await cached.GetCbbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd, CacheMode.Use);

                // each line binds at compile time to a call that reads the cache, so none asks Databento again
                await cached.GetCbbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd);
                await cached.GetCbbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd, CancellationToken.None);
                await cached.GetCbbo1mAsync(Datasets.OpraPillar, Symbols, DayStart, DayEnd, default);
                await cached.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await cached.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CancellationToken.None);
                await cached.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, default);
                cached.GetCbbo1m(Datasets.OpraPillar, Symbols, DayStart, DayEnd);
                cached.GetCbbo1m(Datasets.OpraPillar, Call, DayStart, DayEnd);
                await client.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CancellationToken.None);

                Assert.AreEqual(1, fixture.Databento.TimeseriesRequests.Count);
                await cached.GetCbbo1mAsync(Datasets.OpraPillar, Call, DayStart, DayEnd, CacheMode.Bypass);
                Assert.AreEqual(2, fixture.Databento.TimeseriesRequests.Count);
            }
        }

        [TestMethod]
        public void RunBatchJob_ThroughTheExtension_PassesItsRangeAndCeilingOn()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                ICachedDatabentoClient cached = client;

                Exception reversed = Catch(() => cached.RunBatchJob(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, Symbols, DayEnd, DayStart, 1m));
                Exception noCeiling = Catch(() => cached.RunBatchJob(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, Symbols, DayStart, DayEnd, -2.5m));
                Exception noSymbols = Catch(() => cached.RunBatchJob(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, Array.Empty<string>(), DayStart, DayEnd, 1m));

                Assert.AreEqual(Catch(() => client.RunBatchJob(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, Symbols, DayEnd, DayStart, 1m)).Message, reversed?.Message);
                Assert.AreEqual(Catch(() => client.RunBatchJob(Datasets.OpraPillar, Schema.ConsolidatedBBO1Min, Symbols, DayStart, DayEnd, -2.5m)).Message, noCeiling?.Message);
                StringAssert.Contains(noCeiling.Message, "-2.5");
                Assert.IsInstanceOfType(noSymbols, typeof(ArgumentException));
                StringAssert.Contains(noSymbols.Message, "symbol");
            }
        }

        [TestMethod]
        public void IngestBatchFolder_ThroughTheExtension_IsTheClientsOwn()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                string folder = Path.Combine(Path.GetTempPath(), "no-such-batch-folder-" + Guid.NewGuid().ToString("N"));

                Exception own = Catch(() => client.IngestBatchFolder(folder));
                Exception throughInterface = Catch(() => ((ICachedDatabentoClient)client).IngestBatchFolder(folder));

                Assert.IsNotNull(own);
                Assert.AreEqual(own.GetType(), throughInterface?.GetType());
                Assert.AreEqual(own.Message, throughInterface.Message);
            }
        }

        [TestMethod]
        public void RunBatchJob_ThroughTheExtension_IsTheClientsOwn()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                Exception own = Catch(() => client.RunBatchJob(Datasets.OpraPillar, "no-such-schema", Symbols, DayStart, DayEnd, 1m));
                Exception throughInterface = Catch(() => ((ICachedDatabentoClient)client).RunBatchJob(Datasets.OpraPillar, "no-such-schema", Symbols, DayStart, DayEnd, 1m));

                Assert.IsNotNull(own);
                Assert.AreEqual(own.GetType(), throughInterface?.GetType());
                Assert.AreEqual(own.Message, throughInterface.Message);
            }
        }

        /// <summary>
        /// The time-series calls offered on <see cref="ICachedDatabentoClient"/>, all with a cache mode: its own and its extensions
        /// </summary>
        private static IEnumerable<MethodInfo> OfferedCachedCalls()
        {
            return typeof(ICachedDatabentoClient).GetMethods().Where(TakesACacheMode)
                .Concat(Extensions(typeof(CachedDatabentoClientExtensions)).Where(TakesACacheMode));
        }

        /// <summary>
        /// Tells an extension that is the synchronous form of an async call on the class: a call added from 1.3.0 on has its sync form
        /// only there
        /// </summary>
        private static bool IsSyncFormOfAClassCall(MethodInfo member, IEnumerable<MethodInfo> calls)
        {
            Type[] withToken = OwnParameters(member).Append(typeof(CancellationToken)).ToArray();
            return member.IsStatic && calls.Any(call => call.Name == member.Name + "Async" && Parameters(call).SequenceEqual(withToken));
        }

        private static bool TakesACacheMode(MethodInfo call)
        {
            return call.GetParameters().Any(parameter => parameter.ParameterType == typeof(CacheMode));
        }

        /// <summary>
        /// The time-series calls offered on <see cref="IDatabentoTimeseriesClient"/>, none with a cache mode: its own and its extensions
        /// </summary>
        private static IEnumerable<MethodInfo> OfferedClientCalls()
        {
            return typeof(IDatabentoTimeseriesClient).GetMethods().Concat(Extensions(typeof(DatabentoTimeseriesClientExtensions)));
        }

        private static IEnumerable<MethodInfo> Extensions(Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.IsDefined(typeof(ExtensionAttribute), false));
        }

        private static async Task InvokeAsync(MethodInfo call, CachedDatabentoClient client, CacheMode cacheMode, CancellationToken cancellationToken)
        {
            List<object> arguments = new List<object>();
            foreach(Type parameter in OwnParameters(call))
            {
                arguments.Add(parameter == typeof(string) && arguments.Count == 0 ? Datasets.OpraPillar
                    : parameter == typeof(string) ? Call
                    : parameter == typeof(IReadOnlyList<string>) ? Symbols
                    : parameter == typeof(DateTimeOffset) && arguments.Count == 2 ? DayStart
                    : parameter == typeof(DateTimeOffset) ? DayEnd
                    : parameter == typeof(CacheMode) ? cacheMode
                    : cancellationToken);
            }

            object answer = call.IsStatic
                ? call.Invoke(null, new object[] { client }.Concat(arguments).ToArray())
                : call.Invoke(client, arguments.ToArray());
            if(answer is Task task)
            {
                await task;
            }
        }

        /// <summary>
        /// The Databento schema a call asks for, from its name: <c>GetCbbo1mAsync</c> asks for <c>cbbo-1m</c>
        /// </summary>
        private static string SchemaOf(MethodInfo call)
        {
            string name = Regex.Replace(call.Name, "^Get|Async$", string.Empty).ToLowerInvariant();
            return Regex.Replace(name, "^([a-z]+)(\\d[a-z])$", "$1-$2");
        }

        private static Exception Catch(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch(Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// A call's parameter types without the client an extension takes first
        /// </summary>
        private static Type[] OwnParameters(MethodInfo call)
        {
            return Parameters(call).Skip(call.IsStatic ? 1 : 0).ToArray();
        }

        private static Type[] Parameters(MethodInfo call)
        {
            return call.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
        }

        private static string Signature(MethodInfo call)
        {
            return call.DeclaringType.Name + "." + call.Name + "(" + string.Join(", ", OwnParameters(call).Select(type => type.Name)) + ")";
        }
    }
}
