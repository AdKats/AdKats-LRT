using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

using Flurl;
using Flurl.Http;

namespace PRoConEvents
{
    public partial class AdKatsLRT
    {
        private readonly Dictionary<String, HashSet<String>> _playerUnlockedItems = new Dictionary<String, HashSet<String>>();
        private Boolean _checkUnlocksBeforeEnforcing = true;
        private void QueuePlayerForBattlelogInfoFetch(AdKatsSubscribedPlayer aPlayer)
        {
            Log.Debug("Entering QueuePlayerForBattlelogInfoFetch", 6);
            try
            {
                Log.Debug("Preparing to queue player for battlelog info fetch.", 6);
                if (_battlelogFetchQueue.Any(bPlayer => bPlayer.GUID == aPlayer.GUID))
                {
                    return;
                }
                lock (_battlelogFetchQueue)
                {
                    _battlelogFetchQueue.Enqueue(aPlayer);
                    Log.Debug("Player queued for battlelog info fetch.", 6);
                    _battlelogCommWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while queuing player for battlelog info fetch.", e);
            }
            Log.Debug("Exiting QueuePlayerForBattlelogInfoFetch", 6);
        }

        public void BattlelogCommThreadLoop()
        {
            try
            {
                Log.Debug("BTLOG: Starting Battlelog Comm Thread", 1);
                Thread.CurrentThread.Name = "BattlelogComm";
                while (true)
                {
                    try
                    {
                        Log.Debug("BTLOG: Entering Battlelog Comm Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug("BTLOG: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        //Sleep for 10ms
                        _threadMasterWaitHandle.WaitOne(10);

                        //Handle Inbound player fetches
                        if (_battlelogFetchQueue.Count > 0)
                        {
                            Queue<AdKatsSubscribedPlayer> unprocessedPlayers;
                            lock (_battlelogFetchQueue)
                            {
                                Log.Debug("BTLOG: Inbound players found. Grabbing.", 6);
                                //Grab all items in the queue
                                unprocessedPlayers = new Queue<AdKatsSubscribedPlayer>(_battlelogFetchQueue.ToArray());
                                //Clear the queue for next run
                                _battlelogFetchQueue.Clear();
                            }
                            //Loop through all players in order that they came in
                            while (unprocessedPlayers.Count > 0)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                Log.Debug("BTLOG: Preparing to fetch battlelog info for player", 6);
                                //Dequeue the record
                                AdKatsSubscribedPlayer aPlayer = unprocessedPlayers.Dequeue();
                                //Run the appropriate action
                                FetchPlayerBattlelogInformation(aPlayer);
                            }
                        }
                        else
                        {
                            //Wait for new actions
                            _battlelogCommWaitHandle.Reset();
                            _battlelogCommWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.Exception("Battlelog comm thread aborted. Exiting.", e);
                            break;
                        }
                        Log.Exception("Error occured in Battlelog comm thread. Skipping current loop.", e);
                    }
                }
                Log.Debug("BTLOG: Ending Battlelog Comm Thread", 1);
                LogThreadExit();
            }
            catch (Exception e)
            {
                Log.Exception("Error occured in battlelog comm thread.", e);
            }
        }

        public void FetchPlayerBattlelogInformation(AdKatsSubscribedPlayer aPlayer)
        {
            try
            {
                if (!String.IsNullOrEmpty(aPlayer.PersonaID))
                {
                    return;
                }
                if (String.IsNullOrEmpty(aPlayer.Name))
                {
                    Log.Error("Attempted to get battlelog information of nameless player.");
                    return;
                }
                try
                {
                    DoBattlelogWait();
                    var httpClient = new HttpClient();
                    String personaResponse = httpClient.GetStringAsync("http://battlelog.battlefield.com/bf4/user/" + aPlayer.Name).Result;
                    Match pid = Regex.Match(personaResponse, @"bf4/soldier/" + aPlayer.Name + @"/stats/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (!pid.Success)
                    {
                        Log.Error("Could not find persona ID for " + aPlayer.Name);
                        return;
                    }
                    aPlayer.PersonaID = pid.Groups[1].Value.Trim();
                    Log.Debug("Persona ID fetched for " + aPlayer.Name, 4);
                    QueueForProcessing(new ProcessObject()
                    {
                        ProcessPlayer = aPlayer,
                        ProcessReason = "listing",
                        ProcessTime = DateTime.UtcNow
                    });
                    DoBattlelogWait();
                    String overviewResponse = ("http://battlelog.battlefield.com/bf4/warsawoverviewpopulate/" + aPlayer.PersonaID + "/1/")
                        .GetStringAsync().Result;

                    Hashtable json = (Hashtable)JSON.JsonDecode(overviewResponse);
                    Hashtable data = (Hashtable)json["data"];
                    Hashtable info = null;
                    if (!data.ContainsKey("viewedPersonaInfo") || (info = (Hashtable)data["viewedPersonaInfo"]) == null)
                    {
                        aPlayer.ClanTag = String.Empty;
                        Log.Debug("Could not find BF4 clan tag for " + aPlayer.Name, 4);
                    }
                    else
                    {
                        String tag = String.Empty;
                        if (!info.ContainsKey("tag") || String.IsNullOrEmpty(tag = (String)info["tag"]))
                        {
                            aPlayer.ClanTag = String.Empty;
                            Log.Debug("Could not find BF4 clan tag for " + aPlayer.Name, 4);
                        }
                        else
                        {
                            aPlayer.ClanTag = tag;
                            Log.Debug("Clan tag [" + aPlayer.ClanTag + "] found for " + aPlayer.Name, 4);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is HttpRequestException)
                    {
                        Log.Warn("Issue connecting to battlelog.");
                        _lastBattlelogAction = DateTime.UtcNow.AddSeconds(30);
                    }
                }
                //Fetch unlock data for the player
                if (_checkUnlocksBeforeEnforcing && !String.IsNullOrEmpty(aPlayer.PersonaID))
                {
                    FetchPlayerUnlocks(aPlayer);
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while fetching battlelog information for " + aPlayer.Name, e);
            }
        }

        private void FetchPlayerUnlocks(AdKatsSubscribedPlayer aPlayer)
        {
            try
            {
                DoBattlelogWait();
                String detailedResponse = ("http://battlelog.battlefield.com/bf4/warsawdetailedstatspopulate/" + aPlayer.PersonaID + "/1/")
                    .GetStringAsync().Result;

                Hashtable detailedJson = (Hashtable)JSON.JsonDecode(detailedResponse);
                if (detailedJson == null || !detailedJson.ContainsKey("data"))
                {
                    Log.Debug("Could not parse detailed stats for " + aPlayer.Name + ". Unlock check will fall back to enforcing.", 3);
                    return;
                }
                var detailedData = (Hashtable)detailedJson["data"];
                if (detailedData == null)
                {
                    Log.Debug("Detailed stats data was null for " + aPlayer.Name + ". Unlock check will fall back to enforcing.", 3);
                    return;
                }

                var unlockedIDs = new HashSet<String>();

                //Parse weapon unlocks from statsTemplate
                if (detailedData.ContainsKey("weaponUnlocks"))
                {
                    var weaponUnlocks = detailedData["weaponUnlocks"] as ArrayList;
                    if (weaponUnlocks != null)
                    {
                        foreach (var entry in weaponUnlocks)
                        {
                            var unlockEntry = entry as Hashtable;
                            if (unlockEntry == null)
                            {
                                continue;
                            }
                            //Each entry has a weaponAddonUnlocks array with individual unlock items
                            var addonUnlocks = unlockEntry["weaponAddonUnlocks"] as ArrayList;
                            if (addonUnlocks != null)
                            {
                                foreach (var addon in addonUnlocks)
                                {
                                    var addonHash = addon as Hashtable;
                                    if (addonHash == null)
                                    {
                                        continue;
                                    }
                                    var unlockedBy = addonHash["unlockedBy"] as Hashtable;
                                    if (unlockedBy != null && unlockedBy.ContainsKey("completion"))
                                    {
                                        var completion = unlockedBy["completion"].ToString();
                                        if (completion == "1")
                                        {
                                            if (addonHash.ContainsKey("weaponAddonGuid"))
                                            {
                                                unlockedIDs.Add(addonHash["weaponAddonGuid"].ToString());
                                            }
                                        }
                                    }
                                }
                            }
                            //The weapon itself — check if it has a guid and is unlocked
                            if (unlockEntry.ContainsKey("guid"))
                            {
                                unlockedIDs.Add(unlockEntry["guid"].ToString());
                            }
                        }
                    }
                }

                //Parse kit item unlocks
                if (detailedData.ContainsKey("kitItemUnlocks"))
                {
                    var kitItemUnlocks = detailedData["kitItemUnlocks"] as ArrayList;
                    if (kitItemUnlocks != null)
                    {
                        foreach (var entry in kitItemUnlocks)
                        {
                            var unlockEntry = entry as Hashtable;
                            if (unlockEntry == null)
                            {
                                continue;
                            }
                            var unlockedBy = unlockEntry["unlockedBy"] as Hashtable;
                            if (unlockedBy != null && unlockedBy.ContainsKey("completion"))
                            {
                                var completion = unlockedBy["completion"].ToString();
                                if (completion == "1" && unlockEntry.ContainsKey("guid"))
                                {
                                    unlockedIDs.Add(unlockEntry["guid"].ToString());
                                }
                            }
                        }
                    }
                }

                //Parse vehicle unlocks
                if (detailedData.ContainsKey("vehicleUnlocks"))
                {
                    var vehicleUnlocks = detailedData["vehicleUnlocks"] as ArrayList;
                    if (vehicleUnlocks != null)
                    {
                        foreach (var entry in vehicleUnlocks)
                        {
                            var unlockEntry = entry as Hashtable;
                            if (unlockEntry == null)
                            {
                                continue;
                            }
                            var addonUnlocks = unlockEntry["vehicleAddonUnlocks"] as ArrayList;
                            if (addonUnlocks != null)
                            {
                                foreach (var addon in addonUnlocks)
                                {
                                    var addonHash = addon as Hashtable;
                                    if (addonHash == null)
                                    {
                                        continue;
                                    }
                                    var unlockedBy = addonHash["unlockedBy"] as Hashtable;
                                    if (unlockedBy != null && unlockedBy.ContainsKey("completion"))
                                    {
                                        var completion = unlockedBy["completion"].ToString();
                                        if (completion == "1" && addonHash.ContainsKey("vehicleAddonGuid"))
                                        {
                                            unlockedIDs.Add(addonHash["vehicleAddonGuid"].ToString());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                lock (_playerUnlockedItems)
                {
                    _playerUnlockedItems[aPlayer.PersonaID] = unlockedIDs;
                }
                Log.Debug("Fetched " + unlockedIDs.Count + " unlocked items for " + aPlayer.Name, 3);
            }
            catch (Exception e)
            {
                if (e is HttpRequestException)
                {
                    Log.Warn("Issue connecting to battlelog while fetching unlocks for " + aPlayer.Name + ".");
                    _lastBattlelogAction = DateTime.UtcNow.AddSeconds(30);
                }
                else
                {
                    Log.Debug("Error fetching unlocks for " + aPlayer.Name + ". Unlock check will fall back to enforcing. " + e.Message, 2);
                }
            }
        }

        private void DoBattlelogWait()
        {
            try
            {

                lock (_battlelogLocker)
                {
                    var now = DateTime.UtcNow;
                    var timeSinceLast = (now - _lastBattlelogAction);
                    var requiredWait = _battlelogWaitDuration;
                    //Reduce required wait time based on how many players are in the queue
                    if (_highRequestVolume)
                    {
                        requiredWait -= TimeSpan.FromSeconds(2);
                    }
                    //Wait between battlelog actions
                    if ((now - _lastBattlelogAction) < requiredWait)
                    {
                        var remainingWait = requiredWait - timeSinceLast;
                        Thread.Sleep(remainingWait);
                    }
                    //Log the request frequency
                    now = DateTime.UtcNow;
                    lock (_BattlelogActionTimes)
                    {
                        _BattlelogActionTimes.Enqueue(now);
                        while (NowDuration(_BattlelogActionTimes.Peek()).TotalMinutes > 4)
                        {
                            _BattlelogActionTimes.Dequeue();
                        }
                        if (_BattlelogActionTimes.Any() && NowDuration(_lastBattlelogFrequencyMessage).TotalSeconds > 30)
                        {
                            if (_isTestingAuthorized)
                            {
                                var frequency = Math.Round(_BattlelogActionTimes.Count() / 4.0, 2);
                                Log.Info("Average battlelog request frequency: " + frequency + " r/m");
                            }
                            _lastBattlelogFrequencyMessage = DateTime.UtcNow;
                        }
                    }
                    _lastBattlelogAction = now;
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while performing battlelog wait.", e);
                Thread.Sleep(_battlelogWaitDuration);
            }
        }

        public TimeSpan NowDuration(DateTime diff)
        {
            return (DateTime.UtcNow - diff).Duration();
        }
    }
}
