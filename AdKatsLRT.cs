/* 
 * AdKatsLRT - Loadout Restriction Tool extention for AdKats.
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
 * Version 1.0.0.0
 * 18-NOV-2014
 * 
 * Automatic Update Information
 * <version_code>1.0.0.0</version_code>
 */

using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using System.CodeDom.Compiler;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Players;
using Microsoft.CSharp;
using MySql.Data.MySqlClient;

namespace PRoConEvents {
    public class AdKatsLRT : PRoConPluginAPI, IPRoConPluginInterface {
        //Current Plugin Version
        private const String PluginVersion = "1.0.0.0";

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

        //Metabans Ref
        internal enum SupportedGames
        {
            BF_3,
            BF_4
        }

        public enum RoundState {
            Loaded,
            Playing,
            Ended
        }

        public enum PopulationState {
            Unknown,
            Low,
            Medium,
            High,
        }

        public enum PlayerType {
            Player,
            Spectator,
            CommanderPC,
            CommanderMobile
        }

        public enum VersionStatus {
            OutdatedBuild,
            StableBuild,
            TestBuild,
            UnknownBuild,
            UnfetchedBuild
        }

        public enum MessageType {
            Say,
            Yell,
            Tell
        }
        
        //State
        private const Boolean FullDebug = false;
        private const Boolean SlowMoOnException = false;
        private Boolean _slowmo;
        private volatile String _pluginChangelog;
        private volatile String _pluginDescription;
        private volatile String _pluginLinks;
        private volatile Boolean _pluginEnabled;
        private volatile Boolean _threadsReady;
        private volatile String _latestPluginVersion;
        private volatile Int32 _latestPluginVersionInt;
        private volatile Int32 _currentPluginVersionInt;
        private volatile String _pluginVersionStatusString;
        private volatile VersionStatus _pluginVersionStatus = VersionStatus.UnfetchedBuild;
        private volatile Boolean _pluginUpdateServerInfoChecked;
        private volatile Boolean _pluginUpdatePatched;
        private volatile String _pluginPatchedVersion;
        private volatile Int32 _pluginPatchedVersionInt;
        private volatile String _pluginUpdateProgress = "NotStarted";
        private volatile String _pluginDescFetchProgress = "NotStarted";
        private volatile Boolean _fetchedPluginInformation;
        private DateTime _LastPluginDescFetch = DateTime.UtcNow;
        private readonly Dictionary<Int32, Thread> _aliveThreads = new Dictionary<Int32, Thread>();
        private Boolean _firstPlayerListComplete;
        private GameVersion _gameVersion = GameVersion.BF3;

        //Messaging
        private Int32 _YellDuration = 5;

        //Debug
        private volatile Int32 _debugLevel;
        private String _debugSoldierName = "ColColonCleaner";
        private Boolean _toldCol;

        //Timing
        private readonly DateTime _proconStartTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _StartTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _LastBattlelogAction = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private TimeSpan _BattlelogWaitDuration = TimeSpan.FromSeconds(1);

        //Threads
        private Thread _Activator;
        private Thread _Finalizer;
        private Thread _SpawnProcessingThread;

        //Threading queues
        private readonly Queue<AdKatsSubscribedPlayer> _LoadoutProcessingQueue = new Queue<AdKatsSubscribedPlayer>();

        //Threading wait handles
        private EventWaitHandle _threadMasterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _LoadoutProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PlayerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        //Players
        private readonly Dictionary<String, AdKatsSubscribedPlayer> _PlayerDictionary = new Dictionary<String, AdKatsSubscribedPlayer>(); 
        private readonly Dictionary<Int64, AdKatsSubscribedPlayer> _PlayerLeftDictionary = new Dictionary<Int64, AdKatsSubscribedPlayer>(); 

        //WARSAW
        private Boolean _WARSAWLibraryLoaded;
        private WarsawLibrary _WARSAWLibrary = new WarsawLibrary();
        private Dictionary<String, String> _WARSAWDeniedIDMessages = new Dictionary<String, String>();

        public AdKatsLRT() {
            //Set defaults for webclient
            System.Net.ServicePointManager.Expect100Continue = false;

            //By default plugin is not enabled or ready
            _pluginEnabled = false;
            _threadsReady = false;
            
            //Debug level is 0 by default
            _debugLevel = 0;

            //Fetch the plugin description and changelog
            FetchPluginDocumentation();

            //Prepare the keep-alive
            SetupStatusMonitor();
        }

        public String GetPluginName() {
            return "AdKats Extension - LRT";
        }

        public String GetPluginVersion() {
            return PluginVersion;
        }

        public String GetPluginAuthor() {
            return "[ADK]ColColonCleaner";
        }

        public String GetPluginWebsite() {
            return "https://github.com/AdKats/AdKatsLRT/";
        }

        public String GetPluginDescription() {
            return "";
        }

