namespace EmergencyVR.Medical.Interaction
{
    public sealed class BloodPressureAcquisitionController : NonInvasiveAcquisitionController
    {
        protected override string MeasurementType => "BloodPressure";
        protected override string EquipmentId => "BloodPressure";
        protected override string DeviceSource => "AutomaticBloodPressureMonitor";
        protected override bool RequiresArmSupport => true;
    }
}
