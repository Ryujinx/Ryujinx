namespace Ryujinx.Horizon.Sdk.Hid.Vibration
{
    enum VibrationGcErmCommand
    {
        // Stops the vibration with a decay phase.
        Stop = 0,
        // Starts the vibration.
        Start = 1,
        // Stops the vibration immediately, with no decay phase.
        StopHard = 2
    }
}
