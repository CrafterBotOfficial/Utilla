using HarmonyLib;
using System.Collections.Generic;

namespace Utilla.HarmonyPatches
{
    [HarmonyPatch(typeof(GameMode))]
    public static class GameModePatches
    {
        [HarmonyPatch("Awake")]
        [HarmonyPrefix]
        private static void Awake_Prefix()
        {
            GameMode.gameModeTable = new Dictionary<int, GorillaGameManager>();
            GameMode.gameModeKeyByName = new Dictionary<string, int>();
            GameMode.gameModes = new List<GorillaGameManager>();
            AccessTools.Field(typeof(GameMode), "gameModeNames").SetValue(typeof(GameMode), new List<string>());
        }

        // GorillaGameManager GetGameModeInstance(GameModeType type)
        public static void GetGameModeInstance_Postfix(GameModeType type, ref GorillaGameManager __result)
        {
            Utilla.Log($"Found {__result.GameModeName()}", BepInEx.Logging.LogLevel.Message);
            __result = GameMode.gameModes[GameModeSerializerPatch.GameType];
        }
    }
}
