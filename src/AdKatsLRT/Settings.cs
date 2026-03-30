using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKatsLRT
    {
        private Int32 _minimumPlayersForEnforcement = 0;
        private Boolean _checkUnlocksBeforeEnforcing = true;
        private Int32 _maxSnipersPerTeam = 0;
        private Int32 _maxDMRsPerTeam = 0;
        private Int32 _maxShotgunsPerTeam = 0;

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            var lstReturn = new List<CPluginVariable>();
            try
            {
                const String separator = " | ";

                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Enable High Request Volume", typeof(Boolean), _highRequestVolume));
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Use Proxy for Battlelog", typeof(Boolean), _useProxy));
                if (_useProxy)
                {
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Proxy URL", typeof(String), _proxyURL));
                }
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Integrate with AdKats", typeof(Boolean), _enableAdKatsIntegration));
                if (_enableAdKatsIntegration)
                {
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforcement Only", typeof(Boolean), _spawnEnforcementOnly));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Admins", typeof(Boolean), _spawnEnforcementActOnAdmins));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Reputable Players", typeof(Boolean), _spawnEnforcementActOnReputablePlayers));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Action Whitelist", typeof(String[]), _Whitelist));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Trigger Enforce Minimum Infraction Points", typeof(Int32), _triggerEnforcementMinimumInfractionPoints));
                }
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Display Weapon Popularity Periodically", typeof(Boolean), _displayWeaponPopularity));
                if (_displayWeaponPopularity)
                {
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Weapon Popularity Display Frequency Minutes", typeof(Int32), _weaponPopularityDisplayMinutes));
                }
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Use Backup AutoAdmin", typeof(Boolean), _UseBackupAutoadmin));
                if (_enableAdKatsIntegration && _UseBackupAutoadmin)
                {
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Backup AutoAdmin Use AdKats Punishments", typeof(Boolean), _UseAdKatsPunishments));
                }
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Global Item Filter", typeof(String[]), _ItemFilter));
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Inverse Mode (Whitelist)", typeof(Boolean), _inverseEnforcementMode));
                lstReturn.Add(new CPluginVariable("Server Settings|Minimum Players for Enforcement", typeof(Int32), _minimumPlayersForEnforcement));
                lstReturn.Add(new CPluginVariable("Server Settings|Check Unlocks Before Enforcing", typeof(enumBoolYesNo), _checkUnlocksBeforeEnforcing ? enumBoolYesNo.Yes : enumBoolYesNo.No));
                lstReturn.Add(new CPluginVariable("Weapon Limits|Max Snipers Per Team", typeof(Int32), _maxSnipersPerTeam));
                lstReturn.Add(new CPluginVariable("Weapon Limits|Max DMRs Per Team", typeof(Int32), _maxDMRsPerTeam));
                lstReturn.Add(new CPluginVariable("Weapon Limits|Max Shotguns Per Team", typeof(Int32), _maxShotgunsPerTeam));
                if (!_warsawLibraryLoaded)
                {
                    lstReturn.Add(new CPluginVariable("The WARSAW library must be loaded to view settings.", typeof(String), "Enable the plugin to fetch the library."));
                    return lstReturn;
                }

                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Map/Mode Settings", typeof(Boolean), _displayMapsModes));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Settings", typeof(Boolean), _displayWeapons));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Accessory Settings", typeof(Boolean), _displayWeaponAccessories));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Gadget Settings", typeof(Boolean), _displayGadgets));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Vehicle Settings", typeof(Boolean), _displayVehicles));

                if (_displayMapsModes)
                {
                    lstReturn.Add(new CPluginVariable(SettingsMapModePrefix + separator.Trim() + "Enforce on Specific Maps/Modes Only", typeof(Boolean), _restrictSpecificMapModes));
                    if (_restrictSpecificMapModes)
                    {
                        lstReturn.AddRange(_availableMapModes.OrderBy(mm => mm.ModeName).ThenBy(mm => mm.MapName).Select(mapMode => new CPluginVariable(SettingsMapModePrefix + " - " + mapMode.ModeName + separator.Trim() + "RMM" + mapMode.MapModeID.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0') + separator + mapMode.MapName + separator + "Enforce?", "enum.EnforceMapEnum(Enforce|Ignore)", _restrictedMapModes.ContainsKey(mapMode.ModeKey + "|" + mapMode.MapKey) ? ("Enforce") : ("Ignore"))));
                    }
                }

                //Run removals
                _warsawSpawnDeniedIDs.RemoveWhere(spawnID => !_warsawInvalidLoadoutIDMessages.ContainsKey(spawnID) && !_warsawInvalidVehicleLoadoutIDMessages.ContainsKey(spawnID));

                String inverseSuffix = _inverseEnforcementMode ? " [Inverse mode]" : "";

                if (_displayWeapons)
                {
                    if (_warsawLibrary.Items.Any())
                    {
                        foreach (WarsawItem weapon in _warsawLibrary.Items.Values.Where(weapon => weapon.CategoryReadable != "GADGET").OrderBy(weapon => weapon.CategoryReadable).ThenBy(weapon => weapon.Slug))
                        {
                            if (_ItemFilter.Any() && !_ItemFilter.Any(item => weapon.Slug.ToLower().Contains(item.ToLower())))
                            {
                                continue;
                            }
                            if (_enableAdKatsIntegration && !_spawnEnforcementOnly)
                            {
                                lstReturn.Add(new CPluginVariable(SettingsWeaponPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on trigger?" + inverseSuffix, "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID))
                                {
                                    lstReturn.Add(new CPluginVariable(SettingsWeaponPrefix + weapon.CategoryTypeReadable + "|ALWS" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?" + inverseSuffix, "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                }
                            }
                            else
                            {
                                lstReturn.Add(new CPluginVariable(SettingsWeaponPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?" + inverseSuffix, "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                if (_displayWeaponAccessories)
                {
                    if (_warsawLibrary.ItemAccessories.Any())
                    {
                        foreach (WarsawItemAccessory weaponAccessory in _warsawLibrary.ItemAccessories.Values.OrderBy(weaponAccessory => weaponAccessory.Slug).ThenBy(weaponAccessory => weaponAccessory.CategoryReadable))
                        {
                            if (_ItemFilter.Any() && !_ItemFilter.Any(item => weaponAccessory.Slug.ToLower().Contains(item.ToLower())))
                            {
                                continue;
                            }
                            if (_enableAdKatsIntegration && !_spawnEnforcementOnly)
                            {
                                lstReturn.Add(new CPluginVariable(SettingsAccessoryPrefix + weaponAccessory.CategoryReadable + "|ALWT" + weaponAccessory.WarsawID + separator + weaponAccessory.Slug + separator + "Allow on trigger?" + inverseSuffix, "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID) ? ("Deny") : ("Allow")));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID))
                                {
                                    lstReturn.Add(new CPluginVariable(SettingsAccessoryPrefix + weaponAccessory.CategoryReadable + "|ALWS" + weaponAccessory.WarsawID + separator + weaponAccessory.Slug + separator + "Allow on spawn?" + inverseSuffix, "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weaponAccessory.WarsawID) ? ("Deny") : ("Allow")));
                                }
                            }
                            else
                            {
                                lstReturn.Add(new CPluginVariable(SettingsAccessoryPrefix + weaponAccessory.CategoryReadable + "|ALWT" + weaponAccessory.WarsawID + separator + weaponAccessory.Slug + separator + "Allow on spawn?" + inverseSuffix, "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weaponAccessory.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                if (_displayGadgets)
                {
                    if (_warsawLibrary.Items.Any())
                    {
                        foreach (WarsawItem weapon in _warsawLibrary.Items.Values.Where(weapon => weapon.CategoryReadable == "GADGET").OrderBy(weapon => weapon.CategoryReadable).ThenBy(weapon => weapon.Slug))
                        {
                            if (String.IsNullOrEmpty(weapon.CategoryTypeReadable))
                            {
                                Log.Error(weapon.WarsawID + " did not have a category type.");
                            }
                            if (_ItemFilter.Any() && !_ItemFilter.Any(item => weapon.Slug.ToLower().Contains(item.ToLower())))
                            {
                                continue;
                            }
                            if (_enableAdKatsIntegration && !_spawnEnforcementOnly)
                            {
                                lstReturn.Add(new CPluginVariable(SettingsGadgetPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on trigger?" + inverseSuffix, "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID))
                                {
                                    lstReturn.Add(new CPluginVariable(SettingsGadgetPrefix + weapon.CategoryTypeReadable + "|ALWS" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?" + inverseSuffix, "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                }
                            }
                            else
                            {
                                lstReturn.Add(new CPluginVariable(SettingsGadgetPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?" + inverseSuffix, "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                if (_displayVehicles)
                {
                    lstReturn.Add(new CPluginVariable(SettingsVehiclePrefix + separator.Trim() + "Spawn Enforce all Vehicles", typeof(Boolean), _spawnEnforceAllVehicles));
                    if (_warsawLibrary.Vehicles.Any())
                    {
                        foreach (var vehicle in _warsawLibrary.Vehicles.Values.OrderBy(vec => vec.CategoryType))
                        {
                            String currentPrefix = SettingsVehiclePrefix + " - " + vehicle.CategoryType + "|";
                            lstReturn.AddRange(vehicle.AllowedPrimaries.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedSecondaries.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedCountermeasures.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedOptics.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedUpgrades.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedSecondariesGunner.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedOpticsGunner.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedUpgradesGunner.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                        }
                    }
                }
                foreach (var pair in _warsawInvalidLoadoutIDMessages.Where(denied => _warsawLibrary.Items.ContainsKey(denied.Key)))
                {
                    WarsawItem deniedItem;
                    if (_warsawLibrary.Items.TryGetValue(pair.Key, out deniedItem))
                    {
                        lstReturn.Add(new CPluginVariable(SettingsDeniedItemMessagePrefix + "MSG" + deniedItem.WarsawID + separator + deniedItem.Slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                foreach (var pair in _warsawInvalidLoadoutIDMessages.Where(denied => _warsawLibrary.ItemAccessories.ContainsKey(denied.Key)))
                {
                    WarsawItemAccessory deniedItemAccessory;
                    if (_warsawLibrary.ItemAccessories.TryGetValue(pair.Key, out deniedItemAccessory))
                    {
                        lstReturn.Add(new CPluginVariable(SettingsDeniedItemAccMessagePrefix + "MSG" + deniedItemAccessory.WarsawID + separator + deniedItemAccessory.Slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                foreach (var pair in _warsawInvalidVehicleLoadoutIDMessages.Where(denied => _warsawLibrary.VehicleUnlocks.ContainsKey(denied.Key)))
                {
                    WarsawItem deniedVehicleUnlock;
                    if (_warsawLibrary.VehicleUnlocks.TryGetValue(pair.Key, out deniedVehicleUnlock))
                    {
                        lstReturn.Add(new CPluginVariable(SettingsDeniedVehicleItemMessagePrefix + "VMSG" + deniedVehicleUnlock.WarsawID + separator + deniedVehicleUnlock.Slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof(Int32), Log.DebugLevel));
            }
            catch (Exception e)
            {
                Log.Exception("Error while getting display plugin variables", e);
            }
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            var lstReturn = new List<CPluginVariable>();
            const String separator = " | ";

            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Enable High Request Volume", typeof(Boolean), _highRequestVolume));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Use Proxy for Battlelog", typeof(Boolean), _useProxy));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Proxy URL", typeof(String), _proxyURL));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Integrate with AdKats", typeof(Boolean), _enableAdKatsIntegration));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Use Backup AutoAdmin", typeof(Boolean), _UseBackupAutoadmin));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforcement Only", typeof(Boolean), _spawnEnforcementOnly));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Backup AutoAdmin Use AdKats Punishments", typeof(Boolean), _UseAdKatsPunishments));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Admins", typeof(Boolean), _spawnEnforcementActOnAdmins));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Reputable Players", typeof(Boolean), _spawnEnforcementActOnReputablePlayers));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Display Weapon Popularity Periodically", typeof(Boolean), _displayWeaponPopularity));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Action Whitelist", typeof(String[]), _Whitelist));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Trigger Enforce Minimum Infraction Points", typeof(Int32), _triggerEnforcementMinimumInfractionPoints));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Weapon Popularity Display Frequency Minutes", typeof(Int32), _weaponPopularityDisplayMinutes));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Global Item Filter", typeof(String[]), _ItemFilter));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Inverse Mode (Whitelist)", typeof(Boolean), _inverseEnforcementMode));
            lstReturn.Add(new CPluginVariable("Server Settings|Minimum Players for Enforcement", typeof(Int32), _minimumPlayersForEnforcement));
            lstReturn.Add(new CPluginVariable("Server Settings|Check Unlocks Before Enforcing", typeof(enumBoolYesNo), _checkUnlocksBeforeEnforcing ? enumBoolYesNo.Yes : enumBoolYesNo.No));
            lstReturn.Add(new CPluginVariable("Weapon Limits|Max Snipers Per Team", typeof(Int32), _maxSnipersPerTeam));
            lstReturn.Add(new CPluginVariable("Weapon Limits|Max DMRs Per Team", typeof(Int32), _maxDMRsPerTeam));
            lstReturn.Add(new CPluginVariable("Weapon Limits|Max Shotguns Per Team", typeof(Int32), _maxShotgunsPerTeam));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Map/Mode Settings", typeof(Boolean), _displayMapsModes));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Settings", typeof(Boolean), _displayWeapons));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Accessory Settings", typeof(Boolean), _displayWeaponAccessories));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Gadget Settings", typeof(Boolean), _displayGadgets));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Vehicle Settings", typeof(Boolean), _displayVehicles));
            lstReturn.Add(new CPluginVariable(SettingsMapModePrefix + "Enforce on Specific Maps/Modes Only", typeof(Boolean), _restrictSpecificMapModes));
            lstReturn.Add(new CPluginVariable(SettingsVehiclePrefix + separator.Trim() + "Spawn Enforce all Vehicles", typeof(Boolean), _spawnEnforceAllVehicles));
            lstReturn.AddRange(_warsawInvalidLoadoutIDMessages.Select(pair => new CPluginVariable("MSG" + pair.Key, typeof(String), pair.Value)));
            lstReturn.AddRange(_warsawInvalidVehicleLoadoutIDMessages.Select(pair => new CPluginVariable("VMSG" + pair.Key, typeof(String), pair.Value)));
            _warsawSpawnDeniedIDs.RemoveWhere(spawnID => !_warsawInvalidLoadoutIDMessages.ContainsKey(spawnID));
            lstReturn.AddRange(_warsawSpawnDeniedIDs.Select(deniedSpawnID => new CPluginVariable("ALWS" + deniedSpawnID, typeof(String), "Deny")));
            lstReturn.AddRange(_restrictedMapModes.Values.Select(restrictedMapMode => new CPluginVariable("RMM" + restrictedMapMode.MapModeID, typeof(String), "Enforce")));
            lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof(Int32), Log.DebugLevel));
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
                    //Settings page will be updated after return.
                }
                else if (Regex.Match(strVariable, @"Debug level").Success)
                {
                    Int32 tmp;
                    if (Int32.TryParse(strValue, out tmp))
                    {
                        if (tmp == 269)
                        {
                            Log.Success("Extended Debug Mode Toggled");
                            _displayLoadoutDebug = !_displayLoadoutDebug;
                            return;
                        }
                        else if (tmp == 2232)
                        {
                            Environment.Exit(2232);
                        }
                        Log.DebugLevel = tmp;
                    }
                }
                else if (Regex.Match(strVariable, @"Enable High Request Volume").Success)
                {
                    Boolean highRequestVolume = Boolean.Parse(strValue);
                    if (highRequestVolume != _highRequestVolume)
                    {
                        _highRequestVolume = highRequestVolume;
                    }
                }
                else if (Regex.Match(strVariable, @"Use Proxy for Battlelog").Success)
                {
                    Boolean useProxy = Boolean.Parse(strValue);
                    if (useProxy != _useProxy)
                    {
                        _useProxy = useProxy;
                    }
                }
                else if (Regex.Match(strVariable, @"Proxy URL").Success)
                {
                    try
                    {
                        if (!String.IsNullOrEmpty(strValue))
                        {
                            Uri uri = new Uri(strValue);
                            Log.Debug("Proxy URL set to " + strValue + ".", 1);
                        }
                    }
                    catch (UriFormatException)
                    {
                        strValue = _proxyURL;
                        Log.Warn("Invalid Proxy URL! Make sure that the URI is valid!");
                    }
                    if (!_proxyURL.Equals(strValue))
                    {
                        _proxyURL = strValue;
                    }
                }
                else if (Regex.Match(strVariable, @"Integrate with AdKats").Success)
                {
                    Boolean enableAdKatsIntegration = Boolean.Parse(strValue);
                    if (enableAdKatsIntegration != _enableAdKatsIntegration)
                    {
                        if (!enableAdKatsIntegration)
                        {
                            _UseAdKatsPunishments = false;
                        }
                        if (_threadsReady)
                        {
                            Log.Info("AdKatsLRT must be rebooted to modify this setting.");
                            Disable();
                        }
                        _enableAdKatsIntegration = enableAdKatsIntegration;
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
                            _displayMapsModes = false;
                            _displayWeapons = false;
                            _displayWeaponAccessories = false;
                            _displayGadgets = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Spawn Enforcement Only").Success)
                {
                    _spawnEnforcementOnly = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Use Backup AutoAdmin").Success)
                {
                    _UseBackupAutoadmin = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Backup AutoAdmin Use AdKats Punishments").Success)
                {
                    _UseAdKatsPunishments = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Spawn Enforce Admins").Success)
                {
                    _spawnEnforcementActOnAdmins = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Spawn Enforce Reputable Players").Success)
                {
                    _spawnEnforcementActOnReputablePlayers = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Display Weapon Popularity Periodically").Success)
                {
                    _displayWeaponPopularity = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Spawn Enforce all Vehicles").Success)
                {
                    _spawnEnforceAllVehicles = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Enforce on Specific Maps/Modes Only").Success)
                {
                    _restrictSpecificMapModes = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Trigger Enforce Minimum Infraction Points").Success)
                {
                    Int32 triggerEnforcementMinimumInfractionPoints;
                    if (Int32.TryParse(strValue, out triggerEnforcementMinimumInfractionPoints))
                    {
                        if (triggerEnforcementMinimumInfractionPoints < 1)
                        {
                            Log.Error("Minimum infraction points for trigger level enforcement cannot be less than 1, use spawn enforcement instead.");
                            triggerEnforcementMinimumInfractionPoints = 1;
                        }
                        _triggerEnforcementMinimumInfractionPoints = triggerEnforcementMinimumInfractionPoints;
                    }
                }
                else if (Regex.Match(strVariable, @"Weapon Popularity Display Frequency Minutes").Success)
                {
                    Int32 weaponPopularityDisplayMinutes;
                    if (Int32.TryParse(strValue, out weaponPopularityDisplayMinutes))
                    {
                        if (weaponPopularityDisplayMinutes < 2)
                        {
                            Log.Error("Frequency cannot be less than every 2 minutes.");
                            weaponPopularityDisplayMinutes = 2;
                        }
                        _weaponPopularityDisplayMinutes = weaponPopularityDisplayMinutes;
                    }
                }
                else if (Regex.Match(strVariable, @"Minimum Players for Enforcement").Success)
                {
                    Int32 minimumPlayersForEnforcement;
                    if (Int32.TryParse(strValue, out minimumPlayersForEnforcement))
                    {
                        if (minimumPlayersForEnforcement < 0)
                        {
                            Log.Error("Minimum players for enforcement cannot be negative.");
                            minimumPlayersForEnforcement = 0;
                        }
                        _minimumPlayersForEnforcement = minimumPlayersForEnforcement;
                    }
                }
                else if (Regex.Match(strVariable, @"Check Unlocks Before Enforcing").Success)
                {
                    _checkUnlocksBeforeEnforcing = strValue == "Yes";
                }
                else if (Regex.Match(strVariable, @"Max Snipers Per Team").Success)
                {
                    Int32 maxSnipersPerTeam;
                    if (Int32.TryParse(strValue, out maxSnipersPerTeam))
                    {
                        if (maxSnipersPerTeam < 0)
                        {
                            Log.Error("Max snipers per team cannot be negative.");
                            maxSnipersPerTeam = 0;
                        }
                        _maxSnipersPerTeam = maxSnipersPerTeam;
                    }
                }
                else if (Regex.Match(strVariable, @"Max DMRs Per Team").Success)
                {
                    Int32 maxDMRsPerTeam;
                    if (Int32.TryParse(strValue, out maxDMRsPerTeam))
                    {
                        if (maxDMRsPerTeam < 0)
                        {
                            Log.Error("Max DMRs per team cannot be negative.");
                            maxDMRsPerTeam = 0;
                        }
                        _maxDMRsPerTeam = maxDMRsPerTeam;
                    }
                }
                else if (Regex.Match(strVariable, @"Max Shotguns Per Team").Success)
                {
                    Int32 maxShotgunsPerTeam;
                    if (Int32.TryParse(strValue, out maxShotgunsPerTeam))
                    {
                        if (maxShotgunsPerTeam < 0)
                        {
                            Log.Error("Max shotguns per team cannot be negative.");
                            maxShotgunsPerTeam = 0;
                        }
                        _maxShotgunsPerTeam = maxShotgunsPerTeam;
                    }
                }
                else if (Regex.Match(strVariable, @"Inverse Mode \(Whitelist\)").Success)
                {
                    _inverseEnforcementMode = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Action Whitelist").Success)
                {
                    _Whitelist = CPluginVariable.DecodeStringArray(strValue).Where(entry => !String.IsNullOrEmpty(entry)).ToArray();
                }
                else if (Regex.Match(strVariable, @"Global Item Search Blacklist").Success || Regex.Match(strVariable, @"Global Item Filter").Success)
                {
                    _ItemFilter = CPluginVariable.DecodeStringArray(strValue).Where(entry => !String.IsNullOrEmpty(entry)).ToArray();
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
                            _warsawInvalidLoadoutIDMessages.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            _warsawInvalidLoadoutIDMessages[warsawID] = "Please remove " + commandSplit[commandSplit.Count() - 2].Trim() + " from your loadout";
                            if (!_enableAdKatsIntegration || _spawnEnforcementOnly)
                            {
                                if (!_warsawSpawnDeniedIDs.Contains(warsawID))
                                {
                                    _warsawSpawnDeniedIDs.Add(warsawID);
                                }
                            }
                            break;
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
                            _warsawInvalidVehicleLoadoutIDMessages.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            WarsawItem item;
                            if (!_warsawLibrary.VehicleUnlocks.TryGetValue(warsawID, out item))
                            {
                                Log.Error("Unable to find vehicle unlock " + warsawID);
                                return;
                            }
                            if (item.AssignedVehicle == null)
                            {
                                Log.Error("Unlock item " + warsawID + " was not assigned to a vehicle.");
                                return;
                            }
                            _warsawInvalidVehicleLoadoutIDMessages[warsawID] = "Please remove " + commandSplit[commandSplit.Count() - 2].Trim() + " from your " + item.AssignedVehicle.CategoryType;
                            if (!_warsawSpawnDeniedIDs.Contains(warsawID))
                            {
                                _warsawSpawnDeniedIDs.Add(warsawID);
                            }
                            foreach (var aPlayer in _playerDictionary.Values)
                            {
                                aPlayer.WatchedVehicles.Clear();
                            }
                            break;
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
                            _warsawSpawnDeniedIDs.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            if (!_warsawSpawnDeniedIDs.Contains(warsawID))
                            {
                                _warsawSpawnDeniedIDs.Add(warsawID);
                            }
                            break;
                    }
                }
                else if (strVariable.StartsWith("RMM"))
                {
                    //Trim off all but the warsaw ID
                    //ALWS3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    Int32 mapModeID = Int32.Parse(commandSplit[0].TrimStart("RMM".ToCharArray()).Trim());
                    MapMode mapMode = _availableMapModes.FirstOrDefault(mm => mm.MapModeID == mapModeID);
                    if (mapMode == null)
                    {
                        Log.Error("Invalid map/mode ID when parsing map enforce settings.");
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
                                if (_warsawLibraryLoaded)
                                {
                                    Log.Info("Enforcing loadout on " + mapMode.ModeName + " " + mapMode.MapName);
                                }
                            }
                            break;
                        case "ignore":
                            //parse allow
                            if (_restrictedMapModes.Remove(mapMode.ModeKey + "|" + mapMode.MapKey) && _warsawLibraryLoaded)
                            {
                                Log.Info("No longer enforcing loadout on " + mapMode.ModeName + " " + mapMode.MapName);
                            }
                            break;
                        default:
                            Log.Error("Unknown setting when parsing map enforce settings.");
                            return;
                    }
                }
                else if (strVariable.StartsWith("MSG"))
                {
                    //Trim off all but the warsaw ID
                    //MSG3495820391
                    if (String.IsNullOrEmpty(strValue))
                    {
                        Log.Error("Kill messages cannot be empty.");
                        return;
                    }
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("MSG".ToCharArray()).Trim();
                    _warsawInvalidLoadoutIDMessages[warsawID] = strValue;
                }
                else if (strVariable.StartsWith("VMSG"))
                {
                    //Trim off all but the warsaw ID
                    //MSG3495820391
                    if (String.IsNullOrEmpty(strValue))
                    {
                        Log.Error("Kill messages cannot be empty.");
                        return;
                    }
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("VMSG".ToCharArray()).Trim();
                    _warsawInvalidVehicleLoadoutIDMessages[warsawID] = strValue;
                }
                else
                {
                    Log.Info(strVariable + " =+= " + strValue);
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error occured while updating AdKatsLRT settings.", e);
            }
        }
    }
}
