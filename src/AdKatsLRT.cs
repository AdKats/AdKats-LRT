/*
 * AdKatsLRT - On-Spawn Loadout Enforcer
 *
 * AdKats and respective extensions are inspired by the gaming community A Different Kind (ADK).
 * Visit http://www.ADKGamers.com/ for more information.
 *
 * The AdKats Frostbite Plugin is open source, and under public domain, but certain extensions are not.
 * The AdKatsLRT extension is not open for free distribution, copyright Daniel J. Gradinjan, with all rights reserved.
 *
 * Development by Daniel J. Gradinjan (ColColonCleaner)
 *
 * AdKatsLRT.cs
 * Version 3.0.0.0
 * 28-MAR-2026
 *
 * Automatic Update Information
 * <version_code>3.0.0.0</version_code>
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

using Flurl;
using Flurl.Http;

using Newtonsoft.Json.Linq;

using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKatsLRT : PRoConPluginAPI, IPRoConPluginInterface
    {
        //Current Plugin Version
        private const String PluginVersion = "3.0.0.0";

        public readonly Logger Log;

        public enum GameVersion
        {
            BF3,
            BF4,
            BFHL
        };

        //Constants
        private const String SettingsInstancePrefix = "0. Instance Settings|";
        private const String SettingsDisplayPrefix = "1. Display Settings|";
        private const String SettingsPresetPrefix = "2. Preset Settings|";
        private const String SettingsMapModePrefix = "3. Map/Mode Settings";
        private const String SettingsWeaponPrefix = "4. Weapons - ";
        private const String SettingsAccessoryPrefix = "5. Weapon Accessories - ";
        private const String SettingsGadgetPrefix = "6. Gadgets - ";
        private const String SettingsVehiclePrefix = "7. Vehicle Weapons/Unlocks";
        private const String SettingsDeniedItemMessagePrefix = "8A. Denied Item Kill Messages|";
        private const String SettingsDeniedItemAccMessagePrefix = "8B. Denied Item Accessory Kill Messages|";
        private const String SettingsDeniedVehicleItemMessagePrefix = "8C. Denied Vehicle Item Kill Messages|";

        //State
        private GameVersion _gameVersion = GameVersion.BF3;
        private volatile Boolean _pluginEnabled;
        private DateTime _pluginStartTime = DateTime.UtcNow;
        private WarsawLibrary _warsawLibrary = new WarsawLibrary();
        private Boolean _warsawLibraryLoaded;
        private readonly HashSet<String> _adminList = new HashSet<String>();
        private readonly Dictionary<String, AdKatsSubscribedPlayer> _playerDictionary = new Dictionary<String, AdKatsSubscribedPlayer>();
        private Boolean _firstPlayerListComplete;
        private Boolean _isTestingAuthorized;
        private readonly Dictionary<String, AdKatsSubscribedPlayer> _playerLeftDictionary = new Dictionary<String, AdKatsSubscribedPlayer>();
        private readonly Queue<ProcessObject> _loadoutProcessingQueue = new Queue<ProcessObject>();
        private readonly Queue<AdKatsSubscribedPlayer> _battlelogFetchQueue = new Queue<AdKatsSubscribedPlayer>();
        private readonly Dictionary<String, String> _warsawInvalidLoadoutIDMessages = new Dictionary<String, String>();
        private readonly Dictionary<String, String> _warsawInvalidVehicleLoadoutIDMessages = new Dictionary<String, String>();
        private readonly HashSet<String> _warsawSpawnDeniedIDs = new HashSet<String>();
        private Int32 _countKilled;
        private Int32 _countFixed;
        private Int32 _countQuit;
        private readonly AdKatsServer _serverInfo;
        private Boolean _displayLoadoutDebug;

        //Settings
        private Boolean _highRequestVolume;
        private Boolean _useProxy = false;
        private String _proxyURL = "";
        private Boolean _enableAdKatsIntegration;
        private Boolean _spawnEnforcementOnly;
        private Boolean _spawnEnforcementActOnAdmins;
        private Boolean _spawnEnforcementActOnReputablePlayers;
        private Boolean _displayWeaponPopularity;
        private Int32 _weaponPopularityDisplayMinutes = 6;
        private Boolean _useWeaponCatchingBackup = true;
        private Int32 _triggerEnforcementMinimumInfractionPoints = 6;
        private Boolean _spawnEnforceAllVehicles;
        private String[] _Whitelist = { };
        private String[] _ItemFilter = { };

        //Display
        private Boolean _displayMapsModes;
        private Boolean _displayWeapons;
        private Boolean _displayWeaponAccessories;
        private Boolean _displayGadgets;
        private Boolean _displayVehicles;

        //Maps Modes
        private Boolean _restrictSpecificMapModes;
        private List<MapMode> _availableMapModes = new List<MapMode>();
        private readonly Dictionary<String, MapMode> _restrictedMapModes = new Dictionary<String, MapMode>();

        //Timing
        private readonly DateTime _proconStartTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private readonly TimeSpan _battlelogWaitDuration = TimeSpan.FromSeconds(3);
        private DateTime _startTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastVersionTrackingUpdate = DateTime.UtcNow - TimeSpan.FromHours(1);
        private Object _battlelogLocker = new Object();
        private DateTime _lastBattlelogAction = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastBattlelogFrequencyMessage = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private Queue<DateTime> _BattlelogActionTimes = new Queue<DateTime>();
        private DateTime _lastCategoryListing = DateTime.UtcNow;

        //Threads
        private readonly Dictionary<Int32, Thread> _aliveThreads = new Dictionary<Int32, Thread>();
        private volatile Boolean _threadsReady;
        private EventWaitHandle _threadMasterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _loadoutProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _playerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _battlelogCommWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _activator;
        private Thread _finalizer;
        private Thread _spawnProcessingThread;
        private Thread _battlelogCommThread;

        //AutoAdmin
        private Boolean _UseBackupAutoadmin;
        private Boolean _UseAdKatsPunishments;
        private Dictionary<String, List<String>> _WarsawRCONMappings = new Dictionary<String, List<String>>();
        private Dictionary<String, List<String>> _RCONWarsawMappings = new Dictionary<String, List<String>>();

        //Settings
        private const Int32 YellDuration = 7;

        //Debug
        private Boolean _slowmo;

        public AdKatsLRT()
        {
            Log = new Logger(this);

            //Create the server reference
            _serverInfo = new AdKatsServer(this);

            //Populate maps/modes
            PopulateMapModes();

            //Populate AutoAdmin Weapon Mappings
            PopulateWarsawRCONCodes();

            //Set defaults for webclient
            ServicePointManager.Expect100Continue = false;

            //By default plugin is not enabled or ready
            _pluginEnabled = false;
            _threadsReady = false;

            //Debug level is 0 by default
            Log.DebugLevel = 0;

            //Prepare the status monitor
            SetupStatusMonitor();
        }

        public String GetPluginName()
        {
            return "AdKatsLRT - Loadout Enforcer";
        }

        public String GetPluginVersion()
        {
            return PluginVersion;
        }

        public String GetPluginAuthor()
        {
            return "[ADK]ColColonCleaner (maintained by Prophet731)";
        }

        public String GetPluginWebsite()
        {
            return "https://github.com/AdKats/";
        }

        public String GetPluginDescription()
        {
            return "";
        }

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion)
        {
            Log.Debug("Entering OnPluginLoaded", 7);
            try
            {
                //Set the server IP
                _serverInfo.ServerIP = strHostName + ":" + strPort;
                //Register all events
                RegisterEvents(GetType().Name,
                    "OnVersion",
                    "OnServerInfo",
                    "OnListPlayers",
                    "OnPlayerSpawned",
                    "OnPlayerKilled",
                    "OnPlayerLeft");
            }
            catch (Exception e)
            {
                Log.Exception("FATAL ERROR on plugin load.", e);
            }
            Log.Debug("Exiting OnPluginLoaded", 7);
        }

        public void OnPluginEnable()
        {
            try
            {
                //If the finalizer is still alive, inform the user and disable
                if (_finalizer != null && _finalizer.IsAlive)
                {
                    Log.Error("Cannot enable the plugin while it is shutting down. Please Wait for it to shut down.");
                    _threadMasterWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                    //Disable the plugin
                    Disable();
                    return;
                }
                if (_gameVersion != GameVersion.BF4)
                {
                    Log.Error("LRT can only be enabled on BF4 at this time.");
                    Disable();
                    return;
                }
                //Create a new thread to activate the plugin
                _activator = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "Enabler";

                        _pluginEnabled = true;

                        if ((DateTime.UtcNow - _proconStartTime).TotalSeconds <= 20)
                        {
                            Log.Write("Waiting a few seconds for requirements and other plugins to initialize, please wait...");
                            //Wait on all settings to be imported by procon for initial start, and for all other plugins to start and register.
                            for (Int32 index = 20 - (Int32)(DateTime.UtcNow - _proconStartTime).TotalSeconds; index > 0; index--)
                            {
                                Log.Write(index + "...");
                                _threadMasterWaitHandle.WaitOne(1000);
                            }
                        }
                        if (!_pluginEnabled)
                        {
                            LogThreadExit();
                            return;
                        }
                        Boolean adKatsFound = GetRegisteredCommands().Any(command => command.RegisteredClassname == "AdKats" && command.RegisteredMethodName == "PluginEnabled");
                        if (adKatsFound)
                        {
                            _enableAdKatsIntegration = true;
                        }
                        if (!_enableAdKatsIntegration || adKatsFound)
                        {
                            _startTime = DateTime.UtcNow;
                            //Set the enabled variable
                            _playerProcessingWaitHandle.Reset();

                            if (!_pluginEnabled)
                            {
                                LogThreadExit();
                                return;
                            }
                            //Fetch all weapon names
                            if (_warsawLibraryLoaded || LoadWarsawLibrary())
                            {
                                if (!_pluginEnabled)
                                {
                                    LogThreadExit();
                                    return;
                                }
                                Log.Success("WARSAW library loaded. " + _warsawLibrary.Items.Count + " items, " + _warsawLibrary.VehicleUnlocks.Count + " vehicle unlocks, and " + _warsawLibrary.ItemAccessories.Count + " accessories.");
                                UpdateSettingPage();

                                if (_enableAdKatsIntegration)
                                {
                                    //Subscribe to online soldiers from AdKats
                                    ExecuteCommand("procon.protected.plugins.call", "AdKats", "SubscribeAsClient", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                        {"caller_identity", "AdKatsLRT"},
                                        {"response_requested", false},
                                        {"subscription_group", "OnlineSoldiers"},
                                        {"subscription_method", "ReceiveOnlineSoldiers"},
                                        {"subscription_enabled", true}
                                    }));
                                    Log.Info("Waiting for player listing response from AdKats.");
                                }
                                else
                                {
                                    Log.Info("Waiting for first player list event.");
                                }
                                _playerProcessingWaitHandle.WaitOne(Timeout.Infinite);
                                if (!_pluginEnabled)
                                {
                                    LogThreadExit();
                                    return;
                                }

                                _pluginStartTime = DateTime.UtcNow;

                                //Init and start all the threads
                                InitWaitHandles();
                                OpenAllHandles();
                                InitThreads();
                                StartThreads();

                                Log.Success("AdKatsLRT " + GetPluginVersion() + " startup complete [" + FormatTimeString(DateTime.UtcNow - _startTime, 3) + "]. Loadout restriction now online.");
                            }
                            else
                            {
                                Log.Error("Failed to load WARSAW library. AdKatsLRT cannot be started.");
                                Disable();
                            }
                        }
                        else
                        {
                            Log.Error("AdKats not installed or enabled. AdKatsLRT cannot be started.");
                            Disable();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Exception("Error while enabling AdKatsLRT.", e);
                    }
                    LogThreadExit();
                }));

                Log.Write("^b^2ENABLED!^n^0 Beginning startup sequence...");
                //Start the thread
                StartAndLogThread(_activator);
            }
            catch (Exception e)
            {
                Log.Exception("Error while initializing activator thread.", e);
            }
        }

        public void OnPluginDisable()
        {
            //If the plugin is already disabling then cancel
            if (_finalizer != null && _finalizer.IsAlive)
                return;
            try
            {
                //Create a new thread to disabled the plugin
                _finalizer = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "Finalizer";
                        Log.Info("Shutting down AdKatsLRT.");
                        //Disable settings
                        _pluginEnabled = false;
                        _threadsReady = false;

                        if (_enableAdKatsIntegration)
                        {
                            //Unsubscribe from online soldiers through AdKats
                            ExecuteCommand("procon.protected.plugins.call", "AdKats", "SubscribeAsClient", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                {"caller_identity", "AdKatsLRT"},
                                {"response_requested", false},
                                {"subscription_group", "OnlineSoldiers"},
                                {"subscription_method", "ReceiveOnlineSoldiers"},
                                {"subscription_enabled", false}
                            }));
                        }

                        //Open all handles. Threads will finish on their own.
                        OpenAllHandles();

                        //Check to make sure all threads have completed and stopped
                        Int32 attempts = 0;
                        Boolean alive = false;
                        do
                        {
                            OpenAllHandles();
                            attempts++;
                            Thread.Sleep(500);
                            alive = false;
                            String aliveThreads = "";
                            lock (_aliveThreads)
                            {
                                foreach (Int32 deadThreadID in _aliveThreads.Values.Where(thread => !thread.IsAlive).Select(thread => thread.ManagedThreadId).ToList())
                                {
                                    _aliveThreads.Remove(deadThreadID);
                                }
                                foreach (Thread aliveThread in _aliveThreads.Values.ToList())
                                {
                                    alive = true;
                                    aliveThreads += (aliveThread.Name + "[" + aliveThread.ManagedThreadId + "] ");
                                }
                            }
                            if (aliveThreads.Length > 0)
                            {
                                if (attempts > 20)
                                {
                                    Log.Warn("Threads still exiting: " + aliveThreads);
                                }
                                else
                                {
                                    Log.Debug("Threads still exiting: " + aliveThreads, 2);
                                }
                            }
                        } while (alive);
                        _firstPlayerListComplete = false;
                        _playerDictionary.Clear();
                        _playerLeftDictionary.Clear();
                        _loadoutProcessingQueue.Clear();
                        _firstPlayerListComplete = false;
                        _countFixed = 0;
                        _countKilled = 0;
                        _countQuit = 0;
                        _slowmo = false;
                        Log.Write("^b^1AdKatsLRT " + GetPluginVersion() + " Disabled! =(^n^0");
                    }
                    catch (Exception e)
                    {
                        Log.Exception("Error occured while disabling AdKatsLRT.", e);
                    }
                }));
                _finalizer.Start();
            }
            catch (Exception e)
            {
                Log.Exception("Error while initializing finalizer thread.", e);
            }
        }

        private void SetupStatusMonitor()
        {
            //Create a new thread to handle status monitoring
            //This thread will remain running for the duration the layer is online
            var statusMonitorThread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    Thread.CurrentThread.Name = "StatusMonitor";
                    DateTime lastKeepAliveCheck = DateTime.UtcNow;
                    while (true)
                    {
                        try
                        {
                            _threadMasterWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                        }
                        catch (Exception)
                        {
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    // Thread exit
                }
            }))
            {
                IsBackground = true
            };
            statusMonitorThread.Start();
        }

        public void InitWaitHandles()
        {
            _loadoutProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _playerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _battlelogCommWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void OpenAllHandles()
        {
            _loadoutProcessingWaitHandle.Set();
            _battlelogCommWaitHandle.Set();
        }

        public void InitThreads()
        {
            try
            {
                _spawnProcessingThread = new Thread(ProcessingThreadLoop)
                {
                    IsBackground = true
                };

                _battlelogCommThread = new Thread(BattlelogCommThreadLoop)
                {
                    IsBackground = true
                };
            }
            catch (Exception e)
            {
                Log.Exception("Error occured while initializing threads.", e);
            }
        }

        public void StartThreads()
        {
            Log.Debug("Entering StartThreads", 7);
            try
            {
                //Reset the master wait handle
                _threadMasterWaitHandle.Reset();
                //Start the spawn processing thread
                StartAndLogThread(_spawnProcessingThread);
                StartAndLogThread(_battlelogCommThread);
                _threadsReady = true;
            }
            catch (Exception e)
            {
                Log.Exception("Error while starting processing threads.", e);
            }
            Log.Debug("Exiting StartThreads", 7);
        }

        private void Disable()
        {
            //Call Disable
            ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "False");
            //Set enabled false so threads begin exiting
            _pluginEnabled = false;
            _threadsReady = false;
        }

        public void OnPluginLoadingEnv(List<String> lstPluginEnv)
        {
            foreach (String env in lstPluginEnv)
            {
                Log.Debug("^9OnPluginLoadingEnv: " + env, 7);
            }
            switch (lstPluginEnv[1])
            {
                case "BF3":
                    _gameVersion = GameVersion.BF3;
                    break;
                case "BF4":
                    _gameVersion = GameVersion.BF4;
                    break;
            }
            Log.Debug("^1Game Version: " + _gameVersion, 1);
        }

        protected void LogThreadExit()
        {
            lock (_aliveThreads)
            {
                _aliveThreads.Remove(Thread.CurrentThread.ManagedThreadId);
            }
        }

        protected void StartAndLogThread(Thread aThread)
        {
            aThread.Start();
            lock (_aliveThreads)
            {
                if (!_aliveThreads.ContainsKey(aThread.ManagedThreadId))
                {
                    _aliveThreads.Add(aThread.ManagedThreadId, aThread);
                    _threadMasterWaitHandle.WaitOne(100);
                }
            }
        }

        public Boolean AdminsOnline()
        {
            return _playerDictionary.Values.Any(aPlayer => aPlayer.IsAdmin);
        }

        public Boolean OnlineAdminSayMessage(String message)
        {
            ProconChatWrite(Log.CMaroon(Log.FBold(message)));
            Boolean adminsTold = false;
            foreach (var aPlayer in _playerDictionary.Values.ToList().Where(aPlayer => aPlayer.IsAdmin))
            {
                adminsTold = true;
                PlayerSayMessage(aPlayer.Name, message, true, 1);
            }
            return adminsTold;
        }

        public void ProconChatWrite(String msg)
        {
            msg = msg.Replace(Environment.NewLine, String.Empty);
            ExecuteCommand("procon.protected.chat.write", "AdKatsLRT > " + msg);
            if (_slowmo)
            {
                _threadMasterWaitHandle.WaitOne(1000);
            }
        }

        public DateTime DateTimeFromEpochSeconds(Double epochSeconds)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(epochSeconds);
        }

        public void UpdateSettingPage()
        {
            SetExternalPluginSetting("AdKatsLRT", "UpdateSettings", "Update");
        }

        public void SetExternalPluginSetting(String pluginName, String settingName, String settingValue)
        {
            if (String.IsNullOrEmpty(pluginName) || String.IsNullOrEmpty(settingName) || settingValue == null)
            {
                Log.Error("Required inputs null or empty in setExternalPluginSetting");
                return;
            }
            ExecuteCommand("procon.protected.plugins.setVariable", pluginName, settingName, settingValue);
        }

        public TimeSpan NowDuration(DateTime diff)
        {
            return (DateTime.UtcNow - diff).Duration();
        }

        public class AdKatsServer
        {
            public Int64 ServerID;
            public Int64 ServerGroup;
            public String ServerIP;
            public String ServerName;
            public String ServerType = "UNKNOWN";
            public Int64 GameID = -1;
            public String ConnectionState;
            public Boolean CommanderEnabled;
            public Boolean FairFightEnabled;
            public Boolean ForceReloadWholeMags;
            public Boolean HitIndicatorEnabled;
            public String GamePatchVersion = "UNKNOWN";
            public Int32 MaxSpectators = -1;
            public CServerInfo InfoObject { get; private set; }
            private DateTime _infoObjectTime = DateTime.UtcNow;

            private AdKatsLRT _plugin;

            public AdKatsServer(AdKatsLRT plugin)
            {
                _plugin = plugin;
            }

            public void SetInfoObject(CServerInfo infoObject)
            {
                InfoObject = infoObject;
                ServerName = infoObject.ServerName;
                _infoObjectTime = DateTime.UtcNow;
            }

            public TimeSpan GetRoundElapsedTime()
            {
                if (InfoObject == null)
                {
                    return TimeSpan.Zero;
                }
                return TimeSpan.FromSeconds(InfoObject.RoundTime) + (DateTime.UtcNow - _infoObjectTime);
            }
        }

        public class AdKatsLoadout
        {
            public HashSet<String> AllKitItemIDs;

            public WarsawItem KitGadget1;
            public WarsawItem KitGadget2;
            public WarsawItem KitGrenade;
            public WarsawItem KitItemPrimary;
            public WarsawItem KitItemSidearm;
            public WarsawItem KitKnife;
            public readonly Dictionary<String, WarsawItem> LoadoutItems;
            public readonly Dictionary<String, WarsawVehicle> LoadoutVehicles;
            public readonly Dictionary<String, WarsawVehicle> LoadoutRCONVehicles;
            public readonly Dictionary<String, WarsawItem> VehicleItems;
            public String Name;
            public String PersonaID;
            public WarsawKit SelectedKit;

            public AdKatsLoadout()
            {
                LoadoutItems = new Dictionary<String, WarsawItem>();
                LoadoutVehicles = new Dictionary<String, WarsawVehicle>();
                LoadoutRCONVehicles = new Dictionary<String, WarsawVehicle>();
                VehicleItems = new Dictionary<String, WarsawItem>();
                AllKitItemIDs = new HashSet<String>();
            }
        }

        public class ProcessObject
        {
            public String ProcessReason;
            public Boolean ProcessManual;
            public DateTime ProcessTime;
            public AdKatsSubscribedPlayer ProcessPlayer;
        }

        public class AdKatsSubscribedPlayer
        {
            public Boolean AA;
            public String ConversationPartner;
            public Int32 Deaths;
            public String GUID;
            public Int64 ID;
            public Int32 InfractionPoints;
            public String IP;
            public Boolean IsAdmin;
            public Double KDR;
            public Int32 Kills;
            public TimeSpan LastAction = TimeSpan.Zero;
            public TimeSpan LastForgive = TimeSpan.Zero;
            public TimeSpan LastPunishment = TimeSpan.Zero;
            public Boolean LoadoutEnforced = false;
            public Boolean LoadoutValid = true;
            public Boolean LoadoutForced;
            public Boolean LoadoutIgnored;
            public String Name;
            public Boolean Online;
            public String PBGUID;
            public String PersonaID;
            public String ClanTag;
            public Double Ping;
            public Boolean Punished;
            public Int32 Rank;
            public Boolean Reported;
            public Double Reputation;
            public String Role;
            public Int32 Score;
            public Boolean SpawnedOnce;
            public Int32 Squad;
            public Int32 Team;
            public String Type;

            public AdKatsLoadout Loadout;
            public HashSet<String> WatchedVehicles;
            public DateTime LastUsage;
            public Int32 LoadoutKills;
            public Int32 MaxDeniedItems;
            public Int32 LoadoutChecks;
            public Int32 SkippedChecks;

            public AdKatsSubscribedPlayer()
            {
                WatchedVehicles = new HashSet<String>();
                LastUsage = DateTime.UtcNow;
            }

            public String GetVerboseName()
            {
                return ((String.IsNullOrEmpty(ClanTag)) ? ("") : ("[" + ClanTag + "]")) + Name;
            }
        }

        public class MapMode
        {
            public Int32 MapModeID;
            public String ModeKey;
            public String MapKey;
            public String ModeName;
            public String MapName;

            public MapMode(Int32 mapModeID, String modeKey, String mapKey, String modeName, String mapName)
            {
                MapModeID = mapModeID;
                ModeKey = modeKey;
                MapKey = mapKey;
                ModeName = modeName;
                MapName = mapName;
            }
        }

        public class WarsawItem
        {
            //only take entries with numeric IDs
            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
            //Parsed categoryType does not make any modifications
            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
            //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
            //If expansion exists assign it, if not, ignore
            public Dictionary<String, Dictionary<String, WarsawItemAccessory>> AccessoriesAllowed;
            public Dictionary<String, WarsawItemAccessory> AccessoriesAssigned;
            public String CategoryReadable;
            public String Category;
            public String CategoryTypeReadable;
            public String CategoryType;
            public String Desc;
            public String Name;
            public String Slug;
            public String WarsawID;
            public WarsawVehicle AssignedVehicle;

            public WarsawItem()
            {
                AccessoriesAssigned = new Dictionary<String, WarsawItemAccessory>();
                AccessoriesAllowed = new Dictionary<String, Dictionary<String, WarsawItemAccessory>>();
            }
        }

        public class WarsawVehicle
        {
            public String CategoryReadable;
            public String Category;
            public String CategoryTypeReadable;
            public String CategoryType;
            public Int32 SlotIndexPrimary = -1;
            public readonly Dictionary<String, WarsawItem> AllowedPrimaries;
            public WarsawItem AssignedPrimary;
            public Int32 SlotIndexSecondary = -1;
            public readonly Dictionary<String, WarsawItem> AllowedSecondaries;
            public WarsawItem AssignedSecondary;
            public Int32 SlotIndexSecondaryGunner = -1;
            public readonly Dictionary<String, WarsawItem> AllowedSecondariesGunner;
            public WarsawItem AssignedSecondaryGunner;
            public Int32 SlotIndexCountermeasure = -1;
            public readonly Dictionary<String, WarsawItem> AllowedCountermeasures;
            public WarsawItem AssignedCountermeasure;
            public Int32 SlotIndexOptic = -1;
            public readonly Dictionary<String, WarsawItem> AllowedOptics;
            public WarsawItem AssignedOptic;
            public Int32 SlotIndexOpticGunner = -1;
            public readonly Dictionary<String, WarsawItem> AllowedOpticsGunner;
            public WarsawItem AssignedOpticGunner;
            public Int32 SlotIndexUpgrade = -1;
            public readonly Dictionary<String, WarsawItem> AllowedUpgrades;
            public WarsawItem AssignedUpgrade;
            public Int32 SlotIndexUpgradeGunner = -1;
            public readonly Dictionary<String, WarsawItem> AllowedUpgradesGunner;
            public WarsawItem AssignedUpgradeGunner;
            public HashSet<String> LinkedRCONCodes;

            public WarsawVehicle()
            {
                AllowedPrimaries = new Dictionary<String, WarsawItem>();
                AllowedSecondaries = new Dictionary<String, WarsawItem>();
                AllowedSecondariesGunner = new Dictionary<String, WarsawItem>();
                AllowedCountermeasures = new Dictionary<String, WarsawItem>();
                AllowedOptics = new Dictionary<String, WarsawItem>();
                AllowedOpticsGunner = new Dictionary<String, WarsawItem>();
                AllowedUpgrades = new Dictionary<String, WarsawItem>();
                AllowedUpgradesGunner = new Dictionary<String, WarsawItem>();
                LinkedRCONCodes = new HashSet<String>();
            }
        }

        public class WarsawKit
        {
            public enum Type
            {
                Assault,
                Engineer,
                Support,
                Recon
            }

            public Type KitType;
            public readonly Dictionary<String, WarsawItem> KitAllowedPrimary;
            public readonly Dictionary<String, WarsawItem> KitAllowedSecondary;
            public readonly Dictionary<String, WarsawItem> KitAllowedGadget1;
            public readonly Dictionary<String, WarsawItem> KitAllowedGadget2;
            public readonly Dictionary<String, WarsawItem> KitAllowedGrenades;
            public readonly Dictionary<String, WarsawItem> KitAllowedKnife;

            public WarsawKit()
            {
                KitAllowedPrimary = new Dictionary<String, WarsawItem>();
                KitAllowedSecondary = new Dictionary<String, WarsawItem>();
                KitAllowedGadget1 = new Dictionary<String, WarsawItem>();
                KitAllowedGadget2 = new Dictionary<String, WarsawItem>();
                KitAllowedGrenades = new Dictionary<String, WarsawItem>();
                KitAllowedKnife = new Dictionary<String, WarsawItem>();
            }
        }

        public class WarsawItemAccessory
        {
            //only take entries with numeric IDs
            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
            //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
            //If expansion exists assign it, if not, ignore
            public String Category;
            public String CategoryReadable;
            public String Name;
            public String Slug;
            public String WarsawID;
        }

        public class WarsawLibrary
        {
            public readonly Dictionary<String, WarsawItem> Items;
            public readonly Dictionary<String, WarsawItemAccessory> ItemAccessories;
            public readonly Dictionary<String, WarsawVehicle> Vehicles;
            public readonly Dictionary<String, WarsawItem> VehicleUnlocks;
            public readonly WarsawKit KitAssault;
            public readonly WarsawKit KitEngineer;
            public readonly WarsawKit KitSupport;
            public readonly WarsawKit KitRecon;

            public WarsawLibrary()
            {
                Items = new Dictionary<String, WarsawItem>();
                ItemAccessories = new Dictionary<String, WarsawItemAccessory>();
                Vehicles = new Dictionary<String, WarsawVehicle>();
                VehicleUnlocks = new Dictionary<String, WarsawItem>();
                KitAssault = new WarsawKit()
                {
                    KitType = WarsawKit.Type.Assault,
                };
                KitEngineer = new WarsawKit()
                {
                    KitType = WarsawKit.Type.Engineer,
                };
                KitSupport = new WarsawKit()
                {
                    KitType = WarsawKit.Type.Support,
                };
                KitRecon = new WarsawKit()
                {
                    KitType = WarsawKit.Type.Recon,
                };
            }
        }

        public class Logger
        {
            private readonly AdKatsLRT _plugin;
            public Int32 DebugLevel { get; set; }
            public Boolean VerboseErrors { get; set; }

            public Logger(AdKatsLRT plugin)
            {
                _plugin = plugin;
            }

            private void WriteConsole(String msg)
            {
                _plugin.ExecuteCommand("procon.protected.pluginconsole.write", "[^b" + _plugin.GetType().Name + "^n] " + msg);
            }

            private void WriteChat(String msg)
            {
                _plugin.ExecuteCommand("procon.protected.chat.write", _plugin.GetType().Name + " > " + msg);
            }

            public void Debug(String msg, Int32 level)
            {
                if (DebugLevel >= level)
                {
                    if (DebugLevel >= 8)
                    {
                        WriteConsole("[" + level + "-" + new StackFrame(1).GetMethod().Name + "-" + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Thread.CurrentThread.Name)) + Thread.CurrentThread.ManagedThreadId + "] " + msg);
                    }
                    else
                    {
                        WriteConsole(msg);
                    }
                }
            }

            public void Write(String msg)
            {
                WriteConsole(msg);
            }

            public void Info(String msg)
            {
                WriteConsole("^b^0INFO^n^0: " + msg);
            }

            public void Warn(String msg)
            {
                WriteConsole("^b^3WARNING^n^0: " + msg);
            }

            public void Error(String msg)
            {
                if (VerboseErrors)
                {
                    //Opening
                    WriteConsole("^b^1ERROR-" +//Plugin version
                                 Int32.Parse(_plugin.GetPluginVersion().Replace(".", "")) + "-" +//Method name
                                 new StackFrame(1).GetMethod().Name + "-" +//Thread
                                 ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Thread.CurrentThread.Name)) + Thread.CurrentThread.ManagedThreadId +//Closing
                                 "^n^0: " +//Error Message
                                 "[" + msg + "]");
                }
                else
                {
                    //Opening
                    WriteConsole("^b^1ERROR-" +//Plugin version
                                 Int32.Parse(_plugin.GetPluginVersion().Replace(".", "")) +//Closing
                                 "^n^0: " +//Error Message
                                 "[" + msg + "]");
                }
            }

            public void Success(String msg)
            {
                WriteConsole("^b^2SUCCESS^n^0: " + msg);
            }

            public void Exception(String msg, Exception e)
            {
                this.Exception(msg, e, 1);
            }

            public void Exception(String msg, Exception e, Int32 level)
            {
                //Opening
                String exceptionMessage = "^b^8EXCEPTION-" +//Plugin version
                                          Int32.Parse(_plugin.GetPluginVersion().Replace(".", ""));
                if (e != null)
                {
                    exceptionMessage += "-";
                    Int64 impericalLineNumber = 0;
                    Int64 parsedLineNumber = 0;
                    StackTrace stack = new StackTrace(e, true);
                    if (stack.FrameCount > 0)
                    {
                        impericalLineNumber = stack.GetFrame(0).GetFileLineNumber();
                    }
                    Int64.TryParse(e.ToString().Split(' ').Last(), out parsedLineNumber);
                    if (impericalLineNumber != 0)
                    {
                        exceptionMessage += impericalLineNumber;
                    }
                    else if (parsedLineNumber != 0)
                    {
                        exceptionMessage += parsedLineNumber;
                    }
                    else
                    {
                        exceptionMessage += "D";
                    }
                }
                exceptionMessage += "-" +//Method name
                                    new StackFrame(level + 1).GetMethod().Name + "-" +//Thread
                                    ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Thread.CurrentThread.Name)) + Thread.CurrentThread.ManagedThreadId +//Closing
                                    "^n^0: " +//Message
                                    "[" + msg + "]" +//Exception string
                                    ((e != null) ? ("[" + e + "]") : (""));
                WriteConsole(exceptionMessage);
            }

            public void Chat(String msg)
            {
                msg = msg.Replace(Environment.NewLine, String.Empty);
                WriteChat(msg);
            }

            public String FBold(String msg)
            {
                return "^b" + msg + "^n";
            }

            public String FItalic(String msg)
            {
                return "^i" + msg + "^n";
            }

            public String CMaroon(String msg)
            {
                return "^1" + msg + "^0";
            }

            public String CGreen(String msg)
            {
                return "^2" + msg + "^0";
            }

            public String COrange(String msg)
            {
                return "^3" + msg + "^0";
            }

            public String CBlue(String msg)
            {
                return "^4" + msg + "^0";
            }

            public String CBlueLight(String msg)
            {
                return "^5" + msg + "^0";
            }

            public String CViolet(String msg)
            {
                return "^6" + msg + "^0";
            }

            public String CPink(String msg)
            {
                return "^7" + msg + "^0";
            }

            public String CRed(String msg)
            {
                return "^8" + msg + "^0";
            }

            public String CGrey(String msg)
            {
                return "^9" + msg + "^0";
            }
        }
    }

    // GZipWebClient removed — replaced by Flurl HTTP client (Procon v2)
}
