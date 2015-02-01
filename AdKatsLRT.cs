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
 * Version 2.0.1.4
 * 1-FEB-2014
 * 
 * Automatic Update Information
 * <version_code>2.0.1.4</version_code>
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public class AdKatsLRT : PRoConPluginAPI, IPRoConPluginInterface
    {
        //Current Plugin Version
        private const String PluginVersion = "2.0.1.4";

        public enum ConsoleMessageType
        {
            Normal,
            Info,
            Success,
            Warning,
            Error,
            Exception
        };

        public enum GameVersion
        {
            BF3,
            BF4
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
        private readonly Dictionary<String, String> _WARSAWInvalidVehicleLoadoutIDMessages = new Dictionary<String, String>(); 
        private readonly HashSet<String> _WARSAWSpawnDeniedIDs = new HashSet<String>();
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
        private Boolean _spawnEnforceAllVehicles;

        //Display
        private Boolean _displayPresets;
        private Boolean _displayMapsModes;
        private Boolean _displayWeapons;
        private Boolean _displayWeaponAccessories;
        private Boolean _displayGadgets;
        private Boolean _displayVehicles;

        //Maps Modes
        private Boolean _restrictSpecificMapModes;
        private List<MapMode> _availableMapModes = new List<MapMode>();
        private readonly Dictionary<String, MapMode> _restrictedMapModes = new Dictionary<String, MapMode>(); 

        //Presets
        private Boolean _presetDenyFragRounds;
        private readonly List<String> fragRoundIDs = new List<String>(new String[] {
            "4292296724", //M26
            "892280283", //DAO
            "956347287", //DBV
            "3314744268", //UTS
            "3318621920", //QBS
            "4102933276", //SAIGA
            "3144055563", //870
            "1996239480", //1014
            "3104246933", //Hawk
            "2346703083" //SPAS
        });
        private Boolean _presetDenyExplosives;
        private Boolean _presetDenyFlaresSmokeFlash;
        private Boolean _presetDenyBipods;

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

            //Populate maps/modes
            PopulateMapModes();

            //Set defaults for webclient
            ServicePointManager.Expect100Continue = false;

            //By default plugin is not enabled or ready
            _pluginEnabled = false;
            _threadsReady = false;

            //Debug level is 0 by default
            _debugLevel = 0;

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
            return "[ADK]ColColonCleaner";
        }

        public String GetPluginWebsite()
        {
            return "https://github.com/AdKats/";
        }

        public String GetPluginDescription()
        {
            return "";
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            var lstReturn = new List<CPluginVariable>();
            try
            {
                const string separator = " | ";

                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Integrate with AdKats", typeof(Boolean), _enableAdKatsIntegration));
                if (_enableAdKatsIntegration)
                {
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Admins", typeof(Boolean), _spawnEnforcementActOnAdmins));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Reputable Players", typeof(Boolean), _spawnEnforcementActOnReputablePlayers));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Trigger Enforce Minimum Infraction Points", typeof(Int32), _triggerEnforcementMinimumInfractionPoints));
                }
                if (!_WARSAWLibraryLoaded)
                {
                    lstReturn.Add(new CPluginVariable("The WARSAW library must be loaded to view settings.", typeof(String), "Enable the plugin to fetch the library."));
                    return lstReturn;
                }

                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Preset Settings", typeof(Boolean), _displayPresets));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Map/Mode Settings", typeof(Boolean), _displayMapsModes));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Settings", typeof(Boolean), _displayWeapons));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Accessory Settings", typeof(Boolean), _displayWeaponAccessories));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Gadget Settings", typeof(Boolean), _displayGadgets));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Vehicle Settings", typeof(Boolean), _displayVehicles));

                if (_displayPresets)
                {
                    lstReturn.Add(new CPluginVariable(SettingsPresetPrefix + "Presets Coming Soon", typeof(String), "Presets Coming Soon"));
                    //lstReturn.Add(new CPluginVariable(presetPrefix + "Preset Deny Frag Rounds", typeof(Boolean), _presetDenyFragRounds));
                    //lstReturn.Add(new CPluginVariable(presetPrefix + "Preset Deny Explosives", typeof(Boolean), _presetDenyExplosives));
                    //lstReturn.Add(new CPluginVariable(presetPrefix + "Preset Deny Flares/Smoke/Flash", typeof(Boolean), _presetDenyFlaresSmokeFlash));
                    //lstReturn.Add(new CPluginVariable(presetPrefix + "Preset Deny Bipods", typeof(Boolean), _presetDenyBipods));
                }

                if (_displayMapsModes)
                {
                    lstReturn.Add(new CPluginVariable(SettingsMapModePrefix + separator.Trim() + "Enforce on Specific Maps/Modes Only", typeof(Boolean), _restrictSpecificMapModes));
                    if (_restrictSpecificMapModes)
                    {
                        foreach (MapMode mapMode in _availableMapModes.OrderBy(mm => mm.ModeName).ThenBy(mm => mm.MapName))
                        {
                            lstReturn.Add(new CPluginVariable(SettingsMapModePrefix + " - " + mapMode.ModeName + separator.Trim() + "RMM" + mapMode.MapModeID.ToString().PadLeft(3, '0') + separator + mapMode.MapName + separator + "Enforce?", "enum.EnforceMapEnum(Enforce|Ignore)", _restrictedMapModes.ContainsKey(mapMode.ModeKey + "|" + mapMode.MapKey) ? ("Enforce") : ("Ignore")));
                        }
                    }
                }

                //Run removals
                _WARSAWSpawnDeniedIDs.RemoveWhere(spawnID => !_WARSAWInvalidLoadoutIDMessages.ContainsKey(spawnID) && !_WARSAWInvalidVehicleLoadoutIDMessages.ContainsKey(spawnID));
                
                if (_displayWeapons)
                {
                    if (_WARSAWLibrary.Items.Any())
                    {
                        foreach (WarsawItem weapon in _WARSAWLibrary.Items.Values.Where(weapon => weapon.CategoryReadable != "GADGET").OrderBy(weapon => weapon.CategoryReadable).ThenBy(weapon => weapon.Slug))
                        {
                            if (_enableAdKatsIntegration)
                            {
                                lstReturn.Add(new CPluginVariable(SettingsWeaponPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on trigger?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID))
                                {
                                    lstReturn.Add(new CPluginVariable(SettingsWeaponPrefix + weapon.CategoryTypeReadable + "|ALWS" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                }
                            }
                            else
                            {
                                lstReturn.Add(new CPluginVariable(SettingsWeaponPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                if (_displayWeaponAccessories)
                {
                    if (_WARSAWLibrary.ItemAccessories.Any())
                    {
                        foreach (WarsawItemAccessory weaponAccessory in _WARSAWLibrary.ItemAccessories.Values.OrderBy(weaponAccessory => weaponAccessory.Slug).ThenBy(weaponAccessory => weaponAccessory.CategoryReadable))
                        {
                            if (_enableAdKatsIntegration)
                            {
                                lstReturn.Add(new CPluginVariable(SettingsAccessoryPrefix + weaponAccessory.CategoryReadable + "|ALWT" + weaponAccessory.WarsawID + separator + weaponAccessory.Slug + separator + "Allow on trigger?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID) ? ("Deny") : ("Allow")));
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID))
                                {
                                    lstReturn.Add(new CPluginVariable(SettingsAccessoryPrefix + weaponAccessory.CategoryReadable + "|ALWS" + weaponAccessory.WarsawID + separator + weaponAccessory.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(weaponAccessory.WarsawID) ? ("Deny") : ("Allow")));
                                }
                            }
                            else
                            {
                                lstReturn.Add(new CPluginVariable(SettingsAccessoryPrefix + weaponAccessory.CategoryReadable + "|ALWT" + weaponAccessory.WarsawID + separator + weaponAccessory.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(weaponAccessory.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                if (_displayGadgets)
                {
                    if (_WARSAWLibrary.Items.Any())
                    {
                        foreach (WarsawItem weapon in _WARSAWLibrary.Items.Values.Where(weapon => weapon.CategoryReadable == "GADGET").OrderBy(weapon => weapon.CategoryReadable).ThenBy(weapon => weapon.Slug))
                        {
                            if (String.IsNullOrEmpty(weapon.CategoryTypeReadable))
                            {
                                ConsoleError(weapon.WarsawID + " did not have a category type.");
                            }
                            if (_enableAdKatsIntegration)
                            {
                                lstReturn.Add(new CPluginVariable(SettingsGadgetPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on trigger?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID))
                                {
                                    lstReturn.Add(new CPluginVariable(SettingsGadgetPrefix + weapon.CategoryTypeReadable + "|ALWS" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                }
                            }
                            else
                            {
                                lstReturn.Add(new CPluginVariable(SettingsGadgetPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                if (_displayVehicles)
                {
                    lstReturn.Add(new CPluginVariable(SettingsVehiclePrefix + separator.Trim() + "Spawn Enforce all Vehicles", typeof(Boolean), _spawnEnforceAllVehicles));
                    if (_WARSAWLibrary.Vehicles.Any())
                    {
                        foreach (var vehicle in _WARSAWLibrary.Vehicles.Values.OrderBy(vec => vec.CategoryType))
                        {
                            String currentPrefix = SettingsVehiclePrefix + " - " + vehicle.CategoryType + "|";
                            foreach (var unlock in vehicle.AllowedPrimaries.Values)
                            {
                                lstReturn.Add(new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles)?("spawn"):("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow")));
                            }
                            foreach (var unlock in vehicle.AllowedSecondaries.Values)
                            {
                                lstReturn.Add(new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow")));
                            }
                            foreach (var unlock in vehicle.AllowedCountermeasures.Values)
                            {
                                lstReturn.Add(new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow")));
                            }
                            foreach (var unlock in vehicle.AllowedOptics.Values)
                            {
                                lstReturn.Add(new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow")));
                            }
                            foreach (var unlock in vehicle.AllowedUpgrades.Values)
                            {
                                lstReturn.Add(new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow")));
                            }
                            foreach (var unlock in vehicle.AllowedSecondariesGunner.Values)
                            {
                                lstReturn.Add(new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow")));
                            }
                            foreach (var unlock in vehicle.AllowedOpticsGunner.Values)
                            {
                                lstReturn.Add(new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow")));
                            }
                            foreach (var unlock in vehicle.AllowedUpgradesGunner.Values)
                            {
                                lstReturn.Add(new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _WARSAWInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                foreach (var pair in _WARSAWInvalidLoadoutIDMessages.Where(denied => _WARSAWLibrary.Items.ContainsKey(denied.Key)))
                {
                    WarsawItem deniedItem;
                    if (_WARSAWLibrary.Items.TryGetValue(pair.Key, out deniedItem))
                    {
                        lstReturn.Add(new CPluginVariable(SettingsDeniedItemMessagePrefix + "MSG" + deniedItem.WarsawID + separator + deniedItem.Slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                foreach (var pair in _WARSAWInvalidLoadoutIDMessages.Where(denied => _WARSAWLibrary.ItemAccessories.ContainsKey(denied.Key)))
                {
                    WarsawItemAccessory deniedItemAccessory;
                    if (_WARSAWLibrary.ItemAccessories.TryGetValue(pair.Key, out deniedItemAccessory))
                    {
                        lstReturn.Add(new CPluginVariable(SettingsDeniedItemAccMessagePrefix + "MSG" + deniedItemAccessory.WarsawID + separator + deniedItemAccessory.Slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                foreach (var pair in _WARSAWInvalidVehicleLoadoutIDMessages.Where(denied => _WARSAWLibrary.VehicleUnlocks.ContainsKey(denied.Key)))
                {
                    WarsawItem deniedVehicleUnlock;
                    if (_WARSAWLibrary.VehicleUnlocks.TryGetValue(pair.Key, out deniedVehicleUnlock))
                    {
                        lstReturn.Add(new CPluginVariable(SettingsDeniedVehicleItemMessagePrefix + "VMSG" + deniedVehicleUnlock.WarsawID + separator + deniedVehicleUnlock.Slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof(int), _debugLevel));
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while getting display plugin variables", e));
            }
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            var lstReturn = new List<CPluginVariable>();
            const string separator = " | ";
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Integrate with AdKats", typeof(Boolean), _enableAdKatsIntegration));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Admins", typeof(Boolean), _spawnEnforcementActOnAdmins));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Reputable Players", typeof(Boolean), _spawnEnforcementActOnReputablePlayers));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Trigger Enforce Minimum Infraction Points", typeof(Int32), _triggerEnforcementMinimumInfractionPoints));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Preset Settings", typeof(Boolean), _displayPresets));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Map/Mode Settings", typeof(Boolean), _displayMapsModes));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Settings", typeof(Boolean), _displayWeapons));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Accessory Settings", typeof(Boolean), _displayWeaponAccessories));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Gadget Settings", typeof(Boolean), _displayGadgets));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Vehicle Settings", typeof(Boolean), _displayVehicles));
            lstReturn.Add(new CPluginVariable(SettingsPresetPrefix + "Preset Deny Frag Rounds", typeof(Boolean), _presetDenyFragRounds));
            lstReturn.Add(new CPluginVariable(SettingsPresetPrefix + "Preset Deny Explosives", typeof(Boolean), _presetDenyExplosives));
            lstReturn.Add(new CPluginVariable(SettingsPresetPrefix + "Preset Deny Flares/Smoke/Flash", typeof(Boolean), _presetDenyFlaresSmokeFlash));
            lstReturn.Add(new CPluginVariable(SettingsPresetPrefix + "Preset Deny Bipods", typeof(Boolean), _presetDenyBipods));
            lstReturn.Add(new CPluginVariable(SettingsMapModePrefix + "Enforce on Specific Maps/Modes Only", typeof(Boolean), _restrictSpecificMapModes));
            lstReturn.Add(new CPluginVariable(SettingsVehiclePrefix + separator.Trim() + "Spawn Enforce all Vehicles", typeof(Boolean), _spawnEnforceAllVehicles));
            foreach (var pair in _WARSAWInvalidLoadoutIDMessages)
            {
                lstReturn.Add(new CPluginVariable("MSG" + pair.Key, typeof(String), pair.Value));
            }
            foreach (var pair in _WARSAWInvalidVehicleLoadoutIDMessages)
            {
                lstReturn.Add(new CPluginVariable("VMSG" + pair.Key, typeof(String), pair.Value));
            }
            _WARSAWSpawnDeniedIDs.RemoveWhere(spawnID => !_WARSAWInvalidLoadoutIDMessages.ContainsKey(spawnID));
            foreach (var deniedSpawnID in _WARSAWSpawnDeniedIDs)
            {
                lstReturn.Add(new CPluginVariable("ALWS" + deniedSpawnID, typeof(String), "Deny"));
            }
            foreach (var restrictedMapMode in _restrictedMapModes.Values)
            {
                lstReturn.Add(new CPluginVariable("RMM" + restrictedMapMode.MapModeID, typeof(String), "Enforce"));
            }
            lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof(int), _debugLevel));
            return lstReturn;
        }

        public void SetPluginVariable(String strVariable, String strValue)
        {
            if (strValue == null)
            {
                return;
            }
            try
            {
                if (strVariable == "UpdateSettings")
                {
                    //Do nothing. Settings page will be updated after return.
                }
                else if (Regex.Match(strVariable, @"Debug level").Success)
                {
                    Int32 tmp;
                    if (int.TryParse(strValue, out tmp))
                    {
                        if (tmp == 269)
                        {
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
                        if (_threadsReady)
                        {
                            ConsoleInfo("AdKatsLRT must be rebooted to modify this setting.");
                            Disable();
                        }
                        _enableAdKatsIntegration = enableAdKatsIntegration;
                    }
                }
                else if (Regex.Match(strVariable, @"Display Preset Settings").Success)
                {
                    Boolean displayPresets = Boolean.Parse(strValue);
                    if (displayPresets != _displayPresets)
                    {
                        _displayPresets = displayPresets;
                        if (_displayPresets)
                        {
                            _displayMapsModes = false;
                            _displayWeapons = false;
                            _displayWeaponAccessories = false;
                            _displayGadgets = false;
                            _displayVehicles = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Display Map/Mode Settings").Success)
                {
                    Boolean displayMapsModes = Boolean.Parse(strValue);
                    if (displayMapsModes != _displayMapsModes)
                    {
                        _displayMapsModes = displayMapsModes;
                        if (_displayMapsModes)
                        {
                            _displayPresets = false;
                            _displayWeapons = false;
                            _displayWeaponAccessories = false;
                            _displayGadgets = false;
                            _displayVehicles = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Display Weapon Settings").Success)
                {
                    Boolean displayWeapons = Boolean.Parse(strValue);
                    if (displayWeapons != _displayWeapons)
                    {
                        _displayWeapons = displayWeapons;
                        if (_displayWeapons)
                        {
                            _displayPresets = false;
                            _displayMapsModes = false;
                            _displayWeaponAccessories = false;
                            _displayGadgets = false;
                            _displayVehicles = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Display Weapon Accessory Settings").Success)
                {
                    Boolean displayWeaponAccessories = Boolean.Parse(strValue);
                    if (displayWeaponAccessories != _displayWeaponAccessories)
                    {
                        _displayWeaponAccessories = displayWeaponAccessories;
                        if (_displayWeaponAccessories)
                        {
                            _displayPresets = false;
                            _displayMapsModes = false;
                            _displayWeapons = false;
                            _displayGadgets = false;
                            _displayVehicles = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Display Gadget Settings").Success)
                {
                    Boolean displayGadgets = Boolean.Parse(strValue);
                    if (displayGadgets != _displayGadgets)
                    {
                        _displayGadgets = displayGadgets;
                        if (_displayGadgets)
                        {
                            _displayPresets = false;
                            _displayMapsModes = false;
                            _displayWeapons = false;
                            _displayWeaponAccessories = false;
                            _displayVehicles = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Display Vehicle Settings").Success)
                {
                    Boolean displayVehicles = Boolean.Parse(strValue);
                    if (displayVehicles != _displayVehicles)
                    {
                        _displayVehicles = displayVehicles;
                        if (_displayVehicles)
                        {
                            _displayPresets = false;
                            _displayMapsModes = false;
                            _displayWeapons = false;
                            _displayWeaponAccessories = false;
                            _displayGadgets = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Preset Deny Frag Rounds").Success)
                {
                    Boolean presetDenyFragRounds = Boolean.Parse(strValue);
                    if (presetDenyFragRounds != _presetDenyFragRounds)
                    {
                        _presetDenyFragRounds = presetDenyFragRounds;
                    }
                }
                else if (Regex.Match(strVariable, @"Preset Deny Explosives").Success)
                {
                    Boolean presetDenyExplosives = Boolean.Parse(strValue);
                    if (presetDenyExplosives != _presetDenyExplosives)
                    {
                        _presetDenyExplosives = presetDenyExplosives;
                    }
                }
                else if (Regex.Match(strVariable, @"Preset Deny Flares/Smoke/Flash").Success)
                {
                    Boolean presetDenyFlaresSmokeFlash = Boolean.Parse(strValue);
                    if (presetDenyFlaresSmokeFlash != _presetDenyFlaresSmokeFlash)
                    {
                        _presetDenyFlaresSmokeFlash = presetDenyFlaresSmokeFlash;
                    }
                }
                else if (Regex.Match(strVariable, @"Preset Deny Bipods").Success)
                {
                    Boolean presetDenyBipods = Boolean.Parse(strValue);
                    if (presetDenyBipods != _presetDenyBipods)
                    {
                        _presetDenyBipods = presetDenyBipods;
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
                else if (Regex.Match(strVariable, @"Spawn Enforce all Vehicles").Success)
                {
                    Boolean spawnEnforceAllVehicles = Boolean.Parse(strValue);
                    if (spawnEnforceAllVehicles != _spawnEnforceAllVehicles)
                    {
                        _spawnEnforceAllVehicles = spawnEnforceAllVehicles;
                    }
                }
                else if (Regex.Match(strVariable, @"Enforce on Specific Maps/Modes Only").Success)
                {
                    Boolean restrictSpecificMapModes = Boolean.Parse(strValue);
                    if (restrictSpecificMapModes != _restrictSpecificMapModes)
                    {
                        _restrictSpecificMapModes = restrictSpecificMapModes;
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
                        if (_triggerEnforcementMinimumInfractionPoints < 1) {
                            ConsoleError("Minimum infraction points for trigger level enforcement cannot be less than 1, use spawn enforcement instead.");
                            _triggerEnforcementMinimumInfractionPoints = 1;
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
                            _WARSAWInvalidLoadoutIDMessages[warsawID] = "Please remove " + commandSplit[commandSplit.Count() - 2].Trim() + " from your loadout";
                            if (!_enableAdKatsIntegration)
                            {
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
                else if (strVariable.StartsWith("ALWK"))
                {
                    //Trim off all but the warsaw ID
                    //ALWK3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("ALWK".ToCharArray()).Trim();
                    //Fetch needed role
                    switch (strValue.ToLower())
                    {
                        case "allow":
                            //parse allow
                            _WARSAWInvalidVehicleLoadoutIDMessages.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            WarsawItem item;
                            if (!_WARSAWLibrary.VehicleUnlocks.TryGetValue(warsawID, out item)) {
                                ConsoleError("Unable to find vehicle unlock " + warsawID);
                                return;
                            }
                            if (item.AssignedVehicle == null) {
                                ConsoleError("Unlock item " + warsawID + " was not assigned to a vehicle.");
                                return;
                            }
                            _WARSAWInvalidVehicleLoadoutIDMessages[warsawID] = "Please remove " + commandSplit[commandSplit.Count() - 2].Trim() + " from your " + item.AssignedVehicle.CategoryType;
                            if (!_WARSAWSpawnDeniedIDs.Contains(warsawID))
                            {
                                _WARSAWSpawnDeniedIDs.Add(warsawID);
                            }
                            foreach (var aPlayer in _PlayerDictionary.Values) {
                                aPlayer.WatchedVehicles.Clear();
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
                            if (!_WARSAWSpawnDeniedIDs.Contains(warsawID))
                            {
                                _WARSAWSpawnDeniedIDs.Add(warsawID);
                            }
                            break;
                        default:
                            ConsoleError("Unknown setting when assigning WARSAW allowance.");
                            return;
                    }
                }
                else if (strVariable.StartsWith("RMM"))
                {
                    //Trim off all but the warsaw ID
                    //ALWS3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    Int32 mapModeID = Int32.Parse(commandSplit[0].TrimStart("RMM".ToCharArray()).Trim());
                    MapMode mapMode = _availableMapModes.FirstOrDefault(mm => mm.MapModeID == mapModeID);
                    if (mapMode == null) {
                        ConsoleError("Invalid map/mode ID when parsing map enforce settings.");
                        return;
                    }
                    //Fetch needed role
                    switch (strValue.ToLower())
                    {
                        case "enforce":
                            //parse deny
                            if (!_restrictedMapModes.ContainsKey(mapMode.ModeKey + "|" + mapMode.MapKey))
                            {
                                _restrictedMapModes[mapMode.ModeKey + "|" + mapMode.MapKey] = mapMode;
                                if (_WARSAWLibraryLoaded)
                                {
                                    ConsoleInfo("Enforcing loadout on " + mapMode.ModeName + " " + mapMode.MapName);
                                }
                            }
                            break;
                        case "ignore":
                            //parse allow
                            if (_restrictedMapModes.Remove(mapMode.ModeKey + "|" + mapMode.MapKey) && _WARSAWLibraryLoaded)
                            {
                                ConsoleInfo("No longer enforcing loadout on " + mapMode.ModeName + " " + mapMode.MapName);
                            }
                            break;
                        default:
                            ConsoleError("Unknown setting when parsing map enforce settings.");
                            return;
                    }
                }
                else if (strVariable.StartsWith("MSG"))
                {
                    //Trim off all but the warsaw ID
                    //MSG3495820391
                    if (String.IsNullOrEmpty(strValue))
                    {
                        ConsoleError("Kill messages cannot be empty.");
                        return;
                    }
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("MSG".ToCharArray()).Trim();
                    _WARSAWInvalidLoadoutIDMessages[warsawID] = strValue;
                }
                else if (strVariable.StartsWith("VMSG"))
                {
                    //Trim off all but the warsaw ID
                    //MSG3495820391
                    if (String.IsNullOrEmpty(strValue))
                    {
                        ConsoleError("Kill messages cannot be empty.");
                        return;
                    }
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("VMSG".ToCharArray()).Trim();
                    _WARSAWInvalidVehicleLoadoutIDMessages[warsawID] = strValue;
                }
                else
                {
                    ConsoleInfo(strVariable + " =+= " + strValue);
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error occured while updating AdKatsLRT settings.", e));
            }
        }

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion)
        {
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
                    "OnPlayerKilled",
                    "OnPlayerLeft");
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("FATAL ERROR on plugin load.", e));
            }
            DebugWrite("Exiting OnPluginLoaded", 7);
        }

        public void OnPluginEnable()
        {
            try
            {
                //If the finalizer is still alive, inform the user and disable
                if (_Finalizer != null && _Finalizer.IsAlive)
                {
                    ConsoleError("Cannot enable the plugin while it is shutting down. Please Wait for it to shut down.");
                    _threadMasterWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                    //Disable the plugin
                    Disable();
                    return;
                }
                if (_gameVersion != GameVersion.BF4)
                {
                    ConsoleError("The AdKatsLRT extension cannot be enabled outside BF4.");
                    Disable();
                    return;
                }
                //Create a new thread to activate the plugin
                _Activator = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "Enabler";

                        _pluginEnabled = true;

                        if ((DateTime.UtcNow - _proconStartTime).TotalSeconds <= 20)
                        {
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
                            LogThreadExit();
                            return;
                        }
                        Boolean AdKatsFound = GetRegisteredCommands().Any(command => command.RegisteredClassname == "AdKats" && command.RegisteredMethodName == "PluginEnabled");
                        if (AdKatsFound)
                        {
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
                            if (_WARSAWLibraryLoaded || LoadWarsawLibrary())
                            {
                                if (!_pluginEnabled)
                                {
                                    LogThreadExit();
                                    return;
                                }
                                ConsoleSuccess("WARSAW library loaded. " + _WARSAWLibrary.Items.Count + " items, " + _WARSAWLibrary.VehicleUnlocks.Count + " vehicle unlocks, and " + _WARSAWLibrary.ItemAccessories.Count + " accessories.");
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

                                _pluginStartTime = DateTime.UtcNow;

                                //Init and start all the threads
                                InitWaitHandles();
                                OpenAllHandles();
                                InitThreads();
                                StartThreads();

                                ConsoleSuccess("AdKatsLRT " + GetPluginVersion() + " startup complete [" + FormatTimeString(DateTime.UtcNow - _StartTime, 3) + "]. Loadout restriction now online.");
                            }
                            else
                            {
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
                    catch (Exception e)
                    {
                        HandleException(new AdKatsException("Error while enabling AdKatsLRT.", e));
                    }
                    LogThreadExit();
                }));

                ConsoleWrite("^b^2ENABLED!^n^0 Beginning startup sequence...");
                //Start the thread
                StartAndLogThread(_Activator);
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while initializing activator thread.", e));
            }
        }

        public void OnPluginDisable()
        {
            //If the plugin is already disabling then cancel
            if (_Finalizer != null && _Finalizer.IsAlive)
                return;
            try
            {
                //Create a new thread to disabled the plugin
                _Finalizer = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "Finalizer";
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
                        do
                        {
                            OpenAllHandles();
                            attempts++;
                            Thread.Sleep(500);
                            alive = false;
                            String aliveThreads = "";
                            lock (_aliveThreads)
                            {
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
                                    ConsoleWarn("Threads still exiting: " + aliveThreads);
                                }
                                else
                                {
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
                        _countFixed = 0;
                        _countKilled = 0;
                        _countQuit = 0;
                        _slowmo = false;
                        ConsoleWrite("^b^1AdKatsLRT " + GetPluginVersion() + " Disabled! =(^n^0");
                    }
                    catch (Exception e)
                    {
                        HandleException(new AdKatsException("Error occured while disabling AdkatsLRT.", e));
                    }
                }));

                //Start the finalizer thread
                _Finalizer.Start();
            }
            catch (Exception e)
            {
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

        public override void OnPlayerKilled(Kill kill)
        {
            DebugWrite("Entering OnPlayerKilled", 7);
            try
            {
                //If the plugin is not enabled and running just return
                if (!_pluginEnabled || !_threadsReady || !_firstPlayerListComplete)
                {
                    return;
                }
                //Fetch player
                AdKatsSubscribedPlayer killer;
                AdKatsSubscribedPlayer victim;
                if (kill.Killer != null && !String.IsNullOrEmpty(kill.Killer.SoldierName)) {
                    if (!_PlayerDictionary.TryGetValue(kill.Killer.SoldierName, out killer)) {
                        ConsoleError("Unable to fetch killer on kill.");
                        return;
                    }
                }
                else {
                    return;
                }
                if (kill.Victim != null && !String.IsNullOrEmpty(kill.Victim.SoldierName)) {
                    if (!_PlayerDictionary.TryGetValue(kill.Victim.SoldierName, out victim)) {
                        ConsoleError("Unable to fetch victim on kill.");
                        return;
                    }
                }
                else {
                    return;
                }
                WarsawVehicle vehicle;
                if (killer.Loadout != null &&
                    killer.Loadout.LoadoutRCONVehicles.TryGetValue(kill.DamageType, out vehicle)) {
                    DebugWrite(killer.player_name + " is using trackable vehicle type " + vehicle.CategoryType + ".", 5);
                    if (!killer.WatchedVehicles.Contains(vehicle.Category)) {
                        killer.WatchedVehicles.Add(vehicle.Category);
                        DebugWrite("Loadout check automatically called on " + killer.player_name + " for trackable vehicle kill.", 4);
                        QueueForProcessing(new ProcessObject()
                        {
                            process_player = killer,
                            process_source = "vehiclekill",
                            process_time = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while handling OnPlayerKilled.", e));
            }
            DebugWrite("Exiting OnPlayerKilled", 7);
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
                    DateTime lastAdminFetch = DateTime.UtcNow;
                    while (true)
                    {
                        try
                        {
                            //Check for thread warning every 30 seconds
                            if ((DateTime.UtcNow - lastKeepAliveCheck).TotalSeconds > 30)
                            {
                                if (_threadsReady)
                                {
                                    Boolean AdKatsFound = GetRegisteredCommands().Any(command => command.RegisteredClassname == "AdKats" && command.RegisteredMethodName == "PluginEnabled");
                                    if (AdKatsFound)
                                    {
                                        if (!_enableAdKatsIntegration)
                                        {
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
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }
                        catch (Exception e)
                        {
                            HandleException(new AdKatsException("Error in keep-alive. Skipping current loop.", e));
                        }
                    }
                }
                catch (Exception e)
                {
                    HandleException(new AdKatsException("Error while running keep-alive.", e));
                }
            }));
            //Start the thread
            statusMonitorThread.Start();
        }

        public void InitWaitHandles()
        {
            //Initializes all wait handles 
            _threadMasterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _LoadoutProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _PlayerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _PluginDescriptionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void OpenAllHandles()
        {
            _threadMasterWaitHandle.Set();
            _LoadoutProcessingWaitHandle.Set();
            _PlayerProcessingWaitHandle.Set();
            _BattlelogCommWaitHandle.Set();
        }

        public void InitThreads()
        {
            try
            {
                _SpawnProcessingThread = new Thread(ProcessingThreadLoop)
                {
                    IsBackground = true
                };

                _BattlelogCommThread = new Thread(BattlelogCommThreadLoop)
                {
                    IsBackground = true
                };
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error occured while initializing threads.", e));
            }
        }

        public void StartThreads()
        {
            DebugWrite("Entering StartThreads", 7);
            try
            {
                //Reset the master wait handle
                _threadMasterWaitHandle.Reset();
                //Start the spawn processing thread
                StartAndLogThread(_SpawnProcessingThread);
                StartAndLogThread(_BattlelogCommThread);
                _threadsReady = true;
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while starting processing threads.", e));
            }
            DebugWrite("Exiting StartThreads", 7);
        }

        private void Disable()
        {
            //Call Disable
            ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "False");
            //Set enabled false so threads begin exiting
            _pluginEnabled = false;
            _threadsReady = false;
        }

        private void Enable()
        {
            if (Thread.CurrentThread.Name == "Finalizer")
            {
                var pluginRebootThread = new Thread(new ThreadStart(delegate
                {
                    DebugWrite("Starting a reboot thread.", 5);
                    try
                    {
                        Thread.CurrentThread.Name = "Reboot";
                        Thread.Sleep(1000);
                        //Call Enable
                        ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "True");
                    }
                    catch (Exception)
                    {
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

        public void OnPluginLoadingEnv(List<String> lstPluginEnv)
        {
            foreach (String env in lstPluginEnv)
            {
                DebugWrite("^9OnPluginLoadingEnv: " + env, 7);
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
            DebugWrite("^1Game Version: " + _gameVersion, 1);
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset cpsSubset)
        {
            //Completely ignore this event if integrated with AdKats
            if (_enableAdKatsIntegration || !_pluginEnabled)
            {
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
                        if (!_PlayerDictionary.TryGetValue(aPlayer.player_name, out dPlayer))
                        {
                            //Not online. Are they returning?
                            if (_PlayerLeftDictionary.TryGetValue(aPlayer.player_guid, out dPlayer))
                            {
                                //They are returning, move their player object
                                DebugWrite(aPlayer.player_name + " is returning.", 6);
                                dPlayer.player_online = true;
                                dPlayer.WatchedVehicles.Clear();
                                _PlayerDictionary[aPlayer.player_name] = dPlayer;
                                _PlayerLeftDictionary.Remove(aPlayer.player_guid);
                            }
                            else
                            {
                                //Not online or returning. New player.
                                DebugWrite(aPlayer.player_name + " is newly joining.", 6);
                                newPlayer = true;
                            }
                        }
                        if (newPlayer)
                        {
                            _PlayerDictionary[aPlayer.player_name] = aPlayer;
                            QueuePlayerForBattlelogInfoFetch(aPlayer);
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
                        if (_PlayerDictionary.TryGetValue(playerName, out aPlayer)) {
                            DebugWrite(aPlayer.player_name + " removed from player list.", 6);
                            _PlayerDictionary.Remove(aPlayer.player_name);
                            List<String> removeNames = _PlayerLeftDictionary.Where(pair => (DateTime.UtcNow - pair.Value.LastUsage).TotalMinutes > 120).Select(pair => pair.Key).ToList();
                            foreach (String removeName in removeNames)
                            {
                                _PlayerLeftDictionary.Remove(removeName);
                            }
                            if (_isTestingAuthorized && removeNames.Any())
                            {
                                ConsoleWarn(removeNames.Count() + " left players removed, " + _PlayerLeftDictionary.Count() + " still in cache.");
                            }
                            aPlayer.LastUsage = DateTime.UtcNow;
                            _PlayerLeftDictionary[aPlayer.player_guid] = aPlayer;
                        }
                        else {
                            ConsoleError("Unable to find " + playerName + " in online players when requesting removal.");
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

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory)
        {
            try
            {
                DateTime spawnTime = DateTime.UtcNow;
                if (_threadsReady && _pluginEnabled && _firstPlayerListComplete)
                {
                    AdKatsSubscribedPlayer aPlayer;
                    if (_PlayerDictionary.TryGetValue(soldierName, out aPlayer))
                    {
                        aPlayer.player_spawnedOnce = true;
                        //Reject spawn processing if player has no persona ID
                        if (String.IsNullOrEmpty(aPlayer.player_personaID))
                        {
                            if (!_enableAdKatsIntegration)
                            {
                                QueuePlayerForBattlelogInfoFetch(aPlayer);
                            }
                            DebugWrite("Spawn process for " + aPlayer.player_name + " cancelled because their Persona ID is not loaded yet.", 3);
                            return;
                        }
                        //Create process object
                        var processObject = new ProcessObject()
                        {
                            process_player = aPlayer,
                            process_source = "spawn",
                            process_time = spawnTime
                        };
                        //Minimum wait time of 5 seconds
                        if (_LoadoutProcessingQueue.Count >= 6)
                        {
                            QueueForProcessing(processObject);
                        }
                        else
                        {
                            var waitTime = TimeSpan.FromSeconds(5 - _LoadoutProcessingQueue.Count);
                            if (waitTime.TotalSeconds <= 0.1) {
                                waitTime = TimeSpan.FromSeconds(5);
                            }
                            DebugWrite("Waiting " + ((int)waitTime.TotalSeconds) + " seconds to process " + aPlayer.GetVerboseName() + " spawn.", 3);
                            //Start a delay thread
                            StartAndLogThread(new Thread(new ThreadStart(delegate
                            {
                                Thread.CurrentThread.Name = "LoadoutCheckDelay";
                                try
                                {
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
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while handling player spawn.", e));
            }
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            DebugWrite("Entering OnPlayerLeft", 7);
            try
            {
                AdKatsSubscribedPlayer aPlayer;
                if (_PlayerDictionary.TryGetValue(playerInfo.SoldierName, out aPlayer))
                {
                    aPlayer.player_online = false;
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while handling player left.", e));
            }
            DebugWrite("Exiting OnPlayerLeft", 7);
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

                var decodedCommand = (Hashtable)JSON.JsonDecode(unparsedCommandJSON);

                var playerName = (String)decodedCommand["player_name"];
                var loadoutCheckReason = (String)decodedCommand["loadoutCheck_reason"];

                if (_threadsReady && _pluginEnabled && _firstPlayerListComplete)
                {
                    AdKatsSubscribedPlayer aPlayer;
                    if (_PlayerDictionary.TryGetValue(playerName, out aPlayer))
                    {
                        ConsoleWrite("Loadout check manually called on " + playerName + ".");
                        QueueForProcessing(new ProcessObject()
                        {
                            process_player = aPlayer,
                            process_source = loadoutCheckReason,
                            process_time = DateTime.UtcNow,
                            process_manual = true
                        });
                    }
                    else
                    {
                        ConsoleError("Attempted to call MANUAL loadout check on " + playerName + " without their player object loaded.");
                    }
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while calling loadout check on player.", e));
            }
            DebugWrite("CallLoadoutCheckOnPlayer finished!", 6);
        }

        public void ReceiveAdminList(params String[] parameters)
        {
            DebugWrite("ReceiveAdminList starting!", 6);
            try
            {
                if (parameters.Length != 2)
                {
                    ConsoleError("Online admin receiving cancelled. Parameters invalid.");
                    return;
                }
                String source = parameters[0];
                String unparsedCommandJSON = parameters[1];

                var decodedCommand = (Hashtable)JSON.JsonDecode(unparsedCommandJSON);

                var unparsedAdminList = (String)decodedCommand["response_value"];

                String[] tempAdminList = CPluginVariable.DecodeStringArray(unparsedAdminList);
                foreach (String adminPlayerName in tempAdminList)
                {
                    if (!_AdminList.Contains(adminPlayerName))
                    {
                        _AdminList.Add(adminPlayerName);
                    }
                }
                _AdminList.RemoveWhere(name => !tempAdminList.Contains(name));
            }
            catch (Exception e)
            {
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
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while queueing player for processing.", e));
            }
            DebugWrite("Exiting QueueForProcessing", 7);
        }

        public void ProcessingThreadLoop()
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
                                if (!importObject.process_player.player_online)
                                {
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
                            else if (processObject.process_source == "vehiclekill")
                            {
                                reason = "[Vehicle Kill] ";
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
                            if (loadout == null)
                            {
                                continue;
                            }
                            aPlayer.Loadout = loadout;

                            //Show the loadout contents
                            String primaryMessage = loadout.KitItemPrimary.Slug + " [" + loadout.KitItemPrimary.AccessoriesAssigned.Values.Aggregate("", (currentString, acc) => currentString + TrimStart(acc.Slug, loadout.KitItemPrimary.Slug).Trim() + ", ").Trim().TrimEnd(',') + "]";
                            String sidearmMessage = loadout.KitItemSidearm.Slug + " [" + loadout.KitItemSidearm.AccessoriesAssigned.Values.Aggregate("", (currentString, acc) => currentString + TrimStart(acc.Slug, loadout.KitItemSidearm.Slug).Trim() + ", ").Trim().TrimEnd(',') + "]";
                            String gadgetMessage = "[" + loadout.KitGadget1.Slug + ", " + loadout.KitGadget2.Slug + "]";
                            String grenadeMessage = "[" + loadout.KitGrenade.Slug + "]";
                            String knifeMessage = "[" + loadout.KitKnife.Slug + "]";
                            String loadoutMessage = "Player " + loadout.Name + " processed as " + loadout.SelectedKit.KitType + " with primary " + primaryMessage + " sidearm " + sidearmMessage + " gadgets " + gadgetMessage + " grenade " + grenadeMessage + " and knife " + knifeMessage;
                            String loadoutShortMessage = "Primary [" + loadout.KitItemPrimary.Slug + "] sidearm [" + loadout.KitItemSidearm.Slug + "] gadgets " + gadgetMessage + " grenade " + grenadeMessage + " and knife " + knifeMessage;
                            DebugWrite(loadoutMessage, 4);

                            //Action taken?
                            Boolean acted = false;

                            HashSet<String> specificMessages = new HashSet<String>();
                            HashSet<String> spawnSpecificMessages = new HashSet<String>();
                            HashSet<String> vehicleSpecificMessages = new HashSet<String>();
                            Boolean loadoutValid = true;
                            Boolean spawnLoadoutValid = true;
                            Boolean vehicleLoadoutValid = true;

                            if (!_restrictSpecificMapModes || _restrictedMapModes.ContainsKey(_serverInfo.InfoObject.GameMode + "|" + _serverInfo.InfoObject.Map))
                            {
                                if (trigger)
                                {
                                    foreach (var warsawDeniedIDMessage in _WARSAWInvalidLoadoutIDMessages)
                                    {
                                        if (loadout.AllKitItemIDs.Contains(warsawDeniedIDMessage.Key))
                                        {
                                            loadoutValid = false;
                                            if (!specificMessages.Contains(warsawDeniedIDMessage.Value))
                                            {
                                                specificMessages.Add(warsawDeniedIDMessage.Value);
                                            }
                                        }
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
                                foreach (var warsawDeniedIDMessage in _WARSAWInvalidVehicleLoadoutIDMessages)
                                {
                                    if (_spawnEnforceAllVehicles)
                                    {
                                        if (loadout.VehicleItems.ContainsKey(warsawDeniedIDMessage.Key))
                                        {
                                            loadoutValid = false;
                                            vehicleLoadoutValid = false;
                                            if (!vehicleSpecificMessages.Contains(warsawDeniedIDMessage.Value))
                                            {
                                                vehicleSpecificMessages.Add(warsawDeniedIDMessage.Value);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //Wow this needs optimization...
                                        foreach (String category in aPlayer.WatchedVehicles)
                                        {
                                            WarsawVehicle vehicle;
                                            if (!loadout.LoadoutVehicles.TryGetValue(category, out vehicle))
                                            {
                                                ConsoleError("Could not fetch used vehicle " + category + " from player loadout, skipping.");
                                                continue;
                                            }
                                            if ((vehicle.AssignedPrimary != null && vehicle.AssignedPrimary.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedSecondary != null && vehicle.AssignedSecondary.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedOptic != null && vehicle.AssignedOptic.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedCountermeasure != null && vehicle.AssignedCountermeasure.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedUpgrade != null && vehicle.AssignedUpgrade.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedSecondaryGunner != null && vehicle.AssignedSecondaryGunner.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedOpticGunner != null && vehicle.AssignedOpticGunner.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedUpgradeGunner != null && vehicle.AssignedUpgradeGunner.WarsawID == warsawDeniedIDMessage.Key))
                                            {
                                                loadoutValid = false;
                                                vehicleLoadoutValid = false;
                                                if (!vehicleSpecificMessages.Contains(warsawDeniedIDMessage.Value))
                                                {
                                                    vehicleSpecificMessages.Add(warsawDeniedIDMessage.Value);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (!trigger && !spawnLoadoutValid)
                            {
                                //Reputable players
                                if (processObject.process_player.player_reputation >= 15)
                                {
                                    //Option for reputation deny
                                    if (!_spawnEnforcementActOnReputablePlayers)
                                    {
                                        DebugWrite(processObject.process_player.player_name + " spawn enforcement cancelled. Player is reputable.", 4);
                                        if (_enableAdKatsIntegration)
                                        {
                                            //Inform AdKats of the loadout
                                            StartAndLogThread(new Thread(new ThreadStart(delegate
                                            {
                                                Thread.CurrentThread.Name = "AdKatsInform";
                                                Thread.Sleep(100);
                                                ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                                    {"caller_identity", "AdKatsLRT"},
                                                    {"response_requested", false},
                                                    {"loadout_player", loadout.Name},
                                                    {"loadout_valid", loadoutValid},
                                                    {"loadout_spawnValid", spawnLoadoutValid},
                                                    {"loadout_acted", false},
                                                    {"loadout_items", loadoutShortMessage},
                                                    {"loadout_deniedItems", ""}
                                                }));
                                                Thread.Sleep(100);
                                                LogThreadExit();
                                            })));
                                        }
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
                                        if (_enableAdKatsIntegration)
                                        {
                                            //Inform AdKats of the loadout
                                            StartAndLogThread(new Thread(new ThreadStart(delegate
                                            {
                                                Thread.CurrentThread.Name = "AdKatsInform";
                                                Thread.Sleep(100);
                                                ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                                    {"caller_identity", "AdKatsLRT"},
                                                    {"response_requested", false},
                                                    {"loadout_player", loadout.Name},
                                                    {"loadout_valid", loadoutValid},
                                                    {"loadout_spawnValid", spawnLoadoutValid},
                                                    {"loadout_acted", false},
                                                    {"loadout_items", loadoutShortMessage},
                                                    {"loadout_deniedItems", ""}
                                                }));
                                                Thread.Sleep(100);
                                                LogThreadExit();
                                            })));
                                        }
                                        continue;
                                    }
                                }
                            }

                            aPlayer.player_loadoutEnforced = true;
                            String deniedWeapons = String.Empty;
                            String spawnDeniedWeapons = String.Empty;
                            if (!loadoutValid)
                            {
                                //Fill the denied messages
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitItemPrimary.WarsawID))
                                {
                                    deniedWeapons += loadout.KitItemPrimary.Slug.ToUpper() + ", ";
                                }
                                deniedWeapons = loadout.KitItemPrimary.AccessoriesAssigned.Values.Where(weaponAccessory => _WARSAWInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID)).Aggregate(deniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitItemSidearm.WarsawID))
                                {
                                    deniedWeapons += loadout.KitItemSidearm.Slug.ToUpper() + ", ";
                                }
                                deniedWeapons = loadout.KitItemSidearm.AccessoriesAssigned.Values.Where(weaponAccessory => _WARSAWInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID)).Aggregate(deniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitGadget1.WarsawID))
                                {
                                    deniedWeapons += loadout.KitGadget1.Slug.ToUpper() + ", ";
                                }
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitGadget2.WarsawID))
                                {
                                    deniedWeapons += loadout.KitGadget2.Slug.ToUpper() + ", ";
                                }
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitGrenade.WarsawID))
                                {
                                    deniedWeapons += loadout.KitGrenade.Slug.ToUpper() + ", ";
                                }
                                if (_WARSAWInvalidLoadoutIDMessages.ContainsKey(loadout.KitKnife.WarsawID))
                                {
                                    deniedWeapons += loadout.KitKnife.Slug.ToUpper() + ", ";
                                }
                                //Fill the spawn denied messages
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitItemPrimary.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitItemPrimary.Slug.ToUpper() + ", ";
                                }
                                spawnDeniedWeapons = loadout.KitItemPrimary.AccessoriesAssigned.Values.Where(weaponAccessory => _WARSAWSpawnDeniedIDs.Contains(weaponAccessory.WarsawID)).Aggregate(spawnDeniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitItemSidearm.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitItemSidearm.Slug.ToUpper() + ", ";
                                }
                                spawnDeniedWeapons = loadout.KitItemSidearm.AccessoriesAssigned.Values.Where(weaponAccessory => _WARSAWSpawnDeniedIDs.Contains(weaponAccessory.WarsawID)).Aggregate(spawnDeniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitGadget1.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGadget1.Slug.ToUpper() + ", ";
                                }
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitGadget2.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGadget2.Slug.ToUpper() + ", ";
                                }
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitGrenade.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGrenade.Slug.ToUpper() + ", ";
                                }
                                if (_WARSAWSpawnDeniedIDs.Contains(loadout.KitKnife.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitKnife.Slug.ToUpper() + ", ";
                                }
                                //Trim the messages
                                deniedWeapons = deniedWeapons.Trim().TrimEnd(',');
                                spawnDeniedWeapons = spawnDeniedWeapons.Trim().TrimEnd(',');

                                //Decide whether to kill the player
                                Boolean adminsOnline = AdminsOnline();
                                if (!vehicleLoadoutValid || !spawnLoadoutValid || killOverride || (!adminsOnline && trigger)) 
                                {
                                    //Player will be killed
                                    acted = true;
                                    aPlayer.player_loadoutKilled = true;
                                    DebugWrite(loadout.Name + " KILLED for invalid loadout.", 1);
                                    if (aPlayer.player_spawnedOnce)
                                    {
                                        aPlayer.LoadoutKills++;
                                        //Start a repeat kill
                                        StartAndLogThread(new Thread(new ThreadStart(delegate
                                        {
                                            Thread.CurrentThread.Name = "PlayerRepeatKill";
                                            Thread.Sleep(100);
                                            for (Int32 index = 0; index < 15; index++)
                                            {
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

                                    String adminMessage = reason + aPlayer.GetVerboseName() + " killed for ";
                                    HashSet<String> tellMessages = new HashSet<String>();
                                    if (trigger && (killOverride || !adminsOnline))
                                    {
                                        //Manual trigger or no admins online, enforce all denied weapons
                                        adminMessage += "denied items [" + deniedWeapons + "]";
                                        PlayerSayMessage(aPlayer.player_name, reason + aPlayer.GetVerboseName() + " please remove [" + deniedWeapons + "] from your loadout.");
                                        foreach (var specificMessage in specificMessages)
                                        {
                                            if (!tellMessages.Contains(specificMessage))
                                            {
                                                tellMessages.Add(specificMessage);
                                            }
                                        }
                                    }
                                    else if (!spawnLoadoutValid)
                                    {
                                        //Loadout enforcement was not triggered, enforce spawn denied weapons only
                                        PlayerSayMessage(aPlayer.player_name, reason + aPlayer.GetVerboseName() + " please remove [" + spawnDeniedWeapons + "] from your loadout.");
                                        foreach (var spawnSpecificMessage in spawnSpecificMessages)
                                        {
                                            if (!tellMessages.Contains(spawnSpecificMessage))
                                            {
                                                tellMessages.Add(spawnSpecificMessage);
                                            }
                                        }
                                    }
                                    if (!vehicleLoadoutValid)
                                    {
                                        if (killOverride)
                                        {
                                            adminMessage += ", and ";
                                        }
                                        adminMessage += "invalid vehicle loadout";
                                        foreach (var vehicleSpecificMessage in vehicleSpecificMessages)
                                        {
                                            if (!tellMessages.Contains(vehicleSpecificMessage))
                                            {
                                                tellMessages.Add(vehicleSpecificMessage);
                                            }
                                        }
                                    }
                                    adminMessage += ".";
                                    //Inform Admins
                                    if (killOverride || !vehicleLoadoutValid)
                                    {
                                        OnlineAdminSayMessage(adminMessage);
                                    }
                                    //Inform Player
                                    Int32 tellIndex = 1;
                                    foreach (String tellMessage in tellMessages)
                                    {
                                        String prefix = ((tellMessages.Count > 1) ? ("(" + (tellIndex++) + "/" + tellMessages.Count + ") ") : (""));
                                        PlayerTellMessage(loadout.Name, prefix + tellMessage);
                                        if (tellMessages.Count > 1)
                                        {
                                            Thread.Sleep(2000);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (!aPlayer.player_loadoutValid) {
                                    PlayerSayMessage(aPlayer.player_name, aPlayer.GetVerboseName() + " thank you for fixing your loadout.");
                                    if (killOverride) {
                                        OnlineAdminSayMessage(reason + aPlayer.GetVerboseName() + " fixed their loadout.");
                                    }
                                }
                                else if (processObject.process_manual) {
                                    OnlineAdminSayMessage(aPlayer.GetVerboseName() + "'s has no banned items.");
                                }
                            }
                            if (_enableAdKatsIntegration)
                            {
                                //Inform AdKats of the loadout
                                StartAndLogThread(new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "AdKatsInform";
                                    Thread.Sleep(100);
                                    ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                        {"caller_identity", "AdKatsLRT"},
                                        {"response_requested", false},
                                        {"loadout_player", loadout.Name},
                                        {"loadout_valid", loadoutValid},
                                        {"loadout_spawnValid", spawnLoadoutValid},
                                        {"loadout_acted", acted},
                                        {"loadout_items", loadoutShortMessage},
                                        {"loadout_deniedItems", deniedWeapons}
                                    }));
                                    Thread.Sleep(100);
                                    LogThreadExit();
                                })));
                            }
                            aPlayer.player_loadoutValid = loadoutValid;
                            lock (_PlayerDictionary)
                            {
                                Int32 totalPlayerCount = _PlayerDictionary.Count + _PlayerLeftDictionary.Count;
                                Int32 countKills = _PlayerDictionary.Values.Sum(dPlayer => dPlayer.LoadoutKills) + _PlayerLeftDictionary.Values.Sum(dPlayer => dPlayer.LoadoutKills);
                                Int32 countEnforced = _PlayerDictionary.Values.Count(dPlayer => dPlayer.player_loadoutEnforced) + _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutEnforced);
                                Int32 countKilled = _PlayerDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled) + _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled);
                                Int32 countFixed = _PlayerDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled && dPlayer.player_loadoutValid) + _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled && dPlayer.player_loadoutValid);
                                Int32 countQuit = _PlayerLeftDictionary.Values.Count(dPlayer => dPlayer.player_loadoutKilled && !dPlayer.player_loadoutValid);
                                Boolean displayStats = (_countKilled != countKilled) ||
                                                       (_countFixed != countFixed) ||
                                                       (_countQuit != countQuit);
                                _countKilled = countKilled;
                                _countFixed = countFixed;
                                _countQuit = countQuit;
                                Double percentEnforced = Math.Round(((Double)countEnforced / (Double)totalPlayerCount) * 100.0);
                                Double percentKilled = Math.Round(((Double)countKilled / (Double)totalPlayerCount) * 100.0);
                                Double percentFixed = Math.Round(((Double)countFixed / (Double)countKilled) * 100.0);
                                Double percentRaged = Math.Round(((Double)countQuit / (Double)countKilled) * 100.0);
                                Double denialKPM = Math.Round((Double)countKills/(DateTime.UtcNow - _pluginStartTime).TotalMinutes, 2);
                                Double killsPerDenial = Math.Round((Double)countKills/(Double)countKilled, 2);
                                if (displayStats)
                                {
                                    DebugWrite("(" + countEnforced + "/" + totalPlayerCount + ") " + percentEnforced + "% enforced. " + "(" + countKilled + "/" + totalPlayerCount + ") " + percentKilled + "% denied. " + "(" + countFixed + "/" + countKilled + ") " + percentFixed + "% fixed. " + "(" + countQuit + "/" + countKilled + ") " + percentRaged + "% quit. " + killsPerDenial + " kills per denial. " + denialKPM + " denial KPM.", 2);
                                }
                            }
                            DebugWrite(_LoadoutProcessingQueue.Count + " players still in queue.", 3);
                            DebugWrite(processObject.process_player.player_name + " processed after " + Math.Round(DateTime.UtcNow.Subtract(processObject.process_time).TotalSeconds, 2) + "s", 5);
                        }
                        else
                        {
                            //Wait for input
                            _LoadoutProcessingWaitHandle.Reset();
                            _LoadoutProcessingWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
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
                        HandleException(new AdKatsException("Error occured in spawn processing thread. Skipping current loop.", e));
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

        private void QueuePlayerForBattlelogInfoFetch(AdKatsSubscribedPlayer aPlayer)
        {
            DebugWrite("Entering QueuePlayerForBattlelogInfoFetch", 6);
            try
            {
                DebugWrite("Preparing to queue player for battlelog info fetch.", 6);
                if (_BattlelogFetchQueue.Any(bPlayer => bPlayer.player_guid == aPlayer.player_guid))
                {
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
                            _BattlelogCommWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
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

        public void ReceiveOnlineSoldiers(params String[] parameters)
        {
            DebugWrite("ReceiveOnlineSoldiers starting!", 6);
            try
            {
                if (!_enableAdKatsIntegration)
                {
                    return;
                }
                if (parameters.Length != 2)
                {
                    ConsoleError("Online soldier handling canceled. Parameters invalid.");
                    return;
                }
                String source = parameters[0];
                String unparsedResponseJSON = parameters[1];

                var decodedResponse = (Hashtable)JSON.JsonDecode(unparsedResponseJSON);

                var decodedSoldierList = (ArrayList)decodedResponse["response_value"];
                if (decodedSoldierList == null)
                {
                    ConsoleError("Soldier params could not be properly converted from JSON. Unable to continue.");
                    return;
                }
                lock (_PlayerDictionary)
                {
                    var validPlayers = new List<String>();
                    foreach (Hashtable soldierHashtable in decodedSoldierList)
                    {
                        var aPlayer = new AdKatsSubscribedPlayer();
                        aPlayer.player_id = Convert.ToInt64((Double)soldierHashtable["player_id"]);
                        aPlayer.player_guid = (String)soldierHashtable["player_guid"];
                        aPlayer.player_pbguid = (String)soldierHashtable["player_pbguid"];
                        aPlayer.player_ip = (String)soldierHashtable["player_ip"];
                        aPlayer.player_name = (String)soldierHashtable["player_name"];
                        aPlayer.player_personaID = (String)soldierHashtable["player_personaID"];
                        aPlayer.player_clanTag = (String)soldierHashtable["player_clanTag"];
                        aPlayer.player_online = (Boolean)soldierHashtable["player_online"];
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
                        var lastPunishment = (Double)soldierHashtable["player_lastPunishment"];
                        if (lastPunishment > 0)
                        {
                            aPlayer.player_lastPunishment = TimeSpan.FromSeconds(lastPunishment);
                        }
                        var lastForgive = (Double)soldierHashtable["player_lastForgive"];
                        if (lastPunishment > 0)
                        {
                            aPlayer.player_lastForgive = TimeSpan.FromSeconds(lastForgive);
                        }
                        var lastAction = (Double)soldierHashtable["player_lastAction"];
                        if (lastPunishment > 0)
                        {
                            aPlayer.player_lastAction = TimeSpan.FromSeconds(lastAction);
                        }
                        aPlayer.player_spawnedOnce = (Boolean)soldierHashtable["player_spawnedOnce"];
                        aPlayer.player_conversationPartner = (String)soldierHashtable["player_conversationPartner"];
                        aPlayer.player_kills = Convert.ToInt32((Double)soldierHashtable["player_kills"]);
                        aPlayer.player_deaths = Convert.ToInt32((Double)soldierHashtable["player_deaths"]);
                        aPlayer.player_kdr = (Double)soldierHashtable["player_kdr"];
                        aPlayer.player_rank = Convert.ToInt32((Double)soldierHashtable["player_rank"]);
                        aPlayer.player_score = Convert.ToInt32((Double)soldierHashtable["player_score"]);
                        aPlayer.player_squad = Convert.ToInt32((Double)soldierHashtable["player_squad"]);
                        aPlayer.player_team = Convert.ToInt32((Double)soldierHashtable["player_team"]);

                        validPlayers.Add(aPlayer.player_name);

                        Boolean process = false;
                        AdKatsSubscribedPlayer dPlayer;
                        Boolean newPlayer = false;
                        //Are they online?
                        if (!_PlayerDictionary.TryGetValue(aPlayer.player_name, out dPlayer))
                        {
                            //Not online. Are they returning?
                            if (_PlayerLeftDictionary.TryGetValue(aPlayer.player_guid, out dPlayer))
                            {
                                //They are returning, move their player object
                                DebugWrite(aPlayer.player_name + " is returning.", 6);
                                dPlayer.player_online = true;
                                _PlayerDictionary[aPlayer.player_name] = dPlayer;
                                _PlayerLeftDictionary.Remove(dPlayer.player_guid);
                            }
                            else
                            {
                                //Not online or returning. New player.
                                DebugWrite(aPlayer.player_name + " is newly joining.", 6);
                                newPlayer = true;
                            }
                        }
                        if (newPlayer)
                        {
                            _PlayerDictionary[aPlayer.player_name] = aPlayer;
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
                        if (process)
                        {
                            QueueForProcessing(new ProcessObject()
                            {
                                process_player = dPlayer,
                                process_source = "listing",
                                process_time = DateTime.UtcNow
                            });
                        }
                        DebugWrite(aPlayer.player_name + " online after listing: " + _PlayerDictionary.ContainsKey(aPlayer.player_name), 7);
                    }
                    foreach (string playerName in _PlayerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList())
                    {
                        AdKatsSubscribedPlayer aPlayer;
                        if (_PlayerDictionary.TryGetValue(playerName, out aPlayer))
                        {
                            DebugWrite(aPlayer.player_name + " removed from player list.", 6);
                            _PlayerDictionary.Remove(aPlayer.player_name);
                            _PlayerLeftDictionary[aPlayer.player_guid] = aPlayer;
                        }
                        else
                        {
                            ConsoleError("Unable to find " + playerName + " in online players when requesting removal.");
                        }
                    }
                }
                _firstPlayerListComplete = true;
                _PlayerProcessingWaitHandle.Set();
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while receiving online soldiers.", e));
            }
            DebugWrite("ReceiveOnlineSoldiers finished!", 6);
        }

        public void AdminSayMessage(String message)
        {
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
                string[] lineSplit = message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                foreach (String line in lineSplit)
                {
                    ExecuteCommand("procon.protected.send", "admin.say", line, "all");
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while sending admin say.", e));
            }
            DebugWrite("Exiting adminSay", 7);
        }

        public void PlayerSayMessage(String target, String message)
        {
            PlayerSayMessage(target, message, true, 1);
        }

        public void PlayerSayMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            DebugWrite("Entering playerSayMessage", 7);
            try
            {
                if (String.IsNullOrEmpty(target) || String.IsNullOrEmpty(message))
                {
                    ConsoleError("target or message null in playerSayMessage");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Say > " + target + " > " + message);
                }
                for (int count = 0; count < spamCount; count++)
                {
                    string[] lineSplit = message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (String line in lineSplit)
                    {
                        ExecuteCommand("procon.protected.send", "admin.say", line, "player", target);
                    }
                    _threadMasterWaitHandle.WaitOne(50);
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while sending message to player.", e));
            }
            DebugWrite("Exiting playerSayMessage", 7);
        }

        public void AdminYellMessage(String message)
        {
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

        public void PlayerYellMessage(String target, String message)
        {
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
                    ExecuteCommand("procon.protected.send", "admin.yell", ((_gameVersion == GameVersion.BF4) ? (Environment.NewLine) : ("")) + message.ToUpper(), _YellDuration + "", "player", target);
                    _threadMasterWaitHandle.WaitOne(50);
                }
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while sending admin yell.", e));
            }
            DebugWrite("Exiting PlayerYellMessage", 7);
        }

        public void AdminTellMessage(String message)
        {
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

        public void PlayerTellMessage(String target, String message)
        {
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

        public Boolean LoadWarsawLibrary()
        {
            DebugWrite("Entering LoadWarsawLibrary", 7);
            try
            {
                Hashtable responseData = null;
                if (_gameVersion == GameVersion.BF4)
                {
                    var library = new WarsawLibrary();
                    ConsoleInfo("Downloading WARSAW library.");
                    responseData = FetchWarsawLibrary();

                    //Response data
                    if (responseData == null)
                    {
                        ConsoleError("WARSAW library fetch failed, unable to generate library.");
                        return false;
                    }
                    //Compact element
                    if (!responseData.ContainsKey("compact"))
                    {
                        ConsoleError("WARSAW library fetch did not contain 'compact' element, unable to generate library.");
                        return false;
                    }
                    var compact = (Hashtable)responseData["compact"];
                    if (compact == null)
                    {
                        ConsoleError("Compact section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact weapons element
                    if (!compact.ContainsKey("weapons"))
                    {
                        ConsoleError("Warsaw compact section did not contain 'weapons' element, unable to generate library.");
                        return false;
                    }
                    var compactWeapons = (Hashtable)compact["weapons"];
                    if (compactWeapons == null)
                    {
                        ConsoleError("Compact weapons section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact weapon accessory element
                    if (!compact.ContainsKey("weaponaccessory"))
                    {
                        ConsoleError("Warsaw compact section did not contain 'weaponaccessory' element, unable to generate library.");
                        return false;
                    }
                    var compactWeaponAccessory = (Hashtable)compact["weaponaccessory"];
                    if (compactWeaponAccessory == null)
                    {
                        ConsoleError("Weapon accessory section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact vehicles element
                    if (!compact.ContainsKey("vehicles"))
                    {
                        ConsoleError("Warsaw compact section did not contain 'vehicles' element, unable to generate library.");
                        return false;
                    }
                    var compactVehicles = (Hashtable)compact["vehicles"];
                    if (compactVehicles == null)
                    {
                        ConsoleError("Compact vehicles section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact kit items element
                    if (!compact.ContainsKey("kititems"))
                    {
                        ConsoleError("Warsaw compact section did not contain 'kititems' element, unable to generate library.");
                        return false;
                    }
                    var compactKitItems = (Hashtable)compact["kititems"];
                    if (compactKitItems == null)
                    {
                        ConsoleError("Kit items section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact vehicle unlocks element
                    if (!compact.ContainsKey("vehicleunlocks"))
                    {
                        ConsoleError("Warsaw compact section did not contain 'vehicleunlocks' element, unable to generate library.");
                        return false;
                    }
                    var compactVehicleUnlocks = (Hashtable)compact["vehicleunlocks"];
                    if (compactVehicleUnlocks == null)
                    {
                        ConsoleError("Vehicle unlocks section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout element
                    if (!responseData.ContainsKey("loadout"))
                    {
                        ConsoleError("WARSAW library fetch did not contain 'loadout' element, unable to generate library.");
                        return false;
                    }
                    var loadout = (Hashtable)responseData["loadout"];
                    if (loadout == null)
                    {
                        ConsoleError("Loadout section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout weapons element
                    if (!loadout.ContainsKey("weapons"))
                    {
                        ConsoleError("Warsaw loadout section did not contain 'weapons' element, unable to generate library.");
                        return false;
                    }
                    var loadoutWeapons = (Hashtable)loadout["weapons"];
                    if (loadoutWeapons == null)
                    {
                        ConsoleError("Loadout weapons section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout kits element
                    if (!loadout.ContainsKey("kits"))
                    {
                        ConsoleError("Warsaw loadout section did not contain 'kits' element, unable to generate library.");
                        return false;
                    }
                    var loadoutKits = (ArrayList)loadout["kits"];
                    if (loadoutKits == null)
                    {
                        ConsoleError("Loadout kits section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout vehicles element
                    if (!loadout.ContainsKey("vehicles"))
                    {
                        ConsoleError("Warsaw loadout section did not contain 'vehicles' element, unable to generate library.");
                        return false;
                    }
                    var loadoutVehicles = (ArrayList)loadout["vehicles"];
                    if (loadoutVehicles == null)
                    {
                        ConsoleError("Loadout vehicles section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }

                    ConsoleInfo("WARSAW library downloaded. Parsing.");
                    //Pause for effect, nothing else
                    Thread.Sleep(200);

                    library.Items.Clear();
                    foreach (DictionaryEntry entry in compactWeapons)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            if (false)
                                ConsoleError("Rejecting weapon element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        var item = new WarsawItem();
                        item.WarsawID = warsawID.ToString();

                        if (_displayLoadoutDebug)
                        {
                            ConsoleInfo("Loading debug warsaw ID " + item.WarsawID);
                        }

                        //Grab the contents
                        var weaponData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        item.Category = (String)weaponData["category"];
                        if (String.IsNullOrEmpty(item.Category))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        item.CategoryReadable = item.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        item.Name = (String)weaponData["name"];
                        if (String.IsNullOrEmpty(item.Name))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        item.Name = item.Name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab categoryType------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("categoryType"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'categoryType'.");
                            continue;
                        }
                        item.CategoryTypeReadable = (String)weaponData["categoryType"];
                        if (String.IsNullOrEmpty(item.CategoryTypeReadable))
                        {
                            item.CategoryTypeReadable = "General";
                        }
                        //Parsed categoryType does not require any modifications

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        item.Slug = (String)weaponData["slug"];
                        if (String.IsNullOrEmpty(item.Slug))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        item.Slug = item.Slug.Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        library.Items[item.WarsawID] = item;
                        if (false)
                            ConsoleSuccess("Weapon " + item.WarsawID + " added. " + library.Items.ContainsKey(item.WarsawID));
                    }

                    foreach (DictionaryEntry entry in compactKitItems)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            if (false)
                                ConsoleError("Rejecting kit item element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        var kitItem = new WarsawItem();
                        kitItem.WarsawID = warsawID.ToString();

                        if (_displayLoadoutDebug)
                        {
                            ConsoleInfo("Loading debug warsaw ID " + kitItem.WarsawID);
                        }

                        //Grab the contents
                        var weaponAccessoryData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category"))
                        {
                            if(_displayLoadoutDebug)
                                ConsoleError("Rejecting kit item '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        kitItem.Category = (String)weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(kitItem.Category))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting kit item '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        kitItem.CategoryReadable = kitItem.Category.Split('_').Last().Replace('_', ' ').ToUpper();
                        if (kitItem.CategoryReadable != "GADGET" && kitItem.CategoryReadable != "GRENADE")
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting kit item '" + warsawID + "'. 'category' not gadget or grenade.");
                            continue;
                        }

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting kit item '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        kitItem.Name = (String)weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(kitItem.Name))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting kit item '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        kitItem.Name = kitItem.Name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting kit item '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        kitItem.Slug = (String)weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(kitItem.Slug))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting kit item '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        kitItem.Slug = kitItem.Slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        if (String.IsNullOrEmpty(kitItem.CategoryTypeReadable))
                        {
                            kitItem.CategoryTypeReadable = "General";
                        }

                        //Assign the item
                        if (!library.Items.ContainsKey(kitItem.WarsawID))
                        {
                            library.Items[kitItem.WarsawID] = kitItem;
                            if (false)
                                ConsoleSuccess("Weapon " + kitItem.WarsawID + " added. " + library.Items.ContainsKey(kitItem.WarsawID));
                        }
                    }
                    ConsoleInfo("WARSAW items parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(200);

                    library.ItemAccessories.Clear();
                    foreach (DictionaryEntry entry in compactWeaponAccessory)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            if (false)
                                ConsoleError("Rejecting weapon accessory element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        var itemAccessory = new WarsawItemAccessory();
                        itemAccessory.WarsawID = warsawID.ToString();

                        //Grab the contents
                        var weaponAccessoryData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        itemAccessory.Category = (String)weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(itemAccessory.Category))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        itemAccessory.CategoryReadable = itemAccessory.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        itemAccessory.Name = (String)weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(itemAccessory.Name))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        itemAccessory.Name = itemAccessory.Name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        itemAccessory.Slug = (String)weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(itemAccessory.Slug))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting weapon accessory '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        itemAccessory.Slug = itemAccessory.Slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        library.ItemAccessories[itemAccessory.WarsawID] = itemAccessory;
                        if (false)
                            ConsoleSuccess("Weapon accessory " + itemAccessory.WarsawID + " added. " + library.ItemAccessories.ContainsKey(itemAccessory.WarsawID));
                    }
                    ConsoleInfo("WARSAW accessories parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(200);

                    library.Vehicles.Clear();
                    foreach (DictionaryEntry entry in compactVehicles) 
                    {
                        String category = (String) entry.Key;
                        if (!category.StartsWith("WARSAW_ID"))
                        {
                            //Reject the entry
                            if (_displayLoadoutDebug)
                                ConsoleInfo("Rejecting vehicle element '" + entry.Key + "', key not a valid ID.");
                            continue;
                        }

                        var vehicle = new WarsawVehicle();
                        vehicle.Category = category;
                        vehicle.CategoryReadable = vehicle.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab the contents
                        var vehicleData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!vehicleData.ContainsKey("categoryType"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting vehicle '" + category + "'. Element did not contain 'categoryType'.");
                            continue;
                        }
                        vehicle.CategoryType = (String)vehicleData["categoryType"];
                        if (String.IsNullOrEmpty(vehicle.CategoryType))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting vehicle '" + category + "'. 'categoryType' was invalid.");
                            continue;
                        }
                        vehicle.CategoryTypeReadable = vehicle.CategoryType;

                        //Assign the linked RCON codes
                        switch (vehicle.Category)
                        {
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEMBT":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/M1A2/M1Abrams");
                                vehicle.LinkedRCONCodes.Add("T90");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_MBT_Type99/CH_MBT_Type99");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEIFV":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/BTR-90/BTR90");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_IFV_ZBD09/CH_IFV_ZBD09");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/LAV25/LAV25");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEAA":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_AA_PGZ-95/CH_AA_PGZ-95");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/LAV25/LAV_AD");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/9K22_Tunguska_M/9K22_Tunguska_M");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKBOAT":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/US_FAC-CB90/US_FAC-CB90");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_FAC_DV15/CH_FAC_DV15");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_FAC_DV15/spec/CH_FAC_DV15_RU");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESTEALTHJET":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/F35/F35B");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/Ch_FJET_J-20/CH_FJET_J-20");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/RU_FJET_T-50_Pak_FA/RU_FJET_T-50_Pak_FA");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESCOUTHELI":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/AH6/AH6_Littlebird");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/Z11W/Z-11w");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/Z11W/spec/Z-11w_CH");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKHELI":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/AH1Z/AH1Z");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/Mi28/Mi28");
                                vehicle.LinkedRCONCodes.Add("Z-10w");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKJET":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/A-10_THUNDERBOLT/A10_THUNDERBOLT");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/SU-25TM/SU-25TM");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_JET_Qiang-5-fantan/CH_JET_Q5_FANTAN");
                                break;
                            default:
                                continue;
                        }

                        //Assign the vehicle
                        library.Vehicles[vehicle.Category] = vehicle;
                        if (_displayLoadoutDebug)
                            ConsoleSuccess("Vehicle " + vehicle.Category + " added. " + library.Vehicles.ContainsKey(vehicle.Category));
                    }
                    ConsoleInfo("WARSAW vehicles parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(200);

                    library.VehicleUnlocks.Clear();
                    foreach (DictionaryEntry entry in compactVehicleUnlocks)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            if (false)
                                ConsoleError("Rejecting vehicle unlock element '" + entry.Key + "', key not numeric.");
                            continue;
                        }
                        var vehicleUnlock = new WarsawItem();
                        vehicleUnlock.WarsawID = warsawID.ToString();

                        //Grab the contents
                        var vehicleUnlockData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        vehicleUnlock.Category = (String)vehicleUnlockData["category"];
                        if (String.IsNullOrEmpty(vehicleUnlock.Category))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        vehicleUnlock.CategoryReadable = vehicleUnlock.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab name------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting vehicle unlock'" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        var name = (String)vehicleUnlockData["name"];
                        if (String.IsNullOrEmpty(name))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        name = name.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab slug------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        vehicleUnlock.Slug = (String)vehicleUnlockData["slug"];
                        if (String.IsNullOrEmpty(vehicleUnlock.Slug))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting vehicle unlock '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        vehicleUnlock.Slug = vehicleUnlock.Slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').TrimEnd('3').TrimEnd('4').TrimEnd('5').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        library.VehicleUnlocks[vehicleUnlock.WarsawID] = vehicleUnlock;
                    }
                    ConsoleInfo("WARSAW vehicle unlocks parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(200);

                    //Fill allowed accessories for each weapon
                    foreach (DictionaryEntry entry in loadoutWeapons)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            if (false)
                                ConsoleError("Rejecting loadout weapon element '" + entry.Key + "', key not numeric.");
                            continue;
                        }

                        WarsawItem weapon;
                        if (!library.Items.TryGetValue(warsawID.ToString(CultureInfo.InvariantCulture), out weapon))
                        {
                            //Reject the entry
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting loadout weapon element '" + warsawID + "', ID not found in weapon library.");
                            continue;
                        }

                        //Grab the contents
                        var weaponData = (Hashtable)entry.Value;
                        if (weaponData == null)
                        {
                            ConsoleError("Rejecting loadout weapon element " + warsawID + ", could not parse weapon data.");
                            continue;
                        }
                        //Grab slots------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("slots"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("Rejecting loadout weapon element '" + warsawID + "'. Element did not contain 'slots'.");
                            continue;
                        }
                        var slots = (ArrayList)weaponData["slots"];
                        foreach (Object slotEntry in slots)
                        {
                            //Grab the contents
                            var slotTable = (Hashtable)slotEntry;
                            if (slotTable == null)
                            {
                                ConsoleError("Rejecting slot entry for " + warsawID + ", could not parse slot into hashtable.");
                                continue;
                            }
                            //Grab category------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("sid"))
                            {
                                if (_displayLoadoutDebug)
                                    ConsoleError("Rejecting slot entry for " + warsawID + ". Element did not contain 'sid'.");
                                continue;
                            }
                            var category = (String)slotTable["sid"];
                            //Reject all paint categories
                            if (category.Contains("PAINT")) {
                                continue;
                            }
                            //Grab items------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("items"))
                            {
                                if (_displayLoadoutDebug)
                                    ConsoleError("Rejecting slot entry for " + warsawID + ". Element did not contain 'items'.");
                                continue;
                            }
                            var items = (ArrayList)slotTable["items"];
                            Dictionary<String, WarsawItemAccessory> allowedItems;
                            if (weapon.AccessoriesAllowed.ContainsKey(category)) {
                                //Existing list, add to it
                                allowedItems = weapon.AccessoriesAllowed[category];
                            }
                            else {
                                //New list, add it
                                allowedItems = new Dictionary<String, WarsawItemAccessory>();
                                weapon.AccessoriesAllowed[category] = allowedItems;
                            }
                            foreach (String accessoryID in items)
                            {
                                //Attempt to fetch accessory from library
                                WarsawItemAccessory accessory;
                                if (library.ItemAccessories.TryGetValue(accessoryID, out accessory)) {
                                    allowedItems[accessoryID] = accessory;
                                }
                                else
                                {
                                    if (_displayLoadoutDebug)
                                        ConsoleError("Rejecting allowed accessory entry for " + accessoryID + ". Accessory not found in library.");
                                }
                            }
                        }
                    }
                    ConsoleInfo("WARSAW allowed weapon accessories parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(200);

                    //Fill allowed items for each class
                    foreach (Hashtable entry in loadoutKits)
                    {
                        //Get the kit key
                        if (!entry.ContainsKey("sid"))
                        {
                            ConsoleError("Kit entry did not contain 'sid' element, unable to generate library.");
                            return false;
                        }
                        var kitKey = (String)entry["sid"];

                        WarsawKit kit;
                        switch (kitKey) {
                            case "WARSAW_ID_M_ASSAULT":
                                kit = library.KitAssault;
                                break;
                            case "WARSAW_ID_M_ENGINEER":
                                kit = library.KitEngineer;
                                break;
                            case "WARSAW_ID_M_SUPPORT":
                                kit = library.KitSupport;
                                break;
                            case "WARSAW_ID_M_RECON":
                                kit = library.KitRecon;
                                break;
                            default:
                                ConsoleError("Kit entry could not be assigned to a valid kit type, unable to generate library.");
                                return false;
                        }

                        //Grab slots------------------------------------------------------------------------------
                        if (!entry.ContainsKey("slots"))
                        {
                            ConsoleError("Kit entry '" + kitKey + "' did not contain 'slots' element, unable to generate library.");
                            return false;
                        }
                        var slots = (ArrayList)entry["slots"];
                        foreach (Object slotEntry in slots)
                        {
                            //Grab the contents
                            var slotTable = (Hashtable)slotEntry;
                            if (slotTable == null)
                            {
                                ConsoleError("Slot entry for kit '" + kitKey + "', could not parse slot into hashtable, unable to generate library.");
                                return false;
                            }
                            //Grab category------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("sid"))
                            {
                                ConsoleError("Slot entry for kit '" + kitKey + "', did not contain 'sid' element, unable to generate library.");
                                return false;
                            }
                            var category = (String)slotTable["sid"];
                            //Reject all paint categories
                            if (category.Contains("PAINT"))
                            {
                                continue;
                            }
                            //Grab items------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("items"))
                            {
                                if (_displayLoadoutDebug)
                                    ConsoleError("Rejecting slot entry '" + category + "' for class '" + kitKey + "', element did not contain 'items'.");
                                continue;
                            }
                            var items = (ArrayList)slotTable["items"];

                            //Decide which structure is being filled for this slot
                            Dictionary<String, WarsawItem> allowedItems;
                            switch (category)
                            {
                                case "WARSAW_ID_M_SOLDIER_PRIMARY":
                                    allowedItems = kit.KitAllowedPrimary;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_SECONDARY":
                                    allowedItems = kit.KitAllowedSecondary;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_GADGET1":
                                    allowedItems = kit.KitAllowedGadget1;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_GADGET2":
                                    allowedItems = kit.KitAllowedGadget2;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_GRENADES":
                                    allowedItems = kit.KitAllowedGrenades;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_KNIFE":
                                    allowedItems = kit.KitAllowedKnife;
                                    break;
                                default:
                                    if (_displayLoadoutDebug)
                                        ConsoleInfo("Rejecting slot item entry '" + category + "' for class '" + kitKey + "'.");
                                    continue;
                            }

                            foreach (String itemID in items)
                            {
                                //Attempt to fetch item from library
                                WarsawItem item;
                                if (library.Items.TryGetValue(itemID, out item))
                                {
                                    allowedItems[itemID] = item;
                                }
                                else
                                {
                                    if (_displayLoadoutDebug)
                                        ConsoleError("Rejecting allowed item entry " + itemID + ". Item not found in library.");
                                }
                            }
                        }
                        if (_displayLoadoutDebug)
                            ConsoleInfo(kit.KitType + " parsed. Allowed: " + kit.KitAllowedPrimary.Count + " primary weapons, " + kit.KitAllowedSecondary.Count + " secondary weapons, " + kit.KitAllowedGadget1.Count + " primary gadgets, " + kit.KitAllowedGadget2.Count + " secondary gadgets, " + kit.KitAllowedGrenades.Count + " grenades, and " + kit.KitAllowedKnife.Count + " knives.");
                    }
                    ConsoleInfo("WARSAW allowed kit weapons parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(200);

                    //Fill allowed items for each vehicle
                    foreach (Hashtable entry in loadoutVehicles)
                    {
                        //Get the kit key
                        if (!entry.ContainsKey("sid"))
                        {
                            if(_displayLoadoutDebug)
                                ConsoleWarn("Vehicle entry did not contain 'sid' element, skipping.");
                            continue;
                        }
                        var vehicleCategory = (String)entry["sid"];

                        //Reject all non-EOR entries
                        if (!vehicleCategory.Contains("WARSAW_ID_EOR"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Vehicle entry was not an EOR entry, skipping.");
                            continue;
                        }

                        WarsawVehicle vehicle;
                        if (!library.Vehicles.TryGetValue(vehicleCategory, out vehicle))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Vehicle category " + vehicleCategory + " not found, skipping.");
                            continue;
                        }

                        //Grab slots------------------------------------------------------------------------------
                        if (!entry.ContainsKey("slots"))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Vehicle entry '" + vehicleCategory + "' did not contain 'slots' element, skipping.");
                            continue;
                        }
                        var slots = (ArrayList)entry["slots"];
                        Int32 slotIndex = 0;
                        foreach (Object slotEntry in slots)
                        {
                            //Grab the contents
                            var slotTable = (Hashtable)slotEntry;
                            if (slotTable == null)
                            {
                                ConsoleError("Slot entry for vehicle '" + vehicleCategory + "', could not parse slot into hashtable, unable to generate library.");
                                return false;
                            }
                            //Grab category------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("sid"))
                            {
                                ConsoleError("Slot entry for vehicle '" + vehicleCategory + "', did not contain 'sid' element, unable to generate library.");
                                return false;
                            }
                            var category = (String)slotTable["sid"];
                            //Reject all paint categories
                            if (category.Contains("PAINT"))
                            {
                                slotIndex++;
                                continue;
                            }
                            //Grab items------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("items"))
                            {
                                if (_displayLoadoutDebug)
                                    ConsoleError("Rejecting slot entry '" + category + "' for vehicle '" + vehicleCategory + "', element did not contain 'items'.");
                                slotIndex++;
                                continue;
                            }
                            var items = (ArrayList)slotTable["items"];

                            //Decide which structure is being filled for this slot
                            Dictionary<String, WarsawItem> allowedUnlocks;
                            switch (category)
                            {
                                case "WARSAW_ID_P_CAT_PRIMARY":
                                    vehicle.SlotIndexPrimary = slotIndex;
                                    allowedUnlocks = vehicle.AllowedPrimaries;
                                    break;
                                case "WARSAW_ID_P_CAT_SECONDARY":
                                    vehicle.SlotIndexSecondary = slotIndex;
                                    allowedUnlocks = vehicle.AllowedSecondaries;
                                    break;
                                case "WARSAW_ID_P_CAT_COUNTERMEASURE":
                                    vehicle.SlotIndexCountermeasure = slotIndex;
                                    allowedUnlocks = vehicle.AllowedCountermeasures;
                                    break;
                                case "WARSAW_ID_P_CAT_SIMPLE_OPTICS":
                                    vehicle.SlotIndexOptic = slotIndex;
                                    allowedUnlocks = vehicle.AllowedOptics;
                                    break;
                                case "WARSAW_ID_P_CAT_UPGRADES":
                                case "WARSAW_ID_P_CAT_UPGRADE":
                                    vehicle.SlotIndexUpgrade = slotIndex;
                                    allowedUnlocks = vehicle.AllowedUpgrades;
                                    break;
                                case "WARSAW_ID_P_CAT_GUNNER_SECONDARY":
                                    vehicle.SlotIndexSecondaryGunner = slotIndex;
                                    allowedUnlocks = vehicle.AllowedSecondariesGunner;
                                    break;
                                case "WARSAW_ID_P_CAT_GUNNER_OPTICS":
                                    vehicle.SlotIndexOpticGunner = slotIndex;
                                    allowedUnlocks = vehicle.AllowedOpticsGunner;
                                    break;
                                case "WARSAW_ID_P_CAT_GUNNER_UPGRADE":
                                    vehicle.SlotIndexUpgradeGunner = slotIndex;
                                    allowedUnlocks = vehicle.AllowedUpgradesGunner;
                                    break;
                                default:
                                    if (_displayLoadoutDebug)
                                        ConsoleInfo("Rejecting slot item entry '" + category + "' for vehicle '" + vehicleCategory + "'.");
                                    slotIndex++;
                                    continue;
                            }

                            foreach (String unlockID in items)
                            {
                                //Attempt to fetch item from library
                                WarsawItem item;
                                if (library.VehicleUnlocks.TryGetValue(unlockID, out item))
                                {
                                    allowedUnlocks[unlockID] = item;
                                    //Assign the vehicle
                                    if (item.AssignedVehicle == null) {
                                        item.AssignedVehicle = vehicle;
                                    }
                                    else {
                                        ConsoleWarn(unlockID + " already assigned to a vehicle, " + item.AssignedVehicle.CategoryType);
                                    }
                                }
                                else
                                {
                                    if (_displayLoadoutDebug)
                                        ConsoleError("Rejecting allowed unlock entry " + unlockID + ". Item not found in library.");
                                }
                            }
                            slotIndex++;
                        }
                        if (_displayLoadoutDebug)
                            ConsoleInfo(vehicle.CategoryType + " parsed. Allowed: " + 
                                vehicle.AllowedPrimaries.Count + " primary weapons, " + 
                                vehicle.AllowedSecondaries.Count + " secondary weapons, " +
                                vehicle.AllowedCountermeasures.Count + " countermeasures, " +
                                vehicle.AllowedOptics.Count + " optics, " +
                                vehicle.AllowedUpgrades.Count + " upgrades, " +
                                vehicle.AllowedSecondariesGunner.Count + " gunner secondary weapons, " +
                                vehicle.AllowedOpticsGunner.Count + " gunner optics, and " +
                                vehicle.AllowedUpgradesGunner.Count + " gunner upgrades. ");
                    }
                    ConsoleInfo("WARSAW allowed vehicle unlocks parsed.");
                    //Pause for effect, nothing else
                    Thread.Sleep(200);

                    _WARSAWLibrary = library;
                    _WARSAWLibraryLoaded = true;
                    UpdateSettingPage();
                    return true;
                }
                ConsoleError("Game not BF4, unable to process WARSAW library.");
                return false;
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while parsing WARSAW library.", e));
            }
            DebugWrite("Exiting LoadWarsawLibrary", 7);
            return false;
        }

        private Hashtable FetchWarsawLibrary()
        {
            Hashtable library = null;
            try
            {
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
            catch (Exception e)
            {
                HandleException(new AdKatsException("Unexpected error while fetching WARSAW library", e));
                return null;
            }
            return library;
        }

        private AdKatsLoadout GetPlayerLoadout(String personaID)
        {
            DebugWrite("Entering GetPlayerLoadout", 7);
            try
            {
                Hashtable responseData = null;
                if (_gameVersion == GameVersion.BF4)
                {
                    var loadout = new AdKatsLoadout();
                    responseData = FetchPlayerLoadout(personaID);
                    if (responseData == null)
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Loadout fetch failed, unable to parse player loadout.");
                        return null;
                    }
                    if (!responseData.ContainsKey("data"))
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Loadout fetch did not contain 'data' element, unable to parse player loadout.");
                        return null;
                    }
                    var data = (Hashtable)responseData["data"];
                    if (data == null)
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Data section of loadout failed parse, unable to parse player loadout.");
                        return null;
                    }
                    //Get parsed back persona ID
                    if (!data.ContainsKey("personaId"))
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Data section of loadout did not contain 'personaId' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.PersonaID = data["personaId"].ToString();
                    //Get persona name
                    if (!data.ContainsKey("personaName"))
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Data section of loadout did not contain 'personaName' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.Name = data["personaName"].ToString();
                    //Get weapons and their attachements
                    if (!data.ContainsKey("currentLoadout"))
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Data section of loadout did not contain 'currentLoadout' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutHashtable = (Hashtable)data["currentLoadout"];
                    if (currentLoadoutHashtable == null)
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Current loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    if (!currentLoadoutHashtable.ContainsKey("weapons"))
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Current loadout section did not contain 'weapons' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutWeapons = (Hashtable)currentLoadoutHashtable["weapons"];
                    if (currentLoadoutWeapons == null)
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Weapon loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    if (!currentLoadoutHashtable.ContainsKey("vehicles"))
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Current loadout section did not contain 'vehicles' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutVehicles = (ArrayList)currentLoadoutHashtable["vehicles"];
                    if (currentLoadoutVehicles == null)
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Vehicles loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    foreach (DictionaryEntry weaponEntry in currentLoadoutWeapons)
                    {
                        if (weaponEntry.Key.ToString() != "0")
                        {
                            WarsawItem warsawItem;
                            if (_WARSAWLibrary.Items.TryGetValue(weaponEntry.Key.ToString(), out warsawItem))
                            {
                                //Create new instance of the weapon for this player
                                var loadoutItem = new WarsawItem()
                                {
                                    WarsawID = warsawItem.WarsawID,
                                    CategoryReadable = warsawItem.CategoryReadable,
                                    CategoryTypeReadable = warsawItem.CategoryTypeReadable,
                                    Name = warsawItem.Name,
                                    Slug = warsawItem.Slug
                                };
                                foreach (String accessoryID in (ArrayList)weaponEntry.Value)
                                {
                                    if (accessoryID != "0")
                                    {
                                        WarsawItemAccessory warsawItemAccessory;
                                        if (_WARSAWLibrary.ItemAccessories.TryGetValue(accessoryID, out warsawItemAccessory))
                                        {
                                            loadoutItem.AccessoriesAssigned[warsawItemAccessory.WarsawID] = warsawItemAccessory;
                                        }
                                    }
                                }
                                loadout.LoadoutItems[loadoutItem.WarsawID] = loadoutItem;
                            }
                        }
                    }

                    //Parse vehicles
                    for (Int32 index = 0; index < currentLoadoutVehicles.Count; index++) {
                        WarsawVehicle libraryVehicle;
                        switch (index)
                        {
                            case 0:
                                //MBT
                                if (!_WARSAWLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEMBT", out libraryVehicle)) {
                                    ConsoleError("Failed to fetch MBT vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 1:
                                //IFV
                                if (!_WARSAWLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEIFV", out libraryVehicle))
                                {
                                    ConsoleInfo("Failed to fetch IFV vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 2:
                                //AA
                                if (!_WARSAWLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEAA", out libraryVehicle))
                                {
                                    ConsoleInfo("Failed to fetch AA vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 3:
                                //Boat
                                if (!_WARSAWLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKBOAT", out libraryVehicle))
                                {
                                    ConsoleInfo("Failed to fetch Boat vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 4:
                                //Stealth
                                if (!_WARSAWLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESTEALTHJET", out libraryVehicle))
                                {
                                    ConsoleInfo("Failed to fetch Stealth vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 5:
                                //Scout
                                if (!_WARSAWLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESCOUTHELI", out libraryVehicle))
                                {
                                    ConsoleInfo("Failed to fetch Scout vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 6:
                                //AttkHeli
                                if (!_WARSAWLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKHELI", out libraryVehicle))
                                {
                                    ConsoleInfo("Failed to fetch AttkHeli vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 7:
                                //AttkJet
                                if (!_WARSAWLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKJET", out libraryVehicle))
                                {
                                    ConsoleInfo("Failed to fetch AttkJet vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            default:
                                continue;
                        }
                        //Duplicate the vehicle
                        var vehicle = new WarsawVehicle() {
                            Category = libraryVehicle.Category,
                            CategoryReadable = libraryVehicle.CategoryReadable,
                            CategoryType = libraryVehicle.CategoryType,
                            CategoryTypeReadable = libraryVehicle.CategoryTypeReadable,
                            LinkedRCONCodes = libraryVehicle.LinkedRCONCodes
                        };
                        //Fetch the vehicle items
                        var vehicleItems = (ArrayList)currentLoadoutVehicles[index];
                        //Assign the primary
                        if (libraryVehicle.SlotIndexPrimary >= 0) {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexPrimary];
                            if (!libraryVehicle.AllowedPrimaries.TryGetValue(itemID, out vehicle.AssignedPrimary)) {
                                var defaultItem = libraryVehicle.AllowedPrimaries.Values.First();
                                if(_displayLoadoutDebug)
                                    ConsoleWarn("Unable to fetch valid vehicle primary " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedPrimary = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedPrimary.WarsawID] = vehicle.AssignedPrimary;
                        }
                        //Assign the secondary
                        if (libraryVehicle.SlotIndexSecondary >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexSecondary];
                            if (!libraryVehicle.AllowedSecondaries.TryGetValue(itemID, out vehicle.AssignedSecondary))
                            {
                                var defaultItem = libraryVehicle.AllowedSecondaries.Values.First();
                                if (_displayLoadoutDebug)
                                    ConsoleWarn("Unable to fetch valid vehicle secondary " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedSecondary = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedSecondary.WarsawID] = vehicle.AssignedSecondary;
                        }
                        //Assign the countermeasure
                        if (libraryVehicle.SlotIndexCountermeasure >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexCountermeasure];
                            if (!libraryVehicle.AllowedCountermeasures.TryGetValue(itemID, out vehicle.AssignedCountermeasure))
                            {
                                var defaultItem = libraryVehicle.AllowedCountermeasures.Values.First();
                                if (_displayLoadoutDebug)
                                    ConsoleWarn("Unable to fetch valid vehicle countermeasure " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedCountermeasure = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedCountermeasure.WarsawID] = vehicle.AssignedCountermeasure;
                        }
                        //Assign the optic
                        if (libraryVehicle.SlotIndexOptic >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexOptic];
                            if (!libraryVehicle.AllowedOptics.TryGetValue(itemID, out vehicle.AssignedOptic))
                            {
                                var defaultItem = libraryVehicle.AllowedOptics.Values.First();
                                if (_displayLoadoutDebug)
                                    ConsoleWarn("Unable to fetch valid vehicle optic " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedOptic = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedOptic.WarsawID] = vehicle.AssignedOptic;
                        }
                        //Assign the upgrade
                        if (libraryVehicle.SlotIndexUpgrade >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexUpgrade];
                            if (!libraryVehicle.AllowedUpgrades.TryGetValue(itemID, out vehicle.AssignedUpgrade))
                            {
                                var defaultItem = libraryVehicle.AllowedUpgrades.Values.First();
                                if (_displayLoadoutDebug)
                                    ConsoleWarn("Unable to fetch valid vehicle upgrade " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedUpgrade = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedUpgrade.WarsawID] = vehicle.AssignedUpgrade;
                        }
                        //Assign the gunner secondary
                        if (libraryVehicle.SlotIndexSecondaryGunner >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexSecondaryGunner];
                            if (!libraryVehicle.AllowedSecondariesGunner.TryGetValue(itemID, out vehicle.AssignedSecondaryGunner))
                            {
                                var defaultItem = libraryVehicle.AllowedSecondariesGunner.Values.First();
                                if (_displayLoadoutDebug)
                                    ConsoleWarn("Unable to fetch valid vehicle gunner secondary " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedSecondaryGunner = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedSecondaryGunner.WarsawID] = vehicle.AssignedSecondaryGunner;
                        }
                        //Assign the gunner optic
                        if (libraryVehicle.SlotIndexOpticGunner >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexOpticGunner];
                            if (!libraryVehicle.AllowedOpticsGunner.TryGetValue(itemID, out vehicle.AssignedOpticGunner))
                            {
                                var defaultItem = libraryVehicle.AllowedOpticsGunner.Values.First();
                                if (_displayLoadoutDebug)
                                    ConsoleWarn("Unable to fetch valid vehicle gunner optic " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedOpticGunner = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedOpticGunner.WarsawID] = vehicle.AssignedOpticGunner;
                        }
                        //Assign the gunner upgrade
                        if (libraryVehicle.SlotIndexUpgradeGunner >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexUpgradeGunner];
                            if (!libraryVehicle.AllowedUpgradesGunner.TryGetValue(itemID, out vehicle.AssignedUpgradeGunner))
                            {
                                var defaultItem = libraryVehicle.AllowedUpgradesGunner.Values.First();
                                if (_displayLoadoutDebug)
                                    ConsoleWarn("Unable to fetch valid vehicle gunner upgrade " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedUpgradeGunner = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedUpgradeGunner.WarsawID] = vehicle.AssignedUpgradeGunner;
                        }
                        loadout.LoadoutVehicles[vehicle.Category] = vehicle;
                        foreach (String RCONCode in vehicle.LinkedRCONCodes) {
                            loadout.LoadoutRCONVehicles[RCONCode] = vehicle;
                        }
                        if(_displayLoadoutDebug)
                            ConsoleInfo(loadout.Name + ": " +
                                vehicle.CategoryType + ": " +
                                ((vehicle.AssignedPrimary == null) ? ("No Primary") : (vehicle.AssignedPrimary.Slug)) + ", " +
                                ((vehicle.AssignedSecondary == null) ? ("No Secondary") : (vehicle.AssignedSecondary.Slug)) + ", " +
                                ((vehicle.AssignedCountermeasure == null) ? ("No Countermeasure") : (vehicle.AssignedCountermeasure.Slug)) + ", " +
                                ((vehicle.AssignedOptic == null) ? ("No Optic") : (vehicle.AssignedOptic.Slug)) + ", " +
                                ((vehicle.AssignedUpgrade == null) ? ("No Upgrade") : (vehicle.AssignedUpgrade.Slug)) + ", " +
                                ((vehicle.AssignedSecondaryGunner == null) ? ("No Gunner Secondary") : (vehicle.AssignedSecondaryGunner.Slug)) + ", " +
                                ((vehicle.AssignedOpticGunner == null) ? ("No Gunner Optic") : (vehicle.AssignedOpticGunner.Slug)) + ", " +
                                ((vehicle.AssignedUpgradeGunner == null) ? ("No Gunner Upgrade") : (vehicle.AssignedUpgradeGunner.Slug)) + ".");
                    }
                    if (!currentLoadoutHashtable.ContainsKey("selectedKit"))
                    {
                        if (_displayLoadoutDebug)
                            ConsoleError("Current loadout section did not contain 'selectedKit' element, unable to parse player loadout.");
                        return null;
                    }
                    String selectedKit = currentLoadoutHashtable["selectedKit"].ToString();
                    ArrayList currentLoadoutList;
                    String loadoutPrimaryID, loadoutSidearmID, loadoutGadget1ID, loadoutGadget2ID, loadoutGrenadeID, loadoutKnifeID;
                    switch (selectedKit)
                    {
                        case "0":
                            loadout.SelectedKit = _WARSAWLibrary.KitAssault;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[0];
                            break;
                        case "1":
                            loadout.SelectedKit= _WARSAWLibrary.KitEngineer;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[1];
                            break;
                        case "2":
                            loadout.SelectedKit = _WARSAWLibrary.KitSupport;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[2];
                            break;
                        case "3":
                            loadout.SelectedKit = _WARSAWLibrary.KitRecon;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[3];
                            break;
                        default:
                            if (_displayLoadoutDebug)
                                ConsoleError("Unable to parse selected kit " + selectedKit + ", value is unknown. Unable to parse player loadout.");
                            return null;
                    }
                    if (currentLoadoutList.Count < 6)
                    {
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
                    String defaultGadget2 = "3164552276"; //nogadget2

                    loadoutGrenadeID = currentLoadoutList[4].ToString();
                    String defaultGrenade = "2670747868"; //m67-frag

                    loadoutKnifeID = currentLoadoutList[5].ToString();
                    String defaultKnife = "3214146841"; //bayonett

                    //PRIMARY
                    WarsawItem loadoutPrimary;
                    String specificDefault;
                    switch (loadout.SelectedKit.KitType)
                    {
                        case WarsawKit.Type.Assault:
                            specificDefault = defaultAssaultPrimary;
                            break;
                        case WarsawKit.Type.Engineer:
                            specificDefault = defaultEngineerPrimary;
                            break;
                        case WarsawKit.Type.Support:
                            specificDefault = defaultSupportPrimary;
                            break;
                        case WarsawKit.Type.Recon:
                            specificDefault = defaultReconPrimary;
                            break;
                        default:
                            if (_displayLoadoutDebug)
                                ConsoleError("Specific kit type not set while assigning primary weapon default. Unable to parse player loadout.");
                            return null;
                    }
                    //Attempt to fetch PRIMARY from library
                    if (!loadout.LoadoutItems.TryGetValue(loadoutPrimaryID, out loadoutPrimary))
                    {
                        if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutPrimary.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm PRIMARY is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedPrimary.ContainsKey(loadoutPrimary.WarsawID))
                    {
                        WarsawItem originalItem = loadoutPrimary;
                        if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " was not valid for " + loadout.SelectedKit.KitType + " kit. Defaulting to " + loadoutPrimary.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for " + loadout.SelectedKit.KitType + " " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitItemPrimary = loadoutPrimary;

                    //SIDEARM
                    WarsawItem loadoutSidearm;
                    //Attempt to fetch SIDEARM from library
                    if (!loadout.LoadoutItems.TryGetValue(loadoutSidearmID, out loadoutSidearm))
                    {
                        if (loadout.LoadoutItems.TryGetValue(defaultSidearm, out loadoutSidearm))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific SIDEARM (" + loadoutSidearmID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutSidearm.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid SIDEARM (" + loadoutSidearmID + "->" + defaultSidearm + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm SIDEARM is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedSecondary.ContainsKey(loadoutSidearm.WarsawID))
                    {
                        WarsawItem originalItem = loadoutSidearm;
                        if (loadout.LoadoutItems.TryGetValue(defaultSidearm, out loadoutSidearm))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific SIDEARM (" + loadoutSidearmID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a SIDEARM. Defaulting to " + loadoutSidearm.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid SIDEARM (" + loadoutSidearmID + "->" + defaultSidearm + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitItemSidearm = loadoutSidearm;

                    //GADGET1
                    WarsawItem loadoutGadget1;
                    //Attempt to fetch GADGET1 from library
                    if (!_WARSAWLibrary.Items.TryGetValue(loadoutGadget1ID, out loadoutGadget1))
                    {
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGadget1, out loadoutGadget1))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GADGET1 (" + loadoutGadget1ID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGadget1.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GADGET1 (" + loadoutGadget1ID + "->" + defaultGadget1 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm GADGET1 is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedGadget1.ContainsKey(loadoutGadget1.WarsawID))
                    {
                        WarsawItem originalItem = loadoutGadget1;
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGadget1, out loadoutGadget1))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GADGET1 (" + loadoutGadget1ID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a GADGET. Defaulting to " + loadoutGadget1.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GADGET1 (" + loadoutGadget1ID + "->" + defaultGadget1 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGadget1 = loadoutGadget1;

                    //GADGET2
                    WarsawItem loadoutGadget2;
                    //Attempt to fetch GADGET2 from library
                    if (!_WARSAWLibrary.Items.TryGetValue(loadoutGadget2ID, out loadoutGadget2))
                    {
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGadget2, out loadoutGadget2))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GADGET2 (" + loadoutGadget2ID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGadget2.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GADGET2 (" + loadoutGadget2ID + "->" + defaultGadget2 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm GADGET2 is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedGadget2.ContainsKey(loadoutGadget2.WarsawID))
                    {
                        WarsawItem originalItem = loadoutGadget2;
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGadget2, out loadoutGadget2))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GADGET2 (" + loadoutGadget2ID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a GADGET. Defaulting to " + loadoutGadget2.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GADGET2 (" + loadoutGadget2ID + "->" + defaultGadget2 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGadget2 = loadoutGadget2;

                    //GRENADE
                    WarsawItem loadoutGrenade;
                    //Attempt to fetch GRENADE from library
                    if (!_WARSAWLibrary.Items.TryGetValue(loadoutGrenadeID, out loadoutGrenade))
                    {
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGrenade, out loadoutGrenade))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GRENADE (" + loadoutGrenadeID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGrenade.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GRENADE (" + loadoutGrenadeID + "->" + defaultGrenade + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm GRENADE is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedGrenades.ContainsKey(loadoutGrenade.WarsawID))
                    {
                        WarsawItem originalItem = loadoutGrenade;
                        if (_WARSAWLibrary.Items.TryGetValue(defaultGrenade, out loadoutGrenade))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific GRENADE (" + loadoutGrenadeID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a GRENADE. Defaulting to " + loadoutGrenade.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid GRENADE (" + loadoutGrenadeID + "->" + defaultGrenade + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGrenade = loadoutGrenade;

                    //KNIFE
                    WarsawItem loadoutKnife;
                    //Attempt to fetch KNIFE from library
                    if (!_WARSAWLibrary.Items.TryGetValue(loadoutKnifeID, out loadoutKnife))
                    {
                        if (_WARSAWLibrary.Items.TryGetValue(defaultKnife, out loadoutKnife))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific KNIFE (" + loadoutKnifeID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutKnife.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid KNIFE (" + loadoutKnifeID + "->" + defaultKnife + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm KNIFE is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedKnife.ContainsKey(loadoutKnife.WarsawID))
                    {
                        WarsawItem originalItem = loadoutKnife;
                        if (_WARSAWLibrary.Items.TryGetValue(defaultKnife, out loadoutKnife))
                        {
                            if (_displayLoadoutDebug)
                                ConsoleWarn("Specific KNIFE (" + loadoutKnifeID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a KNIFE. Defaulting to " + loadoutKnife.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                ConsoleError("No valid KNIFE (" + loadoutKnifeID + "->" + defaultKnife + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitKnife = loadoutKnife;

                    //Fill the kit ID listings
                    if (!loadout.AllKitItemIDs.Contains(loadoutPrimary.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutPrimary.WarsawID);
                    }
                    foreach (WarsawItemAccessory accessory in loadoutPrimary.AccessoriesAssigned.Values)
                    {
                        if (!loadout.AllKitItemIDs.Contains(accessory.WarsawID))
                        {
                            loadout.AllKitItemIDs.Add(accessory.WarsawID);
                        }
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutSidearm.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutSidearm.WarsawID);
                    }
                    foreach (WarsawItemAccessory accessory in loadoutSidearm.AccessoriesAssigned.Values)
                    {
                        if (!loadout.AllKitItemIDs.Contains(accessory.WarsawID))
                        {
                            loadout.AllKitItemIDs.Add(accessory.WarsawID);
                        }
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutGadget1.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutGadget1.WarsawID);
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutGadget2.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutGadget2.WarsawID);
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutGrenade.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutGrenade.WarsawID);
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutKnife.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutKnife.WarsawID);
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

        private Hashtable FetchPlayerLoadout(String personaID)
        {
            Hashtable loadout = null;
            try
            {
                using (var client = new WebClient())
                {
                    try
                    {
                        DoBattlelogWait();
                        String response = client.DownloadString("http://battlelog.battlefield.com/bf4/loadout/get/PLAYER/" + personaID + "/1/");
                        loadout = (Hashtable)JSON.JsonDecode(response);
                    }
                    catch (Exception e)
                    {
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

        public String ExtractString(String s, String tag)
        {
            if (String.IsNullOrEmpty(s) || String.IsNullOrEmpty(tag))
            {
                ConsoleError("Unable to extract String. Invalid inputs.");
                return null;
            }
            String startTag = "<" + tag + ">";
            Int32 startIndex = s.IndexOf(startTag, StringComparison.Ordinal) + startTag.Length;
            if (startIndex == -1)
            {
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

        public String FormatTimeString(TimeSpan timeSpan, Int32 maxComponents)
        {
            DebugWrite("Entering formatTimeString", 7);
            String timeString = null;
            if (maxComponents < 1)
            {
                return timeString;
            }
            try
            {
                String formattedTime = (timeSpan.TotalMilliseconds >= 0) ? ("") : ("-");

                Double secondSubset = Math.Abs(timeSpan.TotalSeconds);
                if (secondSubset < 1)
                {
                    return "0s";
                }
                Double minuteSubset = (secondSubset / 60);
                Double hourSubset = (minuteSubset / 60);
                Double daySubset = (hourSubset / 24);
                Double weekSubset = (daySubset / 7);
                Double monthSubset = (weekSubset / 4);
                Double yearSubset = (monthSubset / 12);

                var years = (Int32)yearSubset;
                Int32 months = (Int32)monthSubset % 12;
                Int32 weeks = (Int32)weekSubset % 4;
                Int32 days = (Int32)daySubset % 7;
                Int32 hours = (Int32)hourSubset % 24;
                Int32 minutes = (Int32)minuteSubset % 60;
                Int32 seconds = (Int32)secondSubset % 60;

                Int32 usedComponents = 0;
                if (years > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += years + "y";
                }
                if (months > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += months + "M";
                }
                if (weeks > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += weeks + "w";
                }
                if (days > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += days + "d";
                }
                if (hours > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += hours + "h";
                }
                if (minutes > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += minutes + "m";
                }
                if (seconds > 0 && usedComponents < maxComponents)
                {
                    usedComponents++;
                    formattedTime += seconds + "s";
                }
                timeString = formattedTime;
            }
            catch (Exception e)
            {
                HandleException(new AdKatsException("Error while formatting time String.", e));
            }
            if (String.IsNullOrEmpty(timeString))
            {
                timeString = "0s";
            }
            DebugWrite("Exiting formatTimeString", 7);
            return timeString;
        }

        public String FormatMessage(String msg, ConsoleMessageType type)
        {
            String prefix = "[^bAdKatsLRT^n] ";
            switch (type)
            {
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

        public String BoldMessage(String msg)
        {
            return "^b" + msg + "^n";
        }

        public String ItalicMessage(String msg)
        {
            return "^i" + msg + "^n";
        }

        public String ColorMessageMaroon(String msg)
        {
            return "^1" + msg + "^0";
        }

        public String ColorMessageGreen(String msg)
        {
            return "^2" + msg + "^0";
        }

        public String ColorMessageOrange(String msg)
        {
            return "^3" + msg + "^0";
        }

        public String ColorMessageBlue(String msg)
        {
            return "^4" + msg + "^0";
        }

        public String ColorMessageBlueLight(String msg)
        {
            return "^5" + msg + "^0";
        }

        public String ColorMessageViolet(String msg)
        {
            return "^6" + msg + "^0";
        }

        public String ColorMessagePink(String msg)
        {
            return "^7" + msg + "^0";
        }

        public String ColorMessageRed(String msg)
        {
            return "^8" + msg + "^0";
        }

        public String ColorMessageGrey(String msg)
        {
            return "^9" + msg + "^0";
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

        public void ProconChatWrite(String msg)
        {
            msg = msg.Replace(Environment.NewLine, String.Empty);
            ExecuteCommand("procon.protected.chat.write", "AdKatsLRT > " + msg);
            if (_slowmo)
            {
                _threadMasterWaitHandle.WaitOne(1000);
            }
        }

        public void ConsoleWrite(String msg, ConsoleMessageType type)
        {
            ExecuteCommand("procon.protected.pluginconsole.write", FormatMessage(msg, type));
            if (_slowmo)
            {
                _threadMasterWaitHandle.WaitOne(1000);
            }
        }

        public void ConsoleWrite(String msg)
        {
            ConsoleWrite(msg, ConsoleMessageType.Normal);
        }

        public void ConsoleInfo(String msg)
        {
            ConsoleWrite(msg, ConsoleMessageType.Info);
        }

        public void ConsoleWarn(String msg)
        {
            ConsoleWrite(msg, ConsoleMessageType.Warning);
        }

        public void ConsoleError(String msg)
        {
            ConsoleWrite(msg, ConsoleMessageType.Error);
        }

        public void ConsoleSuccess(String msg)
        {
            ConsoleWrite(msg, ConsoleMessageType.Success);
        }

        public void DebugWrite(String msg, Int32 level)
        {
            if (_debugLevel >= level)
            {
                ConsoleWrite(level + ":(" + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("main") : (Thread.CurrentThread.Name)) + ":" + Thread.CurrentThread.ManagedThreadId + ") " + msg, ConsoleMessageType.Normal);
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

        private void DoBattlelogWait()
        {
            //Wait 2 seconds between battlelog actions
            if ((DateTime.UtcNow - _LastBattlelogAction) < _BattlelogWaitDuration)
            {
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
            try
            {
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
                }
            }
            catch (Exception e)
            {
                //Ignore errors
            }
            _LastVersionTrackingUpdate = DateTime.UtcNow;
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
                    prefix += ": ";
                }
                //Check if the exception attributes to the database
                ConsoleWrite(prefix + aException, ConsoleMessageType.Exception);
                return aException;
            }
            catch (Exception e)
            {
                ConsoleWrite(e.ToString(), ConsoleMessageType.Exception);
            }
            return null;
        }

        public class AdKatsException
        {
            public Exception InternalException = null;
            public String Message = String.Empty;
            public String Method = String.Empty;
            //Param Constructors
            public AdKatsException(String message, Exception internalException)
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
            public String process_source;
            public Boolean process_manual;
            public DateTime process_time;
            public AdKatsSubscribedPlayer process_player;
        }

        public class AdKatsSubscribedPlayer
        {
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
            public HashSet<String> WatchedVehicles;
            public DateTime LastUsage;
            public Int32 LoadoutKills;

            public AdKatsSubscribedPlayer() {
                WatchedVehicles = new HashSet<String>();
                LastUsage = DateTime.UtcNow;
            }

            public String GetVerboseName()
            {
                return ((String.IsNullOrEmpty(player_clanTag)) ? ("") : ("[" + player_clanTag + "]")) + player_name;
            }
        }

        public class MapMode {
            public Int32 MapModeID;
            public String ModeKey;
            public String MapKey;
            public String ModeName;
            public String MapName;

            public MapMode(Int32 mapModeID, String modeKey, String mapKey, String modeName, String mapName) {
                MapModeID = mapModeID;
                ModeKey = modeKey;
                MapKey = mapKey;
                ModeName = modeName;
                MapName = mapName;
            }
        }

        public void PopulateMapModes() {
            _availableMapModes = new List<MapMode>(); 
            _availableMapModes.Add(new MapMode(1, "ConquestLarge0", "MP_Abandoned", "Conquest Large", "Zavod 311"));
            _availableMapModes.Add(new MapMode(2, "ConquestLarge0", "MP_Damage", "Conquest Large", "Lancang Dam"));
            _availableMapModes.Add(new MapMode(3, "ConquestLarge0", "MP_Flooded", "Conquest Large", "Flood Zone"));
            _availableMapModes.Add(new MapMode(4, "ConquestLarge0", "MP_Journey", "Conquest Large", "Golmud Railway"));
            _availableMapModes.Add(new MapMode(5, "ConquestLarge0", "MP_Naval", "Conquest Large", "Paracel Storm"));
            _availableMapModes.Add(new MapMode(6, "ConquestLarge0", "MP_Prison", "Conquest Large", "Operation Locker"));
            _availableMapModes.Add(new MapMode(7, "ConquestLarge0", "MP_Resort", "Conquest Large", "Hainan Resort"));
            _availableMapModes.Add(new MapMode(8, "ConquestLarge0", "MP_Siege", "Conquest Large", "Siege of Shanghai"));
            _availableMapModes.Add(new MapMode(9, "ConquestLarge0", "MP_TheDish", "Conquest Large", "Rogue Transmission"));
            _availableMapModes.Add(new MapMode(10, "ConquestLarge0", "MP_Tremors", "Conquest Large", "Dawnbreaker"));
            _availableMapModes.Add(new MapMode(11, "ConquestSmall0", "MP_Abandoned", "Conquest Small", "Zavod 311"));
            _availableMapModes.Add(new MapMode(12, "ConquestSmall0", "MP_Damage", "Conquest Small", "Lancang Dam"));
            _availableMapModes.Add(new MapMode(13, "ConquestSmall0", "MP_Flooded", "Conquest Small", "Flood Zone"));
            _availableMapModes.Add(new MapMode(14, "ConquestSmall0", "MP_Journey", "Conquest Small", "Golmud Railway"));
            _availableMapModes.Add(new MapMode(15, "ConquestSmall0", "MP_Naval", "Conquest Small", "Paracel Storm"));
            _availableMapModes.Add(new MapMode(16, "ConquestSmall0", "MP_Prison", "Conquest Small", "Operation Locker"));
            _availableMapModes.Add(new MapMode(17, "ConquestSmall0", "MP_Resort", "Conquest Small", "Hainan Resort"));
            _availableMapModes.Add(new MapMode(18, "ConquestSmall0", "MP_Siege", "Conquest Small", "Siege of Shanghai"));
            _availableMapModes.Add(new MapMode(19, "ConquestSmall0", "MP_TheDish", "Conquest Small", "Rogue Transmission"));
            _availableMapModes.Add(new MapMode(20, "ConquestSmall0", "MP_Tremors", "Conquest Small", "Dawnbreaker"));
            _availableMapModes.Add(new MapMode(21, "Domination0", "MP_Abandoned", "Domination", "Zavod 311"));
            _availableMapModes.Add(new MapMode(22, "Domination0", "MP_Damage", "Domination", "Lancang Dam"));
            _availableMapModes.Add(new MapMode(23, "Domination0", "MP_Flooded", "Domination", "Flood Zone"));
            _availableMapModes.Add(new MapMode(24, "Domination0", "MP_Journey", "Domination", "Golmud Railway"));
            _availableMapModes.Add(new MapMode(25, "Domination0", "MP_Naval", "Domination", "Paracel Storm"));
            _availableMapModes.Add(new MapMode(26, "Domination0", "MP_Prison", "Domination", "Operation Locker"));
            _availableMapModes.Add(new MapMode(27, "Domination0", "MP_Resort", "Domination", "Hainan Resort"));
            _availableMapModes.Add(new MapMode(28, "Domination0", "MP_Siege", "Domination", "Siege of Shanghai"));
            _availableMapModes.Add(new MapMode(29, "Domination0", "MP_TheDish", "Domination", "Rogue Transmission"));
            _availableMapModes.Add(new MapMode(30, "Domination0", "MP_Tremors", "Domination", "Dawnbreaker"));
            _availableMapModes.Add(new MapMode(31, "Elimination0", "MP_Abandoned", "Defuse", "Zavod 311"));
            _availableMapModes.Add(new MapMode(32, "Elimination0", "MP_Damage", "Defuse", "Lancang Dam"));
            _availableMapModes.Add(new MapMode(33, "Elimination0", "MP_Flooded", "Defuse", "Flood Zone"));
            _availableMapModes.Add(new MapMode(34, "Elimination0", "MP_Journey", "Defuse", "Golmud Railway"));
            _availableMapModes.Add(new MapMode(35, "Elimination0", "MP_Naval", "Defuse", "Paracel Storm"));
            _availableMapModes.Add(new MapMode(36, "Elimination0", "MP_Prison", "Defuse", "Operation Locker"));
            _availableMapModes.Add(new MapMode(37, "Elimination0", "MP_Resort", "Defuse", "Hainan Resort"));
            _availableMapModes.Add(new MapMode(38, "Elimination0", "MP_Siege", "Defuse", "Siege of Shanghai"));
            _availableMapModes.Add(new MapMode(39, "Elimination0", "MP_TheDish", "Defuse", "Rogue Transmission"));
            _availableMapModes.Add(new MapMode(40, "Obliteration", "MP_Abandoned", "Obliteration", "Zavod 311"));
            _availableMapModes.Add(new MapMode(41, "Obliteration", "MP_Damage", "Obliteration", "Lancang Dam"));
            _availableMapModes.Add(new MapMode(42, "Obliteration", "MP_Flooded", "Obliteration", "Flood Zone"));
            _availableMapModes.Add(new MapMode(43, "Obliteration", "MP_Journey", "Obliteration", "Golmud Railway"));
            _availableMapModes.Add(new MapMode(44, "Obliteration", "MP_Naval", "Obliteration", "Paracel Storm"));
            _availableMapModes.Add(new MapMode(45, "Obliteration", "MP_Prison", "Obliteration", "Operation Locker"));
            _availableMapModes.Add(new MapMode(46, "Obliteration", "MP_Resort", "Obliteration", "Hainan Resort"));
            _availableMapModes.Add(new MapMode(47, "Obliteration", "MP_Siege", "Obliteration", "Siege of Shanghai"));
            _availableMapModes.Add(new MapMode(48, "Obliteration", "MP_TheDish", "Obliteration", "Rogue Transmission"));
            _availableMapModes.Add(new MapMode(49, "Obliteration", "MP_Tremors", "Obliteration", "Dawnbreaker"));
            _availableMapModes.Add(new MapMode(50, "RushLarge0", "MP_Abandoned", "Rush", "Zavod 311"));
            _availableMapModes.Add(new MapMode(51, "RushLarge0", "MP_Damage", "Rush", "Lancang Dam"));
            _availableMapModes.Add(new MapMode(52, "RushLarge0", "MP_Flooded", "Rush", "Flood Zone"));
            _availableMapModes.Add(new MapMode(53, "RushLarge0", "MP_Journey", "Rush", "Golmud Railway"));
            _availableMapModes.Add(new MapMode(54, "RushLarge0", "MP_Naval", "Rush", "Paracel Storm"));
            _availableMapModes.Add(new MapMode(55, "RushLarge0", "MP_Prison", "Rush", "Operation Locker"));
            _availableMapModes.Add(new MapMode(56, "RushLarge0", "MP_Resort", "Rush", "Hainan Resort"));
            _availableMapModes.Add(new MapMode(57, "RushLarge0", "MP_Siege", "Rush", "Siege of Shanghai"));
            _availableMapModes.Add(new MapMode(58, "RushLarge0", "MP_TheDish", "Rush", "Rogue Transmission"));
            _availableMapModes.Add(new MapMode(59, "RushLarge0", "MP_Tremors", "Rush", "Dawnbreaker"));
            _availableMapModes.Add(new MapMode(60, "SquadDeathMatch0", "MP_Abandoned", "Squad Deathmatch", "Zavod 311"));
            _availableMapModes.Add(new MapMode(61, "SquadDeathMatch0", "MP_Damage", "Squad Deathmatch", "Lancang Dam"));
            _availableMapModes.Add(new MapMode(62, "SquadDeathMatch0", "MP_Flooded", "Squad Deathmatch", "Flood Zone"));
            _availableMapModes.Add(new MapMode(63, "SquadDeathMatch0", "MP_Journey", "Squad Deathmatch", "Golmud Railway"));
            _availableMapModes.Add(new MapMode(64, "SquadDeathMatch0", "MP_Naval", "Squad Deathmatch", "Paracel Storm"));
            _availableMapModes.Add(new MapMode(65, "SquadDeathMatch0", "MP_Prison", "Squad Deathmatch", "Operation Locker"));
            _availableMapModes.Add(new MapMode(66, "SquadDeathMatch0", "MP_Resort", "Squad Deathmatch", "Hainan Resort"));
            _availableMapModes.Add(new MapMode(67, "SquadDeathMatch0", "MP_Siege", "Squad Deathmatch", "Siege of Shanghai"));
            _availableMapModes.Add(new MapMode(68, "SquadDeathMatch0", "MP_TheDish", "Squad Deathmatch", "Rogue Transmission"));
            _availableMapModes.Add(new MapMode(69, "SquadDeathMatch0", "MP_Tremors", "Squad Deathmatch", "Dawnbreaker"));
            _availableMapModes.Add(new MapMode(70, "TeamDeathMatch0", "MP_Abandoned", "Team Deathmatch", "Zavod 311"));
            _availableMapModes.Add(new MapMode(71, "TeamDeathMatch0", "MP_Damage", "Team Deathmatch", "Lancang Dam"));
            _availableMapModes.Add(new MapMode(72, "TeamDeathMatch0", "MP_Flooded", "Team Deathmatch", "Flood Zone"));
            _availableMapModes.Add(new MapMode(73, "TeamDeathMatch0", "MP_Journey", "Team Deathmatch", "Golmud Railway"));
            _availableMapModes.Add(new MapMode(74, "TeamDeathMatch0", "MP_Naval", "Team Deathmatch", "Paracel Storm"));
            _availableMapModes.Add(new MapMode(75, "TeamDeathMatch0", "MP_Prison", "Team Deathmatch", "Operation Locker"));
            _availableMapModes.Add(new MapMode(76, "TeamDeathMatch0", "MP_Resort", "Team Deathmatch", "Hainan Resort"));
            _availableMapModes.Add(new MapMode(77, "TeamDeathMatch0", "MP_Siege", "Team Deathmatch", "Siege of Shanghai"));
            _availableMapModes.Add(new MapMode(78, "TeamDeathMatch0", "MP_TheDish", "Team Deathmatch", "Rogue Transmission"));
            _availableMapModes.Add(new MapMode(79, "TeamDeathMatch0", "MP_Tremors", "Team Deathmatch", "Dawnbreaker"));
            _availableMapModes.Add(new MapMode(80, "ConquestLarge0", "XP1_001", "Conquest Large", "Silk Road"));
            _availableMapModes.Add(new MapMode(81, "ConquestLarge0", "XP1_002", "Conquest Large", "Altai Range"));
            _availableMapModes.Add(new MapMode(82, "ConquestLarge0", "XP1_003", "Conquest Large", "Guilin Peaks"));
            _availableMapModes.Add(new MapMode(83, "ConquestLarge0", "XP1_004", "Conquest Large", "Dragon Pass"));
            _availableMapModes.Add(new MapMode(84, "ConquestSmall0", "XP1_001", "Conquest Small", "Silk Road"));
            _availableMapModes.Add(new MapMode(85, "ConquestSmall0", "XP1_002", "Conquest Small", "Altai Range"));
            _availableMapModes.Add(new MapMode(86, "ConquestSmall0", "XP1_003", "Conquest Small", "Guilin Peaks"));
            _availableMapModes.Add(new MapMode(87, "ConquestSmall0", "XP1_004", "Conquest Small", "Dragon Pass"));
            _availableMapModes.Add(new MapMode(88, "Domination0", "XP1_001", "Domination", "Silk Road"));
            _availableMapModes.Add(new MapMode(89, "Domination0", "XP1_002", "Domination", "Altai Range"));
            _availableMapModes.Add(new MapMode(90, "Domination0", "XP1_003", "Domination", "Guilin Peaks"));
            _availableMapModes.Add(new MapMode(91, "Domination0", "XP1_004", "Domination", "Dragon Pass"));
            _availableMapModes.Add(new MapMode(92, "Elimination0", "XP1_001", "Defuse", "Silk Road"));
            _availableMapModes.Add(new MapMode(93, "Elimination0", "XP1_002", "Defuse", "Altai Range"));
            _availableMapModes.Add(new MapMode(94, "Elimination0", "XP1_003", "Defuse", "Guilin Peaks"));
            _availableMapModes.Add(new MapMode(95, "Elimination0", "XP1_004", "Defuse", "Dragon Pass"));
            _availableMapModes.Add(new MapMode(96, "Obliteration", "XP1_001", "Obliteration", "Silk Road"));
            _availableMapModes.Add(new MapMode(97, "Obliteration", "XP1_002", "Obliteration", "Altai Range"));
            _availableMapModes.Add(new MapMode(98, "Obliteration", "XP1_003", "Obliteration", "Guilin Peaks"));
            _availableMapModes.Add(new MapMode(99, "Obliteration", "XP1_004", "Obliteration", "Dragon Pass"));
            _availableMapModes.Add(new MapMode(100, "RushLarge0", "XP1_001", "Rush", "Silk Road"));
            _availableMapModes.Add(new MapMode(101, "RushLarge0", "XP1_002", "Rush", "Altai Range"));
            _availableMapModes.Add(new MapMode(102, "RushLarge0", "XP1_003", "Rush", "Guilin Peaks"));
            _availableMapModes.Add(new MapMode(103, "RushLarge0", "XP1_004", "Rush", "Dragon Pass"));
            _availableMapModes.Add(new MapMode(104, "SquadDeathMatch0", "XP1_001", "Squad Deathmatch", "Silk Road"));
            _availableMapModes.Add(new MapMode(105, "SquadDeathMatch0", "XP1_002", "Squad Deathmatch", "Altai Range"));
            _availableMapModes.Add(new MapMode(106, "SquadDeathMatch0", "XP1_003", "Squad Deathmatch", "Guilin Peaks"));
            _availableMapModes.Add(new MapMode(107, "SquadDeathMatch0", "XP1_004", "Squad Deathmatch", "Dragon Pass"));
            _availableMapModes.Add(new MapMode(108, "TeamDeathMatch0", "XP1_001", "Team Deathmatch", "Silk Road"));
            _availableMapModes.Add(new MapMode(109, "TeamDeathMatch0", "XP1_002", "Team Deathmatch", "Altai Range"));
            _availableMapModes.Add(new MapMode(110, "TeamDeathMatch0", "XP1_003", "Team Deathmatch", "Guilin Peaks"));
            _availableMapModes.Add(new MapMode(111, "TeamDeathMatch0", "XP1_004", "Team Deathmatch", "Dragon Pass"));
            _availableMapModes.Add(new MapMode(112, "AirSuperiority0", "XP1_001", "Air Superiority", "Silk Road"));
            _availableMapModes.Add(new MapMode(113, "AirSuperiority0", "XP1_002", "Air Superiority", "Altai Range"));
            _availableMapModes.Add(new MapMode(114, "AirSuperiority0", "XP1_003", "Air Superiority", "Guilin Peaks"));
            _availableMapModes.Add(new MapMode(115, "AirSuperiority0", "XP1_004", "Air Superiority", "Dragon Pass"));
            _availableMapModes.Add(new MapMode(116, "ConquestLarge0", "XP0_Caspian", "Conquest Large", "Caspian Border 2014"));
            _availableMapModes.Add(new MapMode(117, "ConquestLarge0", "XP0_Firestorm", "Conquest Large", "Operation Firestorm 2014"));
            _availableMapModes.Add(new MapMode(118, "ConquestLarge0", "XP0_Metro", "Conquest Large", "Operation Metro 2014"));
            _availableMapModes.Add(new MapMode(119, "ConquestLarge0", "XP0_Oman", "Conquest Large", "Gulf of Oman 2014"));
            _availableMapModes.Add(new MapMode(120, "ConquestSmall0", "XP0_Caspian", "Conquest Small", "Caspian Border 2014"));
            _availableMapModes.Add(new MapMode(121, "ConquestSmall0", "XP0_Firestorm", "Conquest Small", "Operation Firestorm 2014"));
            _availableMapModes.Add(new MapMode(122, "ConquestSmall0", "XP0_Metro", "Conquest Small", "Operation Metro 2014"));
            _availableMapModes.Add(new MapMode(123, "ConquestSmall0", "XP0_Oman", "Conquest Small", "Gulf of Oman 2014"));
            _availableMapModes.Add(new MapMode(124, "Domination0", "XP0_Caspian", "Domination", "Caspian Border 2014"));
            _availableMapModes.Add(new MapMode(125, "Domination0", "XP0_Firestorm", "Domination", "Operation Firestorm 2014"));
            _availableMapModes.Add(new MapMode(126, "Domination0", "XP0_Metro", "Domination", "Operation Metro 2014"));
            _availableMapModes.Add(new MapMode(127, "Domination0", "XP0_Oman", "Domination", "Gulf of Oman 2014"));
            _availableMapModes.Add(new MapMode(128, "Elimination0", "XP0_Caspian", "Defuse", "Caspian Border 2014"));
            _availableMapModes.Add(new MapMode(129, "Elimination0", "XP0_Firestorm", "Defuse", "Operation Firestorm 2014"));
            _availableMapModes.Add(new MapMode(130, "Elimination0", "XP0_Metro", "Defuse", "Operation Metro 2014"));
            _availableMapModes.Add(new MapMode(131, "Elimination0", "XP0_Oman", "Defuse", "Gulf of Oman 2014"));
            _availableMapModes.Add(new MapMode(132, "Obliteration", "XP0_Caspian", "Obliteration", "Caspian Border 2014"));
            _availableMapModes.Add(new MapMode(133, "Obliteration", "XP0_Firestorm", "Obliteration", "Operation Firestorm 2014"));
            _availableMapModes.Add(new MapMode(134, "Obliteration", "XP0_Metro", "Obliteration", "Operation Metro 2014"));
            _availableMapModes.Add(new MapMode(135, "Obliteration", "XP0_Oman", "Obliteration", "Gulf of Oman 2014"));
            _availableMapModes.Add(new MapMode(136, "RushLarge0", "XP0_Caspian", "Rush", "Caspian Border 2014"));
            _availableMapModes.Add(new MapMode(137, "RushLarge0", "XP0_Firestorm", "Rush", "Operation Firestorm 2014"));
            _availableMapModes.Add(new MapMode(138, "RushLarge0", "XP0_Metro", "Rush", "Operation Metro 2014"));
            _availableMapModes.Add(new MapMode(139, "RushLarge0", "XP0_Oman", "Rush", "Gulf of Oman 2014"));
            _availableMapModes.Add(new MapMode(140, "SquadDeathMatch0", "XP0_Caspian", "Squad Deathmatch", "Caspian Border 2014"));
            _availableMapModes.Add(new MapMode(141, "SquadDeathMatch0", "XP0_Firestorm", "Squad Deathmatch", "Operation Firestorm 2014"));
            _availableMapModes.Add(new MapMode(142, "SquadDeathMatch0", "XP0_Metro", "Squad Deathmatch", "Operation Metro 2014"));
            _availableMapModes.Add(new MapMode(143, "SquadDeathMatch0", "XP0_Oman", "Squad Deathmatch", "Gulf of Oman 2014"));
            _availableMapModes.Add(new MapMode(144, "TeamDeathMatch0", "XP0_Caspian", "Team Deathmatch", "Caspian Border 2014"));
            _availableMapModes.Add(new MapMode(145, "TeamDeathMatch0", "XP0_Firestorm", "Team Deathmatch", "Operation Firestorm 2014"));
            _availableMapModes.Add(new MapMode(146, "TeamDeathMatch0", "XP0_Metro", "Team Deathmatch", "Operation Metro 2014"));
            _availableMapModes.Add(new MapMode(147, "TeamDeathMatch0", "XP0_Oman", "Team Deathmatch", "Gulf of Oman 2014"));
            _availableMapModes.Add(new MapMode(148, "CaptureTheFlag0", "XP0_Caspian", "CTF", "Caspian Border 2014"));
            _availableMapModes.Add(new MapMode(149, "CaptureTheFlag0", "XP0_Firestorm", "CTF", "Operation Firestorm 2014"));
            _availableMapModes.Add(new MapMode(150, "CaptureTheFlag0", "XP0_Metro", "CTF", "Operation Metro 2014"));
            _availableMapModes.Add(new MapMode(151, "CaptureTheFlag0", "XP0_Oman", "CTF", "Gulf of Oman 2014"));
            _availableMapModes.Add(new MapMode(152, "ConquestLarge0", "XP2_001", "Conquest Large", "Lost Islands"));
            _availableMapModes.Add(new MapMode(153, "ConquestLarge0", "XP2_002", "Conquest Large", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(154, "ConquestLarge0", "XP2_003", "Conquest Large", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(155, "ConquestLarge0", "XP2_004", "Conquest Large", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(156, "ConquestSmall0", "XP2_001", "Conquest Small", "Lost Islands"));
            _availableMapModes.Add(new MapMode(157, "ConquestSmall0", "XP2_002", "Conquest Small", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(158, "ConquestSmall0", "XP2_003", "Conquest Small", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(159, "ConquestSmall0", "XP2_004", "Conquest Small", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(160, "Domination0", "XP2_001", "Domination", "Lost Islands"));
            _availableMapModes.Add(new MapMode(161, "Domination0", "XP2_002", "Domination", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(162, "Domination0", "XP2_003", "Domination", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(163, "Domination0", "XP2_004", "Domination", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(164, "Elimination0", "XP2_001", "Defuse", "Lost Islands"));
            _availableMapModes.Add(new MapMode(165, "Elimination0", "XP2_002", "Defuse", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(166, "Elimination0", "XP2_003", "Defuse", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(167, "Elimination0", "XP2_004", "Defuse", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(168, "Obliteration", "XP2_001", "Obliteration", "Lost Islands"));
            _availableMapModes.Add(new MapMode(169, "Obliteration", "XP2_002", "Obliteration", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(170, "Obliteration", "XP2_003", "Obliteration", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(171, "Obliteration", "XP2_004", "Obliteration", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(172, "RushLarge0", "XP2_001", "Rush", "Lost Islands"));
            _availableMapModes.Add(new MapMode(173, "RushLarge0", "XP2_002", "Rush", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(174, "RushLarge0", "XP2_003", "Rush", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(175, "RushLarge0", "XP2_004", "Rush", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(176, "SquadDeathMatch0", "XP2_001", "Squad Deathmatch", "Lost Islands"));
            _availableMapModes.Add(new MapMode(177, "SquadDeathMatch0", "XP2_002", "Squad Deathmatch", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(178, "SquadDeathMatch0", "XP2_003", "Squad Deathmatch", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(179, "SquadDeathMatch0", "XP2_004", "Squad Deathmatch", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(180, "TeamDeathMatch0", "XP2_001", "Team Deathmatch", "Lost Islands"));
            _availableMapModes.Add(new MapMode(181, "TeamDeathMatch0", "XP2_002", "Team Deathmatch", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(182, "TeamDeathMatch0", "XP2_003", "Team Deathmatch", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(183, "TeamDeathMatch0", "XP2_004", "Team Deathmatch", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(184, "CarrierAssaultLarge0", "XP2_001", "Carrier Assault Large", "Lost Islands"));
            _availableMapModes.Add(new MapMode(185, "CarrierAssaultLarge0", "XP2_002", "Carrier Assault Large", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(186, "CarrierAssaultLarge0", "XP2_003", "Carrier Assault Large", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(187, "CarrierAssaultLarge0", "XP2_004", "Carrier Assault Large", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(188, "CarrierAssaultSmall0", "XP2_001", "Carrier Assault Small", "Lost Islands"));
            _availableMapModes.Add(new MapMode(189, "CarrierAssaultSmall0", "XP2_002", "Carrier Assault Small", "Nansha Strike"));
            _availableMapModes.Add(new MapMode(190, "CarrierAssaultSmall0", "XP2_003", "Carrier Assault Small", "Wavebreaker"));
            _availableMapModes.Add(new MapMode(191, "CarrierAssaultSmall0", "XP2_004", "Carrier Assault Small", "Operation Mortar"));
            _availableMapModes.Add(new MapMode(192, "ConquestLarge0", "XP3_MarketPl", "Conquest Large", "Pearl Market"));
            _availableMapModes.Add(new MapMode(193, "ConquestLarge0", "XP3_Prpganda", "Conquest Large", "Propaganda"));
            _availableMapModes.Add(new MapMode(194, "ConquestLarge0", "XP3_UrbanGdn", "Conquest Large", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(195, "ConquestLarge0", "XP3_WtrFront", "Conquest Large", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(196, "ConquestSmall0", "XP3_MarketPl", "Conquest Small", "Pearl Market"));
            _availableMapModes.Add(new MapMode(197, "ConquestSmall0", "XP3_Prpganda", "Conquest Small", "Propaganda"));
            _availableMapModes.Add(new MapMode(198, "ConquestSmall0", "XP3_UrbanGdn", "Conquest Small", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(199, "ConquestSmall0", "XP3_WtrFront", "Conquest Small", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(200, "Domination0", "XP3_MarketPl", "Domination", "Pearl Market"));
            _availableMapModes.Add(new MapMode(201, "Domination0", "XP3_Prpganda", "Domination", "Propaganda"));
            _availableMapModes.Add(new MapMode(202, "Domination0", "XP3_UrbanGdn", "Domination", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(203, "Domination0", "XP3_WtrFront", "Domination", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(204, "Elimination0", "XP3_MarketPl", "Defuse", "Pearl Market"));
            _availableMapModes.Add(new MapMode(205, "Elimination0", "XP3_Prpganda", "Defuse", "Propaganda"));
            _availableMapModes.Add(new MapMode(206, "Elimination0", "XP3_UrbanGdn", "Defuse", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(207, "Elimination0", "XP3_WtrFront", "Defuse", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(208, "Obliteration", "XP3_MarketPl", "Obliteration", "Pearl Market"));
            _availableMapModes.Add(new MapMode(209, "Obliteration", "XP3_Prpganda", "Obliteration", "Propaganda"));
            _availableMapModes.Add(new MapMode(210, "Obliteration", "XP3_UrbanGdn", "Obliteration", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(211, "Obliteration", "XP3_WtrFront", "Obliteration", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(212, "RushLarge0", "XP3_MarketPl", "Rush", "Pearl Market"));
            _availableMapModes.Add(new MapMode(213, "RushLarge0", "XP3_Prpganda", "Rush", "Propaganda"));
            _availableMapModes.Add(new MapMode(214, "RushLarge0", "XP3_UrbanGdn", "Rush", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(215, "RushLarge0", "XP3_WtrFront", "Rush", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(216, "SquadDeathMatch0", "XP3_MarketPl", "Squad Deathmatch", "Pearl Market"));
            _availableMapModes.Add(new MapMode(217, "SquadDeathMatch0", "XP3_Prpganda", "Squad Deathmatch", "Propaganda"));
            _availableMapModes.Add(new MapMode(218, "SquadDeathMatch0", "XP3_UrbanGdn", "Squad Deathmatch", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(219, "SquadDeathMatch0", "XP3_WtrFront", "Squad Deathmatch", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(220, "TeamDeathMatch0", "XP3_MarketPl", "Team Deathmatch", "Pearl Market"));
            _availableMapModes.Add(new MapMode(221, "TeamDeathMatch0", "XP3_Prpganda", "Team Deathmatch", "Propaganda"));
            _availableMapModes.Add(new MapMode(222, "TeamDeathMatch0", "XP3_UrbanGdn", "Team Deathmatch", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(223, "TeamDeathMatch0", "XP3_WtrFront", "Team Deathmatch", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(224, "CaptureTheFlag0", "XP3_MarketPl", "CTF", "Pearl Market"));
            _availableMapModes.Add(new MapMode(225, "CaptureTheFlag0", "XP3_Prpganda", "CTF", "Propaganda"));
            _availableMapModes.Add(new MapMode(226, "CaptureTheFlag0", "XP3_UrbanGdn", "CTF", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(227, "CaptureTheFlag0", "XP3_WtrFront", "CTF", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(228, "Chainlink0", "XP3_MarketPl", "Chain Link", "Pearl Market"));
            _availableMapModes.Add(new MapMode(229, "Chainlink0", "XP3_Prpganda", "Chain Link", "Propaganda"));
            _availableMapModes.Add(new MapMode(230, "Chainlink0", "XP3_UrbanGdn", "Chain Link", "Lumphini Garden"));
            _availableMapModes.Add(new MapMode(231, "Chainlink0", "XP3_WtrFront", "Chain Link", "Sunken Dragon"));
            _availableMapModes.Add(new MapMode(232, "ConquestLarge0", "XP4_Arctic", "Conquest Large", "Operation Whiteout"));
            _availableMapModes.Add(new MapMode(233, "ConquestLarge0", "XP4_SubBase", "Conquest Large", "Hammerhead"));
            _availableMapModes.Add(new MapMode(234, "ConquestLarge0", "XP4_Titan", "Conquest Large", "Hangar 21"));
            _availableMapModes.Add(new MapMode(235, "ConquestLarge0", "XP4_WlkrFtry", "Conquest Large", "Giants Of Karelia"));
            _availableMapModes.Add(new MapMode(236, "ConquestSmall0", "XP4_Arctic", "Conquest Small", "Operation Whiteout"));
            _availableMapModes.Add(new MapMode(237, "ConquestSmall0", "XP4_SubBase", "Conquest Small", "Hammerhead"));
            _availableMapModes.Add(new MapMode(238, "ConquestSmall0", "XP4_Titan", "Conquest Small", "Hangar 21"));
            _availableMapModes.Add(new MapMode(239, "ConquestSmall0", "XP4_WlkrFtry", "Conquest Small", "Giants Of Karelia"));
            _availableMapModes.Add(new MapMode(240, "Domination0", "XP4_Arctic", "Domination", "Operation Whiteout"));
            _availableMapModes.Add(new MapMode(241, "Domination0", "XP4_SubBase", "Domination", "Hammerhead"));
            _availableMapModes.Add(new MapMode(242, "Domination0", "XP4_Titan", "Domination", "Hangar 21"));
            _availableMapModes.Add(new MapMode(243, "Domination0", "XP4_WlkrFtry", "Domination", "Giants Of Karelia"));
            _availableMapModes.Add(new MapMode(244, "Elimination0", "XP4_Arctic", "Defuse", "Operation Whiteout"));
            _availableMapModes.Add(new MapMode(245, "Elimination0", "XP4_SubBase", "Defuse", "Hammerhead"));
            _availableMapModes.Add(new MapMode(246, "Elimination0", "XP4_Titan", "Defuse", "Hangar 21"));
            _availableMapModes.Add(new MapMode(247, "Elimination0", "XP4_WlkrFtry", "Defuse", "Giants Of Karelia"));
            _availableMapModes.Add(new MapMode(248, "Obliteration", "XP4_Arctic", "Obliteration", "Operation Whiteout"));
            _availableMapModes.Add(new MapMode(249, "Obliteration", "XP4_SubBase", "Obliteration", "Hammerhead"));
            _availableMapModes.Add(new MapMode(250, "Obliteration", "XP4_Titan", "Obliteration", "Hangar 21"));
            _availableMapModes.Add(new MapMode(251, "Obliteration", "XP4_WlkrFtry", "Obliteration", "Giants Of Karelia"));
            _availableMapModes.Add(new MapMode(252, "RushLarge0", "XP4_Arctic", "Rush", "Operation Whiteout"));
            _availableMapModes.Add(new MapMode(253, "RushLarge0", "XP4_SubBase", "Rush", "Hammerhead"));
            _availableMapModes.Add(new MapMode(254, "RushLarge0", "XP4_Titan", "Rush", "Hangar 21"));
            _availableMapModes.Add(new MapMode(255, "RushLarge0", "XP4_WlkrFtry", "Rush", "Giants Of Karelia"));
            _availableMapModes.Add(new MapMode(256, "SquadDeathMatch0", "XP4_Arctic", "Squad Deathmatch", "Operation Whiteout"));
            _availableMapModes.Add(new MapMode(257, "SquadDeathMatch0", "XP4_SubBase", "Squad Deathmatch", "Hammerhead"));
            _availableMapModes.Add(new MapMode(258, "SquadDeathMatch0", "XP4_Titan", "Squad Deathmatch", "Hangar 21"));
            _availableMapModes.Add(new MapMode(259, "SquadDeathMatch0", "XP4_WlkrFtry", "Squad Deathmatch", "Giants Of Karelia"));
            _availableMapModes.Add(new MapMode(260, "TeamDeathMatch0", "XP4_Arctic", "Team Deathmatch", "Operation Whiteout"));
            _availableMapModes.Add(new MapMode(261, "TeamDeathMatch0", "XP4_SubBase", "Team Deathmatch", "Hammerhead"));
            _availableMapModes.Add(new MapMode(262, "TeamDeathMatch0", "XP4_Titan", "Team Deathmatch", "Hangar 21"));
            _availableMapModes.Add(new MapMode(263, "TeamDeathMatch0", "XP4_WlkrFtry", "Team Deathmatch", "Giants Of Karelia"));
            _availableMapModes.Add(new MapMode(264, "CaptureTheFlag0", "XP4_Arctic", "CTF", "Operation Whiteout"));
            _availableMapModes.Add(new MapMode(265, "CaptureTheFlag0", "XP4_SubBase", "CTF", "Hammerhead"));
            _availableMapModes.Add(new MapMode(266, "CaptureTheFlag0", "XP4_Titan", "CTF", "Hangar 21"));
            _availableMapModes.Add(new MapMode(267, "CaptureTheFlag0", "XP4_WlkrFtry", "CTF", "Giants Of Karelia"));
        }

        internal enum SupportedGames
        {
            BF_3,
            BF_4
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
                AccessoriesAssigned = new Dictionary<string, WarsawItemAccessory>();
                AccessoriesAllowed = new Dictionary<String, Dictionary<String, WarsawItemAccessory>>();
            }
        }

        public class WarsawVehicle {
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
            public String SlugReadable;
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
    }
}