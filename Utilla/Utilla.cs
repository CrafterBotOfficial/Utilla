using BepInEx;
using BepInEx.Logging;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilla.HarmonyPatches;
using Utilla.Utils;

namespace Utilla
{

    [BepInPlugin("org.legoandmars.gorillatag.utilla", "Utilla", "1.6.12")]
    public class Utilla : BaseUnityPlugin
    {
        static Utilla instance;
        static Events events = new Events();

        void Start()
        {
            instance = this;
            RoomUtils.RoomCode = RoomUtils.RandomString(6); // Generate a random room code in case we need it

            GameObject dataObject = new GameObject();
            DontDestroyOnLoad(dataObject);
            gameObject.AddComponent<UtillaNetworkController>();

            Events.GameInitialized += PostInitialized;

            UtillaNetworkController.events = events;
            PostInitializedPatch.events = events;

            UtillaPatches.ApplyHarmonyPatches();
        }

        void PostInitialized(object sender, EventArgs e)
        {
            // GameObject.DontDestroyOnLoad(this.gameObject);
            var go = new GameObject("CustomGamemodesManager");
            GameObject.DontDestroyOnLoad(go);
            var gmm = go.AddComponent<GamemodeManager>();
            this.gameObject.GetComponent<UtillaNetworkController>().gameModeManager = gmm;
        }

        public static void Log(object message, LogLevel level = LogLevel.Info)
        {
            instance?.Logger.Log(level, message); // Saves to the logoutput which makes it easier for ppl to debug
        }
    }
}
