namespace EmergencyVR.Core
{
    // Explicit values keep serialized case assets stable when new states are added.
    public enum PatientState
    {
        Normal = 0,
        Conscious = 10,
        UnconsciousBreathing = 20,
        UnconsciousNotBreathing = 30,
        CardiacArrest = 40,
        Recovering = 50,
        Recovered = 60
    }
}
