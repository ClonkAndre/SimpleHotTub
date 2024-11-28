using System;
using System.Numerics;

using Newtonsoft.Json;

namespace SimpleHotTub.Classes.Json
{
    internal class CinematicInfo
    {

        #region Variables
        public Vector3 From;
        public Vector3 To;

        public Vector3 LookAt;
        public Vector3 FixedRotation;

        public float FOV;
        public float Speed;

        public bool UseFixedRotation;
        public bool IsWithinInterior;

        [JsonIgnore()] public bool Visualize;
        #endregion

        #region Constructor
        public CinematicInfo(bool isWithinInterior, Vector3 lookAt, Vector3 from, Vector3 to)
        {
            From = from;
            To = to;

            LookAt = lookAt;
            FixedRotation = Vector3.Zero;

            FOV = 45f;

            UseFixedRotation = false;
            IsWithinInterior = isWithinInterior;
        }
        public CinematicInfo(bool isWithinInterior, bool useFixedRotation, Vector3 fixedRotation, Vector3 from, Vector3 to)
        {
            From = from;
            To = to;

            LookAt = Vector3.Zero;
            FixedRotation = fixedRotation;

            FOV = 45f;

            UseFixedRotation = useFixedRotation;
            IsWithinInterior = isWithinInterior;
        }
        public CinematicInfo()
        {
            
        }
        #endregion

    }
}
