using System;

namespace PRoConEvents
{
    public partial class AdKatsLRT
    {
        public void AdminSayMessage(String message)
        {
            AdminSayMessage(message, true);
        }

        public void AdminSayMessage(String message, Boolean displayProconChat)
        {
            Log.Debug("Entering adminSay", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    Log.Error("message null in adminSay");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Say > " + message);
                }
                String[] lineSplit = message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                foreach (String line in lineSplit)
                {
                    ExecuteCommand("procon.protected.send", "admin.say", line, "all");
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while sending admin say.", e);
            }
            Log.Debug("Exiting adminSay", 7);
        }

        public void PlayerSayMessage(String target, String message)
        {
            PlayerSayMessage(target, message, true, 1);
        }

        public void PlayerSayMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            Log.Debug("Entering playerSayMessage", 7);
            try
            {
                if (String.IsNullOrEmpty(target) || String.IsNullOrEmpty(message))
                {
                    Log.Error("target or message null in playerSayMessage");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Say > " + target + " > " + message);
                }
                for (Int32 count = 0; count < spamCount; count++)
                {
                    String[] lineSplit = message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (String line in lineSplit)
                    {
                        ExecuteCommand("procon.protected.send", "admin.say", line, "player", target);
                    }
                    _threadMasterWaitHandle.WaitOne(50);
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while sending message to player.", e);
            }
            Log.Debug("Exiting playerSayMessage", 7);
        }

        public void AdminYellMessage(String message)
        {
            AdminYellMessage(message, true);
        }

        public void AdminYellMessage(String message, Boolean displayProconChat)
        {
            Log.Debug("Entering adminYell", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    Log.Error("message null in adminYell");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Yell[" + YellDuration + "s] > " + message);
                }
                ExecuteCommand("procon.protected.send", "admin.yell", message.ToUpper(), YellDuration + "", "all");
            }
            catch (Exception e)
            {
                Log.Exception("Error while sending admin yell.", e);
            }
            Log.Debug("Exiting adminYell", 7);
        }

        public void PlayerYellMessage(String target, String message)
        {
            PlayerYellMessage(target, message, true, 1);
        }

        public void PlayerYellMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            Log.Debug("Entering PlayerYellMessage", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    Log.Error("message null in PlayerYellMessage");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Yell[" + YellDuration + "s] > " + target + " > " + message);
                }
                for (Int32 count = 0; count < spamCount; count++)
                {
                    ExecuteCommand("procon.protected.send", "admin.yell", ((_gameVersion == GameVersion.BF4) ? (Environment.NewLine) : ("")) + message.ToUpper(), YellDuration + "", "player", target);
                    _threadMasterWaitHandle.WaitOne(50);
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while sending admin yell.", e);
            }
            Log.Debug("Exiting PlayerYellMessage", 7);
        }

        public void AdminTellMessage(String message)
        {
            AdminTellMessage(message, true);
        }

        public void AdminTellMessage(String message, Boolean displayProconChat)
        {
            if (displayProconChat)
            {
                ProconChatWrite("Tell[" + YellDuration + "s] > " + message);
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
                ProconChatWrite("Tell[" + YellDuration + "s] > " + target + " > " + message);
            }
            PlayerSayMessage(target, message, false, spamCount);
            PlayerYellMessage(target, message, false, spamCount);
        }
    }
}
