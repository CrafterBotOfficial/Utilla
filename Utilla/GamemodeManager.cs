using BepInEx;
using BepInEx.Logging;
using GorillaNetworking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilla.Models;

namespace Utilla
{
    public class GamemodeManager : MonoBehaviour
    {
        public static GamemodeManager Instance { get; private set; }

        public int PageCount => Mathf.CeilToInt(Gamemodes.Count() / 4f);

        List<Gamemode> DefaultModdedGamemodes = new List<Gamemode>()
        {
            new Gamemode("MODDED_CASUAL", "MODDED CASUAL", BaseGamemode.Casual),
            new Gamemode("MODDED_DEFAULT", "MODDED", BaseGamemode.Infection),
            new Gamemode("MODDED_HUNT", "MODDED HUNT", BaseGamemode.Hunt),
            new Gamemode("MODDED_BATTLE", "MODDED BRAWL", BaseGamemode.Paintbrawl)
        };
        public List<Gamemode> Gamemodes { get; private set; } = new List<Gamemode>() {
            new Gamemode("CASUAL", "CASUAL"),
            new Gamemode("INFECTION", "INFECTION"),
            new Gamemode("HUNT", "HUNT"),
            new Gamemode("BATTLE", "PAINTBRAWL")
        };

        List<PluginInfo> pluginInfos;
        Transform gameManagerParent;

        void Start()
        {
            Instance = this;

            Events.RoomJoined += OnRoomJoin;
            Events.RoomLeft += OnRoomLeft;

            // transform.parent = GameObject.Find(UIRootPath).transform;

            GorillaComputer.instance.currentGameMode.Value = PlayerPrefs.GetString("currentGameMode", "INFECTION");

            pluginInfos = GetPluginInfos();

            gameManagerParent = GameObject.FindObjectOfType<GameMode>().transform;
            Gamemodes.AddRange(GetGamemodes(pluginInfos));
            Gamemodes.ForEach(AddGamemodeToManagerPool);

            InitializeSelector("TreehouseSelector",
                GameObject.Find("Environment Objects/LocalObjects_Prefab/TreeRoom/TreeRoomInteractables/UI/Selector Buttons").transform,
                "anchor",
                "anchor"
                );

            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void SceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            switch (arg0.name)
            {
                case "Skyjungle":
                    InitializeSelector("SkySelector",
                        GameObject.Find("skyjungle/UI/-- Clouds ModeSelectBox UI --").transform,
                        "anchor",
                        "ModeSelectorText"
                    );
                    break;
                case "Mountain":
                    InitializeSelector("MountainSelector",
                            GameObject.Find("Mountain").transform,
                            "Geometry/goodigloo/modeselectbox (1)/anchor",
                            "UI/Text"
                        );
                    break;
                case "Beach":
                    InitializeSelector("BeachSelector",
                        GameObject.Find("Beach/BeachComputer").transform,
                        "modeselectbox (3)/anchor",
                        "UI FOR BEACH COMPUTER"
                    );
                    break;
                default:
                    Utilla.Log($"Unknown scene was loaded {arg0.name}", LogLevel.Warning);
                    break;
            }
        }

        void InitializeSelector(string name, Transform parent, string buttonPath, string gamemodesPath)
        {
            Utilla.Log($"Initializing selector {name}");
            try
            {
                var selector = new GameObject(name).AddComponent<GamemodeSelector>();

                // child objects might be removed when gamemodes is released, keeping default behaviour for now
                var ButtonParent = parent.Find(buttonPath);
                foreach (Transform child in ButtonParent)
                {
                    if (child.gameObject.name.StartsWith("ENABLE FOR BETA"))
                    {
                        ButtonParent = child;
                        break;
                    }
                }

                // gameobject name for the text object changed but might change back after gamemodes is released
                var GamemodesList = parent.Find(gamemodesPath);
                foreach (Transform child in GamemodesList)
                {
                    if (child.gameObject.name.StartsWith("Game Mode List Text ENABLE FOR BETA"))
                    {
                        GamemodesList = child;
                        break;
                    }
                }

                selector.Initialize(parent, ButtonParent, GamemodesList);
            }
            catch (Exception e)
            {
                Utilla.Log($"Utilla: Failed to initialize {name}: {e}", LogLevel.Error);
            }
        }

        List<Gamemode> GetGamemodes(List<PluginInfo> infos)
        {
            List<Gamemode> gamemodes = new List<Gamemode>();
            gamemodes.AddRange(DefaultModdedGamemodes);

            HashSet<Gamemode> additonalGamemodes = new HashSet<Gamemode>();
            foreach (var info in infos)
            {
                additonalGamemodes.UnionWith(info.Gamemodes);
            }

            foreach (var gamemode in DefaultModdedGamemodes)
            {
                additonalGamemodes.Remove(gamemode);
            }

            gamemodes.AddRange(additonalGamemodes);

            return gamemodes;
        }

