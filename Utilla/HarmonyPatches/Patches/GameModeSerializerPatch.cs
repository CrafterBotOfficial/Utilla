using HarmonyLib;
using Photon.Pun;

namespace Utilla.HarmonyPatches
{
    [HarmonyPatch(typeof(GameModeSerializer), "OnInstantiateSetup")]
    public static class GameModeSerializerPatch
    {
        public static int GameType;
        private static void Prefix(PhotonMessageInfo info)
        {
            Utilla.Log("Got gamemode data " + info.photonView.name);
            GameType = (int)info.photonView.InstantiationData[0];
        }
    }
}
