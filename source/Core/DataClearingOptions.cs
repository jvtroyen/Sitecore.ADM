using System;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Core
{
    public class DataClearingOptions
    {
        public DateTime EndDate { get; set; }

        public DateTime StartDate { get; set; }

        public bool RemoveUserAgents { get; set; }

        public bool RemoveFormData { get; set; }

        public bool RemoveContacts { get; set; }

        public bool RemoveDevices { get; set; }

        public DataClearingOptions(DateTime endDate)
        {
            EndDate = endDate;
            StartDate = DateTime.MinValue;
            RemoveUserAgents = false;
            RemoveDevices = false;
            RemoveContacts = true;
            RemoveFormData = true;
        }
    }
}