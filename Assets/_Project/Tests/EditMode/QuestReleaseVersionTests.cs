using System;
using EmergencyVR.Editor;
using NUnit.Framework;

namespace EmergencyVR.Tests
{
    public sealed class QuestReleaseVersionTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("0")]
        [TestCase("1")]
        [TestCase("-1")]
        [TestCase("two")]
        [TestCase("2147483648")]
        public void ReleaseRejectsMissingInvalidOrUploadedCode(string value)
        {
            var error = Assert.Throws<InvalidOperationException>(() => QuestProjectSetup.ValidateReleaseVersionCode(value));
            StringAssert.Contains("VITAL_VERSION_CODE", error.Message);
        }

        [TestCase("2", 2)]
        [TestCase("42", 42)]
        public void ReleaseAcceptsNewCode(string value, int expected)
        {
            Assert.That(QuestProjectSetup.ValidateReleaseVersionCode(value), Is.EqualTo(expected));
        }
    }
}
