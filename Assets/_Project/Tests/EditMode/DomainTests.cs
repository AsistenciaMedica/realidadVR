using System.Collections.Generic;
using NUnit.Framework;

namespace EmergencyVR.Tests
{
    public sealed class DomainTests
    {
        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (var test in DomainTestCases.All())
                yield return new TestCaseData(test).SetName(test.Name);
        }

        [TestCaseSource(nameof(Cases))]
        public void DomainRule(NamedTest test) { test.Run(); }
    }
}
