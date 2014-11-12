/* 
 * AdKats LRT - Loadout Restriction Tool extention for AdKats.
 * 
 * AdKats and respective extensions are inspired by the gaming community A Different Kind (ADK). 
 * Visit http://www.ADKGamers.com/ for more information.
 *
 * The AdKats Frostbite Plugin is open source, and under public domain, but certain extensions are not. 
 * The AdKats LRT extension is not open for free distribution, copyright Daniel J. Gradinjan, with all rights reserved.
 * 
 * Development by Daniel J. Gradinjan (ColColonCleaner)
 * 
 * AdKatsLRT.cs
 * Version 0.0.0.1
 * 11-NOV-2014
 * 
 * Automatic Update Information
 * <version_code>0.0.0.1</version_code>
 */

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Forms;
using System.CodeDom.Compiler;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Globalization;
using PRoCon.Core;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using Microsoft.CSharp;
using MySql.Data.MySqlClient;

namespace PRoConEvents {
    public class AdKatsLRT : PRoConPluginAPI, IPRoConPluginInterface {
        //Current Plugin Version
        private const String PluginVersion = "0.0.0.1";

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
        private Boolean _firstPlayerListStarted;
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

        //Threads
        private Thread _Activator;
        private Thread _Finalizer;
        private Thread _SpawnProcessingThread;

        //Threading queues
        private readonly Queue<String> _SpawnProcessingQueue = new Queue<String>();

        //Threading wait handles
        private EventWaitHandle _threadMasterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _SpawnProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);

