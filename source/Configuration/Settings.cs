using Sitecore.Configuration;
using System;
using System.Collections.Generic;
using SCConfiguration = Sitecore.Configuration;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Configuration
{
    public static class Settings
    {
        public static bool UseUpdateMongoDriverSettingsPipeline
        {
            get
            {
                return SCConfiguration.Settings.GetBoolSetting("ADM.UseUpdateMongoDriverSettingsPipeline", false);
            }
        }

        public static bool CreateMongoIndexes
        {
            get
            {
                return SCConfiguration.Settings.GetBoolSetting("ADM.CreateMongoIndexes", false);
            }
        }

        public static List<string> RobotUserAgentNameParts
        {
            get
            {
                var result = new List<string>();

                var node = Factory.GetConfigNode("analyticsDatabaseManager/robotNameParts");
                if (node != null)
                {
                    var setting = node.InnerText;
                    if (!string.IsNullOrEmpty(setting))
                    {
                        var arr = setting.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var s in arr)
                        {
                            //Lowercase, since this will be used in a case-INsensitive regex-match
                            var namePart = s.Trim().ToLower();
                            if (!string.IsNullOrEmpty(namePart))
                            {
                                result.Add(namePart);
                            }
                        }
                    }
                }

                return result;
            }
        }

        public static List<string> RobotUserAgentNames
        {
            get
            {
                var result = new List<string>();

                var node = Factory.GetConfigNode("analyticsDatabaseManager/robotUserAgents");
                if (node != null)
                {
                    var setting = node.InnerText;
                    if (!string.IsNullOrEmpty(setting))
                    {
                        var arr = setting.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var s in arr)
                        {
                            //NO lowercase, as we will be searching the index for an exact match (faster)
                            var userAgentName = s.Trim();
                            if (!string.IsNullOrEmpty(userAgentName))
                            {
                                result.Add(userAgentName);
                            }
                        }
                    }
                }

                return result;
            }
        }
    }
}