using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using PRoCon.Core;
using PRoCon.Core.Players;

namespace PRoConEvents
{
    public partial class AdKatsLRT
    {
        public override void OnServerInfo(CServerInfo serverInfo)
        {
            Log.Debug("Entering OnServerInfo", 7);
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
                                Log.Info("LRT is testing authorized.");
                            }
                        }
                        else
                        {
                            Log.Error("Server info was null");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while processing server info.", e);
            }
            Log.Debug("Exiting OnServerInfo", 7);
        }

        public override void OnPlayerKilled(Kill kill)
        {
            Log.Debug("Entering OnPlayerKilled", 7);
            try
            {
                //If the plugin is not enabled and running just return
                if (!_pluginEnabled || !_threadsReady || !_firstPlayerListComplete)
                {
                    return;
                }
                //Fetch players
                AdKatsSubscribedPlayer killer;
                AdKatsSubscribedPlayer victim;
                if (kill.Killer != null && !String.IsNullOrEmpty(kill.Killer.SoldierName))
                {
                    if (!_playerDictionary.TryGetValue(kill.Killer.SoldierName, out killer))
                    {
                        Log.Error("Unable to fetch killer " + kill.Killer.SoldierName + " on kill.");
                        return;
                    }
                }
                else
                {
                    return;
                }
                if (kill.Victim != null && !String.IsNullOrEmpty(kill.Victim.SoldierName))
                {
                    if (!_playerDictionary.TryGetValue(kill.Victim.SoldierName, out victim))
                    {
                        Log.Error("Unable to fetch victim " + kill.Victim.SoldierName + " on kill.");
                        return;
                    }
                }
                else
                {
                    return;
                }

                WarsawVehicle vehicle;
                //Check for vehicle restrictions
                if (killer.Loadout != null &&
                    killer.Loadout.LoadoutRCONVehicles.TryGetValue(kill.DamageType, out vehicle))
                {
                    Log.Debug(killer.Name + " is using trackable vehicle type " + vehicle.CategoryType + ".", 5);
                    if (!killer.WatchedVehicles.Contains(vehicle.Category))
                    {
                        killer.WatchedVehicles.Add(vehicle.Category);
                        Log.Debug("Loadout check automatically called on " + killer.Name + " for trackable vehicle kill.", 4);
                        QueueForProcessing(new ProcessObject()
                        {
                            ProcessPlayer = killer,
                            ProcessReason = "vehiclekill",
                            ProcessTime = DateTime.UtcNow
                        });
                    }
                }
                else if (_UseBackupAutoadmin &&
                           _serverInfo.InfoObject.GameMode != "GunMaster0" &&
                           _serverInfo.InfoObject.GameMode != "GunMaster1" &&
                           (!_restrictSpecificMapModes || _restrictedMapModes.ContainsKey(_serverInfo.InfoObject.GameMode + "|" + _serverInfo.InfoObject.Map)))
                {
                    String rejectionMessage = null;

                    List<String> matchingKillWarsaw;
                    if (_RCONWarsawMappings.TryGetValue(kill.DamageType, out matchingKillWarsaw))
                    {
                        if (_inverseEnforcementMode)
                        {
                            // Inverse mode: reject if weapon is NOT in the whitelist
                            foreach (String warsawID in matchingKillWarsaw)
                            {
                                if (!_warsawInvalidLoadoutIDMessages.ContainsKey(warsawID))
                                {
                                    rejectionMessage = "Item not whitelisted in your loadout";
                                    break;
                                }
                            }
                        }
                        else
                        {
                            foreach (String warsawID in matchingKillWarsaw)
                            {
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(warsawID))
                                {
                                    rejectionMessage = _warsawInvalidLoadoutIDMessages[warsawID];
                                    break;
                                }
                            }
                        }
                    }

                    if (!String.IsNullOrEmpty(rejectionMessage))
                    {
                        if (_enableAdKatsIntegration)
                        {
                            String action = "player_kill";
                            if (_UseAdKatsPunishments)
                            {
                                action = "player_punish";
                            }
                            else if (killer.Punished)
                            {
                                action = "player_kick";
                            }
                            else
                            {
                                killer.Punished = true;
                            }
                            var requestHashtable = new Hashtable {
                                {"caller_identity", "AdKatsLRT"},
                                {"response_requested", false},
                                {"command_type", action},
                                {"source_name", "LoadoutEnforcer"},
                                {"target_name", killer.Name},
                                {"target_guid", killer.GUID},
                                {"record_message", rejectionMessage}
                            };
                            Log.Info("Sending backup AutoAdmin " + action + " to AdKats for " + killer.GetVerboseName());
                            ExecuteCommand("procon.protected.plugins.call", "AdKats", "IssueCommand", "AdKatsLRT", JSON.JsonEncode(requestHashtable));
                        }
                        else
                        {
                            //Weapon is invalid, perform kill or kick based on previous actions
                            if (killer.Punished)
                            {
                                Log.Info("Kicking " + killer.GetVerboseName() + " for using restricted item. [" + rejectionMessage + "].");
                                AdminSayMessage(killer.GetVerboseName() + " was KICKED by LoadoutEnforcer for " + rejectionMessage + ".");
                                ExecuteCommand("procon.protected.send", "admin.kickPlayer", killer.Name, GenerateKickReason(rejectionMessage, "LoadoutEnforcer"));
                            }
                            else
                            {
                                killer.Punished = true;
                                PlayerTellMessage(killer.Name, rejectionMessage);
                                AdminSayMessage(killer.GetVerboseName() + " was KILLED by LoadoutEnforcer for " + rejectionMessage + ".");
                                ExecuteCommand("procon.protected.send", "admin.killPlayer", killer.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while handling OnPlayerKilled.", e);
            }
            Log.Debug("Exiting OnPlayerKilled", 7);
        }

        public String GenerateKickReason(String reason, String source)
        {
            String sourceNameString = "[" + source + "]";

            //Create the full message
            String fullMessage = reason + " " + sourceNameString;

            //Trim the kick message if necessary
            Int32 cutLength = fullMessage.Length - 80;
            if (cutLength > 0)
            {
                String cutReason = reason.Substring(0, reason.Length - cutLength);
                fullMessage = cutReason + " " + sourceNameString;
            }
            return fullMessage;
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset cpsSubset)
        {
            //Completely ignore this event if integrated with AdKats
            if (_enableAdKatsIntegration || !_pluginEnabled)
            {
                return;
            }
            Log.Debug("Entering OnListPlayers", 7);
            try
            {
                if (cpsSubset.Subset != CPlayerSubset.PlayerSubsetType.All)
                {
                    return;
                }
                lock (_playerDictionary)
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
                        var aPlayer = new AdKatsSubscribedPlayer
                        {
                            ID = 0,
                            GUID = cPlayer.GUID,
                            PBGUID = null,
                            IP = null,
                            Name = cPlayer.SoldierName,
                            PersonaID = null,
                            ClanTag = null,
                            Online = true,
                            AA = false,
                            Ping = cPlayer.Ping,
                            Reputation = 0,
                            InfractionPoints = 0,
                            Role = "guest_default"
                        };
                        switch (cPlayer.Type)
                        {
                            case 0:
                                aPlayer.Type = "Player";
                                break;
                            case 1:
                                aPlayer.Type = "Spectator";
                                break;
                            case 2:
                                aPlayer.Type = "CommanderPC";
                                break;
                            case 3:
                                aPlayer.Type = "CommanderMobile";
                                break;
                            default:
                                Log.Error("Player type " + cPlayer.Type + " is not valid.");
                                break;
                        }
                        aPlayer.IsAdmin = false;
                        aPlayer.Reported = false;
                        aPlayer.Punished = false;
                        aPlayer.LoadoutForced = false;
                        aPlayer.LoadoutIgnored = false;
                        aPlayer.LastPunishment = TimeSpan.FromSeconds(0);
                        aPlayer.LastForgive = TimeSpan.FromSeconds(0);
                        aPlayer.LastAction = TimeSpan.FromSeconds(0);
                        aPlayer.SpawnedOnce = false;
                        aPlayer.ConversationPartner = null;
                        aPlayer.Kills = cPlayer.Kills;
                        aPlayer.Deaths = cPlayer.Deaths;
                        aPlayer.KDR = cPlayer.Kdr;
                        aPlayer.Rank = cPlayer.Rank;
                        aPlayer.Score = cPlayer.Score;
                        aPlayer.Squad = cPlayer.SquadID;
                        aPlayer.Team = cPlayer.TeamID;

                        validPlayers.Add(aPlayer.Name);

                        AdKatsSubscribedPlayer dPlayer;
                        Boolean newPlayer = false;
                        //Are they online?
                        if (!_playerDictionary.TryGetValue(aPlayer.Name, out dPlayer))
                        {
                            //Not online. Are they returning?
                            if (_playerLeftDictionary.TryGetValue(aPlayer.GUID, out dPlayer))
                            {
                                //They are returning, move their player object
                                Log.Debug(aPlayer.Name + " is returning.", 6);
                                dPlayer.Online = true;
                                dPlayer.WatchedVehicles.Clear();
                                _playerDictionary[aPlayer.Name] = dPlayer;
                                _playerLeftDictionary.Remove(aPlayer.GUID);
                            }
                            else
                            {
                                //Not online or returning. New player.
                                Log.Debug(aPlayer.Name + " is newly joining.", 6);
                                newPlayer = true;
                            }
                        }
                        if (newPlayer)
                        {
                            _playerDictionary[aPlayer.Name] = aPlayer;
                            QueuePlayerForBattlelogInfoFetch(aPlayer);
                        }
                        else
                        {
                            dPlayer.Name = aPlayer.Name;
                            dPlayer.IP = aPlayer.IP;
                            dPlayer.AA = aPlayer.AA;
                            dPlayer.Ping = aPlayer.Ping;
                            dPlayer.Type = aPlayer.Type;
                            dPlayer.SpawnedOnce = aPlayer.SpawnedOnce;
                            dPlayer.Kills = aPlayer.Kills;
                            dPlayer.Deaths = aPlayer.Deaths;
                            dPlayer.KDR = aPlayer.KDR;
                            dPlayer.Rank = aPlayer.Rank;
                            dPlayer.Score = aPlayer.Score;
                            dPlayer.Squad = aPlayer.Squad;
                            dPlayer.Team = aPlayer.Team;
                        }
                    }
                    List<String> removeNames = _playerLeftDictionary.Where(pair => (DateTime.UtcNow - pair.Value.LastUsage).TotalMinutes > 120).Select(pair => pair.Key).ToList();
                    foreach (String removeName in removeNames)
                    {
                        _playerLeftDictionary.Remove(removeName);
                    }
                    if (_isTestingAuthorized && removeNames.Any())
                    {
                        Log.Warn(removeNames.Count() + " left players removed, " + _playerLeftDictionary.Count() + " still in cache.");
                    }
                    foreach (String playerName in _playerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList())
                    {
                        AdKatsSubscribedPlayer aPlayer;
                        if (_playerDictionary.TryGetValue(playerName, out aPlayer))
                        {
                            Log.Debug(aPlayer.Name + " removed from player list.", 6);
                            _playerDictionary.Remove(aPlayer.Name);
                            aPlayer.LastUsage = DateTime.UtcNow;
                            _playerLeftDictionary[aPlayer.GUID] = aPlayer;
                            aPlayer.LoadoutChecks = 0;
                        }
                        else
                        {
                            Log.Error("Unable to find " + playerName + " in online players when requesting removal.");
                        }
                    }
                }
                _firstPlayerListComplete = true;
                _playerProcessingWaitHandle.Set();
            }
            catch (Exception e)
            {
                Log.Exception("Error occured while listing players.", e);
            }
            Log.Debug("Exiting OnListPlayers", 7);
        }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory)
        {
            try
            {
                DateTime spawnTime = DateTime.UtcNow;
                if (_threadsReady && _pluginEnabled && _firstPlayerListComplete)
                {
                    AdKatsSubscribedPlayer aPlayer;
                    if (_playerDictionary.TryGetValue(soldierName, out aPlayer))
                    {
                        aPlayer.SpawnedOnce = true;
                        //Reject spawn processing if player has no persona ID
                        if (String.IsNullOrEmpty(aPlayer.PersonaID))
                        {
                            if (!_enableAdKatsIntegration)
                            {
                                QueuePlayerForBattlelogInfoFetch(aPlayer);
                            }
                            Log.Debug("Spawn process for " + aPlayer.Name + " cancelled because their Persona ID is not loaded yet. Scheduling retry.", 3);
                            //Schedule a delayed retry to check loadout once persona data arrives
                            String playerName = aPlayer.Name;
                            ThreadPool.QueueUserWorkItem(delegate
                            {
                                try
                                {
                                    Thread.Sleep(3000);
                                    if (!_pluginEnabled || !_threadsReady)
                                    {
                                        return;
                                    }
                                    AdKatsSubscribedPlayer retryPlayer;
                                    if (_playerDictionary.TryGetValue(playerName, out retryPlayer) && !String.IsNullOrEmpty(retryPlayer.PersonaID))
                                    {
                                        Log.Debug("Persona ID now available for " + retryPlayer.Name + ", queuing delayed loadout check.", 3);
                                        QueueForProcessing(new ProcessObject()
                                        {
                                            ProcessPlayer = retryPlayer,
                                            ProcessReason = "spawn",
                                            ProcessTime = spawnTime
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Exception("Error in persona ID retry for " + playerName + ".", ex);
                                }
                            });
                            return;
                        }
                        //Create process object
                        var processObject = new ProcessObject()
                        {
                            ProcessPlayer = aPlayer,
                            ProcessReason = "spawn",
                            ProcessTime = spawnTime
                        };
                        //Minimum wait time of 5 seconds
                        if (_loadoutProcessingQueue.Count >= 6)
                        {
                            QueueForProcessing(processObject);
                        }
                        else
                        {
                            var waitTime = TimeSpan.FromSeconds(5 - _loadoutProcessingQueue.Count);
                            if (waitTime.TotalSeconds <= 0.1)
                            {
                                waitTime = TimeSpan.FromSeconds(5);
                            }
                            Log.Debug("Waiting " + ((Int32)waitTime.TotalSeconds) + " seconds to process " + aPlayer.GetVerboseName() + " spawn.", 5);
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
                                    Log.Exception("Error running loadout check delay thread.", e);
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
                Log.Exception("Error while handling player spawn.", e);
            }
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            Log.Debug("Entering OnPlayerLeft", 7);
            try
            {
                AdKatsSubscribedPlayer aPlayer;
                if (_playerDictionary.TryGetValue(playerInfo.SoldierName, out aPlayer))
                {
                    aPlayer.Online = false;
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while handling player left.", e);
            }
            Log.Debug("Exiting OnPlayerLeft", 7);
        }

        public void CallLoadoutCheckOnPlayer(params String[] parameters)
        {
            Log.Debug("CallLoadoutCheckOnPlayer starting!", 6);
            try
            {
                if (parameters.Length != 2)
                {
                    Log.Error("Call loadout check canceled. Parameters invalid.");
                    return;
                }
                String unparsedCommandJson = parameters[1];

                var decodedCommand = (Hashtable)JSON.JsonDecode(unparsedCommandJson);

                var playerName = (String)decodedCommand["player_name"];
                var loadoutCheckReason = (String)decodedCommand["loadoutCheck_reason"];

                if (_threadsReady && _pluginEnabled && _firstPlayerListComplete)
                {
                    AdKatsSubscribedPlayer aPlayer;
                    if (_playerDictionary.TryGetValue(playerName, out aPlayer))
                    {
                        Log.Write("Loadout check manually called on " + playerName + ".");
                        QueueForProcessing(new ProcessObject()
                        {
                            ProcessPlayer = aPlayer,
                            ProcessReason = loadoutCheckReason,
                            ProcessTime = DateTime.UtcNow,
                            ProcessManual = true
                        });
                    }
                    else
                    {
                        Log.Error("Attempted to call MANUAL loadout check on " + playerName + " without their player object loaded.");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while calling loadout check on player.", e);
            }
            Log.Debug("CallLoadoutCheckOnPlayer finished!", 6);
        }

        public void ReceiveAdminList(params String[] parameters)
        {
            Log.Debug("ReceiveAdminList starting!", 6);
            try
            {
                if (parameters.Length != 2)
                {
                    Log.Error("Online admin receiving cancelled. Parameters invalid.");
                    return;
                }
                String unparsedCommandJson = parameters[1];

                var decodedCommand = (Hashtable)JSON.JsonDecode(unparsedCommandJson);

                var unparsedAdminList = (String)decodedCommand["response_value"];

                String[] tempAdminList = CPluginVariable.DecodeStringArray(unparsedAdminList);
                foreach (String adminPlayerName in tempAdminList)
                {
                    if (!_adminList.Contains(adminPlayerName))
                    {
                        _adminList.Add(adminPlayerName);
                    }
                }
                _adminList.RemoveWhere(name => !tempAdminList.Contains(name));
            }
            catch (Exception e)
            {
                Log.Exception("Error while calling loadout check on player.", e);
            }
            Log.Debug("ReceiveAdminList finished!", 6);
        }

        public void ReceiveOnlineSoldiers(params String[] parameters)
        {
            Log.Debug("ReceiveOnlineSoldiers starting!", 6);
            try
            {
                if (!_enableAdKatsIntegration)
                {
                    return;
                }
                if (parameters.Length != 2)
                {
                    Log.Error("Online soldier handling canceled. Parameters invalid.");
                    return;
                }
                String unparsedResponseJSON = parameters[1];

                var decodedResponse = (Hashtable)JSON.JsonDecode(unparsedResponseJSON);

                var decodedSoldierList = (ArrayList)decodedResponse["response_value"];
                if (decodedSoldierList == null)
                {
                    Log.Error("Soldier params could not be properly converted from JSON. Unable to continue.");
                    return;
                }
                lock (_playerDictionary)
                {
                    var validPlayers = new List<String>();
                    foreach (Hashtable soldierHashtable in decodedSoldierList)
                    {
                        var aPlayer = new AdKatsSubscribedPlayer
                        {
                            ID = Convert.ToInt64((Double)soldierHashtable["player_id"]),
                            GUID = (String)soldierHashtable["player_guid"],
                            PBGUID = (String)soldierHashtable["player_pbguid"],
                            IP = (String)soldierHashtable["player_ip"],
                            Name = (String)soldierHashtable["player_name"],
                            PersonaID = (String)soldierHashtable["player_personaID"],
                            ClanTag = (String)soldierHashtable["player_clanTag"],
                            Online = (Boolean)soldierHashtable["player_online"],
                            AA = (Boolean)soldierHashtable["player_aa"],
                            Ping = (Double)soldierHashtable["player_ping"],
                            Reputation = (Double)soldierHashtable["player_reputation"],
                            InfractionPoints = Convert.ToInt32((Double)soldierHashtable["player_infractionPoints"]),
                            Role = (String)soldierHashtable["player_role"],
                            Type = (String)soldierHashtable["player_type"],
                            IsAdmin = (Boolean)soldierHashtable["player_isAdmin"],
                            Reported = (Boolean)soldierHashtable["player_reported"],
                            Punished = (Boolean)soldierHashtable["player_punished"],
                            LoadoutForced = (Boolean)soldierHashtable["player_loadout_forced"],
                            LoadoutIgnored = (Boolean)soldierHashtable["player_loadout_ignored"]
                        };
                        var lastPunishment = (Double)soldierHashtable["player_lastPunishment"];
                        if (lastPunishment > 0)
                        {
                            aPlayer.LastPunishment = TimeSpan.FromSeconds(lastPunishment);
                        }
                        var lastForgive = (Double)soldierHashtable["player_lastForgive"];
                        if (lastPunishment > 0)
                        {
                            aPlayer.LastForgive = TimeSpan.FromSeconds(lastForgive);
                        }
                        var lastAction = (Double)soldierHashtable["player_lastAction"];
                        if (lastPunishment > 0)
                        {
                            aPlayer.LastAction = TimeSpan.FromSeconds(lastAction);
                        }
                        aPlayer.SpawnedOnce = (Boolean)soldierHashtable["player_spawnedOnce"];
                        aPlayer.ConversationPartner = (String)soldierHashtable["player_conversationPartner"];
                        aPlayer.Kills = Convert.ToInt32((Double)soldierHashtable["player_kills"]);
                        aPlayer.Deaths = Convert.ToInt32((Double)soldierHashtable["player_deaths"]);
                        aPlayer.KDR = (Double)soldierHashtable["player_kdr"];
                        aPlayer.Rank = Convert.ToInt32((Double)soldierHashtable["player_rank"]);
                        aPlayer.Score = Convert.ToInt32((Double)soldierHashtable["player_score"]);
                        aPlayer.Squad = Convert.ToInt32((Double)soldierHashtable["player_squad"]);
                        aPlayer.Team = Convert.ToInt32((Double)soldierHashtable["player_team"]);

                        validPlayers.Add(aPlayer.Name);

                        Boolean process = false;
                        AdKatsSubscribedPlayer dPlayer;
                        Boolean newPlayer = false;
                        //Are they online?
                        if (!_playerDictionary.TryGetValue(aPlayer.Name, out dPlayer))
                        {
                            //Not online. Are they returning?
                            if (_playerLeftDictionary.TryGetValue(aPlayer.GUID, out dPlayer))
                            {
                                //They are returning, move their player object
                                Log.Debug(aPlayer.Name + " is returning.", 6);
                                dPlayer.Online = true;
                                _playerDictionary[aPlayer.Name] = dPlayer;
                                _playerLeftDictionary.Remove(dPlayer.GUID);
                            }
                            else
                            {
                                //Not online or returning. New player.
                                Log.Debug(aPlayer.Name + " is newly joining.", 6);
                                newPlayer = true;
                            }
                        }
                        if (newPlayer)
                        {
                            _playerDictionary[aPlayer.Name] = aPlayer;
                            dPlayer = aPlayer;
                            process = true;
                        }
                        else
                        {
                            dPlayer.Name = aPlayer.Name;
                            dPlayer.IP = aPlayer.IP;
                            dPlayer.AA = aPlayer.AA;
                            if (String.IsNullOrEmpty(dPlayer.PersonaID) && !String.IsNullOrEmpty(aPlayer.PersonaID))
                            {
                                process = true;
                            }
                            dPlayer.PersonaID = aPlayer.PersonaID;
                            dPlayer.ClanTag = aPlayer.ClanTag;
                            dPlayer.Online = aPlayer.Online;
                            dPlayer.Ping = aPlayer.Ping;
                            dPlayer.Reputation = aPlayer.Reputation;
                            dPlayer.InfractionPoints = aPlayer.InfractionPoints;
                            dPlayer.Role = aPlayer.Role;
                            dPlayer.Type = aPlayer.Type;
                            dPlayer.IsAdmin = aPlayer.IsAdmin;
                            dPlayer.Reported = aPlayer.Reported;
                            dPlayer.Punished = aPlayer.Punished;
                            dPlayer.LoadoutForced = aPlayer.LoadoutForced;
                            dPlayer.LoadoutIgnored = aPlayer.LoadoutIgnored;
                            dPlayer.SpawnedOnce = aPlayer.SpawnedOnce;
                            dPlayer.ConversationPartner = aPlayer.ConversationPartner;
                            dPlayer.Kills = aPlayer.Kills;
                            dPlayer.Deaths = aPlayer.Deaths;
                            dPlayer.KDR = aPlayer.KDR;
                            dPlayer.Rank = aPlayer.Rank;
                            dPlayer.Score = aPlayer.Score;
                            dPlayer.Squad = aPlayer.Squad;
                            dPlayer.Team = aPlayer.Team;
                        }
                        dPlayer.LastUsage = DateTime.UtcNow;
                        if (process)
                        {
                            QueueForProcessing(new ProcessObject()
                            {
                                ProcessPlayer = dPlayer,
                                ProcessReason = "listing",
                                ProcessTime = DateTime.UtcNow
                            });
                        }
                        Log.Debug(aPlayer.Name + " online after listing: " + _playerDictionary.ContainsKey(aPlayer.Name), 7);
                    }
                    foreach (String playerName in _playerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList())
                    {
                        AdKatsSubscribedPlayer aPlayer;
                        if (_playerDictionary.TryGetValue(playerName, out aPlayer))
                        {
                            Log.Debug(aPlayer.Name + " removed from player list.", 6);
                            aPlayer.LastUsage = DateTime.UtcNow;
                            _playerDictionary.Remove(aPlayer.Name);
                            _playerLeftDictionary[aPlayer.GUID] = aPlayer;
                            aPlayer.LoadoutChecks = 0;
                        }
                        else
                        {
                            Log.Error("Unable to find " + playerName + " in online players when requesting removal.");
                        }
                    }
                    if (_displayWeaponPopularity && (DateTime.UtcNow - _lastCategoryListing).TotalMinutes > _weaponPopularityDisplayMinutes)
                    {
                        var loadoutPlayers = _playerDictionary.Values.Where(aPlayer => aPlayer.Loadout != null);
                        if (loadoutPlayers.Any())
                        {
                            var loadoutPlayers1 = loadoutPlayers.Where(aPlayer => aPlayer.Team == 1);
                            var loadoutPlayers2 = loadoutPlayers.Where(aPlayer => aPlayer.Team == 2);

                            var highestCategory1 = loadoutPlayers1
                                .GroupBy(aPlayer => aPlayer.Loadout.KitItemPrimary.CategoryReadable)
                                .Select(listing => new
                                {
                                    weaponCategory = listing.Key,
                                    Count = listing.Count()
                                })
                                .OrderByDescending(listing => listing.Count)
                                .FirstOrDefault();

                            var highestCategory2 = loadoutPlayers2
                                .GroupBy(aPlayer => aPlayer.Loadout.KitItemPrimary.CategoryReadable)
                                .Select(listing => new
                                {
                                    weaponCategory = listing.Key,
                                    Count = listing.Count()
                                })
                                .OrderByDescending(listing => listing.Count)
                                .FirstOrDefault();

                            var weaponCounts = loadoutPlayers
                                .GroupBy(aPlayer => aPlayer.Loadout.KitItemPrimary.Slug)
                                .Select(listing => new
                                {
                                    weaponSlug = listing.Key,
                                    Count = listing.Count()
                                });
                            var highestCount = weaponCounts.Max(listing => listing.Count);
                            var highestWeapons = weaponCounts.Where(listing => listing.Count >= highestCount);
                            var highestWeapon = highestWeapons.ElementAt(new Random(Environment.TickCount).Next(highestWeapons.Count()));

                            _lastCategoryListing = DateTime.UtcNow;
                            if (highestWeapon != null && highestCategory1 != null && highestCategory2 != null)
                            {
                                String message = "US " + highestCategory1.weaponCategory.ToLower() + " " + Math.Round((Double)highestCategory1.Count / (Double)loadoutPlayers1.Count() * 100.0) + "% / RU " + highestCategory2.weaponCategory.ToLower() + " " + Math.Round((Double)highestCategory2.Count / (Double)loadoutPlayers2.Count() * 100.0) + "% / Top Weap: " + highestWeapon.weaponSlug + ", " + highestWeapon.Count + " players";
                                AdminSayMessage(message);
                                Log.Info(message);
                            }
                        }
                    }
                }
                _firstPlayerListComplete = true;
                _playerProcessingWaitHandle.Set();
            }
            catch (Exception e)
            {
                Log.Exception("Error while receiving online soldiers.", e);
            }
            Log.Debug("ReceiveOnlineSoldiers finished!", 6);
        }

        public Int32 CountTeamWeaponCategory(Int32 teamId, String category, String excludePlayerName)
        {
            Int32 count = 0;
            lock (_playerDictionary)
            {
                foreach (AdKatsSubscribedPlayer player in _playerDictionary.Values)
                {
                    if (player.Team == teamId &&
                        player.Online &&
                        player.SpawnedOnce &&
                        player.Loadout != null &&
                        player.Name != excludePlayerName &&
                        String.Equals(player.Loadout.KitItemPrimary.CategoryReadable, category, StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
