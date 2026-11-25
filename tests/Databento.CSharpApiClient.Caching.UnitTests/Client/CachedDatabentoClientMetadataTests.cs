using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel.Metadata;
using Databento.CSharpApiClient.DataModel.Symbology;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Databento.CSharpApiClient.Caching.UnitTests.Client.CachedClientFixture;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// The metadata and symbology calls of <see cref="CachedDatabentoClient"/> are Databento's own: each form of each call asks its own
    /// endpoint every time, passes its cancellation token on, and caches nothing.
    /// </summary>
    [TestClass]
    public sealed class CachedDatabentoClientMetadataTests
    {
        private static readonly Dictionary<string, string> Endpoints = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ListDatasets"] = "metadata.list_datasets",
            ["ListSchemas"] = "metadata.list_schemas",
            ["ListPublishers"] = "metadata.list_publishers",
            ["ListFields"] = "metadata.list_fields",
            ["ListUnitPrices"] = "metadata.list_unit_prices",
            ["GetDatasetCondition"] = "metadata.get_dataset_condition",
            ["GetDatasetRange"] = "metadata.get_dataset_range",
            ["ListConditions"] = "metadata.get_dataset_condition",
            ["GetRecordCount"] = "metadata.get_record_count",
            ["GetBillableSize"] = "metadata.get_billable_size",
            ["GetCost"] = "metadata.get_cost",
            ["ResolveSymbols"] = "symbology.resolve",
        };

        [TestMethod]
        public async Task EveryFormOfEveryCall_AsksItsEndpoint_EachTime()
        {
            List<MethodInfo> forms = Forms().ToList();
            List<string> wrong = new List<string>();
            foreach(MethodInfo form in forms)
            {
                using(CachedClientFixture fixture = new CachedClientFixture())
                using(CachedDatabentoClient client = fixture.CreateClient())
                {
                    string endpoint = Endpoints[CallName(form.Name)];
                    await InvokeAsync(form, client, CancellationToken.None);
                    await InvokeAsync(form, client, CancellationToken.None);

                    int asked = fixture.Databento.RequestsTo(endpoint).Count;
                    if(asked != 2 || fixture.Databento.TimeseriesRequests.Count != 0)
                    {
                        wrong.Add(Signature(form) + ": " + endpoint + " asked " + asked + " times");
                    }
                }
            }

            Assert.AreEqual(3 * Endpoints.Count, forms.Count, "the class's async form, the interface's and the extension's sync form");
            Assert.AreEqual(0, wrong.Count, string.Join(Environment.NewLine, wrong));
        }

        [TestMethod]
        public async Task EveryAsyncForm_PassesItsCancellationTokenOn()
        {
            List<string> notCanceled = new List<string>();
            using(CancellationTokenSource canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                foreach(MethodInfo form in Forms().Where(form => form.Name.EndsWith("Async", StringComparison.Ordinal)))
                {
                    using(CachedClientFixture fixture = new CachedClientFixture())
                    using(CachedDatabentoClient client = fixture.CreateClient())
                    {
                        try
                        {
                            await InvokeAsync(form, client, canceled.Token);
                            notCanceled.Add(Signature(form));
                        }
                        catch(OperationCanceledException)
                        {
                            // the token reached the transport
                        }
                    }
                }
            }

            Assert.AreEqual(0, notCanceled.Count, "Ran to the end with a canceled token: " + string.Join("; ", notCanceled));
        }

        [TestMethod]
        public void EveryInterfaceCall_IsImplementedByTheClient()
        {
            InterfaceMapping map = typeof(CachedDatabentoClient).GetInterfaceMap(typeof(ICachedDatabentoClient));
            List<string> notImplemented = new List<string>();
            for(int index = 0; index < map.InterfaceMethods.Length; ++index)
            {
                if(Endpoints.ContainsKey(CallName(map.InterfaceMethods[index].Name)) && map.TargetMethods[index].DeclaringType != typeof(CachedDatabentoClient))
                {
                    notImplemented.Add(map.InterfaceMethods[index].Name);
                }
            }

            Assert.AreEqual(0, notImplemented.Count, "Served by the interface's default: " + string.Join("; ", notImplemented));
        }

        [TestMethod]
        public void GetCost_IsDatabentosAnswer()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                fixture.Databento.Cost = 0.125m;

                double cost = ((ICachedDatabentoClient)client).GetCost("OPRA.PILLAR", new[] { Call }, "cbbo-1m", DayStart, DayEnd);

                Assert.AreEqual(0.125, cost, 1e-12);
            }
        }

        [TestMethod]
        public async Task GetDatasetRangeAsync_AfterDatabentoMovesItsEnd_ReturnsTheNewEnd()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                DateRange first = await client.GetDatasetRangeAsync("OPRA.PILLAR");
                fixture.Databento.AvailableEndUtc = fixture.Databento.AvailableEndUtc.AddDays(1);
                DateRange second = await client.GetDatasetRangeAsync("OPRA.PILLAR");

                Assert.AreEqual(TimeSpan.FromDays(1), second.End - first.End);
            }
        }

        [TestMethod]
        public void ResolveSymbols_IsDatabentosAnswer()
        {
            using(CachedClientFixture fixture = new CachedClientFixture())
            using(CachedDatabentoClient client = fixture.CreateClient())
            {
                SymbologyResolution resolution = client.ResolveSymbols(Request());

                Assert.IsTrue(resolution.Result.ContainsKey(Call));
            }
        }

        /// <summary>
        /// Every form of the metadata and symbology calls: the class's async methods, the interface's async calls and the extensions'
        /// sync forms
        /// </summary>
        private static IEnumerable<MethodInfo> Forms()
        {
            IEnumerable<MethodInfo> own = typeof(CachedDatabentoClient).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.DeclaringType == typeof(CachedDatabentoClient));
            IEnumerable<MethodInfo> extensions = typeof(CachedDatabentoClientExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.IsDefined(typeof(ExtensionAttribute), false));
            return own.Concat(typeof(ICachedDatabentoClient).GetMethods()).Concat(extensions).Where(method => Endpoints.ContainsKey(CallName(method.Name)));
        }

        private static async Task InvokeAsync(MethodInfo form, CachedDatabentoClient client, CancellationToken cancellationToken)
        {
            List<object> arguments = new List<object>();
            foreach(ParameterInfo parameter in form.GetParameters().Skip(form.IsStatic ? 1 : 0))
            {
                Type type = parameter.ParameterType;
                arguments.Add(type == typeof(CancellationToken) ? cancellationToken
                    : type == typeof(SymbologyRequest) ? Request()
                    : type == typeof(IReadOnlyList<string>) ? new[] { Call }
                    : type == typeof(DateTimeOffset) && parameter.Name == "startUtc" ? DayStart
                    : type == typeof(DateTimeOffset) ? DayEnd
                    : parameter.Name == "dataset" ? "OPRA.PILLAR"
                    : parameter.Name == "schema" ? "cbbo-1m"
                    : "2026-10-16");
            }

            try
            {
                object answer = form.IsStatic
                    ? form.Invoke(null, new object[] { client }.Concat(arguments).ToArray())
                    : form.Invoke(client, arguments.ToArray());
                if(answer is Task task)
                {
                    await task;
                }
            }
            catch(TargetInvocationException ex) when(ex.InnerException is OperationCanceledException)
            {
                throw ex.InnerException;
            }
            catch(TargetInvocationException)
            {
                // the fake does not answer every endpoint: the request it records is what is checked
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                // as above, for an async form
            }
        }

        private static SymbologyRequest Request()
        {
            return new SymbologyRequest
            {
                Dataset = "OPRA.PILLAR",
                Symbols = new[] { Call },
                StartDate = "2026-10-16",
                EndDate = "2026-10-17",
            };
        }

        /// <summary>
        /// A call's name without its <c>Async</c> suffix
        /// </summary>
        private static string CallName(string name)
        {
            return name.EndsWith("Async", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Async".Length) : name;
        }

        private static string Signature(MethodInfo form)
        {
            return form.DeclaringType.Name + "." + form.Name + "(" + string.Join(", ", form.GetParameters().Select(parameter => parameter.ParameterType.Name)) + ")";
        }
    }
}
