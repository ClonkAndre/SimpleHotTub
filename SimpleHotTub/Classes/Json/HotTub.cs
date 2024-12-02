using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Newtonsoft.Json;

using IVSDKDotNet;
using static IVSDKDotNet.Native.Natives;

namespace SimpleHotTub.Classes.Json
{
    internal class HotTub
    {

        #region Variables
        public Vector3 Position;

        public List<SeatInfo> SeatInfo;
        public List<CinematicInfo> CinematicCameras;
        public List<StaticCamInfo> StaticCameras;

        [JsonIgnore()] public CinematicInfo CurrentCinematicInfo;
        [JsonIgnore()] public int LastCinematicInfoIndex;

        [JsonIgnore()] public int CurrentStaticCamIndex;

        [JsonIgnore()] public bool Visualize;
        #endregion

        #region Constructor
        public HotTub()
        {
            SeatInfo = new List<SeatInfo>();
            CinematicCameras = new List<CinematicInfo>();
            StaticCameras = new List<StaticCamInfo>();
        }
        #endregion

        public void Reset()
        {
            CurrentStaticCamIndex = 0;

            ResetCinematicInfo();

            // Reset seats
            SeatInfo.ForEach(x => x.Reset());
        }
        public void ResetCinematicInfo()
        {
            CurrentCinematicInfo = null;
            LastCinematicInfoIndex = 0;
        }

        public SeatInfo GetRandomUnoccupiedSeat()
        {
            if (SeatInfo.Count == 0)
                return null;
            if (SeatInfo.All(x => x.IsOccupied))
                return null;

            int rndSeat = GENERATE_RANDOM_INT_IN_RANGE(0, SeatInfo.Count - 1);
            SeatInfo foundSeat = SeatInfo[rndSeat];

            while (foundSeat.IsOccupied)
            {
                rndSeat = GENERATE_RANDOM_INT_IN_RANGE(0, SeatInfo.Count - 1);
                foundSeat = SeatInfo[rndSeat];
            }

            return foundSeat;
        }
        public SeatInfo GetSeatOccupiedByPlayer()
        {
            return SeatInfo.Where(x => x.IsOccupied && x.Occupant.GetUIntPtr() == IVPlayerInfo.FindThePlayerPed()).FirstOrDefault();
        }
        public SeatInfo GetSeat(int index)
        {
            if (index < 0)
                return null;
            if (index > SeatInfo.Count - 1)
                return null;

            return SeatInfo[index];
        }

    }
}
