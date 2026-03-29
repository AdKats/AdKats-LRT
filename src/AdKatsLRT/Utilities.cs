using System;
using System.Text.RegularExpressions;

namespace PRoConEvents
{
    public partial class AdKatsLRT
    {
        public String ExtractString(String s, String tag)
        {
            if (String.IsNullOrEmpty(s) || String.IsNullOrEmpty(tag))
            {
                Log.Error("Unable to extract String. Invalid inputs.");
                return null;
            }
            String startTag = "<" + tag + ">";
            Int32 startIndex = s.IndexOf(startTag, StringComparison.Ordinal) + startTag.Length;
            if (startIndex == -1)
            {
                Log.Error("Unable to extract String. Tag not found.");
            }
            Int32 endIndex = s.IndexOf("</" + tag + ">", startIndex, StringComparison.Ordinal);
            return s.Substring(startIndex, endIndex - startIndex);
        }

        public Boolean SoldierNameValid(String input)
        {
            try
            {
                Log.Debug("Checking player '" + input + "' for validity.", 7);
                if (String.IsNullOrEmpty(input))
                {
                    Log.Debug("Soldier Name empty or null.", 5);
                    return false;
                }
                if (input.Length > 16)
                {
                    Log.Debug("Soldier Name '" + input + "' too long, maximum length is 16 characters.", 5);
                    return false;
                }
                if (new Regex("[^a-zA-Z0-9_-]").Replace(input, "").Length != input.Length)
                {
                    Log.Debug("Soldier Name '" + input + "' contained invalid characters.", 5);
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                //Soldier id caused exception in the regex, definitely not valid
                Log.Error("Soldier Name '" + input + "' contained invalid characters.");
                return false;
            }
        }

        public String FormatTimeString(TimeSpan timeSpan, Int32 maxComponents)
        {
            Log.Debug("Entering formatTimeString", 7);
            String timeString = null;
            if (maxComponents < 1)
            {
                return null;
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
                Log.Exception("Error while formatting time String.", e);
            }
            if (String.IsNullOrEmpty(timeString))
            {
                timeString = "0s";
            }
            Log.Debug("Exiting formatTimeString", 7);
            return timeString;
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

    }
}
