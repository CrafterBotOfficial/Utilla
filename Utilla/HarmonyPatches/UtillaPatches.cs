using HarmonyLib;
using System;
using System.Reflection;

namespace Utilla.HarmonyPatches
{
    /// <summary>
    /// Apply and remove all of our Harmony patches through this class
    /// </summary>
    public class UtillaPatches
    {
        private static Harmony instance;

        public static bool IsPatched { get; private set; }
        public const string InstanceId = "com.legoandmars.gorillatag.utilla";

        static UtillaPatches()
        {
            instance = new Harmony(InstanceId);
            instance.PatchAll(typeof(HarmonyPatches.GameModePatches)); // Needs to be patched before the Awake method is called, however if everything is patched is completely breaks the game.
        }

        internal static void ApplyHarmonyPatches()
        {
            if (!IsPatched)
            {
                // Manually patching since theres a overload generic method
                MethodBase getGameModeInstance = typeof(GameMode).GetMethod("GetGameModeInstance", 0, new Type[] { typeof(GameModeType) });
                instance.Patch(getGameModeInstance, postfix: new HarmonyMethod(typeof(GameModePatches), "GetGameModeInstance_Postfix"));
                
                instance.PatchAll();
                IsPatched = true;
            }
        }

        internal static void RemoveHarmonyPatches()
        {
            if (instance != null && IsPatched)
            {
                instance.UnpatchSelf();
                IsPatched = false;
            }
        }
    }
}