        public List<CPluginVariable> GetDisplayPluginVariables() {
            try {
                var lstReturn = new List<CPluginVariable>();
                const string separator = " | ";
                if (!_WARSAWLibraryLoaded)
                {
                    lstReturn.Add(new CPluginVariable("The WARSAW library must be loaded to view settings.", typeof(String), "Enable the plugin to fetch the library."));
                    return lstReturn;
                }
                if (_WARSAWLibrary.Weapons.Any())
                {
                    foreach (var weapon in _WARSAWLibrary.Weapons.Values.OrderBy(weapon => weapon.category).ThenBy(weapon => weapon.slug))
                    {
                        lstReturn.Add(new CPluginVariable("3. Weapons|ALW" + weapon.warsawID + separator + weapon.categoryType + separator + weapon.slug + separator + "Allow?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWDeniedIDMessages.ContainsKey(weapon.warsawID) ? ("Deny") : ("Allow")));
                    }
                }
                if (_WARSAWLibrary.KitItems.Any())
                {
                    foreach (var kitItem in _WARSAWLibrary.KitItems.Values.OrderBy(kitItem => kitItem.category).ThenBy(kitItem => kitItem.slug))
                    {
                        lstReturn.Add(new CPluginVariable("4. Kit Items|ALW" + kitItem.warsawID + separator + kitItem.category + separator + kitItem.slug + separator + "Allow?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWDeniedIDMessages.ContainsKey(kitItem.warsawID) ? ("Deny") : ("Allow")));
                    }
                }
                if (_WARSAWLibrary.VehicleUnlocks.Any())
                {
                    foreach (var unlock in _WARSAWLibrary.VehicleUnlocks.Values.OrderBy(vehicleUnlock => vehicleUnlock.category).ThenBy(vehicleUnlock => vehicleUnlock.slug))
                    {
                        lstReturn.Add(new CPluginVariable("5. Vehicle Unlocks|ALW" + unlock.warsawID + separator + unlock.category + separator + unlock.slug + separator + "Allow?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWDeniedIDMessages.ContainsKey(unlock.warsawID) ? ("Deny") : ("Allow")));
                    }
                }
                if (_WARSAWLibrary.WeaponAccessories.Any())
                {
                    foreach (var weaponAccessory in _WARSAWLibrary.WeaponAccessories.Values.OrderBy(weaponAccessory => weaponAccessory.slug).ThenBy(weaponAccessory => weaponAccessory.category))
                    {
                        lstReturn.Add(new CPluginVariable("6. Weapon Accessories|ALW" + weaponAccessory.warsawID + separator + weaponAccessory.slug + separator + "Allow?", "enum.roleAllowCommandEnum(Allow|Deny)", _WARSAWDeniedIDMessages.ContainsKey(weaponAccessory.warsawID) ? ("Deny") : ("Allow")));
                    }
                }
                foreach (var pair in _WARSAWDeniedIDMessages.Where(denied => _WARSAWLibrary.Weapons.ContainsKey(denied.Key)))
                {
                    WarsawWeapon deniedWeapon;
                    if (_WARSAWLibrary.Weapons.TryGetValue(pair.Key, out deniedWeapon))
                    {
                        lstReturn.Add(new CPluginVariable("7A. Denied Weapon Kill Messages|MSG" + deniedWeapon.warsawID + separator + deniedWeapon.slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                foreach (var pair in _WARSAWDeniedIDMessages.Where(denied => _WARSAWLibrary.KitItems.ContainsKey(denied.Key)))
                {
                    WarsawKitItem deniedKitItem;
                    if (_WARSAWLibrary.KitItems.TryGetValue(pair.Key, out deniedKitItem))
                    {
                        lstReturn.Add(new CPluginVariable("7B. Denied Kit Item Kill Messages|MSG" + deniedKitItem.warsawID + separator + deniedKitItem.slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                foreach (var pair in _WARSAWDeniedIDMessages.Where(denied => _WARSAWLibrary.VehicleUnlocks.ContainsKey(denied.Key)))
                {
                    WarsawVehicleUnlock deniedVehicleUnlock;
                    if (_WARSAWLibrary.VehicleUnlocks.TryGetValue(pair.Key, out deniedVehicleUnlock))
                    {
                        lstReturn.Add(new CPluginVariable("7C. Denied Vehicle Unlock Kill Messages|MSG" + deniedVehicleUnlock.warsawID + separator + deniedVehicleUnlock.slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                foreach (var pair in _WARSAWDeniedIDMessages.Where(denied => _WARSAWLibrary.WeaponAccessories.ContainsKey(denied.Key)))
                {
                    WarsawWeaponAccessory deniedWeaponAccessory;
                    if (_WARSAWLibrary.WeaponAccessories.TryGetValue(pair.Key, out deniedWeaponAccessory))
                    {
                        lstReturn.Add(new CPluginVariable("7D. Denied Weapon Accessory Kill Messages|MSG" + deniedWeaponAccessory.warsawID + separator + deniedWeaponAccessory.slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                return lstReturn;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching display vars.", e));
                return new List<CPluginVariable>();
            }
        }

        public List<CPluginVariable> GetPluginVariables() {
            var lstReturn = new List<CPluginVariable>();
            const string separator = " | ";
            foreach (var pair in _WARSAWDeniedIDMessages)
            {
                lstReturn.Add(new CPluginVariable("MSG" + pair.Key, typeof(String), pair.Value));
            }
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
                else if (strVariable.StartsWith("ALW"))
                {
                    //Trim off all but the warsaw ID
                    //ALW3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("ALW".ToCharArray()).Trim();
                    //Fetch needed role
                    switch (strValue.ToLower())
                    {
                        case "allow":
                            //parse allow
                            ConsoleWarn("id " + warsawID + " removed");
                            _WARSAWDeniedIDMessages.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            _WARSAWDeniedIDMessages[warsawID] = "Please respawn without " + commandSplit[commandSplit.Count() - 2].Trim() + " in your loadout";
                            break;
                        default:
                            ConsoleError("Unknown setting when assigning WARSAW allowance.");
                            return;
                    }
                }
                else if (strVariable.StartsWith("MSG"))
                {
                    //Trim off all but the warsaw ID
                    //MSG3495820391
                    if (String.IsNullOrEmpty(strValue)) {
                        ConsoleError("Kill messages cannot be empty.");
                        return;
                    }
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("MSG".ToCharArray()).Trim();
                    _WARSAWDeniedIDMessages[warsawID] = strValue;
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
            try {
                //Register all events
                RegisterEvents(GetType().Name, 
                    "OnVersion", 
                    "OnPlayerSpawned",
                    "OnListPlayers");
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

                        if ((DateTime.UtcNow - _proconStartTime).TotalSeconds <= 20) {
                            ConsoleWrite("Waiting a few seconds for requirements and other plugins to initialize, please wait...");
                            //Wait on all settings to be imported by procon for initial start, and for all other plugins to start and register.
                            for (Int32 index = 20 - (Int32)(DateTime.UtcNow - _proconStartTime).TotalSeconds; index > 0; index--)
                            {
                                ConsoleWrite(index + "...");
                                _threadMasterWaitHandle.WaitOne(1000);
                            }
                        }
                        if (!_pluginEnabled) 
                        {
                            return;
                        }
                        if (GetRegisteredCommands().Any(command => 
                            command.RegisteredClassname == "AdKats" && 
                            command.RegisteredMethodName == "PluginEnabled")) {
                            _StartTime = DateTime.UtcNow;
                            //Set the enabled variable
                            _PlayerProcessingWaitHandle.Reset();

                            if (!_pluginEnabled)
                            {
                                return;
                            }
                            //Fetch all weapon names
                            if (LoadWarsawLibrary())
                            {
                                if (!_pluginEnabled)
                                {
                                    return;
                                }
                                ConsoleSuccess("WARSAW library loaded. " + _WARSAWLibrary.Weapons.Count + " weapons, " + _WARSAWLibrary.KitItems.Count + " kit items, " + _WARSAWLibrary.VehicleUnlocks.Count + " vehicle unlocks, and " + _WARSAWLibrary.WeaponAccessories.Count + " accessories.");
                                UpdateSettingPage();

                                //Subscribe to online soldiers from AdKats
                                ExecuteCommand("procon.protected.plugins.call", "AdKats", "SubscribeAsClient", "AdKatsLRT", JSON.JsonEncode(new Hashtable{
                                    {"caller_identity", "AdKatsLRT"},
                                    {"response_requested", false},
                                    {"subscription_group", "OnlineSoldiers"},
                                    {"subscription_method", "ReceiveOnlineSoldiers"},
                                    {"subscription_enabled", true}
                                }));

                                ConsoleInfo("Waiting for player listing response from AdKats.");
                                _PlayerProcessingWaitHandle.WaitOne(Timeout.Infinite);
                                if (!_pluginEnabled)
                                {
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
                        else
                        {
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

                        //Unsubscribe from online soldiers through AdKats
                        ExecuteCommand("procon.protected.plugins.call", "AdKats", "SubscribeAsClient", "AdKatsLRT", JSON.JsonEncode(new Hashtable{
                            {"caller_identity", "AdKatsLRT"},
                            {"response_requested", false},
                            {"subscription_group", "OnlineSoldiers"},
                            {"subscription_method", "ReceiveOnlineSoldiers"},
                            {"subscription_enabled", false}
                        }));

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
                        _WARSAWLibrary = null;
                        _WARSAWLibraryLoaded = false;
                        _firstPlayerListComplete = false;
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

        private void FetchPluginDocumentation() {
            if (_aliveThreads.Values.Any(aThread => aThread.Name == "descfetching")) {
                return;
            }
            _PluginDescriptionWaitHandle.Reset();
            //Create a new thread to fetch the plugin description and changelog
            var descFetcher = new Thread(new ThreadStart(delegate {
                try {
                    Thread.CurrentThread.Name = "descfetching";
                    _pluginDescFetchProgress = "Started";
                    //Create web client
                    var client = new WebClient();
                    //Download the readme and changelog
                    DebugWrite("Fetching plugin links...", 2);
                    try
                    {
                        _pluginLinks = client.DownloadString("https://raw.github.com/AdKats/AdKats/master/LINKS.md");
                        DebugWrite("Plugin links fetched.", 1);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            _pluginLinks = client.DownloadString("http://api.gamerethos.net/adkats/fetch/links");
                            DebugWrite("Plugin links fetched from backup location.", 1);
                        }
                        catch (Exception)
                        {
                            ConsoleError("Failed to fetch plugin links.");
                        }
                    }
                    _pluginDescFetchProgress = "LinksFetched";
                    DebugWrite("Fetching plugin readme...", 2);
                    try
                    {
                        _pluginDescription = client.DownloadString("https://raw.github.com/AdKats/AdKats/master/README.md");
                        DebugWrite("Plugin readme fetched.", 1);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            _pluginDescription = client.DownloadString("http://api.gamerethos.net/adkats/fetch/readme");
                            DebugWrite("Plugin readme fetched from backup location.", 1);
                        }
                        catch (Exception)
                        {
                            ConsoleError("Failed to fetch plugin readme.");
                        }
                    }
                    _pluginDescFetchProgress = "DescFetched";
                    DebugWrite("Fetching plugin changelog...", 2);
                    try
                    {
                        _pluginChangelog = client.DownloadString("https://raw.github.com/AdKats/AdKats/master/CHANGELOG.md");
                        DebugWrite("Plugin changelog fetched.", 1);
                    }
                    catch (Exception)
                    {
                        try
                        {
                            _pluginChangelog = client.DownloadString("http://api.gamerethos.net/adkats/fetch/changelog");
                            DebugWrite("Plugin changelog fetched from backup location.", 1);
                        }
                        catch (Exception)
                        {
                            ConsoleError("Failed to fetch plugin changelog.");
                        }
                    }
                    _pluginDescFetchProgress = "ChangeFetched";
                    if (!String.IsNullOrEmpty(_pluginDescription)) {
                        //Extract the latest stable version
                        String latestStableVersion = ExtractString(_pluginDescription, "latest_stable_release");
                        if (!String.IsNullOrEmpty(latestStableVersion)) {
                            //Convert it to an integer
                            String trimmedLatestStableVersion = latestStableVersion.Replace(".", "");
                            _latestPluginVersion = latestStableVersion;
                            _latestPluginVersionInt = Int32.Parse(trimmedLatestStableVersion);
                            //Get current plugin version
                            _currentPluginVersionInt = Int32.Parse(PluginVersion.Replace(".", ""));

                            String versionStatus = String.Empty;
                            //Add the appropriate message to plugin description
                            if (_latestPluginVersionInt > _currentPluginVersionInt) {
                                if (_pluginUpdatePatched) {
                                    versionStatus = @"
                                    <h2 style='color:#DF0101;'>
                                        You are running an outdated version! The update has been patched, reboot PRoCon to run version " + latestStableVersion + @"!
                                    </h2>";
                                }
                                else {
                                    versionStatus = @"
                                    <h2 style='color:#DF0101;'>
                                        You are running an outdated version! Version " + latestStableVersion + @" is available for download!
                                    </h2>
                                    <a href='https://sourceforge.net/projects/adkats/files/latest/download' target='_blank'>
                                        Download Version " + latestStableVersion + @"!
                                    </a><br/>
                                    Download link below.";
                                }
                                _pluginVersionStatus = VersionStatus.OutdatedBuild;
                            }
                            else if (_latestPluginVersionInt == _currentPluginVersionInt) {
                                versionStatus = @"
                                <h2 style='color:#01DF01;'>
                                    Congrats! You are running the latest stable version!
                                </h2>";
                                _pluginVersionStatus = VersionStatus.StableBuild;
                            }
                            else if (_latestPluginVersionInt < _currentPluginVersionInt) {
                                versionStatus = @"
                                <h2 style='color:#FF8000;'>
                                    CAUTION! You are running a TEST version! Functionality might not be completely tested.
                                </h2>";
                                _pluginVersionStatus = VersionStatus.TestBuild;
                            }
                            else {
                                _pluginVersionStatus = VersionStatus.UnknownBuild;
                            }
                            //Prepend the message
                            _pluginVersionStatusString = versionStatus;
                            _pluginDescFetchProgress = "VersionStatusSet";
                            //Check for plugin updates
                            CheckForPluginUpdates();
                            _pluginDescFetchProgress = "UpdateChecked";
                        }
                    }
                    else if (!_fetchedPluginInformation) {
                        ConsoleError("Unable to fetch required documentation files. AdKatsLRT cannot be started.");
                        Disable();
                        return;
                    }
                    DebugWrite("Setting desc fetch handle.", 1);
                    _fetchedPluginInformation = true;
                    _LastPluginDescFetch = DateTime.UtcNow;
                    _PluginDescriptionWaitHandle.Set();
                    _pluginDescFetchProgress = "Completed";
                }
                catch (Exception e) {
                    HandleException(new AdKatsException("Error while fetching plugin description and changelog.", e));
                }
                LogThreadExit();
            }));
            //Start the thread
            StartAndLogThread(descFetcher);
        }

        private void SetupStatusMonitor() {
            //Create a new thread to handle status monitoring
            //This thread will remain running for the duration the layer is online
            var statusMonitorThread = new Thread(new ThreadStart(delegate {
                try {
                    Thread.CurrentThread.Name = "StatusMonitor";
                    DateTime lastKeepAliveCheck = DateTime.UtcNow;
                    DateTime lastServerInfoRequest = DateTime.UtcNow;
                    while (true)
                    {
                        try
                        {
                            //Check for plugin updates at interval
                            if ((DateTime.UtcNow - _LastPluginDescFetch).TotalMinutes > 20)
                            {
                                FetchPluginDocumentation();
                            }

                            //Check for thread warning every 30 seconds
                            if ((DateTime.UtcNow - lastKeepAliveCheck).TotalSeconds > 30)
                            {
                                if (_pluginEnabled && 
                                    !GetRegisteredCommands().Any(command => 
                                        command.RegisteredClassname == "AdKats" && 
                                        command.RegisteredMethodName == "PluginEnabled")) {
                                    ConsoleSuccess("AdKats was disabled. The AdKatsLRT extension requires that plugin to function.");
                                    Disable();
                                }
                                lastKeepAliveCheck = DateTime.UtcNow;

                                if (_aliveThreads.Count() >= 20)
                                {
                                    String aliveThreads = "";
                                    lock (_aliveThreads)
                                    {
                                        foreach (Thread value in _aliveThreads.Values.ToList())
                                            aliveThreads = aliveThreads + (value.Name + "[" + value.ManagedThreadId + "] ");
                                    }
                                    ConsoleWarn("Thread warning: " + aliveThreads);
                                }
                            }
                            //Sleep 1 second between loops
                            _threadMasterWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                        }
                        catch (Exception e)
                        {
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
                _SpawnProcessingThread = new Thread(SpawnProcessingThreadLoop) {
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
            if (Thread.CurrentThread.Name == "finalizer")
            {
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
            else
            {
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

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory)
        {
            if (_threadsReady && _pluginEnabled && _firstPlayerListComplete)
            {
                AdKatsSubscribedPlayer aPlayer;
                if (_PlayerDictionary.TryGetValue(soldierName, out aPlayer))
                {
                    if ((aPlayer.player_reported && aPlayer.player_reputation < 15) || aPlayer.player_punished || aPlayer.player_marked || aPlayer.player_infractionPoints > 5)
                    {
                        //Start a delay thread
                        StartAndLogThread(new Thread(new ThreadStart(delegate
                        {
                            Thread.CurrentThread.Name = "LoadoutCheckDelay";
                            Thread.Sleep(5000);
                            QueuePlayerForProcessing(aPlayer);
                            LogThreadExit();
                        })));
                    }
                }
                else
                {
                    ConsoleError("Attempted to process spawn of " + soldierName + " without their player object loaded.");
                }
            }
        }

        public void CallLoadoutCheckOnPlayer(params String[] parameters)
        {
            DebugWrite("CallLoadoutCheckOnPlayer starting!", 6);
            try
            {
                if (parameters.Length != 2)
                {
                    ConsoleError("Call loadout check canceled. Parameters invalid.");
                    return;
                }
                String source = parameters[0];
                String unparsedCommandJSON = parameters[1];

                Hashtable decodedCommand = (Hashtable)JSON.JsonDecode(unparsedCommandJSON);

                String playerName = (String)decodedCommand["player_name"];

                if (_threadsReady && _pluginEnabled && _firstPlayerListComplete)
                {
                    AdKatsSubscribedPlayer aPlayer;
                    if (_PlayerDictionary.TryGetValue(playerName, out aPlayer))
                    {
                        ConsoleWrite("Loadout check manually called on " + playerName + ".");
                        QueuePlayerForProcessing(aPlayer);
                    }
                    else
                    {
                        ConsoleError("Attempted to call loadout check on " + playerName + " without their player object loaded.");
                    }
                }
                
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while calling loadout check on player.", e));
            }
            DebugWrite("CallLoadoutCheckOnPlayer finished!", 6);
        }

        private void QueuePlayerForProcessing(AdKatsSubscribedPlayer aPlayer)
        {
            DebugWrite("Entering QueuePlayerForProcessing", 7);
            try {
                if (_LoadoutProcessingQueue.All(pPlayer => pPlayer.player_id != aPlayer.player_id))
                {
                    _LoadoutProcessingQueue.Enqueue(aPlayer);
                    _LoadoutProcessingWaitHandle.Set();
                    DebugWrite(aPlayer.player_name + " queued for processing", 6);
                }
                else {
                    ConsoleWarn(aPlayer.player_name + " already in queue. Cancelling.");
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while queueing player for processing.", e));
            }
            DebugWrite("Exiting QueuePlayerForProcessing", 7);
        }

        public void SpawnProcessingThreadLoop()
        {
            try
            {
                DebugWrite("SPROC: Starting Spawn Processing Thread", 1);
                Thread.CurrentThread.Name = "SpawnProcessing";
                DateTime loopStart = DateTime.UtcNow;
                while (true)
                {
                    try
                    {
                        DebugWrite("SPROC: Entering Spawn Processing Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            DebugWrite("SPROC: Detected AdKatsLRT not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        if (_LoadoutProcessingQueue.Count > 0)
                        {
                            AdKatsSubscribedPlayer aPlayer;
                            //Dequeue the next player
                            aPlayer = _LoadoutProcessingQueue.Dequeue();
                            //Fetch the loadout
                            AdKatsLoadout loadout = GetPlayerLoadout(aPlayer.player_personaID);
                            //Process the loadout
                            String message = "Player " + loadout.Name + " processed as " + loadout.SelectedKit.ToString() + " with weapons (";
                            String weapons = loadout.KitWeapons.Values.Aggregate("", (current, weapon) => current + (weapon.slug + " [" + weapon.WeaponAccessories.Values.Aggregate("", (currentString, acc) => currentString + acc.slug + ", ").Trim().TrimEnd(',') + "], ")).Trim().TrimEnd(',');
                            if (String.IsNullOrEmpty(weapons))
                            {
                                weapons = "none";
                            }
                            message += weapons + ") and kit items (";
                            String kitItems = loadout.KitItems.Values.Aggregate("", (current, kitItem) => current + (kitItem.slug + ", "));
                            kitItems = kitItems.Trim().TrimEnd(',');
                            if (String.IsNullOrEmpty(kitItems))
                            {
                                kitItems = "none";
                            }
                            message += kitItems + ")";
                            ConsoleInfo(message);
                            Boolean loadoutValid = true;
                            foreach (var warsawDeniedIDMessage in _WARSAWDeniedIDMessages) 
                            {
                                if (loadout.AllKitIDs.Contains(warsawDeniedIDMessage.Key))
                                {
                                    loadoutValid = false;
                                    PlayerTellMessage(loadout.Name, warsawDeniedIDMessage.Value);
                                    break;
                                }
                            }
                            //Inform AdKats of the loadout
                            StartAndLogThread(new Thread(new ThreadStart(delegate
                            {
                                Thread.CurrentThread.Name = "AdKatsInformThread";
                                Thread.Sleep(100);
                                ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable{
                                    {"caller_identity", "AdKatsLRT"},
                                    {"response_requested", false},
                                    {"loadout_player", loadout.Name},
                                    {"loadout_valid", loadoutValid}
                                }));
                                Thread.Sleep(100);
                                LogThreadExit();
                            })));
                            aPlayer.player_loadoutEnforced = true;
                            if (!loadoutValid) {
                                //Tell them any other items that are invalid in their loadout
                                String deniedWeapons = String.Empty;
                                foreach (WarsawWeapon weapon in loadout.KitWeapons.Values) {
                                    if (_WARSAWDeniedIDMessages.ContainsKey(weapon.warsawID)) {
                                        deniedWeapons += weapon.slug.ToUpper() + ", ";
                                    }
                                    deniedWeapons = weapon.WeaponAccessories.Values.Where(weaponAccessory => _WARSAWDeniedIDMessages.ContainsKey(weaponAccessory.warsawID)).Aggregate(deniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.slug.ToUpper() + ", "));
                                }
                                deniedWeapons = loadout.KitItems.Values.Where(kitItem => _WARSAWDeniedIDMessages.ContainsKey(kitItem.warsawID)).Aggregate(deniedWeapons, (current, kitItem) => current + (kitItem.slug.ToUpper() + ", ")).Trim().TrimEnd(',');
                                String reason = "";
                                if (aPlayer.player_infractionPoints > 5) {
                                    reason = "[" + aPlayer.player_infractionPoints + " infractions] ";
                                }
                                if (aPlayer.player_reported) {
                                    reason = "[reported] ";
                                }
                                if (aPlayer.player_punished) {
                                    reason = "[punished recently] ";
                                }
                                if (aPlayer.player_marked) {
                                    reason = "[marked] ";
                                }
                                AdminSayMessage(reason + aPlayer.player_name + " please remove [" + deniedWeapons + "] from your loadout.");
                                if (((aPlayer.player_infractionPoints > 5 || aPlayer.player_reported) && aPlayer.player_reputation < 0) ||
                                    aPlayer.player_punished || 
                                    aPlayer.player_marked)
                                {
                                    aPlayer.player_loadoutKilled = true;
                                    Thread.Sleep(2000);
                                    ExecuteCommand("procon.protected.send", "admin.killPlayer", loadout.Name);
                                }
                            }
                            else {
                                if (!aPlayer.player_loadoutValid)
                                {
                                    AdminSayMessage(aPlayer.player_name + " thank you for fixing your loadout.");
                                }
                            }
                            aPlayer.player_loadoutValid = loadoutValid;
                            Double totalPlayerCount = _PlayerDictionary.Count + _PlayerLeftDictionary.Count;
                            Double countEnforced = _PlayerDictionary.Values.Count(dPlayer => dPlayer.player_loadoutEnforced) + _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutEnforced);
                            Double countKilled = _PlayerDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled) + _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled);
                            Double countFixed = _PlayerDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled && dPlayer.player_loadoutValid) + _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled && dPlayer.player_loadoutValid);
                            Double countRaged = _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled && !dPlayer.player_loadoutValid);
                            Double percentEnforced = Math.Round(countEnforced / totalPlayerCount * 100.0, 2);
                            Double percentKilled = Math.Round(countKilled / totalPlayerCount * 100.0, 2);
                            Double percentFixed = Math.Round(countFixed / countKilled * 100.0, 2);
                            Double percentRaged = Math.Round(countRaged / countKilled * 100.0, 2);
                            ConsoleInfo("(" + countEnforced + "/" + totalPlayerCount + ") " + percentEnforced + "% under loadout enforcement. " + "(" + countKilled + "/" + totalPlayerCount + ") " + percentKilled + "% killed for loadout enforcement. " + "(" + countFixed + "/" + countKilled + ") " + percentFixed + "% fixed their loadouts after kill. " + "(" + countRaged + "/" + countKilled + ") " + percentRaged + "% ragequit without fixing.");
                        }
                        else
                        {
                            //Wait for input
                            if ((DateTime.UtcNow - loopStart).TotalMilliseconds > 1000)
                                DebugWrite("Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((DateTime.UtcNow - loopStart).TotalMilliseconds)) + "ms", 4);
                            _LoadoutProcessingWaitHandle.Reset();
                            _LoadoutProcessingWaitHandle.WaitOne(Timeout.Infinite);
                            loopStart = DateTime.UtcNow;
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            HandleException(new AdKatsException("Spawn processing thread aborted. Exiting."));
                            break;
                        }
                        HandleException(new AdKatsException("Error occured in spawn processing thread.", e));
                    }
                }
                DebugWrite("SPROC: Ending Spawn Processing Thread", 1);
                LogThreadExit();
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error occured in kill processing thread.", e));
            }
        }

        public void ReceiveOnlineSoldiers(params String[] parameters) {
            DebugWrite("ReceiveOnlineSoldiers starting!", 6);
            try {
                if (parameters.Length != 2) {
                    ConsoleError("Online soldier handling canceled. Parameters invalid.");
                    return;
                }
                String source = parameters[0];
                String unparsedResponseJSON = parameters[1];

                Hashtable decodedResponse = (Hashtable)JSON.JsonDecode(unparsedResponseJSON);

                ArrayList decodedSoldierList = (ArrayList)decodedResponse["response_value"];
                if (decodedSoldierList == null)
                {
                    ConsoleError("Soldier params could not be properly converted from JSON. Unable to continue.");
                    return;
                }
                lock (_PlayerDictionary)
                {
                    List<String> validPlayers = new List<String>();
                    foreach (Hashtable soldierHashtable in decodedSoldierList)
                    {
                        AdKatsSubscribedPlayer aPlayer = new AdKatsSubscribedPlayer();
                        aPlayer.player_id = Convert.ToInt64((Double)soldierHashtable["player_id"]);
                        aPlayer.player_guid = (String)soldierHashtable["player_guid"];
                        aPlayer.player_pbguid = (String)soldierHashtable["player_pbguid"];
                        aPlayer.player_ip = (String)soldierHashtable["player_ip"];
                        aPlayer.player_name = (String)soldierHashtable["player_name"];
                        aPlayer.player_personaID = (String)soldierHashtable["player_personaID"];
                        aPlayer.player_aa = (Boolean)soldierHashtable["player_aa"];
                        aPlayer.player_ping = (Double)soldierHashtable["player_ping"];
                        aPlayer.player_reputation = (Double)soldierHashtable["player_reputation"];
                        aPlayer.player_infractionPoints = Convert.ToInt32((Double)soldierHashtable["player_infractionPoints"]);
                        aPlayer.player_role = (String)soldierHashtable["player_role"];
                        aPlayer.player_type = (String)soldierHashtable["player_type"];
                        aPlayer.player_isAdmin = (Boolean)soldierHashtable["player_isAdmin"];
                        aPlayer.player_reported = (Boolean)soldierHashtable["player_reported"];
                        aPlayer.player_punished = (Boolean)soldierHashtable["player_punished"];
                        aPlayer.player_marked = (Boolean)soldierHashtable["player_marked"];
                        aPlayer.player_spawnedOnce = (Boolean)soldierHashtable["player_spawnedOnce"];
                        aPlayer.player_conversationPartner = (String)soldierHashtable["player_conversationPartner"];
                        aPlayer.player_kills = Convert.ToInt32((Double)soldierHashtable["player_kills"]);
                        aPlayer.player_deaths = Convert.ToInt32((Double)soldierHashtable["player_deaths"]);
                        aPlayer.player_kdr = (Double) soldierHashtable["player_kdr"];
                        aPlayer.player_rank = Convert.ToInt32((Double)soldierHashtable["player_rank"]);
                        aPlayer.player_score = Convert.ToInt32((Double)soldierHashtable["player_score"]);
                        aPlayer.player_squad = Convert.ToInt32((Double)soldierHashtable["player_squad"]);
                        aPlayer.player_team = Convert.ToInt32((Double)soldierHashtable["player_team"]);

                        validPlayers.Add(aPlayer.player_name);

                        AdKatsSubscribedPlayer dPlayer;
                        if (_PlayerDictionary.TryGetValue(aPlayer.player_name, out dPlayer)) {
                            //Player already exists, update the model
                            dPlayer.player_ip = aPlayer.player_ip;
                            dPlayer.player_aa = aPlayer.player_aa;
                            dPlayer.player_ping = aPlayer.player_ping;
                            dPlayer.player_reputation = aPlayer.player_reputation;
                            dPlayer.player_infractionPoints = aPlayer.player_infractionPoints;
                            dPlayer.player_role = aPlayer.player_role;
                            dPlayer.player_type = aPlayer.player_type;
                            dPlayer.player_isAdmin = aPlayer.player_isAdmin;
                            Boolean action = false;
                            //Check player loadout if they've been reported and have low rep
                            if (!dPlayer.player_reported && aPlayer.player_reported && dPlayer.player_reputation < 15) {
                                action = true;
                            }
                            dPlayer.player_reported = aPlayer.player_reported;
                            if (!dPlayer.player_punished && aPlayer.player_punished)
                            {
                                action = true;
                            }
                            dPlayer.player_punished = aPlayer.player_punished;
                            if (!dPlayer.player_marked && aPlayer.player_marked)
                            {
                                action = true;
                            }
                            dPlayer.player_marked = aPlayer.player_marked;
                            if (action) {
                                QueuePlayerForProcessing(dPlayer);
                            }
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
                        else {
                            if (aPlayer.player_infractionPoints > 5 && aPlayer.player_spawnedOnce) {
                                QueuePlayerForProcessing(aPlayer);
                            }
                            _PlayerDictionary[aPlayer.player_name] = aPlayer;
                            _PlayerLeftDictionary.Remove(aPlayer.player_id);
                        }
                    }
                    foreach (string playerName in _PlayerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList()) {
                        AdKatsSubscribedPlayer aPlayer;
                        if (_PlayerDictionary.TryGetValue(playerName, out aPlayer))
                        {
                            _PlayerDictionary.Remove(aPlayer.player_name);
                            _PlayerLeftDictionary[aPlayer.player_id] = aPlayer;
                        }
                    }
                }
                if (!_firstPlayerListComplete) {
                    _firstPlayerListComplete = true;
                    _PlayerProcessingWaitHandle.Set();
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while receiving online soldiers.", e));
            }
            DebugWrite("ReceiveOnlineSoldiers finished!", 6);
        }

        public void AdminSayMessage(String message) {
            AdminSayMessage(message, true);
        }

        public void AdminSayMessage(String message, Boolean displayProconChat)
        {
            DebugWrite("Entering adminSay", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    ConsoleError("message null in adminSay");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Say > " + message);
                }
                var lineSplit = message.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                foreach (String line in lineSplit) {
                    ExecuteCommand("procon.protected.send", "admin.say", line, "all");
                }
            }
            catch (Exception e)
            {
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
                if (displayProconChat)
                {
                    ProconChatWrite("Say > " + target + " > " + message);
                }
                for (int count = 0; count < spamCount; count++) {
                    var lineSplit = message.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
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

        public void AdminYellMessage(String message, Boolean displayProconChat)
        {
            DebugWrite("Entering adminYell", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    ConsoleError("message null in adminYell");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Yell[" + _YellDuration + "s] > " + message);
                }
                ExecuteCommand("procon.protected.send", "admin.yell", message.ToUpper(), _YellDuration + "", "all");
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while sending admin yell.", e));
            }
            DebugWrite("Exiting adminYell", 7);
        }

        public void PlayerYellMessage(String target, String message) {
            PlayerYellMessage(target, message, true, 1);
        }

        public void PlayerYellMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            DebugWrite("Entering PlayerYellMessage", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    ConsoleError("message null in PlayerYellMessage");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Yell[" + _YellDuration + "s] > " + target + " > " + message);
                }
                for (int count = 0; count < spamCount; count++)
                {
                    ExecuteCommand("procon.protected.send", "admin.yell", ((_gameVersion == GameVersion.BF4) ? (System.Environment.NewLine) : ("")) + message.ToUpper(), _YellDuration + "", "player", target);
                    _threadMasterWaitHandle.WaitOne(50);
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while sending admin yell.", e));
            }
            DebugWrite("Exiting PlayerYellMessage", 7);
        }

        public void AdminTellMessage(String message) {
            AdminTellMessage(message, true);
        }

        public void AdminTellMessage(String message, Boolean displayProconChat)
        {
            if (displayProconChat)
            {
                ProconChatWrite("Tell[" + _YellDuration + "s] > " + message);
            }
            AdminSayMessage(message, false);
            AdminYellMessage(message, false);
        }

        public void PlayerTellMessage(String target, String message) {
            PlayerTellMessage(target, message, true, 1);
        }

        public void PlayerTellMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            if (displayProconChat)
            {
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
                    WarsawLibrary library = new WarsawLibrary();
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
                    Hashtable compact = (Hashtable) responseData["compact"];
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
                    Hashtable weapons = (Hashtable) compact["weapons"];
                    if (weapons == null) {
                        ConsoleError("Weapons section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    ConsoleInfo("WARSAW library downloaded. Parsing.");
                    //Pause for effect, nothing else
                    Thread.Sleep(500);

                    Dictionary<String, WarsawWeapon> weaponDictionary = new Dictionary<String, WarsawWeapon>();
                    foreach (DictionaryEntry entry in weapons)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            //ConsoleError("Rejecting weapon element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        WarsawWeapon weapon = new WarsawWeapon();
                        weapon.warsawID = warsawID.ToString();
                        Boolean debug = false;
                        if (false) {
                            debug = true;
                            ConsoleInfo("Loading debug warsaw ID " + weapon.warsawID);
                        }

                        //Grab the contents
                        Hashtable weaponData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("category"))
                        {
                            if(debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        weapon.category = (String)weaponData["category"];
                        if (String.IsNullOrEmpty(weapon.category))
                        {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
                        weapon.category = weapon.category.Split('_').Last().Replace('_', ' ').ToUpper();
                        //weapon.category = weapon.category.TrimStart("WARSAW_ID_P_CAT_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("name"))
                        {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        weapon.name = (String)weaponData["name"];
                        if (String.IsNullOrEmpty(weapon.name))
                        {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        weapon.name = weapon.name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab categoryType------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("categoryType"))
                        {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'categoryType'.");
                            continue;
                        }
                        weapon.categoryType = (String)weaponData["categoryType"];
                        if (String.IsNullOrEmpty(weapon.category))
                        {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'categoryType' was invalid.");
                            continue;
                        }
                        //Parsed categoryType does not require any modifications

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("slug"))
                        {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        weapon.slug = (String)weaponData["slug"];
                        if (String.IsNullOrEmpty(weapon.slug))
                        {
                            if (debug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        weapon.slug = weapon.slug.Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        weaponDictionary[weapon.warsawID] = weapon;
                        if(debug)
                            ConsoleSuccess("Weapon " + weapon.warsawID + " added. " + weaponDictionary.ContainsKey(weapon.warsawID));
                    }
                    //Assign the new built dictionary
                    library.Weapons = weaponDictionary;
                    ConsoleInfo("WARSAW weapons parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(500);

                    Hashtable weaponaccessory = (Hashtable) compact["weaponaccessory"];
                    if (weaponaccessory == null) {
                        ConsoleError("Weapon accessory section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    Dictionary<String, WarsawWeaponAccessory> weaponAccessoryDictionary = new Dictionary<String, WarsawWeaponAccessory>();
                    foreach (DictionaryEntry entry in weaponaccessory) {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String) entry.Key, out warsawID)) {
                            //Reject the entry
                            //ConsoleError("Rejecting weapon accessory element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        WarsawWeaponAccessory weaponAccessory = new WarsawWeaponAccessory();
                        weaponAccessory.warsawID = warsawID.ToString();

                        //Grab the contents
                        Hashtable weaponAccessoryData = (Hashtable) entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category")) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        weaponAccessory.category = (String) weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(weaponAccessory.category)) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
                        weaponAccessory.category = weaponAccessory.category.Split('_').Last().Replace('_', ' ').ToUpper();
                        //weaponAccessory.category = weaponAccessory.category.Substring(15, weaponAccessory.category.Length - 15).Replace('_', ' ').ToLower();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name")) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        weaponAccessory.name = (String) weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(weaponAccessory.name)) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        weaponAccessory.name = weaponAccessory.name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug")) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        weaponAccessory.slug = (String) weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(weaponAccessory.slug)) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        weaponAccessory.slug = weaponAccessory.slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        weaponAccessoryDictionary[weaponAccessory.warsawID] = weaponAccessory;
                    }
                    library.WeaponAccessories = weaponAccessoryDictionary;
                    ConsoleInfo("WARSAW weapon accessories parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(500);

                    Hashtable kititems = (Hashtable)compact["kititems"];
                    if (kititems == null)
                    {
                        ConsoleError("Kit items section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    Dictionary<String, WarsawKitItem> kitItemsDictionary = new Dictionary<String, WarsawKitItem>();
                    foreach (DictionaryEntry entry in kititems)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            //ConsoleError("Rejecting kit item element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        WarsawKitItem kitItem = new WarsawKitItem();
                        kitItem.warsawID = warsawID.ToString();

                        //Grab the contents
                        Hashtable weaponAccessoryData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category"))
                        {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        kitItem.category = (String)weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(kitItem.category))
                        {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
                        kitItem.category = kitItem.category.Split('_').Last().Replace('_', ' ').ToUpper();
                        //kitItem.category = kitItem.category.TrimStart("WARSAW_ID_P_CAT_".ToCharArray()).Replace('_', ' ').ToLower();
                        if (kitItem.category != "GADGET" && kitItem.category != "GRENADE")
                        {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. 'category' not gadget or grenade.");
                            continue;
                        }

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name"))
                        {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        kitItem.name = (String)weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(kitItem.name))
                        {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        kitItem.name = kitItem.name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug"))
                        {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        kitItem.slug = (String)weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(kitItem.slug))
                        {
                            //ConsoleError("Rejecting kit item '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        kitItem.slug = kitItem.slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        kitItemsDictionary[kitItem.warsawID] = kitItem;
                    }
                    library.KitItems = kitItemsDictionary;
                    ConsoleInfo("WARSAW kit items parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(500);

                    Hashtable vehicleunlocks = (Hashtable)compact["vehicleunlocks"];
                    if (vehicleunlocks == null)
                    {
                        ConsoleError("Vehicle unlocks section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    Dictionary<String, WarsawVehicleUnlock> vehicleUnlockDictionary = new Dictionary<String, WarsawVehicleUnlock>();
                    foreach (DictionaryEntry entry in vehicleunlocks)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            //ConsoleError("Rejecting vehicle unlock element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        WarsawVehicleUnlock vehicleUnlock = new WarsawVehicleUnlock();
                        vehicleUnlock.warsawID = warsawID.ToString();

                        //Grab the contents
                        Hashtable vehicleUnlockData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("category"))
                        {
                            //ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        vehicleUnlock.category = (String)vehicleUnlockData["category"];
                        if (String.IsNullOrEmpty(vehicleUnlock.category))
                        {
                            //ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
                        vehicleUnlock.category = vehicleUnlock.category.Split('_').Last().Replace('_', ' ').ToUpper();
                        //kitItem.category = kitItem.category.TrimStart("WARSAW_ID_P_CAT_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab name------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("name"))
                        {
                            //ConsoleError("Rejecting vehicle unlock'" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        String name = (String)vehicleUnlockData["name"];
                        if (String.IsNullOrEmpty(name))
                        {
                            //ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        name = name.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab slug------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("slug"))
                        {
                            //ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        vehicleUnlock.slug = (String)vehicleUnlockData["slug"];
                        if (String.IsNullOrEmpty(vehicleUnlock.slug))
                        {
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
            try
            {
                using (var client = new WebClient())
                {
                    try
                    {
                        String response = client.DownloadString("https://raw.githubusercontent.com/AdKats/AdKats-LRT/test/WarsawCodeBook.json?token=AB0Lkwfvlgjp3-4U8T4rrKUrhrYnXYOGks5UbN3HwA%3D%3D");
                        library = (Hashtable) JSON.JsonDecode(response);
                    }
                    catch (Exception e) {
                        HandleException(new AdKatsException("Error while loading WARSAW library raw.", e));
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Unexpected error while fetching WARSAW library", e));
                return null;
            }
            return library;
        }

        private AdKatsLoadout GetPlayerLoadout(String personaID) {
            DebugWrite("Entering GetPlayerLoadout", 7);
            try
            {
                Hashtable responseData = null;
                if (_gameVersion == GameVersion.BF4)
                {
                    AdKatsLoadout loadout = new AdKatsLoadout();
                    responseData = FetchPlayerLoadout(personaID);
                    if (responseData == null)
                    {
                        ConsoleError("Loadout fetch failed, unable to parse player loadout.");
                        return null;
                    }
                    if (!responseData.ContainsKey("data"))
                    {
                        ConsoleError("Loadout fetch did not contain 'data' element, unable to parse player loadout.");
                        return null;
                    }
                    Hashtable data = (Hashtable)responseData["data"];
                    if (data == null)
                    {
                        ConsoleError("Data section of loadout failed parse, unable to parse player loadout.");
                        return null;
                    }
                    //Get parsed back persona ID
                    if (!data.ContainsKey("personaId"))
                    {
                        ConsoleError("Data section of loadout did not contain 'personaId' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.PersonaID = data["personaId"].ToString();
                    //Get persona name
                    if (!data.ContainsKey("personaName"))
                    {
                        ConsoleError("Data section of loadout did not contain 'personaName' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.Name = data["personaName"].ToString();
                    //Get weapons and their attachements
                    if (!data.ContainsKey("currentLoadout"))
                    {
                        ConsoleError("Data section of loadout did not contain 'currentLoadout' element, unable to parse player loadout.");
                        return null;
                    }
                    Hashtable currentLoadoutHashtable = (Hashtable)data["currentLoadout"];
                    if (currentLoadoutHashtable == null)
                    {
                        ConsoleError("Current loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    if (!currentLoadoutHashtable.ContainsKey("weapons"))
                    {
                        ConsoleError("Current loadout section did not contain 'weapons' element, unable to parse player loadout.");
                        return null;
                    }
                    Hashtable currentLoadoutWeapons = (Hashtable) currentLoadoutHashtable["weapons"];
                    if (currentLoadoutWeapons == null)
                    {
                        ConsoleError("Weapon loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    foreach (DictionaryEntry weaponEntry in currentLoadoutWeapons) {
                        if (weaponEntry.Key.ToString() != "0")
                        {
                            WarsawWeapon warsawWeapon;
                            if (_WARSAWLibrary.Weapons.TryGetValue(weaponEntry.Key.ToString(), out warsawWeapon))
                            {
                                //Create new instance of the weapon for this player
                                WarsawWeapon loadoutWeapon = new WarsawWeapon() {
                                    warsawID = warsawWeapon.warsawID,
                                    category = warsawWeapon.category,
                                    categoryType = warsawWeapon.categoryType,
                                    name = warsawWeapon.name,
                                    slug = warsawWeapon.slug
                                };
                                foreach (String accessoryID in (ArrayList)weaponEntry.Value)
                                {
                                    if (accessoryID != "0")
                                    {
                                        WarsawWeaponAccessory warsawWeaponAccessory;
                                        if (_WARSAWLibrary.WeaponAccessories.TryGetValue(accessoryID, out warsawWeaponAccessory))
                                        {
                                            loadoutWeapon.WeaponAccessories[warsawWeaponAccessory.warsawID] = warsawWeaponAccessory;
                                        }
                                    }
                                }
                                loadout.LoadoutWeapons[loadoutWeapon.warsawID] = loadoutWeapon;
                            }
                        }
                    }
                    if (!currentLoadoutHashtable.ContainsKey("selectedKit"))
                    {
                        ConsoleError("Current loadout section did not contain 'selectedKit' element, unable to parse player loadout.");
                        return null;
                    }
                    String selectedKit = currentLoadoutHashtable["selectedKit"].ToString();
                    List<String> selectedKitItems = new List<String>();
                    switch (selectedKit) {
                        case "0":
                            loadout.SelectedKit = AdKatsLoadout.KitType.Assault;
                            foreach (var element in (ArrayList) ((ArrayList) currentLoadoutHashtable["kits"])[0]) {
                                //ConsoleWrite(loadout.Name + " | " + loadout.SelectedKit.ToString() + " | " + element.ToString());
                                selectedKitItems.Add(element.ToString());
                            }
                            break;
                        case "1":
                            loadout.SelectedKit = AdKatsLoadout.KitType.Engineer;
                            foreach (var element in (ArrayList) ((ArrayList) currentLoadoutHashtable["kits"])[1]) {
                                //ConsoleWrite(loadout.Name + " | " + loadout.SelectedKit.ToString() + " | " + element.ToString());
                                selectedKitItems.Add(element.ToString());
                            }
                            break;
                        case "2":
                            loadout.SelectedKit = AdKatsLoadout.KitType.Support;
                            foreach (var element in (ArrayList) ((ArrayList) currentLoadoutHashtable["kits"])[2]) {
                                //ConsoleWrite(loadout.Name + " | " + loadout.SelectedKit.ToString() + " | " + element.ToString());
                                selectedKitItems.Add(element.ToString());
                            }
                            break;
                        case "3":
                            loadout.SelectedKit = AdKatsLoadout.KitType.Recon;
                            foreach (var element in (ArrayList) ((ArrayList) currentLoadoutHashtable["kits"])[3]) {
                                //ConsoleWrite(loadout.Name + " | " + loadout.SelectedKit.ToString() + " | " + element.ToString());
                                selectedKitItems.Add(element.ToString());
                            }
                            break;
                        default:
                            ConsoleError("Unable to parse selected kit " + selectedKit + ", value is unknown. Unable to parse player loadout.");
                            return null;
                    }
                    foreach (String itemID in selectedKitItems) {
                        //Check if ID is a loadout weapon
                        WarsawWeapon loadoutWeapon;
                        if (loadout.LoadoutWeapons.TryGetValue(itemID, out loadoutWeapon)) {
                            //ConsoleSuccess("Found " + itemID + " as loadout weapon " + loadoutWeapon.slug + " for " + loadout.Name);
                            //Loadout weapon found, assign it to the kit.
                            loadout.KitWeapons[loadoutWeapon.warsawID] = loadoutWeapon;
                        }
                        else
                        {
                            //Check if ID is a kit item
                            WarsawKitItem kitItem;
                            if (_WARSAWLibrary.KitItems.TryGetValue(itemID, out kitItem))
                            {
                                //ConsoleSuccess("Found " + itemID + " as loadout weapon accessory " + kitItem.slug + " for " + loadout.Name);
                                //Kit item found, assign it to the kit.
                                loadout.KitItems[kitItem.warsawID] = kitItem;
                            }
                            else
                            {
                                //Check if ID is a library weapon
                                WarsawWeapon libraryWeapon;
                                if (_WARSAWLibrary.Weapons.TryGetValue(itemID, out libraryWeapon))
                                {
                                    //ConsoleSuccess("Found " + itemID + " as library weapon " + libraryWeapon.slug + " for " + loadout.Name);
                                    //Library weapon found, assign it to the kit.
                                    loadout.KitWeapons[libraryWeapon.warsawID] = libraryWeapon;
                                }
                                else
                                {
                                    //ConsoleWarn("Unable to find " + itemID + " as a valid weapon or accessory for " + loadout.Name);
                                }
                            }
                        }
                    }
                    //Fill the kit ID listings
                    foreach (WarsawWeapon weapon in loadout.KitWeapons.Values)
                    {
                        if (!loadout.AllKitIDs.Contains(weapon.warsawID))
                        {
                            loadout.AllKitIDs.Add(weapon.warsawID);
                        }
                        foreach (WarsawWeaponAccessory accessory in weapon.WeaponAccessories.Values)
                        {
                            if (!loadout.AllKitIDs.Contains(accessory.warsawID))
                            {
                                loadout.AllKitIDs.Add(accessory.warsawID);
                            }
                        }
                    }
                    foreach (WarsawKitItem kitItem in loadout.KitItems.Values)
                    {
                        if (!loadout.AllKitIDs.Contains(kitItem.warsawID))
                        {
                            loadout.AllKitIDs.Add(kitItem.warsawID);
                        }
                    }
                    return loadout;
                }
                ConsoleError("Game not BF4, unable to process player loadout.");
                return null;
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while parsing player loadout.", e));
            }
            DebugWrite("Exiting GetPlayerLoadout", 7);
            return null;
        }

        private Hashtable FetchPlayerLoadout(String personaID) {
            Hashtable loadout = null;
            try
            {
                using (var client = new WebClient())
                {
                    try
                    {
                        DoBattlelogWait();
                        String response = client.DownloadString("http://battlelog.battlefield.com/bf4/loadout/get/PLAYER/" + personaID + "/1/");
                        loadout = (Hashtable) JSON.JsonDecode(response);
                    }
                    catch (Exception e) {
                        HandleException(new AdKatsException("Error while loading player loadout.", e));
                    }
                }
            }
            catch (Exception e)
            {
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
            Int32 startIndex = s.IndexOf(startTag, System.StringComparison.Ordinal) + startTag.Length;
            if (startIndex == -1) {
                ConsoleError("Unable to extract String. Tag not found.");
            }
            Int32 endIndex = s.IndexOf("</" + tag + ">", startIndex, System.StringComparison.Ordinal);
            return s.Substring(startIndex, endIndex - startIndex);
        }

        public Boolean SoldierNameValid(String input) {
            try {
                DebugWrite("Checking player '" + input + "' for validity.", 7);
                if (String.IsNullOrEmpty(input)) {
                    DebugWrite("Soldier Name empty or null.", 5);
                    return false;
                }
                if (input.Length > 16) {
                    DebugWrite("Soldier Name '" + input + "' too long, maximum length is 16 characters.", 5);
                    return false;
                }
                if (new Regex("[^a-zA-Z0-9_-]").Replace(input, "").Length != input.Length) {
                    DebugWrite("Soldier Name '" + input + "' contained invalid characters.", 5);
                    return false;
                }
                return true;
            }
            catch (Exception) {
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

        public static String Encode(String str) {
            byte[] encbuff = System.Text.Encoding.UTF8.GetBytes(str);
            return Convert.ToBase64String(encbuff);
        }

        public static String Decode(String str) {
            byte[] decbuff = Convert.FromBase64String(str.Replace(" ", "+"));
            return System.Text.Encoding.UTF8.GetString(decbuff);
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
            lock (_aliveThreads)
            {
                _aliveThreads.Remove(Thread.CurrentThread.ManagedThreadId);
                //ConsoleWarn("THREAD DEBUG: Stopping [" + Thread.CurrentThread.ManagedThreadId + ":'" + Thread.CurrentThread.Name + "']. " + _aliveThreads.Count + " threads running.");
            }
        }

        protected void StartAndLogThread(Thread aThread) {
            aThread.Start();
            lock (_aliveThreads) {
                if (!_aliveThreads.ContainsKey(aThread.ManagedThreadId)) {
                    _aliveThreads.Add(aThread.ManagedThreadId, aThread);
                    _threadMasterWaitHandle.WaitOne(100);
                    //ConsoleWarn("THREAD DEBUG: Starting [" + aThread.ManagedThreadId + ":'" + aThread.Name + "']. " + _aliveThreads.Count + " threads running.");
                }
            }
        }

        public void CheckForPluginUpdates() {
            try {
                //TODO: add back
                return;
                if ((_pluginVersionStatus == VersionStatus.OutdatedBuild && !_pluginUpdatePatched) || (false))
                {
                    if (_aliveThreads.Values.Any(aThread => aThread.Name == "PluginUpdater"))
                    {
                        return;
                    }
                    var pluginUpdater = new Thread(new ThreadStart(delegate
                    {
                        try
                        {
                            Thread.CurrentThread.Name = "PluginUpdater";
                            _pluginUpdateProgress = "Started";
                            if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                ConsoleInfo("Preparing to download plugin update to version " + _latestPluginVersion);
                            String pluginSource = null;
                            using (var client = new WebClient())
                            {
                                try
                                {
                                    const string stableURL = "https://raw.githubusercontent.com/AdKats/AdKats/master/AdKats.cs";
                                    const string testURL = "https://raw.githubusercontent.com/AdKats/AdKats/test/AdKats.cs";
                                    if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                    {
                                        pluginSource = client.DownloadString(stableURL);
                                    }
                                    else
                                    {
                                        pluginSource = client.DownloadString(testURL);
                                    }
                                }
                                catch (Exception e)
                                {
                                    if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                        ConsoleError("Unable to download plugin update to version " + _latestPluginVersion);
                                    return;
                                }
                            }
                            if (String.IsNullOrEmpty(pluginSource))
                            {
                                if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                    ConsoleError("Downloaded plugin source was empty. Unable update to version " + _latestPluginVersion);
                                return;
                            }
                            _pluginUpdateProgress = "Downloaded";
                            if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                            {
                                ConsoleSuccess("Updated plugin source downloaded.");
                                ConsoleInfo("Preparing test compile on updated plugin source.");
                            }
                            String pluginFileName = "AdKatsLRT.cs";
                            String dllPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
                            String pluginPath = Path.Combine(dllPath.Trim(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }), pluginFileName);
                            String procon_path = Directory.GetParent(Application.ExecutablePath).FullName;
                            String pluginDirectory = Path.Combine(procon_path, Path.Combine("Plugins", "BF4"));
                            var providerOptions = new Dictionary<String, String>();
                            providerOptions.Add("CompilerVersion", "v3.5");
                            var cSharpCodeProvider = new CSharpCodeProvider(providerOptions);
                            var compilerParameters = new CompilerParameters();
                            compilerParameters.ReferencedAssemblies.Add("System.dll");
                            compilerParameters.ReferencedAssemblies.Add("System.Core.dll");
                            compilerParameters.ReferencedAssemblies.Add("System.Data.dll");
                            compilerParameters.ReferencedAssemblies.Add("System.Windows.Forms.dll");
                            compilerParameters.ReferencedAssemblies.Add("System.Xml.dll");
                            compilerParameters.ReferencedAssemblies.Add("MySql.Data.dll");
                            compilerParameters.ReferencedAssemblies.Add("PRoCon.Core.dll");
                            compilerParameters.GenerateInMemory = true;
                            compilerParameters.IncludeDebugInformation = false;
                            compilerParameters.TempFiles = new TempFileCollection(pluginDirectory);
                            var compileResults = cSharpCodeProvider.CompileAssemblyFromSource(compilerParameters, pluginSource);
                            if (compileResults.Errors.HasErrors)
                            {
                                foreach (CompilerError errComp in compileResults.Errors)
                                {
                                    if (String.Compare(errComp.ErrorNumber, "CS0016", StringComparison.Ordinal) != 0 && errComp.IsWarning == false)
                                    {
                                        if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                            ConsoleError(String.Format("\t^1{0} (Line: {1}, C: {2}) {3}: {4}", new object[] { pluginFileName, errComp.Line, errComp.Column, errComp.ErrorNumber, errComp.ErrorText }));
                                    }
                                }
                                if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                    ConsoleError("Updated plugin source could not compile. Unable to update to version " + _latestPluginVersion);
                                return;
                            }
                            else
                            {
                                if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                    ConsoleSuccess("Plugin update compiled successfully.");
                            }
                            _pluginUpdateProgress = "Compiled";
                            if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                ConsoleInfo("Preparing to update source file on disk.");
                            using (FileStream stream = File.Open(pluginPath, FileMode.Create))
                            {
                                if (!stream.CanWrite)
                                {
                                    if (_pluginVersionStatus == VersionStatus.OutdatedBuild)
                                        ConsoleError("Cannot write updates to source file. Unable to update to version " + _latestPluginVersion);
                                }
                                Byte[] info = new UTF8Encoding(true).GetBytes(pluginSource);
                                stream.Write(info, 0, info.Length);
                            }
                            String patchedVersion = ExtractString(pluginSource, "version_code");
                            if (!String.IsNullOrEmpty(patchedVersion)) {
                                String trimmedPatchedVersion = patchedVersion.Replace(".", "");
                                Int32 patchedVersionInt = Int32.Parse(trimmedPatchedVersion);
                                if (patchedVersionInt >= _currentPluginVersionInt) {
                                    //Patched version is newer than current version
                                    if (patchedVersionInt > _pluginPatchedVersionInt && _pluginUpdatePatched)
                                    {
                                        //Patched version is newer than an already patched version
                                        ConsoleSuccess("Previous update " + _pluginPatchedVersion + " overwritten by newer patch " + patchedVersion + ". Restart procon to run this version.");
                                    }
                                    else if (!_pluginUpdatePatched && patchedVersionInt > _currentPluginVersionInt) {
                                        //User not notified of patch yet
                                        ConsoleSuccess("Plugin updated to version " + patchedVersion + ". Restart procon to run this version.");
                                        ConsoleSuccess("Updated plugin file located at: " + pluginPath);
                                    }
                                }
                                else if (!_pluginUpdatePatched) {
                                    //Patched version is older than current version
                                    ConsoleWarn("Plugin reverted to previous version " + patchedVersion + ". Restart procon to run this version.");
                                }
                                _pluginPatchedVersion = patchedVersion;
                                _pluginPatchedVersionInt = patchedVersionInt;
                            }
                            else {
                                ConsoleWarn("Plugin update patched, but its version could not be extracted.");
                            }
                            _pluginUpdateProgress = "Patched";
                            _pluginUpdatePatched = true;
                        }
                        catch (Exception e) {
                            HandleException(new AdKatsException("Error while running update thread.", e));
                        }
                        LogThreadExit();
                    }));
                    StartAndLogThread(pluginUpdater);
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while updating plugin source to latest version", e));
            }
        }

        public void ProconChatWrite(String msg) {
            msg = msg.Replace(System.Environment.NewLine, String.Empty);
            ExecuteCommand("procon.protected.chat.write", "AdKatsLRT > " + msg);
            if (_slowmo) {
                _threadMasterWaitHandle.WaitOne(1000);
            }
        }

        public void ConsoleWrite(String msg, ConsoleMessageType type) {
            ExecuteCommand("procon.protected.pluginconsole.write", FormatMessage(msg, type));
            if (_slowmo)
            {
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

        public void PrintPreparedCommand(MySqlCommand cmd) {
            String query = cmd.Parameters.Cast<MySqlParameter>().Aggregate(cmd.CommandText, (current, p) => current.Replace(p.ParameterName, (p.Value != null)?(p.Value.ToString()):("NULL")));
            ConsoleWrite(query);
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
                ConsoleError("Required inputs null or empty in setExternalPluginSetting");
                return;
            }
            ExecuteCommand("procon.protected.plugins.setVariable", pluginName, settingName, settingValue);
        }

        private void DoBattlelogWait() {
            //Wait 2 seconds between battlelog actions
            if ((DateTime.UtcNow - _LastBattlelogAction) < _BattlelogWaitDuration)
            {
                Thread.Sleep(_BattlelogWaitDuration - (DateTime.UtcNow - _LastBattlelogAction));
            }
            _LastBattlelogAction = DateTime.UtcNow;
        }

        public AdKatsException HandleException(AdKatsException aException)
        {
            try
            {
                //If it's null or AdKatsLRT isn't enabled, just return
                if (aException == null)
                {
                    ConsoleError("Attempted to handle exception when none was given.");
                    return null;
                }
                _slowmo = SlowMoOnException;
                String prefix = "Line ";
                if (aException.InternalException != null)
                {
                    Int64 impericalLineNumber = (new StackTrace(aException.InternalException, true)).GetFrame(0).GetFileLineNumber();
                    Int64 parsedLineNumber = 0;
                    Int64.TryParse(aException.InternalException.ToString().Split(' ').Last(), out parsedLineNumber);
                    if (impericalLineNumber != 0)
                    {
                        prefix += impericalLineNumber;
                    }
                    else if (parsedLineNumber != 0)
                    {
                        prefix += parsedLineNumber;
                    }
                    else
                    {
                        prefix += "Unknown";
                    }
                    prefix += "-" + _currentPluginVersionInt + ": ";
                }
                //Check if the exception attributes to the database
                ConsoleWrite(prefix + aException, ConsoleMessageType.Exception);
                StartAndLogThread(new Thread(new ThreadStart(delegate
                {
                    Thread.CurrentThread.Name = "AdKatsExceptionInformThread";
                    var requestHashtable = new Hashtable{
                         {"caller_identity", "AdKatsLRT"},
                         {"response_requested", false},
                         {"command_type", "adkats_exception"},
                         {"source_name", "AdKatsLRT"},
                         {"target_name", "AdKatsLRT"},
                         {"record_message", prefix + aException.ToString()}
                    };
                    ExecuteCommand("procon.protected.plugins.call", "AdKats", "IssueCommand", "AdKatsLRT", JSON.JsonEncode(requestHashtable));
                    LogThreadExit();
                })));
                return aException;
            }
            catch (Exception e)
            {
                ConsoleWrite(e.ToString(), ConsoleMessageType.Exception);
            }
            return null;
        }

        public class AdKatsSubscribedPlayer {
            public Int64 player_id;
            public String player_guid;
            public String player_pbguid;
            public String player_ip;
            public String player_name;
            public String player_personaID;
            public Boolean player_aa;
            public Double player_ping;
            public Double player_reputation;
            public Int32 player_infractionPoints;
            public String player_role;
            public String player_type;
            public Boolean player_isAdmin;
            public Boolean player_reported;
            public Boolean player_punished;
            public Boolean player_marked;
            public Boolean player_spawnedOnce;
            public String player_conversationPartner;
            public Int32 player_kills;
            public Int32 player_deaths;
            public Double player_kdr;
            public Int32 player_rank;
            public Int32 player_score;
            public Int32 player_squad;
            public Int32 player_team;

            public Boolean player_loadoutValid = true;
            public Boolean player_loadoutEnforced = false;
            public Boolean player_loadoutKilled = false;
        }


        public class WarsawLibrary {
            public Dictionary<String, WarsawWeapon> Weapons;
            public Dictionary<String, WarsawWeaponAccessory> WeaponAccessories;
            public Dictionary<String, WarsawKitItem> KitItems;
            public Dictionary<String, WarsawVehicleUnlock> VehicleUnlocks;

            public WarsawLibrary() {
                Weapons = new Dictionary<string, WarsawWeapon>();
                WeaponAccessories = new Dictionary<string, WarsawWeaponAccessory>();
                KitItems = new Dictionary<string, WarsawKitItem>();
                VehicleUnlocks = new Dictionary<string, WarsawVehicleUnlock>();
            }
        }

        public class WarsawWeapon
        {
            //only take entries with numeric IDs
            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
            //Parsed categoryType does not make any modifications
            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
            //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
            //If expansion exists assign it, if not, ignore
            public String warsawID;
            public String category;
            public String name;
            public String categoryType;
            public String slug;
            public Dictionary<String, WarsawWeaponAccessory> WeaponAccessories;

            public WarsawWeapon() {
                WeaponAccessories = new Dictionary<string, WarsawWeaponAccessory>();
            }
        }

        public class WarsawWeaponAccessory
        {
            //only take entries with numeric IDs
            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
            //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
            //If expansion exists assign it, if not, ignore
            public String warsawID;
            public String category;
            public String categoryReadable;
            public String slug;
            public String slugReadable;
            public String name;
        }

        public class WarsawVehicleUnlock
        {
            public String warsawID;
            public String category;
            public String slug;
        }

        public class WarsawKitItem
        {
            //only take entries with numeric IDs
            //Reject parsing of kit items for categories not gadget, or grenade
            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
            //Parsed slug removes ending digits, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
            public String warsawID;
            public String category;
            public String name;
            public String slug;
            public String desc;
        }

        public class AdKatsLoadout {
            public enum KitType {
                Assault,
                Engineer,
                Support,
                Recon
            }

            public String Name;
            public String PersonaID;
            public Dictionary<String, WarsawWeapon> LoadoutWeapons;
            public Dictionary<String, WarsawVehicleUnlock> VehicleUnlocks; 
            public KitType SelectedKit;
            public Dictionary<String, WarsawWeapon> KitWeapons;
            public Dictionary<String, WarsawKitItem> KitItems;
            public HashSet<String> AllKitIDs; 

            public AdKatsLoadout() {
                LoadoutWeapons = new Dictionary<String, WarsawWeapon>();
                VehicleUnlocks = new Dictionary<String, WarsawVehicleUnlock>();
                KitWeapons = new Dictionary<String, WarsawWeapon>();
                KitItems = new Dictionary<String, WarsawKitItem>();
                AllKitIDs = new HashSet<String>();
            }
        }

        public class AdKatsException
        {
            public System.Exception InternalException = null;
            public String Message = String.Empty;
            public String Method = String.Empty;
            //Param Constructors
            public AdKatsException(String message, System.Exception internalException)
            {
                Method = new StackFrame(1).GetMethod().Name;
                Message = message;
                InternalException = internalException;
            }

            public AdKatsException(String message)
            {
                Method = new StackFrame(1).GetMethod().Name;
                Message = message;
            }

            //Override toString
            public override String ToString()
            {
                return "[" + Method + "][" + Message + "]" + ((InternalException != null) ? (": " + InternalException) : (""));
            }
        }
    }
}