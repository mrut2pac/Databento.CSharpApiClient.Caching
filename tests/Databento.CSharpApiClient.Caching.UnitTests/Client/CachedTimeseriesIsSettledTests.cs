using System;

using Databento.CSharpApiClient.Caching.Client;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    [TestClass]
    public sealed class CachedTimeseriesIsSettledTests
    {
        private static readonly DateTime Day = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

        private static DateTimeOffset Utc(DateTime time)
        {
            return new DateTimeOffset(time, TimeSpan.Zero);
        }

        [TestMethod]
        public void IsSettled_DayBeforeToday_IsSettled()
        {
            Assert.IsTrue(CachedTimeseries.IsSettled(Day, Utc(Day.AddDays(1).AddHours(9)), TimeSpan.Zero));
        }

        [TestMethod]
        public void IsSettled_Today_IsNotSettled()
        {
            Assert.IsFalse(CachedTimeseries.IsSettled(Day, Utc(Day.AddHours(23).AddMinutes(59)), TimeSpan.Zero));
        }

        [TestMethod]
        public void IsSettled_NowExactlyTheMidnightTheDayEndsAt_IsSettled()
        {
            Assert.IsTrue(CachedTimeseries.IsSettled(Day, Utc(Day.AddDays(1)), TimeSpan.Zero));
        }

        [TestMethod]
        public void IsSettled_FutureDay_IsNotSettled()
        {
            Assert.IsFalse(CachedTimeseries.IsSettled(Day.AddDays(3), Utc(Day.AddHours(12)), TimeSpan.Zero));
        }

        [TestMethod]
        public void IsSettled_NowGivenInAnotherOffset_IsJudgedInUtc()
        {
            // 01:00 of the next day at UTC+2 is still 23:00 of the day in UTC
            Assert.IsFalse(CachedTimeseries.IsSettled(Day, new DateTimeOffset(2026, 10, 17, 1, 0, 0, TimeSpan.FromHours(2)), TimeSpan.Zero));
        }

        [TestMethod]
        public void IsSettled_DayWithATimeOfDay_IsJudgedByItsDate()
        {
            Assert.IsFalse(CachedTimeseries.IsSettled(Day.AddHours(18), Utc(Day.AddHours(20)), TimeSpan.Zero));
            Assert.IsTrue(CachedTimeseries.IsSettled(Day.AddHours(18), Utc(Day.AddDays(1)), TimeSpan.Zero));
        }

        [TestMethod]
        public void IsSettled_EndedLessThanTheDelayAgo_IsNotSettled()
        {
            Assert.IsFalse(CachedTimeseries.IsSettled(Day, Utc(Day.AddDays(1).AddMinutes(59)), TimeSpan.FromHours(1)));
        }

        [TestMethod]
        public void IsSettled_EndedExactlyTheDelayAgo_IsSettled()
        {
            Assert.IsTrue(CachedTimeseries.IsSettled(Day, Utc(Day.AddDays(1).AddHours(1)), TimeSpan.FromHours(1)));
        }
    }
}
