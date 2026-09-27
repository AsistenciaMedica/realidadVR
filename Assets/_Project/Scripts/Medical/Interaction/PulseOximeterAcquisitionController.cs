namespace EmergencyVR.Medical.Interaction
{
    public sealed class PulseOximeterAcquisitionController : NonInvasiveAcquisitionController
    {
        protected override string MeasurementType => "SpO2";
        protected override string EquipmentId => "SpO2";
        protected override string DeviceSource => "PulseOximeter";
    }
}