        List<PluginInfo> GetPluginInfos()
        {
            List<PluginInfo> infos = new List<PluginInfo>();
            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos)
            {
                if (info.Value == null) continue;
                BaseUnityPlugin plugin = info.Value.Instance;
                if (plugin == null) continue;
                Type type = plugin.GetType();

                IEnumerable<Gamemode> gamemodes = GetGamemodes(type);

                if (gamemodes.Count() > 0)
                {
                    infos.Add(new PluginInfo
                    {
                        Plugin = plugin,
                        Gamemodes = gamemodes.ToArray(),
                        OnGamemodeJoin = CreateJoinLeaveAction(plugin, type, typeof(ModdedGamemodeJoinAttribute)),
                        OnGamemodeLeave = CreateJoinLeaveAction(plugin, type, typeof(ModdedGamemodeLeaveAttribute))
                    });
                }
            }

            return infos;
        }

        Action<string> CreateJoinLeaveAction(BaseUnityPlugin plugin, Type baseType, Type attribute)
        {
            ParameterExpression param = Expression.Parameter(typeof(string));
            ParameterExpression[] paramExpression = new ParameterExpression[] { param };
            ConstantExpression instance = Expression.Constant(plugin);
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            Action<string> action = null;
            foreach (var method in baseType.GetMethods(bindingFlags).Where(m => m.GetCustomAttribute(attribute) != null))
            {
                var parameters = method.GetParameters();
                MethodCallExpression methodCall;
                if (parameters.Length == 0)
                {
                    methodCall = Expression.Call(instance, method);
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    methodCall = Expression.Call(instance, method, param);
                }
                else
                {
                    continue;
                }

                action += Expression.Lambda<Action<string>>(methodCall, paramExpression).Compile();
            }

            return action;
        }

        HashSet<Gamemode> GetGamemodes(Type type)
        {
            IEnumerable<ModdedGamemodeAttribute> attributes = type.GetCustomAttributes<ModdedGamemodeAttribute>();

            HashSet<Gamemode> gamemodes = new HashSet<Gamemode>();
            if (attributes != null)
            {
                foreach (ModdedGamemodeAttribute attribute in attributes)
                {
                    if (attribute.gamemode != null)
                    {
                        gamemodes.Add(attribute.gamemode);
                    }
                    else
                    {
                        gamemodes.UnionWith(DefaultModdedGamemodes);
                    }
                }
            }

            return gamemodes;
        }

        void AddGamemodeToManagerPool(Gamemode gamemode)
        {
            if (gamemode.GameManager is null) return;
            Utilla.Log($"Adding {gamemode.ID} to gamemanager parent");

            GameObject managerObject = new GameObject(gamemode.ID);
            managerObject.transform.parent = gameManagerParent;
            GorillaGameManager gameManager = managerObject.AddComponent(gamemode.GameManager) as GorillaGameManager;

            int gameManagerTypeNumber = GameMode.gameModes.Count;
            string gameManagerName = gamemode.ID;

            if (GameMode.gameModeNames.Contains(gameManagerName))
            {
                Utilla.Log($"Duplicate gamemodes found, please rename your gamemode to something unique. {gamemode.ID}", LogLevel.Error);
                return;
            }

            GameMode.gameModeTable.Add(gameManagerTypeNumber, gameManager);
            GameMode.gameModeKeyByName.Add(gameManagerName, gameManagerTypeNumber);
            GameMode.gameModes.Add(gameManager);
            GameMode.gameModeNames.Add(gameManagerName);
        }

        internal void OnRoomJoin(object sender, Events.RoomJoinedArgs args)
        {
            string gamemode = args.Gamemode;

            foreach (var pluginInfo in pluginInfos)
            {
                if (pluginInfo.Gamemodes.Any(x => gamemode.Contains(x.GamemodeString)))
                {
                    try
                    {
                        pluginInfo.OnGamemodeJoin?.Invoke(gamemode);
                    }
                    catch (Exception e)
                    {
                        Utilla.Log(e, LogLevel.Error);
                    }
                }
            }
        }

        internal void OnRoomLeft(object sender, Events.RoomJoinedArgs args)
        {
            string gamemode = args.Gamemode;

            foreach (var pluginInfo in pluginInfos)
            {
                if (pluginInfo.Gamemodes.Any(x => gamemode.Contains(x.GamemodeString)))
                {
                    try
                    {
                        pluginInfo.OnGamemodeLeave?.Invoke(gamemode);
                    }
                    catch (Exception e)
                    {
                        Utilla.Log(e, LogLevel.Error);
                    }
                }
            }
        }
    }
}
