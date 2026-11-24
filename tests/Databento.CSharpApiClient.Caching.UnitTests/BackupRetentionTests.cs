using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Databento.CSharpApiClient.Caching.UnitTests
{
    /// <summary>
    /// Which backups each retention keeps: the newest few, or every one of the new backup's month and the last of each month before
    /// </summary>
    [TestClass]
    public class BackupRetentionTests
    {
        private const string New = "20261119T021530";

        [TestMethod]
        public void KeepLast_MoreBackupsThanKept_DeletesTheOldestCountingTheNewOne()
        {
            string[] backups = { "20261101T000000", "20261102T000000", "20261103T000000", New };

            CollectionAssert.AreEqual(new[] { "20261101T000000", "20261102T000000" }, BackupRetention.KeepLast(2).SelectExpired(backups, New).ToArray());
        }

        [TestMethod]
        public void KeepLast_NewOneNotListed_StillCountsIt()
        {
            // a store that does not list the new backup yet
            string[] backups = { "20261101T000000", "20261102T000000" };

            CollectionAssert.AreEqual(new[] { "20261101T000000" }, BackupRetention.KeepLast(2).SelectExpired(backups, New).ToArray());
        }

        [TestMethod]
        public void KeepLast_FewerBackupsThanKept_DeletesNone()
        {
            Assert.AreEqual(0, BackupRetention.KeepLast(5).SelectExpired(new[] { "20261101T000000", New }, New).Count);
        }

        [TestMethod]
        public void KeepLast_One_KeepsOnlyTheNewOne()
        {
            CollectionAssert.AreEqual(new[] { "20261101T000000" }, BackupRetention.KeepLast(1).SelectExpired(new[] { "20261101T000000", New }, New).ToArray());
        }

        [TestMethod]
        public void Calendar_KeepsTheMonthsBackupsAndTheLastOfEachMonthBefore()
        {
            string[] backups =
            {
                "20260731T000000", "20260815T000000", "20260830T000000", "20260901T000000", "20260929T235959",
                "20261005T000000", "20261031T120000", "20261101T000000", "20261110T000000", New,
            };

            // November is the new backup's month; two months before keep their last: October and September; August and July go
            CollectionAssert.AreEqual(
                new[] { "20260731T000000", "20260815T000000", "20260830T000000", "20260901T000000", "20261005T000000" },
                BackupRetention.Calendar(2).SelectExpired(backups, New).ToArray());
        }

        [TestMethod]
        public void Calendar_MonthWithoutBackups_IsStillOneOfTheMonths()
        {
            // October has none, so of the two months before November only September keeps one
            string[] backups = { "20260815T000000", "20260930T000000", New };

            CollectionAssert.AreEqual(new[] { "20260815T000000" }, BackupRetention.Calendar(2).SelectExpired(backups, New).ToArray());
        }

        [TestMethod]
        public void Calendar_Zero_KeepsOnlyTheNewBackupsMonth()
        {
            string[] backups = { "20261031T235959", "20261101T000000", New };

            CollectionAssert.AreEqual(new[] { "20261031T235959" }, BackupRetention.Calendar(0).SelectExpired(backups, New).ToArray());
        }

        [TestMethod]
        public void Calendar_AcrossAYear_CountsMonthsBack()
        {
            string[] backups = { "20251130T000000", "20251231T000000", "20260115T000000" };

            CollectionAssert.AreEqual(new[] { "20251130T000000" }, BackupRetention.Calendar(2).SelectExpired(backups, "20260201T000000").ToArray());
        }

        [TestMethod]
        public void BothRules_NameThatIsNoBackups_IsNeverDeleted()
        {
            // a store listing more than backups: the rule only deletes what it can read the age of
            string[] backups = { "..", "notes", "20250101T000000", New };

            CollectionAssert.AreEqual(new[] { "20250101T000000" }, BackupRetention.KeepLast(1).SelectExpired(backups, New).ToArray());
            CollectionAssert.AreEqual(new[] { "20250101T000000" }, BackupRetention.Calendar(1).SelectExpired(backups, New).ToArray());
        }

        [TestMethod]
        public void Factories_OutOfRange_Throw()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => BackupRetention.KeepLast(0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => BackupRetention.Calendar(-1));
        }

        [TestMethod]
        public void ToString_NamesTheRule()
        {
            Assert.AreEqual("KeepLast(5)", BackupRetention.KeepLast(5).ToString());
            Assert.AreEqual("Calendar(3)", BackupRetention.Calendar(3).ToString());
        }
    }
}
