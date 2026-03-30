using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;

using Flurl;
using Flurl.Http;

namespace PRoConEvents
{
    public partial class AdKatsLRT
    {
        private void QueueForProcessing(ProcessObject processObject)
        {
            Log.Debug("Entering QueueForProcessing", 7);
            try
            {
                if (processObject == null || processObject.ProcessPlayer == null)
                {
                    Log.Error("Attempted to process null object or player.");
                    return;
                }
                if (!processObject.ProcessPlayer.Online ||
                    String.IsNullOrEmpty(processObject.ProcessPlayer.PersonaID))
                {
                    Log.Debug(processObject.ProcessPlayer.Name + " queue cancelled. Player is not online, or has no persona ID.", 4);
                    return;
                }
                lock (_loadoutProcessingQueue)
                {
                    if (_loadoutProcessingQueue.Any(obj =>
                        obj != null &&
                        obj.ProcessPlayer != null &&
                        obj.ProcessPlayer.GUID == processObject.ProcessPlayer.GUID))
                    {
                        Log.Debug(processObject.ProcessPlayer.Name + " queue cancelled. Player already in queue.", 4);
                        return;
                    }
                    Int32 oldCount = _loadoutProcessingQueue.Count();
                    _loadoutProcessingQueue.Enqueue(processObject);
                    Log.Debug(processObject.ProcessPlayer.Name + " queued [" + oldCount + "->" + _loadoutProcessingQueue.Count + "] after " + Math.Round(DateTime.UtcNow.Subtract(processObject.ProcessTime).TotalSeconds, 2) + "s", 5);
                    _loadoutProcessingWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while queueing player for processing.", e);
            }
            Log.Debug("Exiting QueueForProcessing", 7);
        }

        public void ProcessingThreadLoop()
        {
            try
            {
                Log.Debug("SPROC: Starting Spawn Processing Thread", 1);
                Thread.CurrentThread.Name = "SpawnProcessing";
                while (true)
                {
                    try
                    {
                        Log.Debug("SPROC: Entering Spawn Processing Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug("SPROC: Detected AdKatsLRT not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        if (_battlelogFetchQueue.Count() >= 5)
                        {
                            Log.Debug("loadout checks waiting on battlelog info fetches to complete.", 4);
                            _threadMasterWaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                            continue;
                        }

                        if (_loadoutProcessingQueue.Count > 0)
                        {
                            ProcessObject processObject = null;
                            lock (_loadoutProcessingQueue)
                            {
                                //Dequeue the next object
                                Int32 oldCount = _loadoutProcessingQueue.Count();
                                ProcessObject importObject = _loadoutProcessingQueue.Dequeue();
                                if (importObject == null)
                                {
                                    Log.Error("Process object was null when entering player processing loop.");
                                    continue;
                                }
                                if (importObject.ProcessPlayer == null)
                                {
                                    Log.Error("Process player was null when entering player processing loop.");
                                    continue;
                                }
                                if (!importObject.ProcessPlayer.Online)
                                {
                                    continue;
                                }
                                var processDelay = DateTime.UtcNow.Subtract(importObject.ProcessTime);
                                if (DateTime.UtcNow.Subtract(importObject.ProcessTime).TotalSeconds > 30 && _loadoutProcessingQueue.Count < 3)
                                {
                                    Log.Warn(importObject.ProcessPlayer.GetVerboseName() + " took abnormally long to start processing. [" + FormatTimeString(processDelay, 2) + "]");
                                }
                                else
                                {
                                    Log.Debug(importObject.ProcessPlayer.Name + " dequeued [" + oldCount + "->" + _loadoutProcessingQueue.Count + "] after " + Math.Round(processDelay.TotalSeconds, 2) + "s", 5);
                                }
                                processObject = importObject;
                            }

                            //Grab the player
                            AdKatsSubscribedPlayer aPlayer = processObject.ProcessPlayer;

                            //Parse the reason for enforcement
                            Boolean fetchOnly = false;
                            Boolean fetchOnlyNotify = true;
                            Boolean trigger = false;
                            Boolean killOverride = false;
                            String reason = "";
                            if (processObject.ProcessReason == "fetch")
                            {
                                reason = "[fetch] ";
                                fetchOnly = true;
                            }
                            else if (aPlayer.LoadoutIgnored || processObject.ProcessReason == "ignored")
                            {
                                reason = "[ignored] ";
                                fetchOnly = true;
                                aPlayer.LoadoutIgnored = true;
                            }
                            else if (aPlayer.LoadoutForced || processObject.ProcessReason == "forced")
                            {
                                reason = "[forced] ";
                                trigger = true;
                                killOverride = true;
                            }
                            else if (aPlayer.Punished || processObject.ProcessReason == "punished")
                            {
                                reason = "[recently punished] ";
                                trigger = true;
                                killOverride = true;
                            }
                            else if ((aPlayer.Reported || processObject.ProcessReason == "reported") && aPlayer.Reputation <= 0)
                            {
                                reason = "[reported] ";
                                trigger = true;
                            }
                            else if (aPlayer.InfractionPoints >= _triggerEnforcementMinimumInfractionPoints && aPlayer.LastPunishment.TotalDays < 60 && aPlayer.Reputation <= 0)
                            {
                                reason = "[" + aPlayer.InfractionPoints + " infractions] ";
                                trigger = true;
                            }
                            else if (processObject.ProcessReason == "vehiclekill")
                            {
                                reason = "[vehicle kill] ";
                            }
                            else if (processObject.ProcessReason == "spawn")
                            {
                                reason = "[spawn] ";
                            }
                            else if (processObject.ProcessReason == "listing")
                            {
                                reason = "[join] ";
                            }
                            else
                            {
                                Log.Error("Unknown reason for processing player. Cancelling processing.");
                                continue;
                            }

                            Log.Debug("Processing " + reason + aPlayer.GetVerboseName(), 4);

                            if (!fetchOnly)
                            {
                                //Process is not fetch only, check to see if we can skip this player
                                Boolean fetch = true;
                                String rejectFetchReason = "Loadout fetches cancelled. No reason given.";
                                if (!trigger)
                                {
                                    if (fetch &&
                                        (aPlayer.Reputation >= 50 && !_spawnEnforcementActOnReputablePlayers))
                                    {
                                        rejectFetchReason = aPlayer.Name + " loadout actions cancelled. Player is reputable.";
                                        if (_displayWeaponPopularity)
                                        {
                                            fetchOnly = true;
                                            fetchOnlyNotify = false;
                                        }
                                        else
                                        {
                                            fetch = false;
                                        }
                                    }
                                    if (fetch &&
                                        (aPlayer.IsAdmin && !_spawnEnforcementActOnAdmins))
                                    {
                                        rejectFetchReason = aPlayer.Name + " loadout actions cancelled. Player is admin.";
                                        if (_displayWeaponPopularity)
                                        {
                                            fetchOnly = true;
                                            fetchOnlyNotify = false;
                                        }
                                        else
                                        {
                                            fetch = false;
                                        }
                                    }
                                    //Special case for large servers to reduce request frequency
                                    if (fetch &&
                                        !_highRequestVolume &&
                                        aPlayer.LoadoutChecks > ((aPlayer.Reputation > 0) ? (0) : (3)) &&
                                        aPlayer.LoadoutValid &&
                                        aPlayer.SkippedChecks < 4)
                                    {
                                        aPlayer.SkippedChecks++;
                                        rejectFetchReason = aPlayer.Name + " loadout actions cancelled. Player clean after " + aPlayer.LoadoutChecks + " checks. " + aPlayer.SkippedChecks + " current skips.";
                                        fetch = false;
                                    }
                                }
                                if (fetch &&
                                    (_Whitelist.Contains(aPlayer.Name) ||
                                    _Whitelist.Contains(aPlayer.GUID) ||
                                    _Whitelist.Contains(aPlayer.PBGUID) ||
                                    _Whitelist.Contains(aPlayer.IP)))
                                {
                                    rejectFetchReason = aPlayer.Name + " loadout actions cancelled. Player on whitelist.";
                                    if (_displayWeaponPopularity)
                                    {
                                        fetchOnly = true;
                                        fetchOnlyNotify = false;
                                    }
                                    else
                                    {
                                        fetch = false;
                                    }
                                }
                                if (!fetch)
                                {
                                    if (_enableAdKatsIntegration)
                                    {
                                        //Inform AdKats of the check rejection
                                        StartAndLogThread(new Thread(new ThreadStart(delegate
                                        {
                                            Thread.CurrentThread.Name = "AdKatsInform";
                                            Thread.Sleep(50);
                                            ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                            {"caller_identity", "AdKatsLRT"},
                                            {"response_requested", false},
                                            {"loadout_player", aPlayer.Name},
                                            {"loadout_valid", true},
                                            {"loadout_spawnValid", true},
                                            {"loadout_acted", false},
                                            {"loadout_items", rejectFetchReason},
                                            {"loadout_items_long", rejectFetchReason},
                                            {"loadout_deniedItems", rejectFetchReason}
                                        }));
                                            Thread.Sleep(50);
                                            LogThreadExit();
                                        })));
                                    }
                                    Log.Debug(rejectFetchReason, 3);
                                    continue;
                                }
                            }

                            //Fetch the loadout
                            AdKatsLoadout loadout = GetPlayerLoadout(aPlayer.PersonaID);
                            if (loadout == null)
                            {
                                continue;
                            }
                            aPlayer.Loadout = loadout;
                            aPlayer.LoadoutChecks++;
                            aPlayer.SkippedChecks = 0;

                            //Show the loadout contents
                            String primaryMessage = loadout.KitItemPrimary.Slug + " [" + loadout.KitItemPrimary.AccessoriesAssigned.Values.Aggregate("", (currentString, acc) => currentString + TrimStart(acc.Slug, loadout.KitItemPrimary.Slug).Trim() + ", ").Trim().TrimEnd(',') + "]";
                            String sidearmMessage = loadout.KitItemSidearm.Slug + " [" + loadout.KitItemSidearm.AccessoriesAssigned.Values.Aggregate("", (currentString, acc) => currentString + TrimStart(acc.Slug, loadout.KitItemSidearm.Slug).Trim() + ", ").Trim().TrimEnd(',') + "]";
                            String gadgetMessage = "[" + loadout.KitGadget1.Slug + ", " + loadout.KitGadget2.Slug + "]";
                            String grenadeMessage = "[" + loadout.KitGrenade.Slug + "]";
                            String knifeMessage = "[" + loadout.KitKnife.Slug + "]";
                            String loadoutLongMessage = "Player " + loadout.Name + " processed as " + loadout.SelectedKit.KitType + " with primary " + primaryMessage + " sidearm " + sidearmMessage + " gadgets " + gadgetMessage + " grenade " + grenadeMessage + " and knife " + knifeMessage;
                            String loadoutShortMessage = "Primary [" + loadout.KitItemPrimary.Slug + "] sidearm [" + loadout.KitItemSidearm.Slug + "] gadgets " + gadgetMessage + " grenade " + grenadeMessage + " and knife " + knifeMessage;
                            Log.Debug(loadoutLongMessage, 4);

                            if (fetchOnly && fetchOnlyNotify)
                            {
                                //Inform AdKats of the loadout
                                StartAndLogThread(new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "AdKatsInform";
                                    Thread.Sleep(100);
                                    Log.Debug("Informing AdKats of " + aPlayer.GetVerboseName() + " fetched loadout.", 3);
                                    ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                        {"caller_identity", "AdKatsLRT"},
                                        {"response_requested", false},
                                        {"loadout_player", loadout.Name},
                                        {"loadout_valid", true},
                                        {"loadout_spawnValid", true},
                                        {"loadout_acted", false},
                                        {"loadout_items", loadoutShortMessage},
                                        {"loadout_items_long", loadoutLongMessage},
                                        {"loadout_deniedItems", ""}
                                    }));
                                    Thread.Sleep(100);
                                    LogThreadExit();
                                })));
                                continue;
                            }

                            //Action taken?
                            Boolean acted = false;

                            HashSet<String> specificMessages = new HashSet<String>();
                            HashSet<String> spawnSpecificMessages = new HashSet<String>();
                            HashSet<String> vehicleSpecificMessages = new HashSet<String>();
                            Boolean loadoutValid = true;
                            Boolean spawnLoadoutValid = true;
                            Boolean vehicleLoadoutValid = true;

                            if (_serverInfo.InfoObject.GameMode != "GunMaster0" &&
                                _serverInfo.InfoObject.GameMode != "GunMaster1" &&
                                (!_restrictSpecificMapModes || _restrictedMapModes.ContainsKey(_serverInfo.InfoObject.GameMode + "|" + _serverInfo.InfoObject.Map)))
                            {
                                if (trigger)
                                {
                                    foreach (var warsawDeniedIDMessage in _warsawInvalidLoadoutIDMessages)
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

                                    foreach (var warsawDeniedID in _warsawSpawnDeniedIDs)
                                    {
                                        if (loadout.AllKitItemIDs.Contains(warsawDeniedID))
                                        {
                                            spawnLoadoutValid = false;
                                            if (!spawnSpecificMessages.Contains(_warsawInvalidLoadoutIDMessages[warsawDeniedID]))
                                            {
                                                spawnSpecificMessages.Add(_warsawInvalidLoadoutIDMessages[warsawDeniedID]);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var warsawDeniedID in _warsawSpawnDeniedIDs)
                                    {
                                        if (loadout.AllKitItemIDs.Contains(warsawDeniedID))
                                        {
                                            loadoutValid = false;
                                            spawnLoadoutValid = false;
                                            if (!spawnSpecificMessages.Contains(_warsawInvalidLoadoutIDMessages[warsawDeniedID]))
                                            {
                                                spawnSpecificMessages.Add(_warsawInvalidLoadoutIDMessages[warsawDeniedID]);
                                            }
                                        }
                                    }
                                }

                                foreach (var warsawDeniedIDMessage in _warsawInvalidVehicleLoadoutIDMessages)
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
                                                Log.Error("Could not fetch used vehicle " + category + " from player loadout, skipping.");
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

                            Boolean act = true;
                            if (!trigger && !spawnLoadoutValid)
                            {
                                if (act && (processObject.ProcessPlayer.Reputation >= 50 && !_spawnEnforcementActOnReputablePlayers))
                                {
                                    Log.Debug(processObject.ProcessPlayer.Name + " spawn loadout enforcement cancelled. Player is reputable.", 4);
                                    act = false;
                                }
                                if (act && (processObject.ProcessPlayer.IsAdmin && !_spawnEnforcementActOnAdmins))
                                {
                                    Log.Debug(processObject.ProcessPlayer.Name + " spawn loadout enforcement cancelled. Player is admin.", 4);
                                    act = false;
                                }
                            }
                            if (act && (_Whitelist.Contains(processObject.ProcessPlayer.Name) ||
                                _Whitelist.Contains(processObject.ProcessPlayer.GUID) ||
                                _Whitelist.Contains(processObject.ProcessPlayer.PBGUID) ||
                                _Whitelist.Contains(processObject.ProcessPlayer.IP)))
                            {
                                Log.Debug(processObject.ProcessPlayer.Name + " loadout enforcement cancelled. Player on whitelist.", 4);
                                act = false;
                            }
                            if (!act)
                            {
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
                                                    {"loadout_items_long", loadoutLongMessage},
                                                    {"loadout_deniedItems", ""}
                                                }));
                                        Thread.Sleep(100);
                                        LogThreadExit();
                                    })));
                                }
                                continue;
                            }

                            aPlayer.LoadoutEnforced = true;
                            String deniedWeapons = String.Empty;
                            String spawnDeniedWeapons = String.Empty;
                            if (!loadoutValid)
                            {
                                //Fill the denied messages
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitItemPrimary.WarsawID))
                                {
                                    deniedWeapons += loadout.KitItemPrimary.Slug.ToUpper() + ", ";
                                }
                                deniedWeapons = loadout.KitItemPrimary.AccessoriesAssigned.Values.Where(weaponAccessory => _warsawInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID)).Aggregate(deniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitItemSidearm.WarsawID))
                                {
                                    deniedWeapons += loadout.KitItemSidearm.Slug.ToUpper() + ", ";
                                }
                                deniedWeapons = loadout.KitItemSidearm.AccessoriesAssigned.Values.Where(weaponAccessory => _warsawInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID)).Aggregate(deniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitGadget1.WarsawID))
                                {
                                    deniedWeapons += loadout.KitGadget1.Slug.ToUpper() + ", ";
                                }
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitGadget2.WarsawID))
                                {
                                    deniedWeapons += loadout.KitGadget2.Slug.ToUpper() + ", ";
                                }
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitGrenade.WarsawID))
                                {
                                    deniedWeapons += loadout.KitGrenade.Slug.ToUpper() + ", ";
                                }
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitKnife.WarsawID))
                                {
                                    deniedWeapons += loadout.KitKnife.Slug.ToUpper() + ", ";
                                }
                                //Fill the spawn denied messages
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitItemPrimary.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitItemPrimary.Slug.ToUpper() + ", ";
                                }
                                spawnDeniedWeapons = loadout.KitItemPrimary.AccessoriesAssigned.Values.Where(weaponAccessory => _warsawSpawnDeniedIDs.Contains(weaponAccessory.WarsawID)).Aggregate(spawnDeniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitItemSidearm.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitItemSidearm.Slug.ToUpper() + ", ";
                                }
                                spawnDeniedWeapons = loadout.KitItemSidearm.AccessoriesAssigned.Values.Where(weaponAccessory => _warsawSpawnDeniedIDs.Contains(weaponAccessory.WarsawID)).Aggregate(spawnDeniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitGadget1.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGadget1.Slug.ToUpper() + ", ";
                                }
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitGadget2.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGadget2.Slug.ToUpper() + ", ";
                                }
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitGrenade.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGrenade.Slug.ToUpper() + ", ";
                                }
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitKnife.WarsawID))
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
                                    Log.Debug(loadout.Name + ((processObject.ProcessReason == "listing") ? (" JOIN") : (" SPAWN")) + " KILLED for invalid loadout.", 1);
                                    if (processObject.ProcessReason != "listing")
                                    {
                                        aPlayer.LoadoutKills++;
                                    }
                                    if (aPlayer.SpawnedOnce)
                                    {
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
                                        PlayerYellMessage(aPlayer.Name, reason + aPlayer.GetVerboseName() + " please remove [" + deniedWeapons + "] from your loadout.");
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
                                        PlayerYellMessage(aPlayer.Name, reason + aPlayer.GetVerboseName() + " please remove [" + spawnDeniedWeapons + "] from your loadout.");
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
                                    //Set max denied items if player has been killed
                                    if (tellMessages.Count > aPlayer.MaxDeniedItems && aPlayer.LoadoutKills > 0)
                                    {
                                        aPlayer.MaxDeniedItems = tellMessages.Count;
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
                                if (!aPlayer.LoadoutValid)
                                {
                                    PlayerSayMessage(aPlayer.Name, aPlayer.GetVerboseName() + " thank you for fixing your loadout.");
                                    if (killOverride)
                                    {
                                        OnlineAdminSayMessage(reason + aPlayer.GetVerboseName() + " fixed their loadout.");
                                    }
                                }
                                else if (processObject.ProcessManual)
                                {
                                    OnlineAdminSayMessage(aPlayer.GetVerboseName() + "'s has no denied items.");
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
                                        {"loadout_items_long", loadoutLongMessage},
                                        {"loadout_deniedItems", deniedWeapons}
                                    }));
                                    Thread.Sleep(100);
                                    LogThreadExit();
                                })));
                            }
                            aPlayer.LoadoutValid = loadoutValid;
                            lock (_playerDictionary)
                            {
                                Int32 totalPlayerCount = _playerDictionary.Count + _playerLeftDictionary.Count;
                                Int32 countKills = _playerDictionary.Values.Sum(dPlayer => dPlayer.LoadoutKills) + _playerLeftDictionary.Values.Sum(dPlayer => dPlayer.LoadoutKills);
                                Int32 countEnforced = _playerDictionary.Values.Count(dPlayer => dPlayer.LoadoutEnforced) + _playerLeftDictionary.Values.Count(dPlayer => dPlayer.LoadoutEnforced);
                                Int32 countKilled = _playerDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0) + _playerLeftDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0);
                                Int32 countFixed = _playerDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0 && dPlayer.LoadoutValid) + _playerLeftDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0 && dPlayer.LoadoutValid);
                                Int32 countQuit = _playerLeftDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0 && !dPlayer.LoadoutValid);
                                Boolean displayStats = (_countKilled != countKilled) ||
                                                       (_countFixed != countFixed) ||
                                                       (_countQuit != countQuit);
                                _countKilled = countKilled;
                                _countFixed = countFixed;
                                _countQuit = countQuit;
                                Double percentEnforced = Math.Round((countEnforced / (Double)totalPlayerCount) * 100.0);
                                Double percentKilled = Math.Round((countKilled / (Double)totalPlayerCount) * 100.0);
                                Double percentFixed = Math.Round((countFixed / (Double)countKilled) * 100.0);
                                Double percentRaged = Math.Round((countQuit / (Double)countKilled) * 100.0);
                                Double denialKpm = Math.Round(countKills / (DateTime.UtcNow - _pluginStartTime).TotalMinutes, 2);
                                Double killsPerDenial = Math.Round(countKills / (Double)countKilled, 2);
                                Double avgDeniedItems = Math.Round((_playerDictionary.Values.Sum(dPlayer => dPlayer.MaxDeniedItems) + _playerLeftDictionary.Values.Sum(dPlayer => dPlayer.MaxDeniedItems)) / (Double)countKilled, 2);
                                if (displayStats)
                                {
                                    Log.Debug("(" + countEnforced + "/" + totalPlayerCount + ") " + percentEnforced + "% enforced. " + "(" + countKilled + "/" + totalPlayerCount + ") " + percentKilled + "% killed. " + "(" + countFixed + "/" + countKilled + ") " + percentFixed + "% fixed. " + "(" + countQuit + "/" + countKilled + ") " + percentRaged + "% quit. " + denialKpm + " denial KPM. " + killsPerDenial + " kills per denial. " + avgDeniedItems + " AVG denied items.", 2);
                                }
                            }
                            Log.Debug(_loadoutProcessingQueue.Count + " players still in queue.", 3);
                            Log.Debug(processObject.ProcessPlayer.Name + " processed after " + Math.Round(DateTime.UtcNow.Subtract(processObject.ProcessTime).TotalSeconds, 2) + "s", 5);
                        }
                        else
                        {
                            //Wait for input
                            _loadoutProcessingWaitHandle.Reset();
                            _loadoutProcessingWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.Exception("Spawn processing thread aborted. Exiting.", e);
                            break;
                        }
                        Log.Exception("Error occured in spawn processing thread. Skipping current loop.", e);
                    }
                }
                Log.Debug("SPROC: Ending Spawn Processing Thread", 1);
                LogThreadExit();
            }
            catch (Exception e)
            {
                Log.Exception("Error occured in kill processing thread.", e);
            }
        }

        public Boolean LoadWarsawLibrary()
        {
            Log.Debug("Entering LoadWarsawLibrary", 7);
            try
            {
                if (_gameVersion == GameVersion.BF4)
                {
                    var library = new WarsawLibrary();
                    Log.Info("Downloading WARSAW library.");
                    Hashtable responseData = FetchWarsawLibrary();

                    //Response data
                    if (responseData == null)
                    {
                        Log.Error("WARSAW library fetch failed, unable to generate library.");
                        return false;
                    }
                    //Compact element
                    if (!responseData.ContainsKey("compact"))
                    {
                        Log.Error("WARSAW library fetch did not contain 'compact' element, unable to generate library.");
                        return false;
                    }
                    var compact = (Hashtable)responseData["compact"];
                    if (compact == null)
                    {
                        Log.Error("Compact section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact weapons element
                    if (!compact.ContainsKey("weapons"))
                    {
                        Log.Error("Warsaw compact section did not contain 'weapons' element, unable to generate library.");
                        return false;
                    }
                    var compactWeapons = (Hashtable)compact["weapons"];
                    if (compactWeapons == null)
                    {
                        Log.Error("Compact weapons section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact weapon accessory element
                    if (!compact.ContainsKey("weaponaccessory"))
                    {
                        Log.Error("Warsaw compact section did not contain 'weaponaccessory' element, unable to generate library.");
                        return false;
                    }
                    var compactWeaponAccessory = (Hashtable)compact["weaponaccessory"];
                    if (compactWeaponAccessory == null)
                    {
                        Log.Error("Weapon accessory section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact vehicles element
                    if (!compact.ContainsKey("vehicles"))
                    {
                        Log.Error("Warsaw compact section did not contain 'vehicles' element, unable to generate library.");
                        return false;
                    }
                    var compactVehicles = (Hashtable)compact["vehicles"];
                    if (compactVehicles == null)
                    {
                        Log.Error("Compact vehicles section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact kit items element
                    if (!compact.ContainsKey("kititems"))
                    {
                        Log.Error("Warsaw compact section did not contain 'kititems' element, unable to generate library.");
                        return false;
                    }
                    var compactKitItems = (Hashtable)compact["kititems"];
                    if (compactKitItems == null)
                    {
                        Log.Error("Kit items section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact vehicle unlocks element
                    if (!compact.ContainsKey("vehicleunlocks"))
                    {
                        Log.Error("Warsaw compact section did not contain 'vehicleunlocks' element, unable to generate library.");
                        return false;
                    }
                    var compactVehicleUnlocks = (Hashtable)compact["vehicleunlocks"];
                    if (compactVehicleUnlocks == null)
                    {
                        Log.Error("Vehicle unlocks section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout element
                    if (!responseData.ContainsKey("loadout"))
                    {
                        Log.Error("WARSAW library fetch did not contain 'loadout' element, unable to generate library.");
                        return false;
                    }
                    var loadout = (Hashtable)responseData["loadout"];
                    if (loadout == null)
                    {
                        Log.Error("Loadout section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout weapons element
                    if (!loadout.ContainsKey("weapons"))
                    {
                        Log.Error("Warsaw loadout section did not contain 'weapons' element, unable to generate library.");
                        return false;
                    }
                    var loadoutWeapons = (Hashtable)loadout["weapons"];
                    if (loadoutWeapons == null)
                    {
                        Log.Error("Loadout weapons section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout kits element
                    if (!loadout.ContainsKey("kits"))
                    {
                        Log.Error("Warsaw loadout section did not contain 'kits' element, unable to generate library.");
                        return false;
                    }
                    var loadoutKits = (ArrayList)loadout["kits"];
                    if (loadoutKits == null)
                    {
                        Log.Error("Loadout kits section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout vehicles element
                    if (!loadout.ContainsKey("vehicles"))
                    {
                        Log.Error("Warsaw loadout section did not contain 'vehicles' element, unable to generate library.");
                        return false;
                    }
                    var loadoutVehicles = (ArrayList)loadout["vehicles"];
                    if (loadoutVehicles == null)
                    {
                        Log.Error("Loadout vehicles section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }

                    library.Items.Clear();
                    foreach (DictionaryEntry entry in compactWeapons)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }
                        var item = new WarsawItem
                        {
                            WarsawID = warsawID.ToString(CultureInfo.InvariantCulture)
                        };

                        if (_displayLoadoutDebug)
                        {
                            Log.Info("Loading debug warsaw ID " + item.WarsawID);
                        }

                        //Grab the contents
                        var weaponData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        item.Category = (String)weaponData["category"];
                        if (String.IsNullOrEmpty(item.Category))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        item.CategoryReadable = item.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        item.Name = (String)weaponData["name"];
                        if (String.IsNullOrEmpty(item.Name))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        item.Name = item.Name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab categoryType------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("categoryType"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. Element did not contain 'categoryType'.");
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
                                Log.Error("Rejecting weapon '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        item.Slug = (String)weaponData["slug"];
                        if (String.IsNullOrEmpty(item.Slug))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        item.Slug = item.Slug.Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        library.Items[item.WarsawID] = item;
                    }

                    foreach (DictionaryEntry entry in compactKitItems)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }
                        var kitItem = new WarsawItem
                        {
                            WarsawID = warsawID.ToString(CultureInfo.InvariantCulture)
                        };

                        if (_displayLoadoutDebug)
                        {
                            Log.Info("Loading debug warsaw ID " + kitItem.WarsawID);
                        }

                        //Grab the contents
                        var weaponAccessoryData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        kitItem.Category = (String)weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(kitItem.Category))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        kitItem.CategoryReadable = kitItem.Category.Split('_').Last().Replace('_', ' ').ToUpper();
                        if (kitItem.CategoryReadable != "GADGET" && kitItem.CategoryReadable != "GRENADE" && kitItem.CategoryReadable != "EQUIPMENT")
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. 'category' not gadget, grenade, or equipment.");
                            continue;
                        }

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        kitItem.Name = (String)weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(kitItem.Name))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        kitItem.Name = kitItem.Name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        kitItem.Slug = (String)weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(kitItem.Slug))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. 'slug' was invalid.");
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
                        }
                    }

                    library.ItemAccessories.Clear();
                    foreach (DictionaryEntry entry in compactWeaponAccessory)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }
                        var itemAccessory = new WarsawItemAccessory
                        {
                            WarsawID = warsawID.ToString(CultureInfo.InvariantCulture)
                        };

                        //Grab the contents
                        var weaponAccessoryData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        itemAccessory.Category = (String)weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(itemAccessory.Category))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        itemAccessory.CategoryReadable = itemAccessory.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        itemAccessory.Name = (String)weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(itemAccessory.Name))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        itemAccessory.Name = itemAccessory.Name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        itemAccessory.Slug = (String)weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(itemAccessory.Slug))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        itemAccessory.Slug = itemAccessory.Slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        library.ItemAccessories[itemAccessory.WarsawID] = itemAccessory;
                    }

                    library.Vehicles.Clear();
                    foreach (DictionaryEntry entry in compactVehicles)
                    {
                        String category = (String)entry.Key;
                        if (!category.StartsWith("WARSAW_ID"))
                        {
                            //Reject the entry
                            if (_displayLoadoutDebug)
                                Log.Info("Rejecting vehicle element '" + entry.Key + "', key not a valid ID.");
                            continue;
                        }

                        var vehicle = new WarsawVehicle
                        {
                            Category = category
                        };
                        vehicle.CategoryReadable = vehicle.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab the contents
                        var vehicleData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!vehicleData.ContainsKey("categoryType"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle '" + category + "'. Element did not contain 'categoryType'.");
                            continue;
                        }
                        vehicle.CategoryType = (String)vehicleData["categoryType"];
                        if (String.IsNullOrEmpty(vehicle.CategoryType))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle '" + category + "'. 'categoryType' was invalid.");
                            continue;
                        }
                        vehicle.CategoryTypeReadable = vehicle.CategoryType;

                        //Assign the linked RCON codes
                        switch (vehicle.Category)
                        {
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEMBT":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/M1A2/M1Abrams");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/M1A2/spec/M1Abrams_Night");
                                vehicle.LinkedRCONCodes.Add("T90");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_MBT_Type99/CH_MBT_Type99");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEIFV":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/BTR-90/BTR90");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/BTR-90/spec/BTR90_Night");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_IFV_ZBD09/CH_IFV_ZBD09");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/LAV25/LAV25");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/LAV25/spec/LAV25_Night");
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
                            Log.Success("Vehicle " + vehicle.Category + " added. " + library.Vehicles.ContainsKey(vehicle.Category));
                    }

                    library.VehicleUnlocks.Clear();
                    foreach (DictionaryEntry entry in compactVehicleUnlocks)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }
                        var vehicleUnlock = new WarsawItem
                        {
                            WarsawID = warsawID.ToString(CultureInfo.InvariantCulture)
                        };

                        //Grab the contents
                        var vehicleUnlockData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        vehicleUnlock.Category = (String)vehicleUnlockData["category"];
                        if (String.IsNullOrEmpty(vehicleUnlock.Category))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        vehicleUnlock.CategoryReadable = vehicleUnlock.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab name------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock'" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        var name = (String)vehicleUnlockData["name"];
                        if (String.IsNullOrEmpty(name))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        name = name.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab slug------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        vehicleUnlock.Slug = (String)vehicleUnlockData["slug"];
                        if (String.IsNullOrEmpty(vehicleUnlock.Slug))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        vehicleUnlock.Slug = vehicleUnlock.Slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').TrimEnd('3').TrimEnd('4').TrimEnd('5').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        library.VehicleUnlocks[vehicleUnlock.WarsawID] = vehicleUnlock;
                    }

                    //Fill allowed accessories for each weapon
                    foreach (DictionaryEntry entry in loadoutWeapons)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }

                        WarsawItem weapon;
                        if (!library.Items.TryGetValue(warsawID.ToString(CultureInfo.InvariantCulture), out weapon))
                        {
                            //Reject the entry
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting loadout weapon element '" + warsawID + "', ID not found in weapon library.");
                            continue;
                        }

                        //Grab the contents
                        var weaponData = (Hashtable)entry.Value;
                        if (weaponData == null)
                        {
                            Log.Error("Rejecting loadout weapon element " + warsawID + ", could not parse weapon data.");
                            continue;
                        }
                        //Grab slots------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("slots"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting loadout weapon element '" + warsawID + "'. Element did not contain 'slots'.");
                            continue;
                        }
                        var slots = (ArrayList)weaponData["slots"];
                        foreach (Object slotEntry in slots)
                        {
                            //Grab the contents
                            var slotTable = (Hashtable)slotEntry;
                            if (slotTable == null)
                            {
                                Log.Error("Rejecting slot entry for " + warsawID + ", could not parse slot into hashtable.");
                                continue;
                            }
                            //Grab category------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("sid"))
                            {
                                if (_displayLoadoutDebug)
                                    Log.Error("Rejecting slot entry for " + warsawID + ". Element did not contain 'sid'.");
                                continue;
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
                                    Log.Error("Rejecting slot entry for " + warsawID + ". Element did not contain 'items'.");
                                continue;
                            }
                            var items = (ArrayList)slotTable["items"];
                            Dictionary<String, WarsawItemAccessory> allowedItems;
                            if (weapon.AccessoriesAllowed.ContainsKey(category))
                            {
                                //Existing list, add to it
                                allowedItems = weapon.AccessoriesAllowed[category];
                            }
                            else
                            {
                                //New list, add it
                                allowedItems = new Dictionary<String, WarsawItemAccessory>();
                                weapon.AccessoriesAllowed[category] = allowedItems;
                            }
                            foreach (String accessoryID in items)
                            {
                                //Attempt to fetch accessory from library
                                WarsawItemAccessory accessory;
                                if (library.ItemAccessories.TryGetValue(accessoryID, out accessory))
                                {
                                    allowedItems[accessoryID] = accessory;
                                }
                                else
                                {
                                    if (_displayLoadoutDebug)
                                        Log.Error("Rejecting allowed accessory entry for " + accessoryID + ". Accessory not found in library.");
                                }
                            }
                        }
                    }

                    //Fill allowed items for each class
                    foreach (Hashtable entry in loadoutKits)
                    {
                        //Get the kit key
                        if (!entry.ContainsKey("sid"))
                        {
                            Log.Error("Kit entry did not contain 'sid' element, unable to generate library.");
                            return false;
                        }
                        var kitKey = (String)entry["sid"];

                        WarsawKit kit;
                        switch (kitKey)
                        {
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
                                Log.Error("Kit entry could not be assigned to a valid kit type, unable to generate library.");
                                return false;
                        }

                        //Grab slots------------------------------------------------------------------------------
                        if (!entry.ContainsKey("slots"))
                        {
                            Log.Error("Kit entry '" + kitKey + "' did not contain 'slots' element, unable to generate library.");
                            return false;
                        }
                        var slots = (ArrayList)entry["slots"];
                        foreach (Object slotEntry in slots)
                        {
                            //Grab the contents
                            var slotTable = (Hashtable)slotEntry;
                            if (slotTable == null)
                            {
                                Log.Error("Slot entry for kit '" + kitKey + "', could not parse slot into hashtable, unable to generate library.");
                                return false;
                            }
                            //Grab category------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("sid"))
                            {
                                Log.Error("Slot entry for kit '" + kitKey + "', did not contain 'sid' element, unable to generate library.");
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
                                    Log.Error("Rejecting slot entry '" + category + "' for class '" + kitKey + "', element did not contain 'items'.");
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
                                        Log.Info("Rejecting slot item entry '" + category + "' for class '" + kitKey + "'.");
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
                                        Log.Error("Rejecting allowed item entry " + itemID + ". Item not found in library.");
                                }
                            }
                        }
                        if (_displayLoadoutDebug)
                            Log.Info(kit.KitType + " parsed. Allowed: " + kit.KitAllowedPrimary.Count + " primary weapons, " + kit.KitAllowedSecondary.Count + " secondary weapons, " + kit.KitAllowedGadget1.Count + " primary gadgets, " + kit.KitAllowedGadget2.Count + " secondary gadgets, " + kit.KitAllowedGrenades.Count + " grenades, and " + kit.KitAllowedKnife.Count + " knives.");
                    }

                    //Fill allowed items for each vehicle
                    foreach (Hashtable entry in loadoutVehicles)
                    {
                        //Get the kit key
                        if (!entry.ContainsKey("sid"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Vehicle entry did not contain 'sid' element, skipping.");
                            continue;
                        }
                        var vehicleCategory = (String)entry["sid"];

                        //Reject all non-EOR entries
                        if (!vehicleCategory.Contains("WARSAW_ID_EOR"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Vehicle entry was not an EOR entry, skipping.");
                            continue;
                        }

                        WarsawVehicle vehicle;
                        if (!library.Vehicles.TryGetValue(vehicleCategory, out vehicle))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Vehicle category " + vehicleCategory + " not found, skipping.");
                            continue;
                        }

                        //Grab slots------------------------------------------------------------------------------
                        if (!entry.ContainsKey("slots"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Vehicle entry '" + vehicleCategory + "' did not contain 'slots' element, skipping.");
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
                                Log.Error("Slot entry for vehicle '" + vehicleCategory + "', could not parse slot into hashtable, unable to generate library.");
                                return false;
                            }
                            //Grab category------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("sid"))
                            {
                                Log.Error("Slot entry for vehicle '" + vehicleCategory + "', did not contain 'sid' element, unable to generate library.");
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
                                    Log.Error("Rejecting slot entry '" + category + "' for vehicle '" + vehicleCategory + "', element did not contain 'items'.");
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
                                        Log.Info("Rejecting slot item entry '" + category + "' for vehicle '" + vehicleCategory + "'.");
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
                                    if (item.AssignedVehicle == null)
                                    {
                                        item.AssignedVehicle = vehicle;
                                    }
                                    else
                                    {
                                        Log.Warn(unlockID + " already assigned to a vehicle, " + item.AssignedVehicle.CategoryType);
                                    }
                                }
                                else
                                {
                                    if (_displayLoadoutDebug)
                                        Log.Error("Rejecting allowed unlock entry " + unlockID + ". Item not found in library.");
                                }
                            }
                            slotIndex++;
                        }
                        if (_displayLoadoutDebug)
                            Log.Info(vehicle.CategoryType + " parsed. Allowed: " +
                                vehicle.AllowedPrimaries.Count + " primary weapons, " +
                                vehicle.AllowedSecondaries.Count + " secondary weapons, " +
                                vehicle.AllowedCountermeasures.Count + " countermeasures, " +
                                vehicle.AllowedOptics.Count + " optics, " +
                                vehicle.AllowedUpgrades.Count + " upgrades, " +
                                vehicle.AllowedSecondariesGunner.Count + " gunner secondary weapons, " +
                                vehicle.AllowedOpticsGunner.Count + " gunner optics, and " +
                                vehicle.AllowedUpgradesGunner.Count + " gunner upgrades. ");
                    }

                    _warsawLibrary = library;
                    _warsawLibraryLoaded = true;
                    UpdateSettingPage();
                    return true;
                }
                Log.Error("Game not BF4, unable to process WARSAW library.");
                return false;
            }
            catch (Exception e)
            {
                Log.Exception("Error while parsing WARSAW library.", e);
            }
            Log.Debug("Exiting LoadWarsawLibrary", 7);
            return false;
        }

        private Hashtable FetchWarsawLibrary()
        {
            Hashtable library = null;
            try
            {
                String response;
                try
                {
                    response = "https://raw.githubusercontent.com/AdKats/AdKats/master/lib/WarsawCodeBook.json"
                        .GetStringAsync().Result;
                }
                catch (Exception)
                {
                    try
                    {
                        response = "http://api.gamerethos.net/adkats/fetch/warsaw"
                            .GetStringAsync().Result;
                    }
                    catch (Exception e)
                    {
                        Log.Exception("Error while downloading raw WARSAW library.", e);
                        return null;
                    }
                }
                library = (Hashtable)JSON.JsonDecode(response);
            }
            catch (Exception e)
            {
                Log.Exception("Unexpected error while fetching WARSAW library", e);
                return null;
            }
            return library;
        }

        private AdKatsLoadout GetPlayerLoadout(String personaID)
        {
            Log.Debug("Entering GetPlayerLoadout", 7);
            try
            {
                if (_gameVersion == GameVersion.BF4)
                {
                    var loadout = new AdKatsLoadout();
                    Hashtable responseData = FetchPlayerLoadout(personaID);
                    if (responseData == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Loadout fetch failed, unable to parse player loadout.");
                        return null;
                    }
                    if (!responseData.ContainsKey("data"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Loadout fetch did not contain 'data' element, unable to parse player loadout.");
                        return null;
                    }
                    var data = (Hashtable)responseData["data"];
                    if (data == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Data section of loadout failed parse, unable to parse player loadout.");
                        return null;
                    }
                    //Get parsed back persona ID
                    if (!data.ContainsKey("personaId"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Data section of loadout did not contain 'personaId' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.PersonaID = data["personaId"].ToString();
                    //Get persona name
                    if (!data.ContainsKey("personaName"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Data section of loadout did not contain 'personaName' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.Name = data["personaName"].ToString();
                    //Get weapons and their attachements
                    if (!data.ContainsKey("currentLoadout"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Data section of loadout did not contain 'currentLoadout' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutHashtable = (Hashtable)data["currentLoadout"];
                    if (currentLoadoutHashtable == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Current loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    if (!currentLoadoutHashtable.ContainsKey("weapons"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Current loadout section did not contain 'weapons' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutWeapons = (Hashtable)currentLoadoutHashtable["weapons"];
                    if (currentLoadoutWeapons == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Weapon loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    if (!currentLoadoutHashtable.ContainsKey("vehicles"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Current loadout section did not contain 'vehicles' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutVehicles = (ArrayList)currentLoadoutHashtable["vehicles"];
                    if (currentLoadoutVehicles == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Vehicles loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    foreach (DictionaryEntry weaponEntry in currentLoadoutWeapons)
                    {
                        if (weaponEntry.Key.ToString() != "0")
                        {
                            WarsawItem warsawItem;
                            if (_warsawLibrary.Items.TryGetValue(weaponEntry.Key.ToString(), out warsawItem))
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
                                        if (_warsawLibrary.ItemAccessories.TryGetValue(accessoryID, out warsawItemAccessory))
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
                    for (Int32 index = 0; index < currentLoadoutVehicles.Count; index++)
                    {
                        WarsawVehicle libraryVehicle;
                        switch (index)
                        {
                            case 0:
                                //MBT
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEMBT", out libraryVehicle))
                                {
                                    Log.Error("Failed to fetch MBT vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 1:
                                //IFV
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEIFV", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch IFV vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 2:
                                //AA
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEAA", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch AA vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 3:
                                //Boat
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKBOAT", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch Boat vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 4:
                                //Stealth
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESTEALTHJET", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch Stealth vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 5:
                                //Scout
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESCOUTHELI", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch Scout vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 6:
                                //AttkHeli
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKHELI", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch AttkHeli vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 7:
                                //AttkJet
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKJET", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch AttkJet vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            default:
                                continue;
                        }
                        //Duplicate the vehicle
                        var vehicle = new WarsawVehicle()
                        {
                            Category = libraryVehicle.Category,
                            CategoryReadable = libraryVehicle.CategoryReadable,
                            CategoryType = libraryVehicle.CategoryType,
                            CategoryTypeReadable = libraryVehicle.CategoryTypeReadable,
                            LinkedRCONCodes = libraryVehicle.LinkedRCONCodes
                        };
                        //Fetch the vehicle items
                        var vehicleItems = (ArrayList)currentLoadoutVehicles[index];
                        //Assign the primary
                        if (libraryVehicle.SlotIndexPrimary >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexPrimary];
                            if (!libraryVehicle.AllowedPrimaries.TryGetValue(itemID, out vehicle.AssignedPrimary))
                            {
                                var defaultItem = libraryVehicle.AllowedPrimaries.Values.First();
                                if (_displayLoadoutDebug)
                                    Log.Warn("Unable to fetch valid vehicle primary " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
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
                                    Log.Warn("Unable to fetch valid vehicle secondary " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
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
                                    Log.Warn("Unable to fetch valid vehicle countermeasure " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
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
                                    Log.Warn("Unable to fetch valid vehicle optic " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
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
                                    Log.Warn("Unable to fetch valid vehicle upgrade " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
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
                                    Log.Warn("Unable to fetch valid vehicle gunner secondary " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
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
                                    Log.Warn("Unable to fetch valid vehicle gunner optic " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
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
                                    Log.Warn("Unable to fetch valid vehicle gunner upgrade " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedUpgradeGunner = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedUpgradeGunner.WarsawID] = vehicle.AssignedUpgradeGunner;
                        }
                        loadout.LoadoutVehicles[vehicle.Category] = vehicle;
                        foreach (String rconCode in vehicle.LinkedRCONCodes)
                        {
                            loadout.LoadoutRCONVehicles[rconCode] = vehicle;
                        }
                        if (_displayLoadoutDebug)
                            Log.Info(loadout.Name + ": " +
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
                            Log.Error("Current loadout section did not contain 'selectedKit' element, unable to parse player loadout.");
                        return null;
                    }
                    String selectedKit = currentLoadoutHashtable["selectedKit"].ToString();
                    ArrayList currentLoadoutList;
                    switch (selectedKit)
                    {
                        case "0":
                            loadout.SelectedKit = _warsawLibrary.KitAssault;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[0];
                            break;
                        case "1":
                            loadout.SelectedKit = _warsawLibrary.KitEngineer;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[1];
                            break;
                        case "2":
                            loadout.SelectedKit = _warsawLibrary.KitSupport;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[2];
                            break;
                        case "3":
                            loadout.SelectedKit = _warsawLibrary.KitRecon;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[3];
                            break;
                        default:
                            if (_displayLoadoutDebug)
                                Log.Error("Unable to parse selected kit " + selectedKit + ", value is unknown. Unable to parse player loadout.");
                            return null;
                    }
                    if (currentLoadoutList.Count < 6)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Loadout kit item entry did not contain 6 valid entries. Unable to parse player loadout.");
                        return null;
                    }
                    //Pull the specifics
                    String loadoutPrimaryID = currentLoadoutList[0].ToString();
                    const String defaultAssaultPrimary = "3590299697"; //ak-12
                    const String defaultEngineerPrimary = "2021343793"; //mx4
                    const String defaultSupportPrimary = "3179658801"; //u-100-mk5
                    const String defaultReconPrimary = "3458855537"; //cs-lr4

                    String loadoutSidearmID = currentLoadoutList[1].ToString();
                    const String defaultSidearm = "944904529"; //p226

                    String loadoutGadget1ID = currentLoadoutList[2].ToString();
                    const String defaultGadget1 = "1694579111"; //nogadget1

                    String loadoutGadget2ID = currentLoadoutList[3].ToString();
                    const String defaultGadget2 = "3164552276"; //nogadget2

                    String loadoutGrenadeID = currentLoadoutList[4].ToString();
                    const String defaultGrenade = "2670747868"; //m67-frag

                    String loadoutKnifeID = currentLoadoutList[5].ToString();
                    const String defaultKnife = "3214146841"; //bayonett

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
                                Log.Error("Specific kit type not set while assigning primary weapon default. Unable to parse player loadout.");
                            return null;
                    }
                    //Attempt to fetch PRIMARY from library
                    if (!loadout.LoadoutItems.TryGetValue(loadoutPrimaryID, out loadoutPrimary))
                    {
                        if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutPrimary.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm PRIMARY is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedPrimary.ContainsKey(loadoutPrimary.WarsawID))
                    {
                        if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " was not valid for " + loadout.SelectedKit.KitType + " kit. Defaulting to " + loadoutPrimary.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for " + loadout.SelectedKit.KitType + " " + loadout.Name + ". Unable to parse player loadout.");
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
                                Log.Warn("Specific SIDEARM (" + loadoutSidearmID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutSidearm.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid SIDEARM (" + loadoutSidearmID + "->" + defaultSidearm + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
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
                                Log.Warn("Specific SIDEARM (" + loadoutSidearmID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a SIDEARM. Defaulting to " + loadoutSidearm.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid SIDEARM (" + loadoutSidearmID + "->" + defaultSidearm + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitItemSidearm = loadoutSidearm;

                    //GADGET1
                    WarsawItem loadoutGadget1;
                    //Attempt to fetch GADGET1 from library
                    if (!_warsawLibrary.Items.TryGetValue(loadoutGadget1ID, out loadoutGadget1))
                    {
                        if (_warsawLibrary.Items.TryGetValue(defaultGadget1, out loadoutGadget1))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GADGET1 (" + loadoutGadget1ID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGadget1.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GADGET1 (" + loadoutGadget1ID + "->" + defaultGadget1 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm GADGET1 is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedGadget1.ContainsKey(loadoutGadget1.WarsawID))
                    {
                        WarsawItem originalItem = loadoutGadget1;
                        if (_warsawLibrary.Items.TryGetValue(defaultGadget1, out loadoutGadget1))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GADGET1 (" + loadoutGadget1ID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a GADGET. Defaulting to " + loadoutGadget1.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GADGET1 (" + loadoutGadget1ID + "->" + defaultGadget1 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGadget1 = loadoutGadget1;

                    //GADGET2
                    WarsawItem loadoutGadget2;
                    //Attempt to fetch GADGET2 from library
                    if (!_warsawLibrary.Items.TryGetValue(loadoutGadget2ID, out loadoutGadget2))
                    {
                        if (_warsawLibrary.Items.TryGetValue(defaultGadget2, out loadoutGadget2))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GADGET2 (" + loadoutGadget2ID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGadget2.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GADGET2 (" + loadoutGadget2ID + "->" + defaultGadget2 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm GADGET2 is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedGadget2.ContainsKey(loadoutGadget2.WarsawID))
                    {
                        WarsawItem originalItem = loadoutGadget2;
                        if (_warsawLibrary.Items.TryGetValue(defaultGadget2, out loadoutGadget2))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GADGET2 (" + loadoutGadget2ID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a GADGET. Defaulting to " + loadoutGadget2.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GADGET2 (" + loadoutGadget2ID + "->" + defaultGadget2 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGadget2 = loadoutGadget2;

                    //GRENADE
                    WarsawItem loadoutGrenade;
                    //Attempt to fetch GRENADE from library
                    if (!_warsawLibrary.Items.TryGetValue(loadoutGrenadeID, out loadoutGrenade))
                    {
                        if (_warsawLibrary.Items.TryGetValue(defaultGrenade, out loadoutGrenade))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GRENADE (" + loadoutGrenadeID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGrenade.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GRENADE (" + loadoutGrenadeID + "->" + defaultGrenade + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm GRENADE is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedGrenades.ContainsKey(loadoutGrenade.WarsawID))
                    {
                        WarsawItem originalItem = loadoutGrenade;
                        if (_warsawLibrary.Items.TryGetValue(defaultGrenade, out loadoutGrenade))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GRENADE (" + loadoutGrenadeID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a GRENADE. Defaulting to " + loadoutGrenade.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GRENADE (" + loadoutGrenadeID + "->" + defaultGrenade + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGrenade = loadoutGrenade;

                    //KNIFE
                    WarsawItem loadoutKnife;
                    //Attempt to fetch KNIFE from library
                    if (!_warsawLibrary.Items.TryGetValue(loadoutKnifeID, out loadoutKnife))
                    {
                        if (_warsawLibrary.Items.TryGetValue(defaultKnife, out loadoutKnife))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific KNIFE (" + loadoutKnifeID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutKnife.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid KNIFE (" + loadoutKnifeID + "->" + defaultKnife + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm KNIFE is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedKnife.ContainsKey(loadoutKnife.WarsawID))
                    {
                        WarsawItem originalItem = loadoutKnife;
                        if (_warsawLibrary.Items.TryGetValue(defaultKnife, out loadoutKnife))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific KNIFE (" + loadoutKnifeID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a KNIFE. Defaulting to " + loadoutKnife.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid KNIFE (" + loadoutKnifeID + "->" + defaultKnife + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
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
                Log.Error("Game not BF4, unable to process player loadout.");
                return null;
            }
            catch (Exception e)
            {
                Log.Exception("Error while parsing player loadout.", e);
            }
            Log.Debug("Exiting GetPlayerLoadout", 7);
            return null;
        }

        private Hashtable FetchPlayerLoadout(String personaID)
        {
            Hashtable loadout = null;
            try
            {
                try
                {
                    DoBattlelogWait();
                    String response = ("http://battlelog.battlefield.com/bf4/loadout/get/PLAYER/" + personaID + "/1/?cacherand=" + Environment.TickCount)
                        .GetStringAsync().Result;
                    loadout = (Hashtable)JSON.JsonDecode(response);
                }
                catch (Exception e)
                {
                    if (e is HttpRequestException)
                    {
                        Log.Warn("Issue connecting to battlelog.");
                        _lastBattlelogAction = DateTime.UtcNow.AddSeconds(30);
                    }
                    else
                    {
                        Log.Exception("Error while loading player loadout.", e);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Unexpected error while fetching player loadout.", e);
                return null;
            }
            return loadout;
        }
    }
}