        private WarsawLibrary _warsawLibrary = new WarsawLibrary();

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
            return "AdKats Extension - Loadout Restriction Tool";
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
            String concat = @"
            <p>
                <a href='https://github.com/AdKats/AdKats' name=adkats>
                    <img src='https://raw.githubusercontent.com/AdKats/AdKatsLRT/master/images/AdKats.jpg' alt='AdKats Advanced In-Game Admin Tools'>
                </a>
            </p>";
            try
            {

                //Parse out the descriptions
                if (!String.IsNullOrEmpty(_pluginVersionStatusString))
                {
                    concat += _pluginVersionStatusString;
                }
                if (!String.IsNullOrEmpty(_pluginLinks))
                {
                    concat += _pluginLinks;
                }
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while fetching plugin information.", e));
            }
            return "";
        }

        public List<CPluginVariable> GetDisplayPluginVariables() {
            try {
                var lstReturn = new List<CPluginVariable>();
                const string separator = " | ";
                if (_warsawLibrary.Weapons.Any())
                {
                    Int32 weaponWarsawMaxChar = _warsawLibrary.Weapons.Values.Max(weapon => weapon.warsawID.Length);
                    foreach (var weapon in _warsawLibrary.Weapons.Values.OrderBy(weapon => weapon.category).ThenBy(weapon => weapon.slug))
                    {
                        lstReturn.Add(new CPluginVariable("3. Weapons" + separator + weapon.warsawID + separator + weapon.categoryType + separator + weapon.slug + separator + "Allow?", typeof(Boolean), true));
                    }
                }
                if (_warsawLibrary.KitItems.Any())
                {
                    Int32 kitItemMaxChar = _warsawLibrary.KitItems.Values.Max(kitItem => kitItem.warsawID.Length);
                    foreach (var kitItem in _warsawLibrary.KitItems.Values.OrderBy(kitItem => kitItem.category).ThenBy(kitItem => kitItem.slug))
                    {
                        lstReturn.Add(new CPluginVariable("4. Kit Items" + separator + kitItem.warsawID + separator + kitItem.category + separator + kitItem.slug + separator + "Allow?", typeof(Boolean), true));
                    }
                }
                if (_warsawLibrary.WeaponAccessories.Any())
                {
                    Int32 weaponAccessoryWarsawMaxChar = _warsawLibrary.WeaponAccessories.Values.Max(accessory => accessory.warsawID.Length);
                    foreach (var weaponAccessory in _warsawLibrary.WeaponAccessories.Values.OrderBy(weaponAccessory => weaponAccessory.slug).ThenBy(weaponAccessory => weaponAccessory.category))
                    {
                        lstReturn.Add(new CPluginVariable("6. Weapon Accessories" + separator + weaponAccessory.warsawID + separator + weaponAccessory.slug + separator + "Allow?", typeof(Boolean), true));
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

            lstReturn.Add(new CPluginVariable("0. Instance Settings|Auto-Enable/Keep-Alive", typeof(Boolean), true));

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
                    /*
                else if (Regex.Match(strVariable, @"Auto-Enable/Keep-Alive").Success) {
                    Boolean autoEnable = Boolean.Parse(strValue);
                    if (autoEnable != _useKeepAlive) {
                        if (autoEnable)
                            Enable();
                        _useKeepAlive = autoEnable;
                    }
                }
                else if (Regex.Match(strVariable, @"Debug Soldier Name").Success) {
                    if (SoldierNameValid(strValue)) {
                        if (strValue != _debugSoldierName) {
                            _debugSoldierName = strValue;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Rule Print Delay").Success)
                {
                    Double delay;
                    if (!Double.TryParse(strValue, out delay))
                    {
                        HandleException(new AdKatsException("Error parsing double value for setting '" + strVariable + "'"));
                        return;
                    }
                    if (_ServerRulesDelay != delay)
                    {
                        if (delay <= 0)
                        {
                            ConsoleError("Delay cannot be negative.");
                            delay = 1.0;
                        }
                        _ServerRulesDelay = delay;
                    }
                }
                else if (Regex.Match(strVariable, @"AFK Minimum Players").Success)
                {
                    Int32 afkAutoKickMinimumPlayers = Int32.Parse(strValue);
                    if (_AFKTriggerMinimumPlayers != afkAutoKickMinimumPlayers)
                    {
                        if (afkAutoKickMinimumPlayers < 0)
                        {
                            ConsoleError("Minimum players cannot be negative.");
                            return;
                        }
                        _AFKTriggerMinimumPlayers = afkAutoKickMinimumPlayers;
                    }
                }
                else if (Regex.Match(strVariable, @"External plugin admin commands").Success)
                {
                    _ExternalAdminCommands = new List<String>(CPluginVariable.DecodeStringArray(strValue));
                }
                else if (strVariable.StartsWith("RLE"))
                {
                    //Trim off all but the role ID and section
                    //RLE1 | Default Guest | CDE3 | Kill Player

                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String roleIDStr = commandSplit[0].TrimStart("RLE".ToCharArray()).Trim();
                    Int32 roleID = Int32.Parse(roleIDStr);

                    //If second section is a command prefix, this is the allow/deny clause
                    if (commandSplit[2].Trim().StartsWith("CDE"))
                    {
                        String commandIDStr = commandSplit[2].Trim().TrimStart("CDE".ToCharArray());
                        Int32 commandID = Int32.Parse(commandIDStr);

                        //Fetch needed role
                        switch (strValue.ToLower())
                        {
                            case "allow":
                                //parse allow
                                break;
                            case "deny":
                                //parse deny
                                break;
                            default:
                                ConsoleError("Unknown setting when assigning command allowance.");
                                return;
                        }
                    }
                }*/
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while updating AdKats settings.", e));
            }
        }

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
            DebugWrite("Entering OnPluginLoaded", 7);
            try {
                //Register all events
                RegisterEvents(GetType().Name, 
                    "OnVersion", 
                    "OnPlayerSpawned");
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
                //Create a new thread to activate the plugin
                _Activator = new Thread(new ThreadStart(delegate {
                    try {
                        Thread.CurrentThread.Name = "enabler";

                        if ((DateTime.UtcNow - _proconStartTime).TotalSeconds <= 20) {
                            ConsoleWrite("Waiting a few seconds for requirements and other plugins to initialize, please wait...");
                            //Wait on all settings to be imported by procon for initial start, and for all other plugins to start and register.
                            for (Int32 index = 20 - (Int32)(DateTime.UtcNow - _proconStartTime).TotalSeconds; index > 0; index--)
                            {
                                ConsoleWrite(index + "...");
                                _threadMasterWaitHandle.WaitOne(1000);
                            }
                        }

                        //Set the enabled variable
                        _pluginEnabled = true;

                        //Fetch all weapon names
                        if (LoadWarsawLibrary())
                        {
                            ConsoleSuccess("Warsaw library loaded. " + _warsawLibrary.Weapons.Count + " weapons, " + _warsawLibrary.WeaponAccessories.Count + " accessories, and " + _warsawLibrary.KitItems.Count + " kit items.");
                            UpdateSettingPage();

                            //Init and start all the threads
                            //InitWaitHandles();
                            //OpenAllHandles();
                            //InitThreads();
                            //StartThreads();
                        }
                        else
                        {
                            ConsoleError("Failed to load warsaw library. AdKatsLRT cannot be started.");
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
                        _firstPlayerListStarted = false;
                        _slowmo = false;
                        ConsoleWrite("^b^1AdKatsLRT " + GetPluginVersion() + " Disabled! =(^n^0");
                    }
                    catch (Exception e) {
                        HandleException(new AdKatsException("Error occured while disabling Adkats.", e));
                    }
                }));

                //Start the finalizer thread
                _Finalizer.Start();
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error occured while initializing AdKats disable thread.", e));
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
                        ConsoleError("Unable to fetch required documentation files. AdKats cannot be started.");
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
            //Create a new thread to handle keep-alive
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

                            //Check for keep alive every 30 seconds
                            if ((DateTime.UtcNow - lastKeepAliveCheck).TotalSeconds > 30)
                            {
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
            _SpawnProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void OpenAllHandles() {
            _threadMasterWaitHandle.Set();
            _SpawnProcessingWaitHandle.Set();
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

        private void QueueSpawnForProcessing(String playerName)
        {
            DebugWrite("Entering queueKillForProcessing", 7);
            try
            {
                if (_pluginEnabled)
                {
                    DebugWrite("Preparing to queue kill for processing", 6);
                    lock (_SpawnProcessingQueue)
                    {
                        _SpawnProcessingQueue.Enqueue(playerName);
                        DebugWrite("Kill queued for processing", 6);
                        _SpawnProcessingWaitHandle.Set();
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while queueing spawn for processing.", e));
            }
            DebugWrite("Exiting queueKillForProcessing", 7);
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

                        //Get all unprocessed inbound spawns
                        Queue<String> inboundPlayerSpawns;
                        if (_SpawnProcessingQueue.Count > 0)
                        {
                            DebugWrite("SPROC: Preparing to lock inbound spawn queue to retrive new player spawns", 7);
                            lock (_SpawnProcessingQueue)
                            {
                                DebugWrite("SPROC: Inbound spawns found. Grabbing.", 6);
                                //Grab all spawns in the queue
                                inboundPlayerSpawns = new Queue<String>(_SpawnProcessingQueue.ToArray());
                                //Clear the queue for next run
                                _SpawnProcessingQueue.Clear();
                            }
                        }
                        else
                        {
                            DebugWrite("SPROC: No inbound player spawns. Waiting for Input.", 6);
                            //Wait for input
                            if ((DateTime.UtcNow - loopStart).TotalMilliseconds > 1000)
                                DebugWrite("Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((DateTime.UtcNow - loopStart).TotalMilliseconds)) + "ms", 4);
                            _SpawnProcessingWaitHandle.Reset();
                            _SpawnProcessingWaitHandle.WaitOne(Timeout.Infinite);
                            loopStart = DateTime.UtcNow;
                            continue;
                        }

                        //Loop through all spawns in order that they came in
                        while (inboundPlayerSpawns.Count > 0)
                        {
                            if (!_pluginEnabled)
                            {
                                break;
                            }
                            DebugWrite("SPROC: begin reading player spawns", 6);
                            //Dequeue the first/next spawn
                            String playerName = inboundPlayerSpawns.Dequeue();

                            //Call processing on the player spawn
                            ProcessPlayerSpawn(playerName);
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

        public void ProcessPlayerSpawn(String playerName) {
            
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
                //Fetch from BF3Stats
                Hashtable responseData = null;
                if (_gameVersion == GameVersion.BF4) {
                    WarsawLibrary library = new WarsawLibrary();
                    ConsoleInfo("Loading WARSAW library.");
                    responseData = FetchWarsawCodeBook();
                    if (responseData == null) {
                        ConsoleError("Warsaw codebook fetch failed, unable to generate library.");
                        return false;
                    }
                    if (!responseData.ContainsKey("compact")) {
                        ConsoleError("Warsaw codebook fetch did not contain 'compact' element, unable to generate library.");
                        return false;
                    }
                    Hashtable compact = (Hashtable) responseData["compact"];
                    if (compact == null) {
                        ConsoleError("Compact section of warsaw codebook failed parse, unable to generate library.");
                        return false;
                    }
                    if (!compact.ContainsKey("weapons")) {
                        ConsoleError("Warsaw compact did not contain 'weapons' element, unable to generate library.");
                        return false;
                    }
                    if (!compact.ContainsKey("weaponaccessory")) {
                        ConsoleError("Warsaw compact did not contain 'weaponaccessory' element, unable to generate library.");
                        return false;
                    }
                    if (!compact.ContainsKey("kititems")) {
                        ConsoleError("Warsaw compact did not contain 'kititems' element, unable to generate library.");
                        return false;
                    }
                    Hashtable weapons = (Hashtable) compact["weapons"];
                    if (weapons == null) {
                        ConsoleError("Weapons section of warsaw codebook failed parse, unable to generate library.");
                        return false;
                    }
                    ConsoleInfo("WARSAW formed correctly, beginning parse.");
                    try
                    {
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

                            //Grab the contents
                            Hashtable weaponData = (Hashtable)entry.Value;
                            //Grab category------------------------------------------------------------------------------
                            if (!weaponData.ContainsKey("category"))
                            {
                                //ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'category'.");
                                continue;
                            }
                            weapon.category = (String)weaponData["category"];
                            if (String.IsNullOrEmpty(weapon.category))
                            {
                                //ConsoleError("Rejecting weapon '" + warsawID + "'. 'category' was invalid.");
                                continue;
                            }
                            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
                            weapon.category = weapon.category.Split('_').Last().Replace('_', ' ').ToUpper();
                            //weapon.category = weapon.category.TrimStart("WARSAW_ID_P_CAT_".ToCharArray()).Replace('_', ' ').ToLower();

                            //Grab name------------------------------------------------------------------------------
                            if (!weaponData.ContainsKey("name"))
                            {
                                //ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'name'.");
                                continue;
                            }
                            weapon.name = (String)weaponData["name"];
                            if (String.IsNullOrEmpty(weapon.name))
                            {
                                //ConsoleError("Rejecting weapon '" + warsawID + "'. 'name' was invalid.");
                                continue;
                            }
                            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                            weapon.name = weapon.name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                            //Grab categoryType------------------------------------------------------------------------------
                            if (!weaponData.ContainsKey("categoryType"))
                            {
                                //ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'categoryType'.");
                                continue;
                            }
                            weapon.categoryType = (String)weaponData["categoryType"];
                            if (String.IsNullOrEmpty(weapon.category))
                            {
                                //ConsoleError("Rejecting weapon '" + warsawID + "'. 'categoryType' was invalid.");
                                continue;
                            }
                            //Parsed categoryType does not require any modifications

                            //Grab slug------------------------------------------------------------------------------
                            if (!weaponData.ContainsKey("slug"))
                            {
                                //ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'slug'.");
                                continue;
                            }
                            weapon.slug = (String)weaponData["slug"];
                            if (String.IsNullOrEmpty(weapon.slug))
                            {
                                //ConsoleError("Rejecting weapon '" + warsawID + "'. 'slug' was invalid.");
                                continue;
                            }
                            //Parsed slug replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                            weapon.slug = weapon.slug.Replace('_', ' ').Replace('-', ' ').ToUpper();

                            //Assign the weapon
                            weaponDictionary[weapon.warsawID] = weapon;
                            //ConsoleSuccess("Weapon " + weapon.warsawID + " added.");
                        }
                        //Assign the new built dictionary
                        library.Weapons = weaponDictionary;
                        ConsoleInfo("WARSAW weapons parsed.");
                    }
                    catch (Exception e) {
                        ConsoleError(e.ToString());
                        ConsoleError("Error while parsing weapons.");
                        return false;
                    }

                    Hashtable weaponaccessory = (Hashtable) compact["weaponaccessory"];
                    if (weaponaccessory == null) {
                        ConsoleError("Weapon accessory section of warsaw codebook failed parse, unable to generate library.");
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

                    Hashtable kititems = (Hashtable) compact["kititems"];
                    if (kititems == null) {
                        ConsoleError("Kit items section of warsaw codebook failed parse, unable to generate library.");
                        return false;
                    }
                    Dictionary<String, WarsawKitItem> kitItemsDictionary = new Dictionary<String, WarsawKitItem>();
                    foreach (DictionaryEntry entry in kititems) {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String) entry.Key, out warsawID)) {
                            //Reject the entry
                            //ConsoleError("Rejecting kit item element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        WarsawKitItem kitItem = new WarsawKitItem();
                        kitItem.warsawID = warsawID.ToString();

                        //Grab the contents
                        Hashtable weaponAccessoryData = (Hashtable) entry.Value;
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
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        kitItem.slug = (String) weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(kitItem.slug)) {
                            //ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        kitItem.slug = kitItem.slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        kitItemsDictionary[kitItem.warsawID] = kitItem;
                    }
                    library.KitItems = kitItemsDictionary;
                    ConsoleInfo("WARSAW kit items parsed.");
                    _warsawLibrary = library;
                    return true;
                }
                ConsoleError("Game not BF4, unable to process warsaw library.");
                return false;
            }
            catch (Exception e) {
                HandleException(new AdKatsException("Error while parsing warsaw codebook.", e));
            }
            DebugWrite("Exiting LoadWarsawLibrary", 7);
            return false;
        }

        private Hashtable FetchWarsawCodeBook() {
            Hashtable codebook = null;
            try
            {
                using (var client = new WebClient())
                {
                    try
                    {
                        String response = client.DownloadString("https://raw.githubusercontent.com/AdKats/AdKats-LRT/test/WarsawCodeBook.json?token=AB0Lkwfvlgjp3-4U8T4rrKUrhrYnXYOGks5UbN3HwA%3D%3D");
                        ConsoleInfo(response.Substring(0, 200));
                        codebook = (Hashtable) JSON.JsonDecode(response);
                    }
                    catch (Exception e) {
                        HandleException(new AdKatsException("Error while loading warsaw codebook raw.", e));
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Unexpected error while fetching warsaw codebook", e));
                return null;
            }
            return codebook;
        }

        public string FetchBF4PersonaID(string playerName)
        {
            try
            {
                using (var client = new WebClient())
                {
                    try
                    {
                        String response = client.DownloadString("http://battlelog.battlefield.com/bf4/user/" + playerName);
                        Match pid = Regex.Match(response, @"bf4/soldier/" + playerName + @"/stats/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        if (!pid.Success) {
                            HandleException(new AdKatsException("Could not find persona ID for " + playerName));
                            return null;
                        }
                        return pid.Groups[1].Value.Trim();
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while finding persona ID for " + playerName, e));
                return null;
            }
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

        public AdKatsException HandleException(AdKatsException aException)
        {
            try
            {
                //If it's null or AdKats isn't enabled, just return
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
                var requestHashtable = new Hashtable{
                     {"caller_identity", "AdKatsLRT"},
                     {"response_requested", false},
                     {"command_type", "adkats_exception"},
                     {"source_name", "AdKatsLRT"},
                     {"target_name", "AdKatsLRT"},
                     {"record_message", prefix + aException.ToString()}
                };
                ExecuteCommand("procon.protected.plugins.call", "AdKats", "IssueCommand", "AdKatsLRT", JSON.JsonEncode(requestHashtable));
                return aException;
            }
            catch (Exception e)
            {
                ConsoleWrite(e.ToString(), ConsoleMessageType.Exception);
            }
            return null;
        }

        public class AdKatsLRTPlayer {
            public String player_name;
            public String player_personaID;
            public DateTime lastRequest = DateTime.UtcNow;
        }


        public class WarsawLibrary {
            public Dictionary<String, WarsawWeapon> Weapons;
            public Dictionary<String, WarsawWeaponAccessory> WeaponAccessories;
            public Dictionary<String, WarsawKitItem> KitItems;
            public Dictionary<String, WarsawVehicle> Vehicles;

            public WarsawLibrary() {
                Weapons = new Dictionary<string, WarsawWeapon>();
                WeaponAccessories = new Dictionary<string, WarsawWeaponAccessory>();
                KitItems = new Dictionary<string, WarsawKitItem>();
                Vehicles = new Dictionary<string, WarsawVehicle>();
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

        public class WarsawVehicle {
        }

        public class WarsawVehicleUnlock {
            
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
            public String categoryParsed;
            public String name;
            public String nameParsed;
            public String slug;
            public String slugParsed;
            public String desc;
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