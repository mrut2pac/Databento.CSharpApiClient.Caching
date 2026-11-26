using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using Databento.CSharpApiClient.Caching.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Logging
{
    /// <summary>
    /// Every line of <see cref="CacheLog"/> rendered the same whatever the current culture, and a logger that throws never reaching the
    /// package's code
    /// </summary>
    [TestClass]
    public sealed class CacheLogTests
    {
        [TestMethod]
        public void LoggerMessages_AnyCulture_RenderAsUnderTheInvariantCulture()
        {
            MethodInfo[] methods = typeof(CacheLog).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() != null)
                .ToArray();
            Assert.AreNotEqual(0, methods.Length);

            Dictionary<string, string> invariant = RenderAll(methods, CultureInfo.InvariantCulture);
            foreach(string name in new[] { "de-DE", "th-TH", "ar-SA", "fr-FR" })
            {
                Dictionary<string, string> rendered = RenderAll(methods, new CultureInfo(name));
                foreach(KeyValuePair<string, string> line in invariant)
                {
                    Assert.AreEqual(line.Value, rendered[line.Key], Invariant($"{line.Key} under {name}"));
                }
            }
        }

        [TestMethod]
        public void LoggerMessages_TemplateArguments_AreAtMostSixSoTheGeneratorUsesDefine()
        {
            foreach(MethodInfo method in typeof(CacheLog).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() != null))
            {
                int arguments = method.GetParameters().Count(parameter => parameter.ParameterType != typeof(ILogger) && !typeof(Exception).IsAssignableFrom(parameter.ParameterType));
                Assert.IsTrue(arguments <= 6, Invariant($"{method.Name} takes {arguments} template arguments"));
            }
        }

        [TestMethod]
        public void LoggerMessages_Templates_EndWithoutAPeriod()
        {
            // a period after a path or a name reads as part of it; a step under way ends with " ..." instead
            foreach(MethodInfo method in typeof(CacheLog).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() != null))
            {
                string message = method.GetCustomAttribute<LoggerMessageAttribute>().Message;
                Assert.IsTrue(!message.EndsWith('.') || message.EndsWith(" ...", StringComparison.Ordinal), Invariant($"{method.Name} ends with a period: {message}"));
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void Wrap_LoggerThatThrows_NeverThrows(int mode)
        {
            FakeLogger logger = FakeLogger.Failing(mode);

            ILogger wrapped = NonThrowingLogger.Wrap(logger);
            wrapped.ServedFromDayFile("SPY", new DateTime(2026, 10, 16), "ohlcv-1d", "a.json.gz", 1);

            Assert.AreEqual(mode == 2 ? 0 : 1, logger.Entries.Count, "a line is handed over unless IsEnabled fails, which reads as off");
            Assert.AreEqual(mode != 2, wrapped.IsEnabled(LogLevel.Information));
            Assert.IsNull(wrapped.BeginScope("scope"));
        }

        [TestMethod]
        public void Wrap_NoLoggerOrAWrappedOne_IsNotWrappedAgain()
        {
            ILogger wrapped = NonThrowingLogger.Wrap(new FakeLogger());

            Assert.AreSame(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, NonThrowingLogger.Wrap(null));
            Assert.AreSame(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, NonThrowingLogger.Wrap(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance));
            Assert.AreSame(wrapped, NonThrowingLogger.Wrap(wrapped));
            Assert.AreSame(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, NonThrowingLogger.Wrap(Microsoft.Extensions.Logging.Abstractions.NullLogger<CacheLogTests>.Instance));
        }

        private static Dictionary<string, string> RenderAll(MethodInfo[] methods, CultureInfo culture)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = culture;
                Dictionary<string, string> rendered = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach(MethodInfo method in methods)
                {
                    FakeLogger logger = new FakeLogger();
                    method.Invoke(null, method.GetParameters().Select(parameter => SampleOf(parameter.ParameterType, logger)).ToArray());
                    rendered.Add(method.Name + "/" + method.GetParameters().Length, logger.Entries.Single().Message);
                }

                return rendered;
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static object SampleOf(Type type, FakeLogger logger)
        {
            if(type == typeof(ILogger))
            {
                return logger;
            }

            if(typeof(Exception).IsAssignableFrom(type))
            {
                return new IOException("sample");
            }

            if(type == typeof(DateTime))
            {
                return new DateTime(2026, 10, 16, 13, 30, 15, DateTimeKind.Utc);
            }

            if(type.IsEnum)
            {
                return Enum.GetValues(type).GetValue(0);
            }

            Dictionary<Type, object> samples = new Dictionary<Type, object>
            {
                [typeof(string)] = "sample",
                [typeof(int)] = -1234567,
                [typeof(long)] = -12345678901L,
                [typeof(bool)] = true,
                [typeof(decimal)] = 1234.5678m,
                [typeof(TimeSpan)] = new TimeSpan(1, 2, 3, 4, 567),
            };
            Assert.IsTrue(samples.ContainsKey(type), Invariant($"No sample of {type}: add one."));
            return samples[type];
        }
    }
}
