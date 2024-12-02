using System.Numerics;

using Newtonsoft.Json;

namespace SimpleHotTub.Classes.Json
{
    internal class StaticCamInfo
    {

        #region Variables
        public Vector3 Position;
        public Vector3 Rotation;
        public float FOV;

        [JsonIgnore()] public bool Visualize;
        #endregion

        #region Constructor
        public StaticCamInfo()
        {
            FOV = 45f;
        }
        #endregion

    }
}
