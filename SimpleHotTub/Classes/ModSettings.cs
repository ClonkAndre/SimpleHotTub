using IVSDKDotNet;

namespace SimpleHotTub.Classes
{
    internal class ModSettings
    {

        #region Variables
        // HUD
        public static bool TurnOffHudAndRadar;

        // Camera
        public static int DefaultHotTubCam;
        #endregion

        public static void Load(SettingsFile settingsFile)
        {
            // HUD
            TurnOffHudAndRadar = settingsFile.GetBoolean("HUD", "TurnOffHudAndRadar", true);

            // Camera
            DefaultHotTubCam = settingsFile.GetInteger("Camera", "DefaultHotTubCam", 0);
        }

    }
}
