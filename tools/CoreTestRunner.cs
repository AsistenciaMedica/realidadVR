using System;
using EmergencyVR.Tests;

internal static class CoreTestRunner
{
    static int Main()
    {
        var passed = 0;
        var failed = 0;
        foreach (var test in DomainTestCases.All())
        {
            try { test.Run(); Console.WriteLine("PASS " + test.Name); passed++; }
            catch (Exception exception)
            {
                Console.WriteLine("FAIL " + test.Name + ": " + exception);
                failed++;
            }
        }
        Console.WriteLine("RESULT: " + passed + " passed; " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }
}
