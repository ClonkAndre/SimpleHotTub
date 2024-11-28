using IVSDKDotNet;

namespace SimpleHotTub.Classes
{
    internal class ModSettings
    {

        #region Variables
        // Camera
        public static int DefaultHotTubCam;
        #endregion

        public static void Load(SettingsFile settingsFile)
        {
            // Camera
            DefaultHotTubCam = settingsFile.GetInteger("Camera", "DefaultHotTubCam", 0);
        }

    }
}
