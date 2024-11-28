using System;
using System.Numerics;

using Newtonsoft.Json;

using IVSDKDotNet;

namespace SimpleHotTub.Classes.Json
{
    internal class SeatInfo
    {

        #region Variables
        public Vector3 Position;
        public float Heading;

        [JsonIgnore()] public bool TemporarilyRaiseHeight;
        [JsonIgnore()] public bool IsOccupied;
        [JsonIgnore()] public IVPed Occupant;
        #endregion

        #region Constructor
        public SeatInfo()
        {

        }
        #endregion

        public void Reset()
        {
            TemporarilyRaiseHeight = false;
            IsOccupied = false;
            Occupant = null;
        }
        public void SetOccupied(IVPed occupant)
        {
            IsOccupied = true;
            Occupant = occupant;
        }

    }
}
