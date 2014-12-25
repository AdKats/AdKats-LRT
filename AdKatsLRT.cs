/* 
 * AdKatsLRT - On-Spawn Loadout Enforcer
 * 
 * AdKats and respective extensions are inspired by the gaming community A Different Kind (ADK). 
 * Visit http://www.ADKGamers.com/ for more information.
 *
 * The AdKats Frostbite Plugin is open source, and under public domain, but certain extensions are not. 
 * The AdKatsLRT extension is not open for free distribution, copyright Daniel J. Gradinjan, with all rights reserved.
 * Version usage of this plugin is tracked by gamerethos.net
 * 
 * Development by Daniel J. Gradinjan (ColColonCleaner)
 * 
 * AdKatsLRT.cs
 * Version 1.0.6.3
 * 25-DEC-2014
 * 
 * Automatic Update Information
 * <version_code>1.0.6.3</version_code>
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using MySql.Data.MySqlClient;
using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents {
    public class AdKatsLRT : PRoConPluginAPI, IPRoConPluginInterface {
        //Current Plugin Version
        private const String PluginVersion = "1.0.6.3";

        public enum ConsoleMessageType {
            Normal,
            Info,
            Success,
            Warning,
            Error,
            Exception
        };

        public enum GameVersion {
            BF3,
            BF4
        };

        //State
        private GameVersion _gameVersion = GameVersion.BF3;
        private volatile Boolean _pluginEnabled;
        private WarsawLibrary _WARSAWLibrary = new WarsawLibrary();
        private Boolean _WARSAWLibraryLoaded;
        private HashSet<String> _AdminList = new HashSet<string>();
        private readonly Dictionary<String, AdKatsSubscribedPlayer> _PlayerDictionary = new Dictionary<String, AdKatsSubscribedPlayer>();
        private Boolean _firstPlayerListComplete;
        private Boolean _isTestingAuthorized;
        private readonly Dictionary<String, AdKatsSubscribedPlayer> _PlayerLeftDictionary = new Dictionary<String, AdKatsSubscribedPlayer>();
        private readonly Queue<ProcessObject> _LoadoutProcessingQueue = new Queue<ProcessObject>();
        private readonly Queue<AdKatsSubscribedPlayer> _BattlelogFetchQueue = new Queue<AdKatsSubscribedPlayer>();
        private readonly Dictionary<String, String> _WARSAWInvalidLoadoutIDMessages = new Dictionary<String, String>();
        private readonly HashSet<String> _WARSAWSpawnDeniedIDs = new HashSet<String>();
        private Int32 _countEnforced;
        private Int32 _countKilled;
        private Int32 _countFixed;
        private Int32 _countQuit;
        private readonly AdKatsServer _serverInfo;
        private Boolean _displayLoadoutDebug = false;

        //Settings
        private Boolean _enableAdKatsIntegration;
        private Boolean _spawnEnforcementActOnAdmins;
        private Boolean _spawnEnforcementActOnReputablePlayers;
        private Int32 _triggerEnforcementMinimumInfractionPoints = 6;

        //Timing
        private readonly DateTime _proconStartTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private readonly TimeSpan _BattlelogWaitDuration = TimeSpan.FromSeconds(0.8);
        private DateTime _StartTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _LastVersionTrackingUpdate = DateTime.UtcNow - TimeSpan.FromHours(1);
        private DateTime _LastBattlelogAction = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastSuccessfulPlayerList = DateTime.UtcNow - TimeSpan.FromSeconds(5);

        //Threads
        private readonly Dictionary<Int32, Thread> _aliveThreads = new Dictionary<Int32, Thread>();
        private volatile Boolean _threadsReady;
        private EventWaitHandle _threadMasterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _LoadoutProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PlayerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _BattlelogCommWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _Activator;
        private Thread _Finalizer;
        private Thread _SpawnProcessingThread;
        private Thread _BattlelogCommThread;

        //Settings
        private Int32 _YellDuration = 5;

        //Debug
        private const Boolean FullDebug = false;
        private const Boolean SlowMoOnException = false;
        private volatile Int32 _debugLevel = 0;
        private String _debugSoldierName = "ColColonCleaner";
        private Boolean _slowmo;
        private Boolean _toldCol;

        public AdKatsLRT()
        {
            //Create the server reference
            _serverInfo = new AdKatsServer(this);

            //Set defaults for webclient
            ServicePointManager.Expect100Continue = false;

            //By default plugin is not enabled or ready
            _pluginEnabled = false;
            _threadsReady = false;

            //Debug level is 0 by default
            _debugLevel = 0;

            //Prepare the keep-alive
            SetupStatusMonitor();
        }

        public String GetPluginName() {
            return "AdKatsLRT - Loadout Enforcer";
        }

        public String GetPluginVersion() {
            return PluginVersion;
        }

        public String GetPluginAuthor() {
            return "[ADK]ColColonCleaner";
        }

        public String GetPluginWebsite() {
            return "https://github.com/AdKats/";
        }

        public String GetPluginDescription() {
            return "";
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            var lstReturn = new List<CPluginVariable>();
            try
            {
                const string separator = " | ";

                lstReturn.Add(new CPluginVariable("0. Instance Settings" + separator + "Integrate with AdKats", typeof(Boolean), _enableAdKatsIntegration));
                if (_enableAdKatsIntegration)
                {
                    lstReturn.Add(new CPluginVariable("0. Instance Settings" + separator + "Spawn Enforce Admins", typeof(Boolean), _spawnEnforcementActOnAdmins));
                    lstReturn.Add(new CPluginVariable("0. Instance Settings" + separator + "Spawn Enforce Reputable Players", typeof(Boolean), _spawnEnforcementActOnReputablePlayers));
                    lstReturn.Add(new CPluginVariable("0. Instance Settings" + separator + "Trigger Enforce Minimum Infraction Points", typeof(Int32), _triggerEnforcementMinimumInfractionPoints));
                }

                if (!_WARSAWLibraryLoaded)
                {
                    lstReturn.Add(new CPluginVariable("The WARSAW library must be loaded to view settings.", typeof(String), "Enable the plugin to fetch the library."));
                    return lstReturn;
                }

                lstReturn.Add(new CPluginVariable("1. Preset Settings" + separator + "Presets Coming Soon", typeof(String), "Presets Coming Soon"));

                _WARSAWSpawnDeniedIDs.RemoveWhere(spawnID => !_WARSAWInvalidLoadoutIDMessages.ContainsKey(spawnID));
                if (_WARSAWLibrary.Items.Any())
                {
                    foreach (WarsawItem weapon in _WARSAWLibrary.Items.Values.Where(weapon => weapon.category != "GADGET").OrderBy(weapon => weapon.category).ThenBy(weapon => weapon.slug))
                    {
                        if (_enableAdKatsIntegration) 
                        {
                            lstReturn.Add(new CPluginVariable("2. Weapons - " + weapon.categoryType + "|ALWT" + weapon.warsawID + separator + weapon.slug + separator + "Allow on trigger?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.warsawID) ? ("Deny") : ("Allow")));
                            if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.warsawID)) 
                            {
                                lstReturn.Add(new CPluginVariable("2. Weapons - " + weapon.categoryType + "|ALWS" + weapon.warsawID + separator + weapon.slug + separator + "Allow on spawn?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(weapon.warsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                        else
                        {
                            lstReturn.Add(new CPluginVariable("2. Weapons - " + weapon.categoryType + "|ALWT" + weapon.warsawID + separator + weapon.slug + separator + "Allow on spawn?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.warsawID) ? ("Deny") : ("Allow")));
                        }
                    }
                }
                if (_WARSAWLibrary.Items.Any())
                {
                    foreach (WarsawItem weapon in _WARSAWLibrary.Items.Values.Where(weapon => weapon.category == "GADGET").OrderBy(weapon => weapon.category).ThenBy(weapon => weapon.slug))
                    {
                        if (String.IsNullOrEmpty(weapon.categoryType)) {
                            ConsoleError(weapon.warsawID + " did not have a category type.");
                        }
                        if (_enableAdKatsIntegration) 
                        {
                            lstReturn.Add(new CPluginVariable("3. Gadgets - " + weapon.categoryType + "|ALWT" + weapon.warsawID + separator + weapon.slug + separator + "Allow on trigger?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.warsawID) ? ("Deny") : ("Allow")));
                            if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.warsawID)) 
                            {
                                lstReturn.Add(new CPluginVariable("3. Gadgets - " + weapon.categoryType + "|ALWS" + weapon.warsawID + separator + weapon.slug + separator + "Allow on spawn?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(weapon.warsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                        else
                        {
                            lstReturn.Add(new CPluginVariable("3. Gadgets - " + weapon.categoryType + "|ALWT" + weapon.warsawID + separator + weapon.slug + separator + "Allow on spawn?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.warsawID) ? ("Deny") : ("Allow")));
                        }
                    }
                }
                /*if (_WARSAWLibrary.VehicleUnlocks.Any()) {
                    foreach (WarsawItem unlock in _WARSAWLibrary.VehicleUnlocks.Values.OrderBy(vehicleUnlock => vehicleUnlock.category).ThenBy(vehicleUnlock => vehicleUnlock.slug)) {
                        lstReturn.Add(new CPluginVariable("4. Vehicle Unlocks|ALWT" + unlock.warsawID + separator + unlock.category + separator + unlock.slug + separator + "Allow on trigger?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(unlock.warsawID) ? ("Deny") : ("Allow")));
                        if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(unlock.warsawID))
                        {
                            lstReturn.Add(new CPluginVariable("4. Vehicle Unlocks|ALWS" + unlock.warsawID + separator + unlock.category + separator + unlock.slug + separator + "Allow on spawn?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(unlock.warsawID) ? ("Deny") : ("Allow")));
                        }                    
                    }
                }*/
                if (_WARSAWLibrary.ItemAccessories.Any())
                {
                    foreach (WarsawItemAccessory weaponAccessory in _WARSAWLibrary.ItemAccessories.Values.OrderBy(weaponAccessory => weaponAccessory.slug).ThenBy(weaponAccessory => weaponAccessory.category))
                    {
                        if (_enableAdKatsIntegration)
                        {
                            lstReturn.Add(new CPluginVariable("4. Weapon Accessories - " + weaponAccessory.category + "|ALWT" + weaponAccessory.warsawID + separator + weaponAccessory.slug + separator + "Allow on trigger?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.warsawID) ? ("Deny") : ("Allow")));
                            if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.warsawID))
                            {
                                lstReturn.Add(new CPluginVariable("4. Weapon Accessories - " + weaponAccessory.category + "|ALWS" + weaponAccessory.warsawID + separator + weaponAccessory.slug + separator + "Allow on spawn?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(weaponAccessory.warsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                        else
                        {
                            lstReturn.Add(new CPluginVariable("4. Weapon Accessories - " + weaponAccessory.category + "|ALWT" + weaponAccessory.warsawID + separator + weaponAccessory.slug + separator + "Allow on spawn?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.warsawID) ? ("Deny") : ("Allow")));
                        }
                    }
                }
                foreach (var pair in _WARSAWInvalidLoadoutIDMessages.Where(denied => _WARSAWLibrary.Items.ContainsKey(denied.Key)))
                {
                    WarsawItem deniedItem;
                    if (_WARSAWLibrary.Items.TryGetValue(pair.Key, out deniedItem))
                    {
                        lstReturn.Add(new CPluginVariable("5A. Denied Item Kill Messages|MSG" + deniedItem.warsawID + separator + deniedItem.slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                /*foreach (var pair in _WARSAWInvalidLoadoutIDMessages.Where(denied => _WARSAWLibrary.VehicleUnlocks.ContainsKey(denied.Key))) {
                    WarsawItem deniedVehicleUnlock;
                    if (_WARSAWLibrary.VehicleUnlocks.TryGetValue(pair.Key, out deniedVehicleUnlock)) {
                        lstReturn.Add(new CPluginVariable("5C. Denied Vehicle Unlock Kill Messages|MSG" + deniedVehicleUnlock.warsawID + separator + deniedVehicleUnlock.slug + separator + "Kill Message", typeof (String), pair.Value));
                    }
                }*/
                foreach (var pair in _WARSAWInvalidLoadoutIDMessages.Where(denied => _WARSAWLibrary.ItemAccessories.ContainsKey(denied.Key)))
                {
                    WarsawItemAccessory deniedItemAccessory;
                    if (_WARSAWLibrary.ItemAccessories.TryGetValue(pair.Key, out deniedItemAccessory))
                    {
                        lstReturn.Add(new CPluginVariable("5B. Denied Item Accessory Kill Messages|MSG" + deniedItemAccessory.warsawID + separator + deniedItemAccessory.slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof(int), _debugLevel));
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while getting display plugin variables", e));
            }
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables() {
            var lstReturn = new List<CPluginVariable>();
            const string separator = " | ";
            lstReturn.Add(new CPluginVariable("0. Instance Settings" + separator + "Integrate with AdKats", typeof(Boolean), _enableAdKatsIntegration));
            lstReturn.Add(new CPluginVariable("0. Instance Settings" + separator + "Spawn Enforce Admins", typeof(Boolean), _spawnEnforcementActOnAdmins));
            lstReturn.Add(new CPluginVariable("0. Instance Settings" + separator + "Spawn Enforce Reputable Players", typeof(Boolean), _spawnEnforcementActOnReputablePlayers));
            lstReturn.Add(new CPluginVariable("0. Instance Settings" + separator + "Trigger Enforce Minimum Infraction Points", typeof(Int32), _triggerEnforcementMinimumInfractionPoints));
            foreach (var pair in _WARSAWInvalidLoadoutIDMessages) {
                lstReturn.Add(new CPluginVariable("MSG" + pair.Key, typeof (String), pair.Value));
            }
            _WARSAWSpawnDeniedIDs.RemoveWhere(spawnID => !_WARSAWInvalidLoadoutIDMessages.ContainsKey(spawnID));
            foreach (var deniedSpawnID in _WARSAWSpawnDeniedIDs)
            {
                lstReturn.Add(new CPluginVariable("ALWS" + deniedSpawnID, typeof(String), "Deny"));
            }
            lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof(int), _debugLevel));
            return lstReturn;
        }

        public void SetPluginVariable(String strVariable, String strValue) {
            if (strValue == null) {
                return;
            }
            try {
                if (strVariable == "UpdateSettings") {
                    //Do nothing. Settings page will be updated after return.
                }
                else if (Regex.Match(strVariable, @"Debug level").Success)
                {
                    Int32 tmp;
                    if (int.TryParse(strValue, out tmp))
                    {
                        if (tmp == 269) {
                            ConsoleSuccess("Extended Debug Mode Activated");
                            _displayLoadoutDebug = true;
                            return;
                        }
                        if (tmp != _debugLevel)
                        {
                            _debugLevel = tmp;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Debug Soldier Name").Success)
                {
                    if (SoldierNameValid(strValue))
                    {
                        if (strValue != _debugSoldierName)
                        {
                            _debugSoldierName = strValue;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Integrate with AdKats").Success)
                {
                    Boolean enableAdKatsIntegration = Boolean.Parse(strValue);
                    if (enableAdKatsIntegration != _enableAdKatsIntegration)
                    {
                        if (_threadsReady) {
                            ConsoleInfo("AdKatsLRT must be rebooted to modify this setting.");
                            Disable();
                        }
                        _enableAdKatsIntegration = enableAdKatsIntegration;
                    }
                }
                else if (Regex.Match(strVariable, @"Spawn Enforce Admins").Success)
                {
                    Boolean spawnEnforcementActOnAdmins = Boolean.Parse(strValue);
                    if (spawnEnforcementActOnAdmins != _spawnEnforcementActOnAdmins)
                    {
                        _spawnEnforcementActOnAdmins = spawnEnforcementActOnAdmins;
                    }
                }
                else if (Regex.Match(strVariable, @"Spawn Enforce Reputable Players").Success)
                {
                    Boolean spawnEnforcementActOnReputablePlayers = Boolean.Parse(strValue);
                    if (spawnEnforcementActOnReputablePlayers != _spawnEnforcementActOnReputablePlayers)
                    {
                        _spawnEnforcementActOnReputablePlayers = spawnEnforcementActOnReputablePlayers;
                    }
                }
                else if (Regex.Match(strVariable, @"Trigger Enforce Minimum Infraction Points").Success)
                {
                    Int32 triggerEnforcementMinimumInfractionPoints;
                    if (int.TryParse(strValue, out triggerEnforcementMinimumInfractionPoints))
                    {
                        if (triggerEnforcementMinimumInfractionPoints != _triggerEnforcementMinimumInfractionPoints)
                        {
                            _triggerEnforcementMinimumInfractionPoints = triggerEnforcementMinimumInfractionPoints;
                        }
                    }
                }
                else if (strVariable.StartsWith("ALWT"))
                {
                    //Trim off all but the warsaw ID
                    //ALWT3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("ALWT".ToCharArray()).Trim();
                    //Fetch needed role
                    switch (strValue.ToLower())
                    {
                        case "allow":
                            //parse allow
                            _WARSAWInvalidLoadoutIDMessages.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            _WARSAWInvalidLoadoutIDMessages[warsawID] = "Please respawn without " + commandSplit[commandSplit.Count() - 2].Trim() + " in your loadout";
                            if (!_enableAdKatsIntegration) {
                                if (!_WARSAWSpawnDeniedIDs.Contains(warsawID))
                                {
                                    _WARSAWSpawnDeniedIDs.Add(warsawID);
                                }
                            }
                            break;
                        default:
                            ConsoleError("Unknown setting when assigning WARSAW allowance.");
                            return;
                    }
                }
                else if (strVariable.StartsWith("ALWS"))
                {
                    //Trim off all but the warsaw ID
                    //ALWS3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("ALWS".ToCharArray()).Trim();
                    //Fetch needed role
                    switch (strValue.ToLower())
                    {
                        case "allow":
                            //parse allow
                            _WARSAWSpawnDeniedIDs.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            if (!_WARSAWSpawnDeniedIDs.Contains(warsawID)) {
                                _WARSAWSpawnDeniedIDs.Add(warsawID);
                            }
                            break;
                        default:
                            ConsoleError("Unknown setting when assigning WARSAW allowance.");
                            return;
                    }
                }
                else if (strVariable.StartsWith("MSG")) {
                    //Trim off all but the warsaw ID
                    //MSG3495820391
                    if (String.IsNullOrEmpty(strValue)) {
                        ConsoleError("Kill messages cannot be empty.");
                        return;
                    }
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("MSG".ToCharArray()).Trim();
                    _WARSAWInvalidLoadoutIDMessages[warsawID] = strValue;
                }
                else {
                    ConsoleInfo(strVariable + " =+= " + strValue);
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while updating AdKatsLRT settings.", e));
            }
        }

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
            DebugWrite("Entering OnPluginLoaded", 7);
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
                    "OnPlayerLeft");
            }
            catch (Exception e) {
                HandleException(new AdKatsException("FATAL ERROR on plugin load.", e));
            }
            DebugWrite("Exiting OnPluginLoaded", 7);
        }

        public void OnPluginEnable() {
            try {
                //If the finalizer is still alive, inform the user and disable
                if (_Finalizer != null && _Finalizer.IsAlive) {
                    ConsoleError("Cannot enable plugin while it is shutting down. Please Wait for it to shut down.");
                    _threadMasterWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                    //Disable the plugin
                    Disable();
                    return;
                }
                if (_gameVersion != GameVersion.BF4) {
                    ConsoleError("The AdKatsLRT extension cannot be enabled outside BF4.");
                    Disable();
                    return;
                }
                //Create a new thread to activate the plugin
                _Activator = new Thread(new ThreadStart(delegate {
                    try {
                        Thread.CurrentThread.Name = "enabler";

                        _pluginEnabled = true;

                        if ((DateTime.UtcNow - _proconStartTime).TotalSeconds <= 20)
                        {
                            ConsoleWrite("Waiting a few seconds for requirements and other plugins to initialize, please wait...");
                            //Wait on all settings to be imported by procon for initial start, and for all other plugins to start and register.
                            for (Int32 index = 20 - (Int32) (DateTime.UtcNow - _proconStartTime).TotalSeconds; index > 0; index--) {
                                ConsoleWrite(index + "...");
                                _threadMasterWaitHandle.WaitOne(1000);
                            }
                        }
                        if (!_pluginEnabled)
                        {
                            LogThreadExit();
                            return;
                        }
                        Boolean AdKatsFound = GetRegisteredCommands().Any(command => command.RegisteredClassname == "AdKats" && command.RegisteredMethodName == "PluginEnabled");
                        if (AdKatsFound) {
                            _enableAdKatsIntegration = true;
                        }
                        if (!_enableAdKatsIntegration || AdKatsFound)
                        {
                            _StartTime = DateTime.UtcNow;
                            //Set the enabled variable
                            _PlayerProcessingWaitHandle.Reset();

                            if (!_pluginEnabled)
                            {
                                LogThreadExit();
                                return;
                            }
                            //Fetch all weapon names
                            if (_WARSAWLibraryLoaded || LoadWarsawLibrary()) {
                                if (!_pluginEnabled)
                                {
                                    LogThreadExit();
                                    return;
                                }
                                ConsoleSuccess("WARSAW library loaded. " + _WARSAWLibrary.Items.Count + " items, " + _WARSAWLibrary.VehicleUnlocks.Count + " vehicle unlocks, and " + _WARSAWLibrary.ItemAccessories.Count + " accessories.");
                                UpdateSettingPage();

                                if (_enableAdKatsIntegration) {
                                    //Subscribe to online soldiers from AdKats
                                    ExecuteCommand("procon.protected.plugins.call", "AdKats", "SubscribeAsClient", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                        {"caller_identity", "AdKatsLRT"},
                                        {"response_requested", false},
                                        {"subscription_group", "OnlineSoldiers"},
                                        {"subscription_method", "ReceiveOnlineSoldiers"},
                                        {"subscription_enabled", true}
                                    }));
                                    ConsoleInfo("Waiting for player listing response from AdKats.");
                                }
                                else
                                {
                                    ConsoleInfo("Waiting for first player list event.");
                                }
                                _PlayerProcessingWaitHandle.WaitOne(Timeout.Infinite);
                                if (!_pluginEnabled)
                                {
                                    LogThreadExit();
                                    return;
                                }

                                //Init and start all the threads
                                InitWaitHandles();
                                OpenAllHandles();
                                InitThreads();
                                StartThreads();

                                ConsoleSuccess("AdKatsLRT " + GetPluginVersion() + " startup complete [" + FormatTimeString(DateTime.UtcNow - _StartTime, 3) + "]. Loadout restriction now online.");
                            }
                            else {
                                ConsoleError("Failed to load WARSAW library. AdKatsLRT cannot be started.");
                                Disable();
                            }
                        }
                        else {
                            ConsoleError("AdKats not installed or enabled. AdKatsLRT cannot be started.");
                            Disable();
                        }
                    }
                    catch (Exception e) {
                        HandleException(new AdKatsException("Error while enabling AdKatsLRT.", e));
                    }
                    LogThreadExit();
                }));

                ConsoleWrite("^b^2ENABLED!^n^0 Beginning startup sequence...");
                //Start the thread
                StartAndLogThread(_Activator);
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while initializing activator thread.", e));
            }
        }

        public void OnPluginDisable() {
            //If the plugin is already disabling then cancel
            if (_Finalizer != null && _Finalizer.IsAlive)
                return;
            try {
                //Create a new thread to disabled the plugin
                _Finalizer = new Thread(new ThreadStart(delegate {
                    try {
                        Thread.CurrentThread.Name = "finalizer";
                        ConsoleInfo("Shutting down AdKatsLRT.");
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
                        do {
                            OpenAllHandles();
                            attempts++;
                            Thread.Sleep(500);
                            alive = false;
                            String aliveThreads = "";
                            lock (_aliveThreads) {
                                foreach (Thread aliveThread in _aliveThreads.Values.ToList()) {
                                    alive = true;
                                    aliveThreads += (aliveThread.Name + "[" + aliveThread.ManagedThreadId + "] ");
                                }
                            }
                            if (aliveThreads.Length > 0) {
                                if (attempts > 20) {
                                    ConsoleWarn("Threads still exiting: " + aliveThreads);
                                }
                                else {
                                    DebugWrite("Threads still exiting: " + aliveThreads, 2);
                                }
                            }
                        } while (alive);
                        _toldCol = false;
                        _firstPlayerListComplete = false;
                        _PlayerDictionary.Clear();
                        _PlayerLeftDictionary.Clear();
                        _LoadoutProcessingQueue.Clear();
                        _firstPlayerListComplete = false;
                        _countEnforced = 0;
                        _countFixed = 0;
                        _countKilled = 0;
                        _countQuit = 0;
                        _slowmo = false;
                        ConsoleWrite("^b^1AdKatsLRT " + GetPluginVersion() + " Disabled! =(^n^0");
                    }
                    catch (Exception e) {
                        HandleException(new AdKatsException("Error occured while disabling AdkatsLRT.", e));
                    }
                }));

                //Start the finalizer thread
                _Finalizer.Start();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while initializing AdKatsLRT disable thread.", e));
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            DebugWrite("Entering OnServerInfo", 7);
            try
            {
                if (_pluginEnabled)
                {
                    lock (_serverInfo)
                    {
                        if (serverInfo != null)
                        {
                            //Get the server info
                            _serverInfo.SetInfoObject(serverInfo);

                            Boolean hadServerName = !String.IsNullOrEmpty(_serverInfo.ServerName);
                            _serverInfo.ServerName = serverInfo.ServerName;
                            Boolean haveServerName = !String.IsNullOrEmpty(_serverInfo.ServerName);
                            Boolean wasADK = _isTestingAuthorized;
                            _isTestingAuthorized = serverInfo.ServerName.Contains("=ADK=");
                            if (!wasADK && _isTestingAuthorized)
                            {
                                ConsoleInfo("LRT is testing authorized.");
                            }
                            if (haveServerName && !hadServerName)
                            {
                                PostVersionTracking();
                            }
                        }
                        else
                        {
                            HandleException(new AdKatsException("Server info was null"));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while processing server info.", e));
            }
            DebugWrite("Exiting OnServerInfo", 7);
        }

        private void SetupStatusMonitor() {
            //Create a new thread to handle status monitoring
            //This thread will remain running for the duration the layer is online
            var statusMonitorThread = new Thread(new ThreadStart(delegate {
                try {
                    Thread.CurrentThread.Name = "StatusMonitor";
                    DateTime lastKeepAliveCheck = DateTime.UtcNow;
                    DateTime lastAdminFetch = DateTime.UtcNow;
                    while (true) {
                        try {
                            //Check for thread warning every 30 seconds
                            if ((DateTime.UtcNow - lastKeepAliveCheck).TotalSeconds > 30)
                            {
                                if (_threadsReady)
                                {
                                    Boolean AdKatsFound = GetRegisteredCommands().Any(command => command.RegisteredClassname == "AdKats" && command.RegisteredMethodName == "PluginEnabled");
                                    if (AdKatsFound) {
                                        if (!_enableAdKatsIntegration) {
                                            ConsoleError("AdKats found, but integration not enabled, disabling.");
                                            Disable();
                                        }
                                    }
                                    else if (_enableAdKatsIntegration)
                                    {
                                        ConsoleError("AdKats was disabled. AdKatsLRT has integration enabled, and must shut down if that plugin shuts down.");
                                        Disable();
                                    }
                                }
                                lastKeepAliveCheck = DateTime.UtcNow;

                                if (_aliveThreads.Count() >= 20) {
                                    String aliveThreads = "";
                                    lock (_aliveThreads) {
                                        foreach (Thread value in _aliveThreads.Values.ToList())
                                            aliveThreads = aliveThreads + (value.Name + "[" + value.ManagedThreadId + "] ");
                                    }
                                    ConsoleWarn("Thread warning: " + aliveThreads);
                                }
                            }

                            //Check for updated admins every minute
                            if (_enableAdKatsIntegration && (DateTime.UtcNow - lastAdminFetch).TotalSeconds > 60 && _threadsReady) 
                            {
                                lastAdminFetch = DateTime.UtcNow;
                                ExecuteCommand("procon.protected.plugins.call", "AdKats", "FetchAuthorizedSoldiers", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                    {"caller_identity", "AdKatsLRT"},
                                    {"response_requested", true},
                                    {"response_class", "AdKatsLRT"},
                                    {"response_method", "ReceiveAdminList"},
                                    {"user_subset", "admin"}
                                }));
                            }

                            //Post usage stats at interval
                            if ((DateTime.UtcNow - _LastVersionTrackingUpdate).TotalMinutes > 20 && 
                                (_threadsReady || (DateTime.UtcNow - _proconStartTime).TotalSeconds > 60))
                            {
                                PostVersionTracking();
                            }

                            //Sleep 1 second between loops
                            _threadMasterWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                        }
                        catch (Exception e) {
                            HandleException(new AdKatsException("Error in keep-alive. Skipping current loop.", e));
                        }
                    }
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error while running keep-alive.", e));
                }
            }));
            //Start the thread
            statusMonitorThread.Start();
        }

        public void InitWaitHandles() {
            //Initializes all wait handles 
            _threadMasterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _LoadoutProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _PlayerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void OpenAllHandles() {
            _threadMasterWaitHandle.Set();
            _LoadoutProcessingWaitHandle.Set();
            _PlayerProcessingWaitHandle.Set();
        }

        public void InitThreads() {
            try {
                _SpawnProcessingThread = new Thread(ProcessingThreadLoop) {
                    IsBackground = true
                };

                _BattlelogCommThread = new Thread(BattlelogCommThreadLoop)
                {
                    IsBackground = true
                };
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while initializing threads.", e));
            }
        }

        public void StartThreads() {
            DebugWrite("Entering StartThreads", 7);
            try {
                //Reset the master wait handle
                _threadMasterWaitHandle.Reset();
                //Start the spawn processing thread
                StartAndLogThread(_SpawnProcessingThread);
                StartAndLogThread(_BattlelogCommThread);
                _threadsReady = true;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while starting processing threads.", e));
            }
            DebugWrite("Exiting StartThreads", 7);
        }

        private void Disable() {
            //Call Disable
            ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "False");
            //Set enabled false so threads begin exiting
            _pluginEnabled = false;
            _threadsReady = false;
        }

        private void Enable() {
            if (Thread.CurrentThread.Name == "finalizer") {
                var pluginRebootThread = new Thread(new ThreadStart(delegate {
                    DebugWrite("Starting a reboot thread.", 5);
                    try {
                        Thread.CurrentThread.Name = "RebootThread";
                        Thread.Sleep(1000);
                        //Call Enable
                        ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "True");
                    }
                    catch (Exception) {
                        HandleException(new AdKatsException("Error while running reboot."));
                    }
                    DebugWrite("Exiting a reboot thread.", 5);
                    LogThreadExit();
                }));
                StartAndLogThread(pluginRebootThread);
            }
            else {
                //Call Enable
                ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "True");
            }
        }

        public void OnPluginLoadingEnv(List<String> lstPluginEnv) {
            foreach (String env in lstPluginEnv) {
                DebugWrite("^9OnPluginLoadingEnv: " + env, 7);
            }
            switch (lstPluginEnv[1]) {
                case "BF3":
                    _gameVersion = GameVersion.BF3;
                    break;
                case "BF4":
                    _gameVersion = GameVersion.BF4;
                    break;
            }
            DebugWrite("^1Game Version: " + _gameVersion, 1);
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset cpsSubset)
        {
            //Completely ignore this event if integrated with AdKats
            if (_enableAdKatsIntegration || !_pluginEnabled) {
                return;
            }
            DebugWrite("Entering OnListPlayers", 7);
            try
            {
                if (cpsSubset.Subset != CPlayerSubset.PlayerSubsetType.All)
                {
                    return;
                }
                lock (_PlayerDictionary)
                {
                    var validPlayers = new List<String>();
                    foreach (CPlayerInfo cPlayer in players)
                    {
                        //Check for glitched players
                        if (String.IsNullOrEmpty(cPlayer.GUID))
                        {
                            continue;
                        }
                        //Ready to parse
                        var aPlayer = new AdKatsSubscribedPlayer();
                        aPlayer.player_id = 0;
                        aPlayer.player_guid = cPlayer.GUID;
                        aPlayer.player_pbguid = null;
                        aPlayer.player_ip = null;
                        aPlayer.player_name = cPlayer.SoldierName;
                        aPlayer.player_personaID = null;
                        aPlayer.player_clanTag = null;
                        aPlayer.player_online = true;
                        aPlayer.player_aa = false;
                        aPlayer.player_ping = cPlayer.Ping;
                        aPlayer.player_reputation = 0;
                        aPlayer.player_infractionPoints = 0;
                        aPlayer.player_role = "guest_default";
                        switch (cPlayer.Type)
                        {
                            case 0:
                                aPlayer.player_type = "Player";
                                break;
                            case 1:
                                aPlayer.player_type = "Spectator";
                                break;
                            case 2:
                                aPlayer.player_type = "CommanderPC";
                                break;
                            case 3:
                                aPlayer.player_type = "CommanderMobile";
                                break;
                            default:
                                ConsoleError("Player type " + cPlayer.Type + " is not valid.");
                                break;
                        }
                        aPlayer.player_isAdmin = false;
                        aPlayer.player_reported = false;
                        aPlayer.player_punished = false;
                        aPlayer.player_marked = false;
                        aPlayer.player_lastPunishment = TimeSpan.FromSeconds(0);
                        aPlayer.player_lastForgive = TimeSpan.FromSeconds(0);
                        aPlayer.player_lastAction = TimeSpan.FromSeconds(0);
                        aPlayer.player_spawnedOnce = false;
                        aPlayer.player_conversationPartner = null;
                        aPlayer.player_kills = cPlayer.Kills;
                        aPlayer.player_deaths = cPlayer.Deaths;
                        aPlayer.player_kdr = cPlayer.Kdr;
                        aPlayer.player_rank = cPlayer.Rank;
                        aPlayer.player_score = cPlayer.Score;
                        aPlayer.player_squad = cPlayer.SquadID;
                        aPlayer.player_team = cPlayer.TeamID;

                        validPlayers.Add(aPlayer.player_name);

                        Boolean process = false;
                        AdKatsSubscribedPlayer dPlayer;
                        Boolean newPlayer = false;
                        //Are they online?
                        if (!_PlayerDictionary.TryGetValue(aPlayer.player_name, out dPlayer)) {
                            //Not online. Are they returning?
                            if (!_PlayerLeftDictionary.TryGetValue(aPlayer.player_guid, out dPlayer)) {
                                //Not online or returning. New player.
                                newPlayer = true;
                            }
                        }
                        if (newPlayer)
                        {
                            _PlayerDictionary[aPlayer.player_name] = aPlayer;
                            _PlayerLeftDictionary.Remove(aPlayer.player_guid);
                            dPlayer = aPlayer;
                            QueuePlayerForBattlelogInfoFetch(dPlayer);
                        }
                        else
                        {
                            dPlayer.player_name = aPlayer.player_name;
                            dPlayer.player_ip = aPlayer.player_ip;
                            dPlayer.player_aa = aPlayer.player_aa;
                            dPlayer.player_ping = aPlayer.player_ping;
                            dPlayer.player_type = aPlayer.player_type;
                            dPlayer.player_spawnedOnce = aPlayer.player_spawnedOnce;
                            dPlayer.player_kills = aPlayer.player_kills;
                            dPlayer.player_deaths = aPlayer.player_deaths;
                            dPlayer.player_kdr = aPlayer.player_kdr;
                            dPlayer.player_rank = aPlayer.player_rank;
                            dPlayer.player_score = aPlayer.player_score;
                            dPlayer.player_squad = aPlayer.player_squad;
                            dPlayer.player_team = aPlayer.player_team;
                        }
                    }
                    foreach (string playerName in _PlayerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList())
                    {
                        AdKatsSubscribedPlayer aPlayer;
                        if (_PlayerDictionary.TryGetValue(playerName, out aPlayer))
                        {
                            _PlayerDictionary.Remove(aPlayer.player_name);
                            _PlayerLeftDictionary[aPlayer.player_guid] = aPlayer;
                        }
                    }
                }
                _firstPlayerListComplete = true;
                _PlayerProcessingWaitHandle.Set();
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error occured while listing players.", e));
            }
            DebugWrite("Exiting OnListPlayers", 7);
        }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) {
            try {
                DateTime spawnTime = DateTime.UtcNow;
                if (_threadsReady && _pluginEnabled && _firstPlayerListComplete)
                {
                    AdKatsSubscribedPlayer aPlayer;
                    if (_PlayerDictionary.TryGetValue(soldierName, out aPlayer) && aPlayer.player_online) {
                        aPlayer.player_spawnedOnce = true;
                        //Reject spawn processing if player has no persona ID
                        if (String.IsNullOrEmpty(aPlayer.player_personaID)) {
                            if (!_enableAdKatsIntegration) {
                                QueuePlayerForBattlelogInfoFetch(aPlayer);
                            }
                            DebugWrite(aPlayer.player_name + " does not have a Persona ID yet.", 3);
                            return;
                        }
                        if (_WARSAWSpawnDeniedIDs.Any() ||
                            (aPlayer.player_reported && aPlayer.player_reputation < 0) || 
                            aPlayer.player_punished || 
                            aPlayer.player_marked ||
                            (aPlayer.player_infractionPoints >= _triggerEnforcementMinimumInfractionPoints && aPlayer.player_lastPunishment.TotalDays < 60))
                        {
                            //Create process object
                            var processObject = new ProcessObject() {
                                process_player = aPlayer,
                                process_source = "spawn",
                                process_time = spawnTime
                            };
                            //Minimum wait time of 5 seconds
                            if (_LoadoutProcessingQueue.Count >= 6) {
                                QueueForProcessing(processObject);
                            }
                            else
                            {
                                var waitTime = TimeSpan.FromSeconds(5 - _LoadoutProcessingQueue.Count);
                                if (waitTime.TotalSeconds > 0) {
                                    DebugWrite("Waiting " + ((int) waitTime.TotalSeconds) + " seconds to process " + aPlayer.GetVerboseName() + " spawn.", 3);
                                }
                                else {
                                    return;
                                }
                                //Start a delay thread
                                StartAndLogThread(new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "LoadoutCheckDelay";
                                    Thread.Sleep(100);
                                    try {
                                        Thread.Sleep(waitTime);
                                        QueueForProcessing(processObject);
                                    }
                                    catch (Exception e)
                                    {
                                        HandleException(new AdKatsException("Error running loadout check delay thread.", e));
                                    }
                                    Thread.Sleep(100);
                                    LogThreadExit();
                                })));
                            }
                        }
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while handling player spawn.", e));
            }
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            DebugWrite("Entering OnPlayerLeft", 7);
            try 
            {
                AdKatsSubscribedPlayer aPlayer;
                if (_PlayerDictionary.TryGetValue(playerInfo.SoldierName, out aPlayer)) {
                    aPlayer.player_online = false;
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while handling player left.", e));
            }
            DebugWrite("Exiting OnPlayerLeft", 7);
        }

        public void CallLoadoutCheckOnPlayer(params String[] parameters) {
            DebugWrite("CallLoadoutCheckOnPlayer starting!", 6);
            try {
                if (parameters.Length != 2) {
                    ConsoleError("Call loadout check canceled. Parameters invalid.");
                    return;
                }
                String source = parameters[0];
                String unparsedCommandJSON = parameters[1];

                var decodedCommand = (Hashtable) JSON.JsonDecode(unparsedCommandJSON);

                var playerName = (String)decodedCommand["player_name"];
                var loadoutCheckReason = (String)decodedCommand["loadoutCheck_reason"];

                if (_threadsReady && _pluginEnabled && _firstPlayerListComplete) {
                    AdKatsSubscribedPlayer aPlayer;
                    if (_PlayerDictionary.TryGetValue(playerName, out aPlayer)) {
                        ConsoleWrite("Loadout check manually called on " + playerName + ".");
                        QueueForProcessing(new ProcessObject() {
                            process_player = aPlayer,
                            process_source = loadoutCheckReason,
                            process_time = DateTime.UtcNow
                        });
                    }
                    else {
                        ConsoleError("Attempted to call MANUAL loadout check on " + playerName + " without their player object loaded.");
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while calling loadout check on player.", e));
            }
            DebugWrite("CallLoadoutCheckOnPlayer finished!", 6);
        }

        public void ReceiveAdminList(params String[] parameters)
        {
            DebugWrite("ReceiveAdminList starting!", 6);
            try {
                if (parameters.Length != 2) {
                    ConsoleError("Online admin receiving cancelled. Parameters invalid.");
                    return;
                }
                String source = parameters[0];
                String unparsedCommandJSON = parameters[1];

                var decodedCommand = (Hashtable) JSON.JsonDecode(unparsedCommandJSON);

                var unparsedAdminList = (String) decodedCommand["response_value"];

                String[] tempAdminList = CPluginVariable.DecodeStringArray(unparsedAdminList);
                foreach (String adminPlayerName in tempAdminList) {
                    if (!_AdminList.Contains(adminPlayerName)) {
                        _AdminList.Add(adminPlayerName);
                    }
                }
                _AdminList.RemoveWhere(name => !tempAdminList.Contains(name));
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while calling loadout check on player.", e));
            }
            DebugWrite("ReceiveAdminList finished!", 6);
        }

        private void QueueForProcessing(ProcessObject processObject)
        {
            DebugWrite("Entering QueueForProcessing", 7);
            try
            {
                if (processObject == null || processObject.process_player == null)
                {
                    ConsoleError("Attempted to process null object or player.");
                    return;
                }
                if (!processObject.process_player.player_online || 
                    String.IsNullOrEmpty(processObject.process_player.player_personaID))
                {
                    DebugWrite(processObject.process_player.player_name + " queue cancelled. Player is not online, or has no persona ID.", 4);
                    return;
                }
                lock (_LoadoutProcessingQueue)
                {
                    if (_LoadoutProcessingQueue.Any(obj => obj != null && obj.process_player != null && obj.process_player.player_id == processObject.process_player.player_id))
                    {
                        DebugWrite(processObject.process_player.player_name + " queue cancelled. Player already in queue.", 4);
                        return;
                    }
                    Int32 oldCount = _LoadoutProcessingQueue.Count();
                    _LoadoutProcessingQueue.Enqueue(processObject);
                    DebugWrite(processObject.process_player.player_name + " queued [" + oldCount + "->" + _LoadoutProcessingQueue.Count + "] after " + Math.Round(DateTime.UtcNow.Subtract(processObject.process_time).TotalSeconds, 2) + "s", 5);
                    _LoadoutProcessingWaitHandle.Set();
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing player for processing.", e));
            }
            DebugWrite("Exiting QueueForProcessing", 7);
        }

        public void ProcessingThreadLoop() {
            try {
                DebugWrite("SPROC: Starting Spawn Processing Thread", 1);
                Thread.CurrentThread.Name = "SpawnProcessing";
                DateTime loopStart = DateTime.UtcNow;
                while (true) {
                    try {
                        DebugWrite("SPROC: Entering Spawn Processing Thread Loop", 7);
                        if (!_pluginEnabled) {
                            DebugWrite("SPROC: Detected AdKatsLRT not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        if (_LoadoutProcessingQueue.Count > 0) {
                            ProcessObject processObject = null;
                            lock (_LoadoutProcessingQueue)
                            {
                                //Dequeue the next object
                                Int32 oldCount = _LoadoutProcessingQueue.Count();
                                ProcessObject importObject = _LoadoutProcessingQueue.Dequeue();
                                if (importObject == null)
                                {
                                    ConsoleError("Process object was null when entering player processing loop.");
                                    continue;
                                }
                                if (importObject.process_player == null)
                                {
                                    ConsoleError("Process player was null when entering player processing loop.");
                                    continue;
                                }
                                var processDelay = DateTime.UtcNow.Subtract(importObject.process_time);
                                if (DateTime.UtcNow.Subtract(importObject.process_time).TotalSeconds > 30 && _LoadoutProcessingQueue.Count < 3)
                                {
                                    ConsoleWarn(importObject.process_player.GetVerboseName() + " took abnormally long to start processing. [" + FormatTimeString(processDelay, 2) + "]");
                                }
                                else
                                {
                                    DebugWrite(importObject.process_player.player_name + " dequeued [" + oldCount + "->" + _LoadoutProcessingQueue.Count + "] after " + Math.Round(processDelay.TotalSeconds, 2) + "s", 5);
                                }
                                processObject = importObject;
                            }
                            
                            //Grab the player
                            AdKatsSubscribedPlayer aPlayer = processObject.process_player;

                            //Parse the reason for enforcement
                            Boolean trigger = false;
                            Boolean killOverride = false;
                            String reason = "";
                            if (aPlayer.player_marked || processObject.process_source == "marked")
                            {
                                reason = "[marked] ";
                                trigger = true;
                                killOverride = true;
                            }
                            else if (aPlayer.player_punished || processObject.process_source == "punished")
                            {
                                reason = "[recently punished] ";
                                trigger = true;
                                killOverride = true;
                            }
                            else if ((aPlayer.player_reported || processObject.process_source == "reported") && aPlayer.player_reputation < 0)
                            {
                                reason = "[reported] ";
                                trigger = true;
                            }
                            else if (aPlayer.player_infractionPoints >= _triggerEnforcementMinimumInfractionPoints && aPlayer.player_lastPunishment.TotalDays < 60 && aPlayer.player_reputation < 0)
                            {
                                reason = "[" + aPlayer.player_infractionPoints + " infractions] ";
                                trigger = true;
                            }
                            else if (processObject.process_source == "spawn")
                            {
                                reason = "[spawn] ";
                            }
                            else if (processObject.process_source == "listing")
                            {
                                reason = "[join] ";
                            }
                            else
                            {
                                ConsoleError("Unknown reason for processing player. Cancelling processing.");
                                continue;
                            }

                            //Fetch the loadout
                            AdKatsLoadout loadout = GetPlayerLoadout(aPlayer.player_personaID);
                            if (loadout == null) {
                                continue;
                            }
                            aPlayer.Loadout = loadout;

                            //Show the loadout contents
                            String primaryMessage = loadout.KitItemPrimary.slug + " [" + loadout.KitItemPrimary.Accessories.Values.Aggregate("", (currentString, acc) => currentString + TrimStart(acc.slug, loadout.KitItemPrimary.slug).Trim() + ", ").Trim().TrimEnd(',') + "]";
                            String sidearmMessage = loadout.KitItemSidearm.slug + " [" + loadout.KitItemSidearm.Accessories.Values.Aggregate("", (currentString, acc) => currentString + TrimStart(acc.slug, loadout.KitItemSidearm.slug).Trim() + ", ").Trim().TrimEnd(',') + "]";
                            String gadgetMessage = "[" + loadout.KitGadget1.slug + ", " + loadout.KitGadget2.slug + "]";
                            String grenadeMessage = "[" + loadout.KitGrenade.slug + "]";
                            String knifeMessage = "[" + loadout.KitKnife.slug + "]";
                            String loadoutMessage = "Player " + loadout.Name + " processed as " + loadout.SelectedKitType + " with primary " + primaryMessage + " sidearm " + sidearmMessage + " gadgets " + gadgetMessage + " grenade " + grenadeMessage + " and knife " + knifeMessage;
                            DebugWrite(loadoutMessage, 4);

                            HashSet<String> specificMessages = new HashSet<String>();
                            HashSet<String> spawnSpecificMessages = new HashSet<String>();
                            Boolean loadoutValid = true;
                            Boolean spawnLoadoutValid = true;
                            if (trigger)
                            {
                                foreach (var warsawDeniedIDMessage in _WARSAWInvalidLoadoutIDMessages)
                                {
                                    if (loadout.AllKitItemIDs.Contains(warsawDeniedIDMessage.Key))
                                    {
                                        loadoutValid = false;
                                        if(!specificMessages.Contains(warsawDeniedIDMessage.Value))
                                        {
                                            specificMessages.Add(warsawDeniedIDMessage.Value);
                                        }
                                    }
                                }

                                if (_enableAdKatsIntegration)
                                {
                                    //Inform AdKats of the loadout
                                    StartAndLogThread(new Thread(new ThreadStart(delegate
                                    {
                                        Thread.CurrentThread.Name = "AdKatsInformThread";
                                        Thread.Sleep(100);
                                        ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                            {"caller_identity", "AdKatsLRT"},
                                            {"response_requested", false},
                                            {"loadout_player", loadout.Name},
                                            {"loadout_valid", loadoutValid}
                                        }));
                                        Thread.Sleep(100);
                                        LogThreadExit();
                                    })));
                                }

                                foreach (var warsawDeniedID in _WARSAWSpawnDeniedIDs)
                                {
                                    if (loadout.AllKitItemIDs.Contains(warsawDeniedID))
                                    {
                                        spawnLoadoutValid = false;
                                        if (!spawnSpecificMessages.Contains(_WARSAWInvalidLoadoutIDMessages[warsawDeniedID]))
                                        {
                                            spawnSpecificMessages.Add(_WARSAWInvalidLoadoutIDMessages[warsawDeniedID]);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                foreach (var warsawDeniedID in _WARSAWSpawnDeniedIDs)
                                {
                                    if (loadout.AllKitItemIDs.Contains(warsawDeniedID))
                                    {
                                        loadoutValid = false;
                                        spawnLoadoutValid = false;
                                        if (!spawnSpecificMessages.Contains(_WARSAWInvalidLoadoutIDMessages[warsawDeniedID]))
                                        {
                                            spawnSpecificMessages.Add(_WARSAWInvalidLoadoutIDMessages[warsawDeniedID]);
                                        }
                                    }
                                }
                            }

                            if (!trigger && !spawnLoadoutValid) {
                                //Reputable players
                                if (processObject.process_player.player_reputation >= 15)
                                {
                                    //Option for reputation deny
                                    if (!_spawnEnforcementActOnReputablePlayers)
                                    {
                                        DebugWrite(processObject.process_player.player_name + " spawn enforcement cancelled. Player is reputable.", 4);
                                        continue;
                                    }
                                }
                                //Admins
                                if (processObject.process_player.player_isAdmin)
                                {
                                    //Option for admin deny
                                    if (!_spawnEnforcementActOnAdmins)
                                    {
                                        DebugWrite(processObject.process_player.player_name + " spawn enforcement cancelled. Player is admin.", 4);
                                        continue;
                                    }
                                }
                            }

                            aPlayer.player_loadoutEnforced = true;
                            if (!loadoutValid)
                            {
                                String deniedWeapons = String.Empty;
                                String spawnDeniedWeapons = String.Empty;
                                //Fill the denied messages
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitItemPrimary.warsawID))
                                {
                                    deniedWeapons += loadout.KitItemPrimary.slug.ToUpper() + ", ";
                                }
                                deniedWeapons = loadout.KitItemPrimary.Accessories.Values.Where(weaponAccessory => _WARSAWInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.warsawID)).Aggregate(deniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.slug.ToUpper() + ", "));
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitItemSidearm.warsawID))
                                {
                                    deniedWeapons += loadout.KitItemSidearm.slug.ToUpper() + ", ";
                                }
                                deniedWeapons = loadout.KitItemSidearm.Accessories.Values.Where(weaponAccessory => _WARSAWInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.warsawID)).Aggregate(deniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.slug.ToUpper() + ", "));
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitGadget1.warsawID))
                                {
                                    deniedWeapons += loadout.KitGadget1.slug.ToUpper() + ", ";
                                }
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitGadget2.warsawID))
                                {
                                    deniedWeapons += loadout.KitGadget2.slug.ToUpper() + ", ";
                                }
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitGrenade.warsawID))
                                {
                                    deniedWeapons += loadout.KitGrenade.slug.ToUpper() + ", ";
                                }
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitKnife.warsawID))
                                {
                                    deniedWeapons += loadout.KitKnife.slug.ToUpper() + ", ";
                                }
                                //Fill the spawn denied messages
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitItemPrimary.warsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitItemPrimary.slug.ToUpper() + ", ";
                                }
                                spawnDeniedWeapons = loadout.KitItemPrimary.Accessories.Values.Where(weaponAccessory => _WARSAWSpawnDeniedIDs.Contains(weaponAccessory.warsawID)).Aggregate(spawnDeniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.slug.ToUpper() + ", "));
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitItemSidearm.warsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitItemSidearm.slug.ToUpper() + ", ";
                                }
                                spawnDeniedWeapons = loadout.KitItemSidearm.Accessories.Values.Where(weaponAccessory => _WARSAWSpawnDeniedIDs.Contains(weaponAccessory.warsawID)).Aggregate(spawnDeniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.slug.ToUpper() + ", "));
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitGadget1.warsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGadget1.slug.ToUpper() + ", ";
                                }
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitGadget2.warsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGadget2.slug.ToUpper() + ", ";
                                }
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitGrenade.warsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGrenade.slug.ToUpper() + ", ";
                                }
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitKnife.warsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitKnife.slug.ToUpper() + ", ";
                                }
                                //Trim the messages
                                deniedWeapons = deniedWeapons.Trim().TrimEnd(',');
                                spawnDeniedWeapons = spawnDeniedWeapons.Trim().TrimEnd(',');

                                //Decide whether to kill the player
                                Boolean killPlayer = false;
                                Boolean adminsOnline = AdminsOnline();
                                if (!spawnLoadoutValid || killOverride || (!adminsOnline && trigger)) {
                                    killPlayer = true;
                                }

                                if (trigger) {
                                    //Loadout enforcement was triggered
                                    if (killOverride || !adminsOnline) {
                                        //Manual trigger or no admins online, enforce all denied weapons
                                        OnlineAdminSayMessage(reason + aPlayer.GetVerboseName() + " killed for denied items [" + deniedWeapons + "].");
                                        PlayerSayMessage(aPlayer.player_name, reason + aPlayer.GetVerboseName() + " please remove [" + deniedWeapons + "] from your loadout.");
                                        foreach (String specificMessage in specificMessages) {
                                            PlayerTellMessage(loadout.Name, specificMessage);
                                        }
                                    }
                                    else {
                                        //Not manual trigger and admins online
                                        if (spawnLoadoutValid) {
                                            //OnlineAdminSayMessage(reason + aPlayer.GetVerboseName() + " has denied items [" + deniedWeapons + "].");
                                        }
                                        else
                                        {
                                            PlayerSayMessage(aPlayer.player_name, reason + aPlayer.GetVerboseName() + " please remove [" + spawnDeniedWeapons + "] from your loadout.");
                                            foreach (String specificMessage in spawnSpecificMessages)
                                            {
                                                PlayerTellMessage(loadout.Name, specificMessage);
                                            }
                                        }
                                    }
                                }
                                else {
                                    //Loadout enforcement was not triggered, enforce spawn denied weapons only
                                    PlayerSayMessage(aPlayer.player_name, reason + aPlayer.GetVerboseName() + " please remove [" + spawnDeniedWeapons + "] from your loadout.");
                                    foreach (String specificMessage in spawnSpecificMessages)
                                    {
                                        PlayerTellMessage(loadout.Name, specificMessage);
                                    }
                                }
                                if (killPlayer)
                                {
                                    aPlayer.player_loadoutKilled = true;
                                    DebugWrite(loadout.Name + " KILLED for invalid loadout.", 1);
                                    if (aPlayer.player_spawnedOnce) {
                                        //Start a repeat kill
                                        StartAndLogThread(new Thread(new ThreadStart(delegate {
                                            Thread.CurrentThread.Name = "PlayerRepeatKill";
                                            Thread.Sleep(100);
                                            for (Int32 index = 0; index < 15; index++) {
                                                ExecuteCommand("procon.protected.send", "admin.killPlayer", loadout.Name);
                                                Thread.Sleep(500);
                                            }
                                            Thread.Sleep(100);
                                            LogThreadExit();
                                        })));
                                    }
                                    else
                                    {
                                        //Perform a single kill
                                        ExecuteCommand("procon.protected.send", "admin.killPlayer", loadout.Name);
                                    }
                                }
                            }
                            else {
                                if (!aPlayer.player_loadoutValid) 
                                {
                                    PlayerSayMessage(aPlayer.player_name, aPlayer.GetVerboseName() + " thank you for fixing your loadout.");
                                    if (killOverride)
                                    {
                                        OnlineAdminSayMessage(reason + aPlayer.GetVerboseName() + " fixed their loadout.");
                                    }
                                }
                            }
                            aPlayer.player_loadoutValid = loadoutValid;
                            Int32 totalPlayerCount = _PlayerDictionary.Count + _PlayerLeftDictionary.Count;
                            Int32 countEnforced = _PlayerDictionary.Values.Count(dPlayer => dPlayer.player_loadoutEnforced) + _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutEnforced);
                            Int32 countKilled = _PlayerDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled) + _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled);
                            Int32 countFixed = _PlayerDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled && dPlayer.player_loadoutValid) + _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled && dPlayer.player_loadoutValid);
                            Int32 countQuit = _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled && !dPlayer.player_loadoutValid);
                            Boolean displayStats =   (_countEnforced != countEnforced) || 
                                                (_countKilled != countKilled) || 
                                                (_countFixed != countFixed) || 
                                                (_countQuit != countQuit);
                            _countEnforced = countEnforced;
                            _countKilled = countKilled;
                            _countFixed = countFixed;
                            _countQuit = countQuit;
                            Double percentEnforced = Math.Round(((Double)countEnforced / (Double)totalPlayerCount) * 100.0);
                            Double percentKilled = Math.Round(((Double)countKilled / (Double)totalPlayerCount) * 100.0);
                            Double percentFixed = Math.Round(((Double)countFixed / (Double)countKilled) * 100.0);
                            Double percentRaged = Math.Round(((Double)countQuit / (Double)countKilled) * 100.0);
                            if (displayStats)
                            {
                                DebugWrite("(" + countEnforced + "/" + totalPlayerCount + ") " + percentEnforced + "% processed. " + "(" + countKilled + "/" + totalPlayerCount + ") " + percentKilled + "% killed. " + "(" + countFixed + "/" + countKilled + ") " + percentFixed + "% fixed. " + "(" + countQuit + "/" + countKilled + ") " + percentRaged + "% quit.", 2);
                            }
                            DebugWrite(_LoadoutProcessingQueue.Count + " players still in queue.", 3);
                            DebugWrite(processObject.process_player.player_name + " processed after " + Math.Round(DateTime.UtcNow.Subtract(processObject.process_time).TotalSeconds, 2) + "s", 5);
                        }
                        else {
                            //Wait for input
                            _LoadoutProcessingWaitHandle.Reset();
                            _LoadoutProcessingWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                            loopStart = DateTime.UtcNow;
                        }
                    }
                    catch (Exception e) {
                        if (e is ThreadAbortException) {
                            HandleException(new AdKatsException("Spawn processing thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in spawn processing thread.", e));
                    }
                }
                DebugWrite("SPROC: Ending Spawn Processing Thread", 1);
                LogThreadExit();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured in kill processing thread.", e));
            }
        }

        private void QueuePlayerForBattlelogInfoFetch(AdKatsSubscribedPlayer aPlayer)
        {
            DebugWrite("Entering QueuePlayerForBattlelogInfoFetch", 6);
            try
            {
                DebugWrite("Preparing to queue player for battlelog info fetch.", 6);
                if (_BattlelogFetchQueue.Any(bPlayer => bPlayer.player_guid == aPlayer.player_guid)) {
                    return;
                }
                lock (_BattlelogFetchQueue)
                {
                    _BattlelogFetchQueue.Enqueue(aPlayer);
                    DebugWrite("Player queued for battlelog info fetch.", 6);
                    _BattlelogCommWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while queuing player for battlelog info fetch.", e));
            }
            DebugWrite("Exiting QueuePlayerForBattlelogInfoFetch", 6);
        }

        public void BattlelogCommThreadLoop()
        {
            try
            {
                DebugWrite("BTLOG: Starting Battlelog Comm Thread", 1);
                Thread.CurrentThread.Name = "BattlelogComm";
                DateTime loopStart = DateTime.UtcNow;
                while (true)
                {
                    try
                    {
                        DebugWrite("BTLOG: Entering Battlelog Comm Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            DebugWrite("BTLOG: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        //Sleep for 10ms
                        _threadMasterWaitHandle.WaitOne(10);

                        //Handle Inbound player fetches
                        if (_BattlelogFetchQueue.Count > 0)
                        {
                            Queue<AdKatsSubscribedPlayer> unprocessedPlayers;
                            lock (_BattlelogFetchQueue)
                            {
                                DebugWrite("BTLOG: Inbound players found. Grabbing.", 6);
                                //Grab all items in the queue
                                unprocessedPlayers = new Queue<AdKatsSubscribedPlayer>(_BattlelogFetchQueue.ToArray());
                                //Clear the queue for next run
                                _BattlelogFetchQueue.Clear();
                            }
                            //Loop through all players in order that they came in
                            while (unprocessedPlayers.Count > 0)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                DebugWrite("BTLOG: Preparing to fetch battlelog info for player", 6);
                                //Dequeue the record
                                AdKatsSubscribedPlayer aPlayer = unprocessedPlayers.Dequeue();
                                //Run the appropriate action
                                FetchPlayerBattlelogInformation(aPlayer);
                            }
                        }
                        else
                        {
                            //Wait for new actions
                            _BattlelogCommWaitHandle.Reset();
                            _BattlelogCommWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                            loopStart = DateTime.UtcNow;
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            HandleException(new AdKatsException("Battlelog comm thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in Battlelog comm thread. Skipping current loop.", e));
                    }
                }
                DebugWrite("BTLOG: Ending Battlelog Comm Thread", 1);
                LogThreadExit();
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error occured in battlelog comm thread.", e));
            }
        }

        public void FetchPlayerBattlelogInformation(AdKatsSubscribedPlayer aPlayer)
        {
            try
            {
                if (!String.IsNullOrEmpty(aPlayer.player_personaID))
                {
                    return;
                }
                if (String.IsNullOrEmpty(aPlayer.player_name))
                {
                    ConsoleError("Attempted to get battlelog information of nameless player.");
                    return;
                }
                using (var client = new WebClient())
                {
                    try
                    {
                        DoBattlelogWait();
                        String personaResponse = client.DownloadString("http://battlelog.battlefield.com/bf4/user/" + aPlayer.player_name);
                        Match pid = Regex.Match(personaResponse, @"bf4/soldier/" + aPlayer.player_name + @"/stats/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        if (!pid.Success)
                        {
                            HandleException(new AdKatsException("Could not find persona ID for " + aPlayer.player_name));
                            return;
                        }
                        aPlayer.player_personaID = pid.Groups[1].Value.Trim();
                        DebugWrite("Persona ID fetched for " + aPlayer.player_name, 4);
                        QueueForProcessing(new ProcessObject()
                        {
                            process_player = aPlayer,
                            process_source = "listing",
                            process_time = DateTime.UtcNow
                        });
                        DoBattlelogWait();
                        String overviewResponse = client.DownloadString("http://battlelog.battlefield.com/bf4/warsawoverviewpopulate/" + aPlayer.player_personaID + "/1/");

                        Hashtable json = (Hashtable)JSON.JsonDecode(overviewResponse);
                        Hashtable data = (Hashtable)json["data"];
                        Hashtable info = null;
                        if (!data.ContainsKey("viewedPersonaInfo") || (info = (Hashtable)data["viewedPersonaInfo"]) == null)
                        {
                            aPlayer.player_clanTag = String.Empty;
                            DebugWrite("Could not find BF4 clan tag for " + aPlayer.player_name, 4);
                        }
                        else
                        {
                            String tag = String.Empty;
                            if (!info.ContainsKey("tag") || String.IsNullOrEmpty(tag = (String)info["tag"]))
                            {
                                aPlayer.player_clanTag = String.Empty;
                                DebugWrite("Could not find BF4 clan tag for " + aPlayer.player_name, 4);
                            }
                            else
                            {
                                aPlayer.player_clanTag = tag;
                                DebugWrite("Clan tag [" + aPlayer.player_clanTag + "] found for " + aPlayer.player_name, 4);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while fetching battlelog information for " + aPlayer.player_name, e));
            }
        }

        public void ReceiveOnlineSoldiers(params String[] parameters) {
            DebugWrite("ReceiveOnlineSoldiers starting!", 6);
            try {
                if (!_enableAdKatsIntegration) {
                    return;
                }
                if (parameters.Length != 2) {
                    ConsoleError("Online soldier handling canceled. Parameters invalid.");
                    return;
                }
                String source = parameters[0];
                String unparsedResponseJSON = parameters[1];

                var decodedResponse = (Hashtable) JSON.JsonDecode(unparsedResponseJSON);

                var decodedSoldierList = (ArrayList) decodedResponse["response_value"];
                if (decodedSoldierList == null) {
                    ConsoleError("Soldier params could not be properly converted from JSON. Unable to continue.");
                    return;
                }
                lock (_PlayerDictionary) {
                    var validPlayers = new List<String>();
                    foreach (Hashtable soldierHashtable in decodedSoldierList) {
                        var aPlayer = new AdKatsSubscribedPlayer();
                        aPlayer.player_id = Convert.ToInt64((Double) soldierHashtable["player_id"]);
                        aPlayer.player_guid = (String) soldierHashtable["player_guid"];
                        aPlayer.player_pbguid = (String) soldierHashtable["player_pbguid"];
                        aPlayer.player_ip = (String) soldierHashtable["player_ip"];
                        aPlayer.player_name = (String) soldierHashtable["player_name"];
                        aPlayer.player_personaID = (String) soldierHashtable["player_personaID"];
                        aPlayer.player_clanTag = (String) soldierHashtable["player_clanTag"];
                        aPlayer.player_online = (Boolean) soldierHashtable["player_online"];
                        aPlayer.player_aa = (Boolean) soldierHashtable["player_aa"];
                        aPlayer.player_ping = (Double) soldierHashtable["player_ping"];
                        aPlayer.player_reputation = (Double) soldierHashtable["player_reputation"];
                        aPlayer.player_infractionPoints = Convert.ToInt32((Double) soldierHashtable["player_infractionPoints"]);
                        aPlayer.player_role = (String) soldierHashtable["player_role"];
                        aPlayer.player_type = (String) soldierHashtable["player_type"];
                        aPlayer.player_isAdmin = (Boolean) soldierHashtable["player_isAdmin"];
                        aPlayer.player_reported = (Boolean) soldierHashtable["player_reported"];
                        aPlayer.player_punished = (Boolean) soldierHashtable["player_punished"];
                        aPlayer.player_marked = (Boolean) soldierHashtable["player_marked"];
                        var lastPunishment = (Double) soldierHashtable["player_lastPunishment"];
                        if (lastPunishment > 0) {
                            aPlayer.player_lastPunishment = TimeSpan.FromSeconds(lastPunishment);
                        }
                        var lastForgive = (Double) soldierHashtable["player_lastForgive"];
                        if (lastPunishment > 0) {
                            aPlayer.player_lastForgive = TimeSpan.FromSeconds(lastForgive);
                        }
                        var lastAction = (Double) soldierHashtable["player_lastAction"];
                        if (lastPunishment > 0) {
                            aPlayer.player_lastAction = TimeSpan.FromSeconds(lastAction);
                        }
                        aPlayer.player_spawnedOnce = (Boolean) soldierHashtable["player_spawnedOnce"];
                        aPlayer.player_conversationPartner = (String) soldierHashtable["player_conversationPartner"];
                        aPlayer.player_kills = Convert.ToInt32((Double) soldierHashtable["player_kills"]);
                        aPlayer.player_deaths = Convert.ToInt32((Double) soldierHashtable["player_deaths"]);
                        aPlayer.player_kdr = (Double) soldierHashtable["player_kdr"];
                        aPlayer.player_rank = Convert.ToInt32((Double) soldierHashtable["player_rank"]);
                        aPlayer.player_score = Convert.ToInt32((Double) soldierHashtable["player_score"]);
                        aPlayer.player_squad = Convert.ToInt32((Double) soldierHashtable["player_squad"]);
                        aPlayer.player_team = Convert.ToInt32((Double) soldierHashtable["player_team"]);

                        validPlayers.Add(aPlayer.player_name);

                        Boolean process = false;
                        AdKatsSubscribedPlayer dPlayer;
                        Boolean newPlayer = false;
                        //Are they online?
                        if (!_PlayerDictionary.TryGetValue(aPlayer.player_name, out dPlayer))
                        {
                            //Not online. Are they returning?
                            if (!_PlayerLeftDictionary.TryGetValue(aPlayer.player_guid, out dPlayer))
                            {
                                //Not online or returning. New player.
                                newPlayer = true;
                            }
                        }
                        if (newPlayer)
                        {
                            _PlayerDictionary[aPlayer.player_name] = aPlayer;
                            _PlayerLeftDictionary.Remove(aPlayer.player_guid);
                            dPlayer = aPlayer;
                            process = true;
                        }
                        else
                        {
                            dPlayer.player_name = aPlayer.player_name;
                            dPlayer.player_ip = aPlayer.player_ip;
                            dPlayer.player_aa = aPlayer.player_aa;
                            if (String.IsNullOrEmpty(dPlayer.player_personaID) && !String.IsNullOrEmpty(aPlayer.player_personaID))
                            {
                                process = true;
                            }
                            dPlayer.player_personaID = aPlayer.player_personaID;
                            dPlayer.player_clanTag = aPlayer.player_clanTag;
                            dPlayer.player_online = aPlayer.player_online;
                            dPlayer.player_ping = aPlayer.player_ping;
                            dPlayer.player_reputation = aPlayer.player_reputation;
                            dPlayer.player_infractionPoints = aPlayer.player_infractionPoints;
                            dPlayer.player_role = aPlayer.player_role;
                            dPlayer.player_type = aPlayer.player_type;
                            dPlayer.player_isAdmin = aPlayer.player_isAdmin;
                            dPlayer.player_reported = aPlayer.player_reported;
                            dPlayer.player_punished = aPlayer.player_punished;
                            dPlayer.player_marked = aPlayer.player_marked;
                            dPlayer.player_spawnedOnce = aPlayer.player_spawnedOnce;
                            dPlayer.player_conversationPartner = aPlayer.player_conversationPartner;
                            dPlayer.player_kills = aPlayer.player_kills;
                            dPlayer.player_deaths = aPlayer.player_deaths;
                            dPlayer.player_kdr = aPlayer.player_kdr;
                            dPlayer.player_rank = aPlayer.player_rank;
                            dPlayer.player_score = aPlayer.player_score;
                            dPlayer.player_squad = aPlayer.player_squad;
                            dPlayer.player_team = aPlayer.player_team;
                        }
                        if (process) {
                            QueueForProcessing(new ProcessObject()
                            {
                                process_player = dPlayer,
                                process_source = "listing",
                                process_time = DateTime.UtcNow
                            });
                        }
                    }
                    foreach (string playerName in _PlayerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList()) {
                        AdKatsSubscribedPlayer aPlayer;
                        if (_PlayerDictionary.TryGetValue(playerName, out aPlayer)) {
                            _PlayerDictionary.Remove(aPlayer.player_name);
                            _PlayerLeftDictionary[aPlayer.player_guid] = aPlayer;
                        }
                    }
                }
                _firstPlayerListComplete = true;
                _PlayerProcessingWaitHandle.Set();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while receiving online soldiers.", e));
            }
            DebugWrite("ReceiveOnlineSoldiers finished!", 6);
        }

        public void AdminSayMessage(String message) {
            AdminSayMessage(message, true);
        }

        public void AdminSayMessage(String message, Boolean displayProconChat) {
            DebugWrite("Entering adminSay", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    ConsoleError("message null in adminSay");
                    return;
                }
                if (displayProconChat) {
                    ProconChatWrite("Say > " + message);
                }
                string[] lineSplit = message.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
                foreach (String line in lineSplit) {
                    ExecuteCommand("procon.protected.send", "admin.say", line, "all");
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while sending admin say.", e));
            }
            DebugWrite("Exiting adminSay", 7);
        }

        public void PlayerSayMessage(String target, String message) {
            PlayerSayMessage(target, message, true, 1);
        }

        public void PlayerSayMessage(String target, String message, Boolean displayProconChat, Int32 spamCount) {
            DebugWrite("Entering playerSayMessage", 7);
            try {
                if (String.IsNullOrEmpty(target) || String.IsNullOrEmpty(message)) {
                    ConsoleError("target or message null in playerSayMessage");
                    return;
                }
                if (displayProconChat) {
                    ProconChatWrite("Say > " + target + " > " + message);
                }
                for (int count = 0; count < spamCount; count++) {
                    string[] lineSplit = message.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
                    foreach (String line in lineSplit) {
                        ExecuteCommand("procon.protected.send", "admin.say", line, "player", target);
                    }
                    _threadMasterWaitHandle.WaitOne(50);
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while sending message to player.", e));
            }
            DebugWrite("Exiting playerSayMessage", 7);
        }

        public void AdminYellMessage(String message) {
            AdminYellMessage(message, true);
        }

        public void AdminYellMessage(String message, Boolean displayProconChat) {
            DebugWrite("Entering adminYell", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    ConsoleError("message null in adminYell");
                    return;
                }
                if (displayProconChat) {
                    ProconChatWrite("Yell[" + _YellDuration + "s] > " + message);
                }
                ExecuteCommand("procon.protected.send", "admin.yell", message.ToUpper(), _YellDuration + "", "all");
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while sending admin yell.", e));
            }
            DebugWrite("Exiting adminYell", 7);
        }

        public void PlayerYellMessage(String target, String message) {
            PlayerYellMessage(target, message, true, 1);
        }

        public void PlayerYellMessage(String target, String message, Boolean displayProconChat, Int32 spamCount) {
            DebugWrite("Entering PlayerYellMessage", 7);
            try {
                if (String.IsNullOrEmpty(message)) {
                    ConsoleError("message null in PlayerYellMessage");
                    return;
                }
                if (displayProconChat) {
                    ProconChatWrite("Yell[" + _YellDuration + "s] > " + target + " > " + message);
                }
                for (int count = 0; count < spamCount; count++) {
                    ExecuteCommand("procon.protected.send", "admin.yell", ((_gameVersion == GameVersion.BF4) ? (Environment.NewLine) : ("")) + message.ToUpper(), _YellDuration + "", "player", target);
                    _threadMasterWaitHandle.WaitOne(50);
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while sending admin yell.", e));
            }
            DebugWrite("Exiting PlayerYellMessage", 7);
        }

        public void AdminTellMessage(String message) {
            AdminTellMessage(message, true);
        }

        public void AdminTellMessage(String message, Boolean displayProconChat) {
            if (displayProconChat) {
                ProconChatWrite("Tell[" + _YellDuration + "s] > " + message);
            }
            AdminSayMessage(message, false);
            AdminYellMessage(message, false);
        }

        public void PlayerTellMessage(String target, String message) {
            PlayerTellMessage(target, message, true, 1);
        }

        public void PlayerTellMessage(String target, String message, Boolean displayProconChat, Int32 spamCount) {
            if (displayProconChat) {
                ProconChatWrite("Tell[" + _YellDuration + "s] > " + target + " > " + message);
            }
            PlayerSayMessage(target, message, false, spamCount);
            PlayerYellMessage(target, message, false, spamCount);
        }

        public Boolean LoadWarsawLibrary() {
            DebugWrite("Entering LoadWarsawLibrary", 7);
            try {
                Hashtable responseData = null;
                if (_gameVersion == GameVersion.BF4) {
                    var library = new WarsawLibrary();
                    ConsoleInfo("Downloading WARSAW library.");
                    responseData = FetchWarsawLibrary();
                    if (responseData == null) {
                        ConsoleError("WARSAW library fetch failed, unable to generate library.");
                        return false;
                    }
                    if (!responseData.ContainsKey("compact")) {
                        ConsoleError("WARSAW library fetch did not contain 'compact' element, unable to generate library.");
                        return false;
                    }
                    var compact = (Hashtable) responseData["compact"];
                    if (compact == null) {
                        ConsoleError("Compact section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    if (!compact.ContainsKey("weapons")) {
                        ConsoleError("Warsaw compact section did not contain 'weapons' element, unable to generate library.");
                        return false;
                    }
                    if (!compact.ContainsKey("weaponaccessory")) {
                        ConsoleError("Warsaw compact section did not contain 'weaponaccessory' element, unable to generate library.");
                        return false;
                    }
                    if (!compact.ContainsKey("kititems")) {
                        ConsoleError("Warsaw compact section did not contain 'kititems' element, unable to generate library.");
                        return false;
                    }
                    var weapons = (Hashtable) compact["weapons"];
                    if (weapons == null) {
                        ConsoleError("Weapons section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    ConsoleInfo("WARSAW library downloaded. Parsing.");
                    //Pause for effect, nothing else
                    Thread.Sleep(500);

                    var itemDictionary = new Dictionary<String, WarsawItem>();
                    foreach (DictionaryEntry entry in weapons) {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String) entry.Key, out warsawID)) {
                            //Reject the entry
                            //ConsoleError("Rejecting weapon element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        var item = new WarsawItem();
                        item.warsawID = warsawID.ToString();
                        Boolean debug = false;
                        if (false) {
                            debug = true;
                            ConsoleInfo("Loading debug warsaw ID " + item.warsawID);
                        }

                        //Grab the contents
                        var weaponData = (Hashtable) entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("category")) {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        item.category = (String) weaponData["category"];
                        if (String.IsNullOrEmpty(item.category)) {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
                        item.category = item.category.Split('_').Last().Replace('_', ' ').ToUpper();
                        //weapon.category = weapon.category.TrimStart("WARSAW_ID_P_CAT_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("name")) {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        item.name = (String) weaponData["name"];
                        if (String.IsNullOrEmpty(item.name)) {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        item.name = item.name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab categoryType------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("categoryType")) {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'categoryType'.");
                            continue;
                        }
                        item.categoryType = (String)weaponData["categoryType"];
                        if (String.IsNullOrEmpty(item.categoryType))
                        {
                            item.categoryType = "General";
                        }
                        //Parsed categoryType does not require any modifications

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("slug")) {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        item.slug = (String) weaponData["slug"];
                        if (String.IsNullOrEmpty(item.slug)) {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        item.slug = item.slug.Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        itemDictionary[item.warsawID] = item;
                        if (debug)
                            ConsoleSuccess("Weapon " + item.warsawID + " added. " + itemDictionary.ContainsKey(item.warsawID));
                    }

                    var kititems = (Hashtable) compact["kititems"];
                    if (kititems == null) {
                        ConsoleError("Kit items section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    foreach (DictionaryEntry entry in kititems) {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String) entry.Key, out warsawID)) {
                            //Reject the entry
                            //ConsoleError("Rejecting kit item element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        var kitItem = new WarsawItem();
                        kitItem.warsawID = warsawID.ToString();

                        //Grab the contents
                        var weaponAccessoryData = (Hashtable) entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category")) {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        kitItem.category = (String) weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(kitItem.category)) {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
                        kitItem.category = kitItem.category.Split('_').Last().Replace('_', ' ').ToUpper();
                        //kitItem.category = kitItem.category.TrimStart("WARSAW_ID_P_CAT_".ToCharArray()).Replace('_', ' ').ToLower();
                        if (kitItem.category != "GADGET" && kitItem.category != "GRENADE") {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. 'category' not gadget or grenade.");
                            continue;
                        }

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name")) {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        kitItem.name = (String) weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(kitItem.name)) {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        kitItem.name = kitItem.name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug")) {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        kitItem.slug = (String) weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(kitItem.slug)) {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        kitItem.slug = kitItem.slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        if (String.IsNullOrEmpty(kitItem.categoryType))
                        {
                            kitItem.categoryType = "General";
                        }

                        //Assign the item
                        if (!itemDictionary.ContainsKey(kitItem.warsawID))
                        {
                            itemDictionary[kitItem.warsawID] = kitItem;
                        }
                    }
                    //Assign the new built dictionary
                    library.Items = itemDictionary;
                    ConsoleInfo("WARSAW items parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(500);

                    var weaponaccessory = (Hashtable) compact["weaponaccessory"];
                    if (weaponaccessory == null) {
                        ConsoleError("Weapon accessory section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    var weaponAccessoryDictionary = new Dictionary<String, WarsawItemAccessory>();
                    foreach (DictionaryEntry entry in weaponaccessory) {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String) entry.Key, out warsawID)) {
                            //Reject the entry
                            //ConsoleError("Rejecting weapon accessory element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        var itemAccessory = new WarsawItemAccessory();
                        itemAccessory.warsawID = warsawID.ToString();

                        //Grab the contents
                        var weaponAccessoryData = (Hashtable) entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category")) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        itemAccessory.category = (String) weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(itemAccessory.category)) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
                        itemAccessory.category = itemAccessory.category.Split('_').Last().Replace('_', ' ').ToUpper();
                        //weaponAccessory.category = weaponAccessory.category.Substring(15, weaponAccessory.category.Length - 15).Replace('_', ' ').ToLower();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name")) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        itemAccessory.name = (String) weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(itemAccessory.name)) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        itemAccessory.name = itemAccessory.name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug")) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        itemAccessory.slug = (String) weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(itemAccessory.slug)) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        itemAccessory.slug = itemAccessory.slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        weaponAccessoryDictionary[itemAccessory.warsawID] = itemAccessory;
                    }
                    library.ItemAccessories = weaponAccessoryDictionary;
                    ConsoleInfo("WARSAW accessories parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(500);

                    var vehicleunlocks = (Hashtable) compact["vehicleunlocks"];
                    if (vehicleunlocks == null) {
                        ConsoleError("Vehicle unlocks section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    var vehicleUnlockDictionary = new Dictionary<String, WarsawItem>();
                    foreach (DictionaryEntry entry in vehicleunlocks) {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String) entry.Key, out warsawID)) {
                            //Reject the entry
                            //ConsoleError("Rejecting vehicle unlock element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        var vehicleUnlock = new WarsawItem();
                        vehicleUnlock.warsawID = warsawID.ToString();

                        //Grab the contents
                        var vehicleUnlockData = (Hashtable) entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("category")) {
                            //ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        vehicleUnlock.category = (String) vehicleUnlockData["category"];
                        if (String.IsNullOrEmpty(vehicleUnlock.category)) {
                            //ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
                        vehicleUnlock.category = vehicleUnlock.category.Split('_').Last().Replace('_', ' ').ToUpper();
                        //kitItem.category = kitItem.category.TrimStart("WARSAW_ID_P_CAT_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab name------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("name")) {
                            //ConsoleError("Rejecting vehicle unlock'" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        var name = (String) vehicleUnlockData["name"];
                        if (String.IsNullOrEmpty(name)) {
                            //ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        name = name.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab slug------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("slug")) {
                            //ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        vehicleUnlock.slug = (String) vehicleUnlockData["slug"];
                        if (String.IsNullOrEmpty(vehicleUnlock.slug)) {
                            //ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        vehicleUnlock.slug = name + " " + vehicleUnlock.slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').TrimEnd('3').TrimEnd('4').TrimEnd('5').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        vehicleUnlockDictionary[vehicleUnlock.warsawID] = vehicleUnlock;
                    }
                    library.VehicleUnlocks = vehicleUnlockDictionary;
                    ConsoleInfo("WARSAW vehicle unlocks parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(500);

                    _WARSAWLibrary = library;
                    _WARSAWLibraryLoaded = true;
                    UpdateSettingPage();
                    return true;
                }
                ConsoleError("Game not BF4, unable to process WARSAW library.");
                return false;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while parsing WARSAW library.", e));
            }
            DebugWrite("Exiting LoadWarsawLibrary", 7);
            return false;
        }

        private Hashtable FetchWarsawLibrary() {
            Hashtable library = null;
            try {
                using (var client = new WebClient())
                {
                    String response;
                    try
                    {
                        response = client.DownloadString("https://raw.githubusercontent.com/AdKats/AdKats/master/lib/WarsawCodeBook.json");
                    }
                    catch (Exception)
                    {
                        try
                        {
                            response = client.DownloadString("http://api.gamerethos.net/adkats/fetch/warsaw");
                        }
                        catch (Exception e)
                        {
                            HandleException(new AdKatsException("Error while downloading raw WARSAW library.", e));
                            return null;
                        }
                    }
                    library = (Hashtable)JSON.JsonDecode(response);
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Unexpected error while fetching WARSAW library", e));
                return null;
            }
            return library;
        }

        private AdKatsLoadout GetPlayerLoadout(String personaID) {
            DebugWrite("Entering GetPlayerLoadout", 7);
            try {
                Hashtable responseData = null;
                if (_gameVersion == GameVersion.BF4) {
                    var loadout = new AdKatsLoadout();
                    responseData = FetchPlayerLoadout(personaID);
                    if (responseData == null) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Loadout fetch failed, unable to parse player loadout.");
                        return null;
                    }
                    if (!responseData.ContainsKey("data")) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Loadout fetch did not contain 'data' element, unable to parse player loadout.");
                        return null;
                    }
                    var data = (Hashtable) responseData["data"];
                    if (data == null) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Data section of loadout failed parse, unable to parse player loadout.");
                        return null;
                    }
                    //Get parsed back persona ID
                    if (!data.ContainsKey("personaId")) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Data section of loadout did not contain 'personaId' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.PersonaID = data["personaId"].ToString();
                    //Get persona name
                    if (!data.ContainsKey("personaName")) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Data section of loadout did not contain 'personaName' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.Name = data["personaName"].ToString();
                    //Get weapons and their attachements
                    if (!data.ContainsKey("currentLoadout")) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Data section of loadout did not contain 'currentLoadout' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutHashtable = (Hashtable) data["currentLoadout"];
                    if (currentLoadoutHashtable == null) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Current loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    if (!currentLoadoutHashtable.ContainsKey("weapons")) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Current loadout section did not contain 'weapons' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutWeapons = (Hashtable) currentLoadoutHashtable["weapons"];
                    if (currentLoadoutWeapons == null) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Weapon loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    foreach (DictionaryEntry weaponEntry in currentLoadoutWeapons) {
                        if (weaponEntry.Key.ToString() != "0") {
                            WarsawItem warsawItem;
                            if (_WARSAWLibrary.Items.TryGetValue(weaponEntry.Key.ToString(), out warsawItem)) {
                                //Create new instance of the weapon for this player
                                var loadoutItem = new WarsawItem() {
                                    warsawID = warsawItem.warsawID,
                                    category = warsawItem.category,
                                    categoryType = warsawItem.categoryType,
                                    name = warsawItem.name,
                                    slug = warsawItem.slug
                                };
                                foreach (String accessoryID in (ArrayList) weaponEntry.Value) {
                                    if (accessoryID != "0") {
                                        WarsawItemAccessory warsawItemAccessory;
                                        if (_WARSAWLibrary.ItemAccessories.TryGetValue(accessoryID, out warsawItemAccessory)) {
                                            loadoutItem.Accessories[warsawItemAccessory.warsawID] = warsawItemAccessory;
                                        }
                                    }
                                }
                                loadout.LoadoutItems[loadoutItem.warsawID] = loadoutItem;
                            }
                        }
                    }
                    if (!currentLoadoutHashtable.ContainsKey("selectedKit")) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Current loadout section did not contain 'selectedKit' element, unable to parse player loadout.");
                        return null;
                    }
                    String selectedKit = currentLoadoutHashtable["selectedKit"].ToString();
                    ArrayList currentLoadoutList;
                    String loadoutPrimaryID, loadoutSidearmID, loadoutGadget1ID, loadoutGadget2ID, loadoutGrenadeID, loadoutKnifeID;
                    Int32 addedKitItems = 0;
                    switch (selectedKit) {
                        case "0":
                            loadout.SelectedKitType = AdKatsLoadout.KitType.Assault;
                            currentLoadoutList = (ArrayList) ((ArrayList) currentLoadoutHashtable["kits"])[0];
                            break;
                        case "1":
                            loadout.SelectedKitType = AdKatsLoadout.KitType.Engineer;
                            currentLoadoutList = (ArrayList) ((ArrayList) currentLoadoutHashtable["kits"])[1];
                            break;
                        case "2":
                            loadout.SelectedKitType = AdKatsLoadout.KitType.Support;
                            currentLoadoutList = (ArrayList) ((ArrayList) currentLoadoutHashtable["kits"])[2];
                            break;
                        case "3":
                            loadout.SelectedKitType = AdKatsLoadout.KitType.Recon;
                            currentLoadoutList = (ArrayList) ((ArrayList) currentLoadoutHashtable["kits"])[3];
                            break;
                        default:
                            if (_displayLoadoutDebug)
                                ConsoleError("Unable to parse selected kit " + selectedKit + ", value is unknown. Unable to parse player loadout.");
                            return null;
                    }
                    if (currentLoadoutList.Count < 6) {
                        if (_displayLoadoutDebug)
                            ConsoleError("Loadout kit item entry did not contain 6 valid entries. Unable to parse player loadout.");
                        return null;
                    }
                    //Pull the specifics
                    loadoutPrimaryID = currentLoadoutList[0].ToString();
                    String defaultAssaultPrimary = "3590299697"; //ak-12
                    String defaultEngineerPrimary = "2021343793"; //mx4
                    String defaultSupportPrimary = "3179658801"; //u-100-mk5
                    String defaultReconPrimary = "3458855537"; //cs-lr4

                    loadoutSidearmID = currentLoadoutList[1].ToString();
                    String defaultSidearm = "944904529"; //p226

                    loadoutGadget1ID = currentLoadoutList[2].ToString();
                    String defaultGadget1 = "1694579111"; //nogadget1

                    loadoutGadget2ID = currentLoadoutList[3].ToString();
                    String defaultGadget2 = "3260690101"; //nogadget2

                    loadoutGrenadeID = currentLoadoutList[4].ToString();
                    String defaultGrenade = "2670747868"; //m67-frag

                    loadoutKnifeID = currentLoadoutList[5].ToString();
                    String defaultKnife = "3214146841"; //bayonett

                    //PRIMARY
                    WarsawItem loadoutPrimary;
                    String specificDefault;
                    switch (loadout.SelectedKitType) {
                        case AdKatsLoadout.KitType.Assault:
                            specificDefault = defaultAssaultPrimary;
                            break;
                        case AdKatsLoadout.KitType.Engineer:
                            specificDefault = defaultEngineerPrimary;
                            break;
                        case AdKatsLoadout.KitType.Support:
                            specificDefault = defaultSupportPrimary;
                            break;
                        case AdKatsLoadout.KitType.Recon:
                            specificDefault = defaultReconPrimary;
                            break;
                        default:
                            if (_displayLoadoutDebug)
                                ConsoleError("Specific kit type not set while assigning primary weapon default. Unable to parse player loadout.");
                            return null;
                    }
                    if (!loadout.LoadoutItems.TryGetValue(loadoutPrimaryID, out loadoutPrimary)) {
                        if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutPrimary.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    switch (loadout.SelectedKitType) {
                        case AdKatsLoadout.KitType.Assault:
                            if (loadoutPrimary.category != "ASSAULTRIFLE" && loadoutPrimary.category != "CARBINE" && loadoutPrimary.category != "SHOTGUN" && loadoutPrimary.category != "DMR") {
                                WarsawItem originalItem = loadoutPrimary;
                                if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary)) {
                                    if (_displayLoadoutDebug)
                                        ConsoleWarn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " was not valid for Assault kit. Defaulting to " + loadoutPrimary.slug + ".");
                                }
                                else {
                                    if (_displayLoadoutDebug)
                                        ConsoleError("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for Assault " + loadout.Name + ". Unable to parse player loadout.");
                                    return null;
                                }
                            }
                            break;
                        case AdKatsLoadout.KitType.Engineer:
                            if (loadoutPrimary.category != "PDW" && loadoutPrimary.category != "CARBINE" && loadoutPrimary.category != "SHOTGUN" && loadoutPrimary.category != "DMR") {
                                WarsawItem originalItem = loadoutPrimary;
                                if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary)) {
                                    if (_displayLoadoutDebug)
                                        ConsoleWarn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " was not valid for Engineer kit. Defaulting to " + loadoutPrimary.slug + ".");
                                }
                                else {
                                    if (_displayLoadoutDebug)
                                        ConsoleError("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for Engineer " + loadout.Name + ". Unable to parse player loadout.");
                                    return null;
                                }
                            }
                            break;
                        case AdKatsLoadout.KitType.Support:
                            if (loadoutPrimary.category != "LMG" && loadoutPrimary.category != "CARBINE" && loadoutPrimary.category != "SHOTGUN" && loadoutPrimary.category != "DMR") {
                                WarsawItem originalItem = loadoutPrimary;
                                if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary)) {
                                    if (_displayLoadoutDebug)
                                        ConsoleWarn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " was not valid for Support kit. Defaulting to " + loadoutPrimary.slug + ".");
                                }
                                else {
                                    if (_displayLoadoutDebug)
                                        ConsoleError("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for Support " + loadout.Name + ". Unable to parse player loadout.");
                                    return null;
                                }
                            }
                            break;
                        case AdKatsLoadout.KitType.Recon:
                            if (loadoutPrimary.category != "SNIPER" && loadoutPrimary.category != "CARBINE" && loadoutPrimary.category != "SHOTGUN" && loadoutPrimary.category != "DMR") {
                                WarsawItem originalItem = loadoutPrimary;
                                if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary)) {
                                    if (_displayLoadoutDebug)
                                        ConsoleWarn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " was not valid for Recon kit. Defaulting to " + loadoutPrimary.slug + ".");
                                }
                                else {
                                    if (_displayLoadoutDebug)
                                        ConsoleError("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for Recon " + loadout.Name + ". Unable to parse player loadout.");
                                    return null;
                                }
                            }
                            break;
                        default:
                            if (_displayLoadoutDebug)
                                ConsoleError("Specific kit type not set while confirming primary weapon type. Unable to parse player loadout.");
                            return null;
                    }
                    loadout.KitItemPrimary = loadoutPrimary;

                    //SIDEARM
                    WarsawItem loadoutSidearm;
                    if (!loadout.LoadoutItems.TryGetValue(loadoutSidearmID, out loadoutSidearm)) {
                        if (loadout.LoadoutItems.TryGetValue(defaultSidearm, out loadoutSidearm)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific SIDEARM (" + loadoutSidearmID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutSidearm.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid SIDEARM (" + loadoutSidearmID + "->" + defaultSidearm + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    if (loadoutSidearm.category != "SIDEARM") {
                        WarsawItem originalItem = loadoutSidearm;
                        if (loadout.LoadoutItems.TryGetValue(defaultSidearm, out loadoutSidearm)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific SIDEARM (" + loadoutSidearmID + ") " + originalItem.slug + " for " + loadout.Name + " was not a SIDEARM. Defaulting to " + loadoutSidearm.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid SIDEARM (" + loadoutSidearmID + "->" + defaultSidearm + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitItemSidearm = loadoutSidearm;

                    //GADGET1
                    WarsawItem loadoutGadget1;
                    if (!_WARSAWLibrary.Items.TryGetValue(loadoutGadget1ID, out loadoutGadget1)) {
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGadget1, out loadoutGadget1)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GADGET1 (" + loadoutGadget1ID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGadget1.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GADGET1 (" + loadoutGadget1ID + "->" + defaultGadget1 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    if (loadoutGadget1.category != "GADGET") {
                        WarsawItem originalItem = loadoutGadget1;
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGadget1, out loadoutGadget1)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GADGET1 (" + loadoutGadget1ID + ") " + originalItem.slug + " for " + loadout.Name + " was not a GADGET. Defaulting to " + loadoutGadget1.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GADGET1 (" + loadoutGadget1ID + "->" + defaultGadget1 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGadget1 = loadoutGadget1;

                    //GADGET2
                    WarsawItem loadoutGadget2;
                    if (!_WARSAWLibrary.Items.TryGetValue(loadoutGadget2ID, out loadoutGadget2)) {
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGadget2, out loadoutGadget2)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GADGET2 (" + loadoutGadget2ID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGadget2.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GADGET2 (" + loadoutGadget2ID + "->" + defaultGadget2 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    if (loadoutGadget2.category != "GADGET") {
                        WarsawItem originalItem = loadoutGadget2;
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGadget2, out loadoutGadget2)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GADGET2 (" + loadoutGadget2ID + ") " + originalItem.slug + " for " + loadout.Name + " was not a GADGET. Defaulting to " + loadoutGadget2.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GADGET2 (" + loadoutGadget2ID + "->" + defaultGadget2 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGadget2 = loadoutGadget2;

                    //GRENADE
                    WarsawItem loadoutGrenade;
                    if (!_WARSAWLibrary.Items.TryGetValue(loadoutGrenadeID, out loadoutGrenade)) {
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGrenade, out loadoutGrenade)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GRENADE (" + loadoutGrenadeID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGrenade.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GRENADE (" + loadoutGrenadeID + "->" + defaultGrenade + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    if (loadoutGrenade.category != "GRENADE") {
                        WarsawItem originalItem = loadoutGrenade;
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGrenade, out loadoutGrenade)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GRENADE (" + loadoutGrenadeID + ") " + originalItem.slug + " for " + loadout.Name + " was not a GRENADE. Defaulting to " + loadoutGrenade.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GRENADE (" + loadoutGrenadeID + "->" + defaultGrenade + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGrenade = loadoutGrenade;

                    //KNIFE
                    WarsawItem loadoutKnife;
                    if (!_WARSAWLibrary.Items.TryGetValue(loadoutKnifeID, out loadoutKnife)) {
                        if (_WARSAWLibrary.Items.TryGetValue(defaultKnife, out loadoutKnife)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific KNIFE (" + loadoutKnifeID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutKnife.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid KNIFE (" + loadoutKnifeID + "->" + defaultKnife + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    if (loadoutKnife.category != "KNIFE") {
                        WarsawItem originalItem = loadoutKnife;
                        if (_WARSAWLibrary.Items.TryGetValue(defaultKnife, out loadoutKnife)) {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific KNIFE (" + loadoutKnifeID + ") " + originalItem.slug + " for " + loadout.Name + " was not a KNIFE. Defaulting to " + loadoutKnife.slug + ".");
                        }
                        else {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid KNIFE (" + loadoutKnifeID + "->" + defaultKnife + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitKnife = loadoutKnife;

                    //Fill the kit ID listings
                    if (!loadout.AllKitItemIDs.Contains(loadoutPrimary.warsawID)) {
                        loadout.AllKitItemIDs.Add(loadoutPrimary.warsawID);
                    }
                    foreach (WarsawItemAccessory accessory in loadoutPrimary.Accessories.Values) {
                        if (!loadout.AllKitItemIDs.Contains(accessory.warsawID)) {
                            loadout.AllKitItemIDs.Add(accessory.warsawID);
                        }
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutSidearm.warsawID)) {
                        loadout.AllKitItemIDs.Add(loadoutSidearm.warsawID);
                    }
                    foreach (WarsawItemAccessory accessory in loadoutSidearm.Accessories.Values) {
                        if (!loadout.AllKitItemIDs.Contains(accessory.warsawID)) {
                            loadout.AllKitItemIDs.Add(accessory.warsawID);
                        }
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutGadget1.warsawID)) {
                        loadout.AllKitItemIDs.Add(loadoutGadget1.warsawID);
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutGadget2.warsawID)) {
                        loadout.AllKitItemIDs.Add(loadoutGadget2.warsawID);
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutGrenade.warsawID)) {
                        loadout.AllKitItemIDs.Add(loadoutGrenade.warsawID);
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutKnife.warsawID)) {
                        loadout.AllKitItemIDs.Add(loadoutKnife.warsawID);
                    }
                    return loadout;
                }
                ConsoleError("Game not BF4, unable to process player loadout.");
                return null;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while parsing player loadout.", e));
            }
            DebugWrite("Exiting GetPlayerLoadout", 7);
            return null;
        }

        private Hashtable FetchPlayerLoadout(String personaID) {
            Hashtable loadout = null;
            try {
                using (var client = new WebClient()) {
                    try {
                        DoBattlelogWait();
                        String response = client.DownloadString("http://battlelog.battlefield.com/bf4/loadout/get/PLAYER/" + personaID + "/1/");
                        loadout = (Hashtable) JSON.JsonDecode(response);
                    }
                    catch (Exception e) {
                        HandleException(new AdKatsException("Error while loading player loadout.", e));
                    }
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Unexpected error while fetching player loadout.", e));
                return null;
            }
            return loadout;
        }

        public String ExtractString(String s, String tag) {
            if (String.IsNullOrEmpty(s) || String.IsNullOrEmpty(tag)) {
                ConsoleError("Unable to extract String. Invalid inputs.");
                return null;
            }
            String startTag = "<" + tag + ">";
            Int32 startIndex = s.IndexOf(startTag, StringComparison.Ordinal) + startTag.Length;
            if (startIndex == -1) {
                ConsoleError("Unable to extract String. Tag not found.");
            }
            Int32 endIndex = s.IndexOf("</" + tag + ">", startIndex, StringComparison.Ordinal);
            return s.Substring(startIndex, endIndex - startIndex);
        }

        public Boolean SoldierNameValid(String input)
        {
            try
            {
                DebugWrite("Checking player '" + input + "' for validity.", 7);
                if (String.IsNullOrEmpty(input))
                {
                    DebugWrite("Soldier Name empty or null.", 5);
                    return false;
                }
                if (input.Length > 16)
                {
                    DebugWrite("Soldier Name '" + input + "' too long, maximum length is 16 characters.", 5);
                    return false;
                }
                if (new Regex("[^a-zA-Z0-9_-]").Replace(input, "").Length != input.Length)
                {
                    DebugWrite("Soldier Name '" + input + "' contained invalid characters.", 5);
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                //Soldier id caused exception in the regex, definitely not valid
                ConsoleError("Soldier Name '" + input + "' contained invalid characters.");
                return false;
            }
        }

        public String FormatTimeString(TimeSpan timeSpan, Int32 maxComponents) {
            DebugWrite("Entering formatTimeString", 7);
            String timeString = null;
            if (maxComponents < 1) {
                return timeString;
            }
            try {
                String formattedTime = (timeSpan.TotalMilliseconds >= 0) ? ("") : ("-");

                Double secondSubset = Math.Abs(timeSpan.TotalSeconds);
                if (secondSubset < 1) {
                    return "0s";
                }
                Double minuteSubset = (secondSubset / 60);
                Double hourSubset = (minuteSubset / 60);
                Double daySubset = (hourSubset / 24);
                Double weekSubset = (daySubset / 7);
                Double monthSubset = (weekSubset / 4);
                Double yearSubset = (monthSubset / 12);

                var years = (Int32) yearSubset;
                Int32 months = (Int32) monthSubset % 12;
                Int32 weeks = (Int32) weekSubset % 4;
                Int32 days = (Int32) daySubset % 7;
                Int32 hours = (Int32) hourSubset % 24;
                Int32 minutes = (Int32) minuteSubset % 60;
                Int32 seconds = (Int32) secondSubset % 60;

                Int32 usedComponents = 0;
                if (years > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += years + "y";
                }
                if (months > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += months + "M";
                }
                if (weeks > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += weeks + "w";
                }
                if (days > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += days + "d";
                }
                if (hours > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += hours + "h";
                }
                if (minutes > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += minutes + "m";
                }
                if (seconds > 0 && usedComponents < maxComponents) {
                    usedComponents++;
                    formattedTime += seconds + "s";
                }
                timeString = formattedTime;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while formatting time String.", e));
            }
            if (String.IsNullOrEmpty(timeString)) {
                timeString = "0s";
            }
            DebugWrite("Exiting formatTimeString", 7);
            return timeString;
        }

        public String FormatMessage(String msg, ConsoleMessageType type) {
            String prefix = "[^bAdKatsLRT^n] ";
            switch (type) {
                case ConsoleMessageType.Info:
                    prefix += "^1^bINFO^0^n: ";
                    break;
                case ConsoleMessageType.Warning:
                    prefix += "^1^bWARNING^0^n: ";
                    break;
                case ConsoleMessageType.Error:
                    prefix += "^1^bERROR^0^n: ";
                    break;
                case ConsoleMessageType.Success:
                    prefix += "^b^2SUCCESS^n^0: ";
                    break;
                case ConsoleMessageType.Exception:
                    prefix += "^1^bEXCEPTION^0^n: ";
                    break;
            }
            return prefix + msg;
        }

        public String BoldMessage(String msg) {
            return "^b" + msg + "^n";
        }

        public String ItalicMessage(String msg) {
            return "^i" + msg + "^n";
        }

        public String ColorMessageMaroon(String msg) {
            return "^1" + msg + "^0";
        }

        public String ColorMessageGreen(String msg) {
            return "^2" + msg + "^0";
        }

        public String ColorMessageOrange(String msg) {
            return "^3" + msg + "^0";
        }

        public String ColorMessageBlue(String msg) {
            return "^4" + msg + "^0";
        }

        public String ColorMessageBlueLight(String msg) {
            return "^5" + msg + "^0";
        }

        public String ColorMessageViolet(String msg) {
            return "^6" + msg + "^0";
        }

        public String ColorMessagePink(String msg) {
            return "^7" + msg + "^0";
        }

        public String ColorMessageRed(String msg) {
            return "^8" + msg + "^0";
        }

        public String ColorMessageGrey(String msg) {
            return "^9" + msg + "^0";
        }

        protected void LogThreadExit() {
            lock (_aliveThreads) {
                _aliveThreads.Remove(Thread.CurrentThread.ManagedThreadId);
            }
        }

        protected void StartAndLogThread(Thread aThread) {
            aThread.Start();
            lock (_aliveThreads) {
                if (!_aliveThreads.ContainsKey(aThread.ManagedThreadId)) {
                    _aliveThreads.Add(aThread.ManagedThreadId, aThread);
                    _threadMasterWaitHandle.WaitOne(100);
                }
            }
        }

        public Boolean AdminsOnline() {
            return _PlayerDictionary.Values.Any(aPlayer => aPlayer.player_isAdmin);
        }

        public Boolean OnlineAdminSayMessage(String message)
        {
            ProconChatWrite(ColorMessageMaroon(BoldMessage(message)));
            Boolean adminsTold = false;
            foreach (var aPlayer in _PlayerDictionary.Values.Where(aPlayer => aPlayer.player_isAdmin))
            {
                adminsTold = true;
                PlayerSayMessage(aPlayer.player_name, message, true, 1);
            }
            return adminsTold;
        }

        public void ProconChatWrite(String msg) {
            msg = msg.Replace(Environment.NewLine, String.Empty);
            ExecuteCommand("procon.protected.chat.write", "AdKatsLRT > " + msg);
            if (_slowmo) {
                _threadMasterWaitHandle.WaitOne(1000);
            }
        }

        public void ConsoleWrite(String msg, ConsoleMessageType type) {
            ExecuteCommand("procon.protected.pluginconsole.write", FormatMessage(msg, type));
            if (_slowmo) {
                _threadMasterWaitHandle.WaitOne(1000);
            }
        }

        public void ConsoleWrite(String msg) {
            ConsoleWrite(msg, ConsoleMessageType.Normal);
        }

        public void ConsoleInfo(String msg) {
            ConsoleWrite(msg, ConsoleMessageType.Info);
        }

        public void ConsoleWarn(String msg) {
            ConsoleWrite(msg, ConsoleMessageType.Warning);
        }

        public void ConsoleError(String msg) {
            ConsoleWrite(msg, ConsoleMessageType.Error);
        }

        public void ConsoleSuccess(String msg) {
            ConsoleWrite(msg, ConsoleMessageType.Success);
        }

        public void DebugWrite(String msg, Int32 level) {
            if (_debugLevel >= level) {
                ConsoleWrite(msg, ConsoleMessageType.Normal);
            }
        }

        public DateTime DateTimeFromEpochSeconds(Double epochSeconds) {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(epochSeconds);
        }

        public void UpdateSettingPage() {
            SetExternalPluginSetting("AdKatsLRT", "UpdateSettings", "Update");
        }

        public void SetExternalPluginSetting(String pluginName, String settingName, String settingValue) {
            if (String.IsNullOrEmpty(pluginName) || String.IsNullOrEmpty(settingName) || settingValue == null) {
                ConsoleError("Required inputs null or empty in setExternalPluginSetting");
                return;
            }
            ExecuteCommand("procon.protected.plugins.setVariable", pluginName, settingName, settingValue);
        }

        public string TrimStart(string target, string trimString)
        {
            string result = target;
            while (result.StartsWith(trimString))
            {
                result = result.Substring(trimString.Length);
            }

            return result;
        }

        private void DoBattlelogWait() {
            //Wait 2 seconds between battlelog actions
            if ((DateTime.UtcNow - _LastBattlelogAction) < _BattlelogWaitDuration) {
                Thread.Sleep(_BattlelogWaitDuration - (DateTime.UtcNow - _LastBattlelogAction));
            }
            _LastBattlelogAction = DateTime.UtcNow;
        }

        private void PostVersionTracking()
        {
            if (String.IsNullOrEmpty(_serverInfo.ServerIP))
            {
                return;
            }
            try {
                using (var client = new WebClient())
                {
                    String server_ip = _serverInfo.ServerIP;
                    String server_name = _serverInfo.ServerName;
                    String adkatslrt_version_current = PluginVersion;
                    String adkatslrt_enabled = _pluginEnabled.ToString().ToLower();
                    String adkatslrt_uptime = (_threadsReady) ? (Math.Round((DateTime.UtcNow - _StartTime).TotalSeconds).ToString()) : ("0");
                    String updates_disabled = false.ToString().ToLower();
                    var data = new NameValueCollection {
                        {"server_ip", server_ip},
                        {"server_name", server_name},
                        {"adkatslrt_version_current", adkatslrt_version_current},
                        {"adkatslrt_enabled", adkatslrt_enabled},
                        {"adkatslrt_uptime", adkatslrt_uptime},
                        {"updates_disabled", updates_disabled}
                    };
                    byte[] response = client.UploadValues("http://api.gamerethos.net/adkats/lrt/usage", data);
                    if (_isTestingAuthorized)
                    {
                        ConsoleSuccess("Version Tracking: '" + adkatslrt_version_current + "' - '" + adkatslrt_enabled + "' - '" + adkatslrt_uptime + "' - '" + updates_disabled);
                    }
                }
            }
            catch (Exception e) {
                //Ignore errors
            }
            _LastVersionTrackingUpdate = DateTime.UtcNow;
        }

        public AdKatsException HandleException(AdKatsException aException) {
            try {
                //If it's null or AdKatsLRT isn't enabled, just return
                if (aException == null) {
                    ConsoleError("Attempted to handle exception when none was given.");
                    return null;
                }
                _slowmo = SlowMoOnException;
                String prefix = "Line ";
                if (aException.InternalException != null) {
                    Int64 impericalLineNumber = (new StackTrace(aException.InternalException, true)).GetFrame(0).GetFileLineNumber();
                    Int64 parsedLineNumber = 0;
                    Int64.TryParse(aException.InternalException.ToString().Split(' ').Last(), out parsedLineNumber);
                    if (impericalLineNumber != 0) {
                        prefix += impericalLineNumber;
                    }
                    else if (parsedLineNumber != 0) {
                        prefix += parsedLineNumber;
                    }
                    else {
                        prefix += "Unknown";
                    }
                    prefix += ": ";
                }
                //Check if the exception attributes to the database
                ConsoleWrite(prefix + aException, ConsoleMessageType.Exception);
                return aException;
            }
            catch (Exception e) {
                ConsoleWrite(e.ToString(), ConsoleMessageType.Exception);
            }
            return null;
        }

        public class AdKatsException {
            public Exception InternalException = null;
            public String Message = String.Empty;
            public String Method = String.Empty;
            //Param Constructors
            public AdKatsException(String message, Exception internalException) {
                Method = new StackFrame(1).GetMethod().Name;
                Message = message;
                InternalException = internalException;
            }

            public AdKatsException(String message) {
                Method = new StackFrame(1).GetMethod().Name;
                Message = message;
            }

            //Override toString
            public override String ToString() {
                return "[" + Method + "][" + Message + "]" + ((InternalException != null) ? (": " + InternalException) : (""));
            }
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
            private DateTime infoObjectTime = DateTime.UtcNow;

            private AdKatsLRT Plugin;

            public AdKatsServer(AdKatsLRT plugin)
            {
                Plugin = plugin;
            }

            public void SetInfoObject(CServerInfo infoObject)
            {
                InfoObject = infoObject;
                ServerName = infoObject.ServerName;
                infoObjectTime = DateTime.UtcNow;
            }

            public TimeSpan GetRoundElapsedTime()
            {
                if (InfoObject == null)
                {
                    return TimeSpan.Zero;
                }
                return TimeSpan.FromSeconds(InfoObject.RoundTime) + (DateTime.UtcNow - infoObjectTime);
            }
        }

        public class AdKatsLoadout {
            public enum KitType {
                Assault,
                Engineer,
                Support,
                Recon
            }

            public HashSet<String> AllKitItemIDs;

            public WarsawItem KitGadget1;
            public WarsawItem KitGadget2;
            public WarsawItem KitGrenade;
            public WarsawItem KitItemPrimary;
            public WarsawItem KitItemSidearm;
            public WarsawItem KitKnife;
            public Dictionary<String, WarsawItem> LoadoutItems;
            public String Name;
            public String PersonaID;
            public KitType SelectedKitType;

            public AdKatsLoadout() {
                LoadoutItems = new Dictionary<String, WarsawItem>();
                AllKitItemIDs = new HashSet<String>();
            }
        }

        public class ProcessObject {
            public String process_source;
            public DateTime process_time;
            public AdKatsSubscribedPlayer process_player;
        }

        public class AdKatsSubscribedPlayer {
            public Boolean player_aa;
            public String player_conversationPartner;
            public Int32 player_deaths;
            public String player_guid;
            public Int64 player_id;
            public Int32 player_infractionPoints;
            public String player_ip;
            public Boolean player_isAdmin;
            public Double player_kdr;
            public Int32 player_kills;
            public TimeSpan player_lastAction = TimeSpan.Zero;
            public TimeSpan player_lastForgive = TimeSpan.Zero;
            public TimeSpan player_lastPunishment = TimeSpan.Zero;
            public Boolean player_loadoutEnforced = false;
            public Boolean player_loadoutKilled = false;
            public Boolean player_loadoutValid = true;
            public Boolean player_marked;
            public String player_name;
            public Boolean player_online;
            public String player_pbguid;
            public String player_personaID;
            public String player_clanTag;
            public Double player_ping;
            public Boolean player_punished;
            public Int32 player_rank;
            public Boolean player_reported;
            public Double player_reputation;
            public String player_role;
            public Int32 player_score;
            public Boolean player_spawnedOnce;
            public Int32 player_squad;
            public Int32 player_team;
            public String player_type;

            public AdKatsLoadout Loadout;

            public String GetVerboseName()
            {
                return ((String.IsNullOrEmpty(player_clanTag)) ? ("") : ("[" + player_clanTag + "]")) + player_name;
            }
        }

        internal enum SupportedGames {
            BF_3,
            BF_4
        }

        public class WarsawItem {
            //only take entries with numeric IDs
            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
            //Parsed categoryType does not make any modifications
            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
            //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
            //If expansion exists assign it, if not, ignore
            public Dictionary<String, WarsawItemAccessory> Accessories;
            public String category;
            public String categoryType;
            public String desc;
            public String name;
            public String slug;
            public String warsawID;

            public WarsawItem() {
                Accessories = new Dictionary<string, WarsawItemAccessory>();
            }
        }

        public class WarsawItemAccessory {
            //only take entries with numeric IDs
            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
            //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
            //If expansion exists assign it, if not, ignore
            public String category;
            public String name;
            public String slug;
            public String slugReadable;
            public String warsawID;
        }

        public class WarsawLibrary {
            public Dictionary<String, WarsawItemAccessory> ItemAccessories;
            public Dictionary<String, WarsawItem> Items;
            public Dictionary<String, WarsawItem> VehicleUnlocks;

            public WarsawLibrary() {
                Items = new Dictionary<String, WarsawItem>();
                VehicleUnlocks = new Dictionary<String, WarsawItem>();
                ItemAccessories = new Dictionary<String, WarsawItemAccessory>();
            }
        }
    }
}
