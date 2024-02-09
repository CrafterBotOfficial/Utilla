﻿using BepInEx;
using BepInEx.Logging;
using GorillaNetworking;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
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

        void Start()
        {
            Instance = this;
            Events.RoomJoined += OnRoomJoin;
            Events.RoomLeft += OnRoomLeft;

            // transform.parent = GameObject.Find(UIRootPath).transform;

            GorillaComputer.instance.currentGameMode = PlayerPrefs.GetString("currentGameMode", "INFECTION");

            pluginInfos = GetPluginInfos();

            Gamemodes.AddRange(GetGamemodes(pluginInfos));
            Gamemodes.ForEach(AddGamemodeToManagerPool);

            ZoneManagement zoneManager = FindObjectOfType<ZoneManagement>();

            ZoneData FindZoneData(GTZone zone)
                => (ZoneData)AccessTools.Method(typeof(ZoneManagement), "GetZoneData").Invoke(zoneManager, new object[] { zone });

            InitializeSelector("TreehouseSelector",
                FindZoneData(GTZone.forest).rootGameObjects[2].transform.Find("TreeRoomInteractables/UI"),
                "Selector Buttons/anchor",
                "Selector Buttons/anchor"
            );
            InitializeSelector("MountainSelector",
                FindZoneData(GTZone.mountain).rootGameObjects[1].transform,
                "Geometry/goodigloo/modeselectbox (1)/anchor",
                "UI/Text"
            );
            InitializeSelector("SkySelector",
                FindZoneData(GTZone.skyJungle).rootGameObjects[1].transform.Find("UI/-- Clouds ModeSelectBox UI --/"),
                "anchor",
                "ModeSelectorText"
            );
            InitializeSelector("BeachSelector",
                FindZoneData(GTZone.beach).rootGameObjects[0].transform.Find("BeachComputer"),
                "modeselectbox (3)/anchor/",
                "UI FOR BEACH COMPUTER"
            );
        }

        void InitializeSelector(string name, Transform parent, string buttonPath, string gamemodesPath)
        {
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
            managerObject.transform.parent = GameObject.FindObjectOfType<CasualGameMode>().transform;
            GorillaGameManager gameManager = managerObject.AddComponent(gamemode.GameManager) as GorillaGameManager;

            int gameManagerNumber = GameMode.gameModes.Count;
            string gameManagerName = gameManager.GameModeName();
            GameMode.gameModeTable.Add(gameManagerNumber, gameManager);
            GameMode.gameModeKeyByName.Add(gameManagerName, gameManagerNumber);
            GameMode.gameModes.Add(gameManager);
            GameMode.gameModeNames.Add(gameManagerName);
        }

        internal void OnRoomJoin(object sender, Events.RoomJoinedArgs args)
        {
            string gamemode = args.Gamemode;

            /*if (PhotonNetwork.IsMasterClient)
			{
				foreach(Gamemode g in Gamemodes.Where(x => x.GameManager != null))
				{
					if (gamemode.Contains(g.ID))
					{
						break;
					}
				}
			}*/

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
