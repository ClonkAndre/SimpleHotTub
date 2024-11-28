using static IVSDKDotNet.Native.Natives;

namespace SimpleHotTub.Classes
{
    internal class PedOutfitStorage
    {

        #region Variables
        public uint UpperModel;
        public uint UpperTexture;

        public uint LowerModel;
        public uint LowerTexture;

        public uint FeetModel;
        public uint FeetTexture;
        #endregion

        #region Constructor
        public PedOutfitStorage()
        {

        }
        #endregion

        public static PedOutfitStorage CreateFromPed(int handle)
        {
            if (handle == 0)
                return null;

            PedOutfitStorage storage = new PedOutfitStorage();

            storage.UpperModel = GET_CHAR_DRAWABLE_VARIATION(handle, 1);
            storage.UpperTexture = GET_CHAR_TEXTURE_VARIATION(handle, 1);

            storage.LowerModel = GET_CHAR_DRAWABLE_VARIATION(handle, 2);
            storage.LowerTexture = GET_CHAR_TEXTURE_VARIATION(handle, 2);

            storage.FeetModel = GET_CHAR_DRAWABLE_VARIATION(handle, 5);
            storage.FeetTexture = GET_CHAR_TEXTURE_VARIATION(handle, 5);

            return storage;
        }

    }
}
