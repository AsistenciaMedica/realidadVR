#if UNITY_EDITOR
namespace EmergencyVR.Tests
{
    // Fixture attributes are intentional: Test Framework filters assembly attributes to assemblies
    // that emit a UnityEditor.TestRunner reference, which a runtime PlayMode test assembly does not.
    internal static class SimulatedXRTestHooks
    {
        public const string Setup = "EmergencyVR.Tests.SimulatedXRTestSetup";
    }
}
#endif
