using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Sitecore;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Configuration;
using TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Core.Helpers;
using TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterContact;
using TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Pipelines.FilterInteraction;
using Jobs = Sitecore.Jobs;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Core
{
    public class MongoDatabaseManager
    {
        protected MongoDatabase Database { get; set; }

        private List<string> robotNameParts;
        private List<string> robotNames;

        public MongoDatabaseManager(string databaseName)
        {
            Database = new MongoClient().GetServer().GetDatabase(databaseName);
        }

        public MongoDatabaseManager()
        {
            Database = MongoSettingsHelper.GetDatabase("analytics");
        }

        private List<string> RobotNameParts
        {
            get
            {
                if (robotNameParts == null)
                {
                    robotNameParts = Settings.RobotUserAgentNameParts;
                }

                return robotNameParts;
            }
        }

        private List<string> RobotNames
        {
            get
            {
                if (robotNames == null)
                {
                    robotNames = Settings.RobotUserAgentNames;
                }

                return robotNames;
            }
        }

        public void RemoveData(DataClearingOptions options)
        {
            DateTime endDate = options.EndDate;
            DateTime? startDate = (options.StartDate != DateTime.MinValue) ? options.StartDate : (DateTime?) null;
            IMongoQuery query;
            if (startDate.HasValue)
                query = Query.And(Query.LT("StartDateTime", endDate), Query.GT("StartDateTime", startDate.Value));
            else
                query = Query.LT("StartDateTime", endDate);

            if (Context.Job != null)
            {
                StringCollection messages = Context.Job.Status.Messages;
                var sb = new StringBuilder();
                sb.AppendFormat("Clearing was initiated with the following parameters: endDate = {0}/{1}/{2}", endDate.Month, endDate.Day, endDate.Year);

                if (options.StartDate > DateTime.MinValue)
                {
                    sb.AppendFormat(", startDate = {0}/{1}/{2}", options.StartDate.Month, options.StartDate.Day, options.StartDate.Year);
                }
                else
                {
                    sb.Append(", no startDate");
                }

                sb.AppendFormat(", removeContacts = {0}", options.RemoveContacts.ToString());
                sb.AppendFormat(", filterContacts = {0}", options.FilterContacts.ToString());
                sb.AppendFormat(", filterInteractions = {0}", options.FilterInteractions.ToString());
                sb.AppendFormat(", removeUserAgents = {0}", options.RemoveUserAgents.ToString());
                sb.AppendFormat(", removeDevices = {0}", options.RemoveDevices.ToString());
                sb.AppendFormat(", removeFormData = {0}", options.RemoveFormData.ToString());
                sb.AppendFormat(", removeRobotsOnly = {0}", options.RemoveRobotsOnly.ToString());

                messages.Add(sb.ToString());

                Context.Job.Status.Total = GetInteractionsCount(startDate, endDate);
                Context.Job.Status.Messages.Add("Removing interactions started. " + Context.Job.Status.Total + " items to process.");
            }

            if (AllIndexesPresent(options.RemoveUserAgents, options.RemoveDevices, options.RemoveFormData))
            {
                bool formDataExists = FormDataExists();
                long numInteractions = 0;

                var contacts = new HashSet<Guid>();
                var devices = new HashSet<Guid>();
                var userAgents = new HashSet<string>();
                var formData = new HashSet<Guid>();

                try
                {
                    MongoCursor<BsonDocument> mongoCursor = Database.GetCollection("Interactions")
                        .Find(query)
                        .SetFlags(QueryFlags.NoCursorTimeout);

                    foreach (BsonDocument interaction in mongoCursor)
                    {
                        //Halt if job stopped from outside
                        if (Context.Job.Status.State == Jobs.JobState.Finished)
                        {
                            Context.Job.Status.Messages.Add("Removing interactions halted by user. " + numInteractions + " interactions removed.");
                            return;
                        }

                        try
                        {
                            if ((!options.FilterInteractions || !IsInteractionRestricted(interaction))
                                && (!options.RemoveRobotsOnly || IsRobotInteraction(interaction)))
                            {
                                RemoveInteraction(interaction["_id"].AsGuid);
                                ++numInteractions;

                                if (formDataExists && options.RemoveFormData)
                                    formData.Add(interaction["_id"].AsGuid);

                                if (options.RemoveContacts)
                                {
                                    BsonValue bsonValue1;
                                    if (interaction.TryGetValue("ContactId", out bsonValue1))
                                        contacts.Add(bsonValue1.AsGuid);
                                    BsonValue bsonValue2;
                                    if (interaction.TryGetValue("DeviceId", out bsonValue2))
                                        devices.Add(bsonValue2.AsGuid);
                                    BsonValue bsonValue3;
                                    if (interaction.TryGetValue("UserAgent", out bsonValue3))
                                        userAgents.Add(bsonValue3.AsString);
                                }
                            }
                            else
                            {
                                //Skip logging for performance reasons
                                //Log.Debug("[Analytics Database Manager] Interaction with id " + interaction["_id"].AsGuid + " was not removed due to the filterInteraction pipeline restrictions");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[Analytics Database Manager] Exception while removing interaction with id " + interaction["_id"].AsGuid, ex, this);
                        }
                        finally
                        {
                            this.IncrementProcessed();
                        }
                    }
                }
                catch (Exception e)
                {
                    //Wut?
                }
                if (Context.Job != null)
                    Context.Job.Status.Messages.Add("Clearing the Interactions collection was finished. " + numInteractions + " items were removed ");

                //Remove FormData?
                if (formDataExists && options.RemoveFormData)
                {
                    if (Context.Job != null)
                    {
                        Context.Job.Status.Total = formData.Count;
                        Context.Job.Status.Processed = 0L;
                        Context.Job.Status.Messages.Add("Removing form data started. " + Context.Job.Status.Total + " items to process.");
                    }
                    long num2 = RemoveFormData(formData);
                    if (Context.Job != null)
                        Context.Job.Status.Messages.Add("Clearing the FormData collection was finished. " + num2 + " items were removed ");
                }

                //Remove Contacts?
                if (!options.RemoveContacts)
                    return;
                if (Context.Job != null)
                {
                    Context.Job.Status.Total = contacts.Count;
                    Context.Job.Status.Processed = 0L;
                    Context.Job.Status.Messages.Add("Removing contacts started. " + Context.Job.Status.Total + " items to process.");
                }
                long num3 = this.RemoveContacts(contacts, options.FilterContacts);
                if (Context.Job != null)
                {
                    Context.Job.Status.Messages.Add("Clearing the Contacts collection was finished. " + num3 + " items were removed ");
                    Context.Job.Status.Processed = 0L;
                }

                //Remove Devices?
                if (options.RemoveDevices)
                {
                    if (Context.Job != null)
                    {
                        Context.Job.Status.Total = devices.Count;
                        Context.Job.Status.Messages.Add("Removing devices started. " + Context.Job.Status.Total + " items to process.");
                    }
                    long num2 = this.RemoveDevices(devices);
                    if (Context.Job != null)
                    {
                        Context.Job.Status.Messages.Add("Clearing the Devices collection was finished. " + num2 + " items were removed ");
                        Context.Job.Status.Processed = 0L;
                    }
                }

                //Remove UserAgents?
                if (!options.RemoveUserAgents || Context.Job == null)
                    return;
                Context.Job.Status.Total = userAgents.Count;
                Context.Job.Status.Messages.Add("Removing user agents started. " + Context.Job.Status.Total + " items to process.");
                long num4 = RemoveUserAgents(userAgents);
                if (Context.Job == null)
                    return;
                Context.Job.Status.Messages.Add("Removing user agents ended. " + num4 + " items were removed ");
                Context.Job.Status.Processed = 0L;
            }
        }

        private bool AllIndexesPresent(bool checkUserAgent, bool checkDevice, bool checkFormData)
        { 
            var allIndexesPresent = true;
            if (checkUserAgent && !IndexExists("Interactions", "UserAgent"))
            {
                allIndexesPresent = false;
                Context.Job.Status.Messages.Add("Missing index : Interactions.UserAgent.");
            }
            if (checkDevice && !IndexExists("Interactions", "DeviceId"))
            {
                allIndexesPresent = false;
                Context.Job.Status.Messages.Add("Missing index : Interactions.DeviceId.");
            }
            if (checkFormData && !IndexExists("FormData", "InteractionId"))
            {
                allIndexesPresent = false;
                Context.Job.Status.Messages.Add("Missing index : FormData.InteractionId.");
            }

            return allIndexesPresent;
        }

        private bool IndexExists(string collectionName, string fieldName)
        {
            var collection = Database.GetCollection(collectionName);
            if (collection != null)
            {
                return collection.IndexExists(new string[] { fieldName });
            }

            return false;
        }

        public void RemoveContactsWithoutInteractions(bool filterContacts)
        {
            MongoCursor<BsonDocument> all = Database.GetCollection("Contacts")
                .FindAll()
                .SetFlags(QueryFlags.NoCursorTimeout);

            if (Context.Job != null)
            {
                Context.Job.Status.Total = all.Count();
                Context.Job.Status.Messages.Add("Removing contact data started. " + Context.Job.Status.Total + " items to process ");
                Context.Job.Status.Processed = 0L;
            }

            long num = 0;
            foreach (BsonDocument contact in all)
            {
                //Halt if job stopped from outside
                if (Context.Job.Status.State == Jobs.JobState.Finished)
                {
                    Context.Job.Status.Messages.Add("Removing contacts halted by user. " + num + " contacts removed.");
                    return;
                }


                Guid asGuid = contact["_id"].AsGuid;
                try
                {
                    if (filterContacts && this.IsContactRestricted(contact))
                        Log.Debug("[Analytics Database Manager] Contact with id " + asGuid + " was not removed due to the filterContact pipeline restrictions");
                    else if (!this.InteractionsExist(asGuid))
                    {
                        this.RemoveContactWithIdentifier(contact);
                        ++num;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[Analytics Database Manager] Exception was thrown while removing contact " + asGuid, ex, this);
                }
                finally
                {
                    IncrementProcessed();
                }
            }
            if (Context.Job == null)
                return;
            Context.Job.Status.Messages.Add("Removing contact data ended. " + num + " items were removed ");
            Context.Job.Status.Processed = 0L;
        }

        public void RemoveDevicesWithoutInteractions()
        {
            MongoCursor<BsonDocument> all = this.Database.GetCollection("Devices")
                .FindAll()
                .SetFlags(QueryFlags.NoCursorTimeout);

            if (Context.Job != null)
            {
                Context.Job.Status.Total = all.Count();
                Context.Job.Status.Messages.Add("Removing devices started. " + Context.Job.Status.Total + " items to process ");
                Context.Job.Status.Processed = 0L;
            }

            if (AllIndexesPresent(false, true, false))
            {

                long num = 0;
                foreach (BsonDocument device in all)
                {
                    //Halt if job stopped from outside
                    if (Context.Job.Status.State == Jobs.JobState.Finished)
                    {
                        Context.Job.Status.Messages.Add("Removing devices halted by user. " + num + " devices removed.");
                        return;
                    }

                    Guid asGuid = device["_id"].AsGuid;

                    try
                    {
                        if (!InteractionForDeviceExist(asGuid))
                        {
                            RemoveDevice(asGuid);
                            ++num;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[Analytics Database Manager] Exception was thrown while removing device " + asGuid, ex, this);
                    }
                    finally
                    {
                        IncrementProcessed();
                    }
                }

                if (Context.Job == null)
                    return;

                Context.Job.Status.Messages.Add("Removing devices ended. " + num + " items were removed ");
                Context.Job.Status.Processed = 0L;
            }
        }

        public void RemoveRobotUserAgents()
        {
            //All useragents that CONTAIN specific words
            var queries = new List<IMongoQuery>();
            foreach (var part in RobotNameParts)
            {
                queries.Add(Query.Matches("UserAgentName", new BsonRegularExpression(new Regex(part, RegexOptions.IgnoreCase))));
            }
            var query = Query.Or(queries);

            var userAgentList = new List<string>();
            var userAgents = this.Database.GetCollection("UserAgents")
                .Find(query)
                .SetFlags(QueryFlags.NoCursorTimeout);

            foreach (BsonDocument userAgent in userAgents)
            {
                BsonValue bsonUserAgent;
                if (userAgent.TryGetValue("UserAgentName", out bsonUserAgent))
                {
                    userAgentList.Add(bsonUserAgent.AsString);
                }
            }

            //Now add all specific useragents
            foreach (var s in RobotNames)
            {
                if (!userAgentList.Contains(s))
                {
                    userAgentList.Add(s);
                }
            }

            var numAgents = userAgentList.Count();
            if (Context.Job != null)
            {
                Context.Job.Status.Total = numAgents;
                Context.Job.Status.Messages.Add("Removing robot userAgents started. " + numAgents + " items to process ");
                Context.Job.Status.Processed = 0L;
            }

            if (AllIndexesPresent(true, false, false))
            {
                var contacts = new HashSet<Guid>();
                var devices = new HashSet<Guid>();

                long num = 0;
                long numInteractions = 0;

                //Remove all interactions that have no useragent
                numInteractions += RemoveInteractionsWithoutUserAgent(contacts, devices, numAgents, numInteractions);

                //Remove all interactions for each useragent found
                foreach (var userAgentName in userAgentList)
                {
                    //Halt if job stopped from outside
                    if (Context.Job.Status.State == Jobs.JobState.Finished)
                    {
                        Context.Job.Status.Messages.Add("Removing robot userAgents halted by user. " + num + " userAgents removed (" + numInteractions + " interactions).");
                        return;
                    }

                    try
                    {
                        //Remove all interactions with this userAgent
                        numInteractions += this.RemoveInteractionsWithUserAgent(userAgentName, contacts, devices, numAgents, numInteractions);

                        this.RemoveUserAgent(userAgentName);

                        ++num;                            
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[Analytics Database Manager] Exception was thrown while removing userAgent '" + userAgentName + "'", ex, this);
                    }
                    finally
                    {
                        IncrementProcessed();
                    }
                }

                if (Context.Job == null)
                    return;

                Context.Job.Status.Messages.Add("Removing robot userAgents ended. " + num + " userAgents were removed (" + numInteractions + " interactions)");
                Context.Job.Status.Processed = 0L;

                //Remove Contacts?
                if (Context.Job != null)
                {
                    Context.Job.Status.Total = contacts.Count;
                    Context.Job.Status.Processed = 0L;
                    Context.Job.Status.Messages.Add("Removing contacts started. " + Context.Job.Status.Total + " items to process.");
                }
                long num3 = this.RemoveContacts(contacts, false);
                if (Context.Job != null)
                {
                    Context.Job.Status.Messages.Add("Clearing the Contacts collection was finished. " + num3 + " items were removed ");
                    Context.Job.Status.Processed = 0L;
                }

                //Remove Devices?
                if (Context.Job != null)
                {
                    Context.Job.Status.Total = devices.Count;
                    Context.Job.Status.Messages.Add("Removing devices started. " + Context.Job.Status.Total + " items to process.");
                }
                long num2 = this.RemoveDevices(devices);
                if (Context.Job != null)
                {
                    Context.Job.Status.Messages.Add("Clearing the Devices collection was finished. " + num2 + " items were removed ");
                    Context.Job.Status.Processed = 0L;
                }
            }
        }

        public void RemoveUserAgentsWithoutInteractions()
        {
            MongoCursor<BsonDocument> all = this.Database.GetCollection("UserAgents")
                .FindAll()
                .SetFlags(QueryFlags.NoCursorTimeout);

            if (Context.Job != null)
            {
                Context.Job.Status.Total = all.Count();
                Context.Job.Status.Messages.Add("Removing userAgents started. " + Context.Job.Status.Total + " items to process ");
                Context.Job.Status.Processed = 0L;
            }

            if (AllIndexesPresent(true, false, false))
            {
                long num = 0;
                foreach (BsonDocument userAgent in all)
                {
                    //Halt if job stopped from outside
                    if (Context.Job.Status.State == Jobs.JobState.Finished)
                    {
                        Context.Job.Status.Messages.Add("Removing userAgents halted by user. " + num + " userAgents removed.");
                        return;
                    }

                    Guid asGuid = userAgent["_id"].AsGuid;

                    BsonValue bsonUserAgent;
                    if (userAgent.TryGetValue("UserAgentName", out bsonUserAgent))
                    {
                        string userAgentName = bsonUserAgent.AsString;
                        try
                        {
                            if (!InteractionsExist(userAgentName))
                            {
                                this.RemoveUserAgent(userAgentName);
                                ++num;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[Analytics Database Manager] Exception was thrown while removing userAgent " + asGuid, ex, this);
                        }
                        finally
                        {
                            IncrementProcessed();
                        }
                    }
                }

                if (Context.Job == null)
                    return;

                Context.Job.Status.Messages.Add("Removing userAgents ended. " + num + " items were removed ");
                Context.Job.Status.Processed = 0L;
            }
        }

        private void IncrementProcessed()
        {
            if (Context.Job == null)
                return;
            ++Context.Job.Status.Processed;
        }

        private void IncrementTotal(long num)
        {
            if (Context.Job == null)
                return;

            Context.Job.Status.Total += num;
        }

        private bool InteractionsExist(Guid contactId)
        {
            return (ulong)Database.GetCollection("Interactions").Find(Query.EQ("ContactId", (BsonValue)contactId)).SetLimit(1).SetFields((IMongoFields)Fields.Include("_id")).Count() > 0UL;
        }      

        private long RemoveInteractionsWithUserAgent(string userAgentName, HashSet<Guid> contacts, HashSet<Guid> devices, long numAgents, long numInteractions)
        {
            var interactions = Database.GetCollection("Interactions").Find(Query.EQ("UserAgent", (BsonValue)userAgentName));
            var returnValue = interactions.Count();
            if (returnValue > 0)
            {
                Context.Job.Status.Messages[1] = "Removing robot userAgents started. " + numAgents + " items to process (" + (numInteractions+returnValue) + " interactions)";
                IncrementTotal(returnValue);

                RemoveInteractions(interactions, contacts, devices);
            }

            return returnValue;
        }

        private long RemoveInteractionsWithoutUserAgent(HashSet<Guid> contacts, HashSet<Guid> devices, long numAgents, long numInteractions)
        {
            var interactions = Database.GetCollection("Interactions").Find(Query.NotExists("UserAgent"));
            var returnValue = interactions.Count();
            if (returnValue > 0)
            {
                Context.Job.Status.Messages[1] = "Removing robot userAgents started. " + numAgents + " items to process (" + (numInteractions + returnValue) + " interactions)";
                IncrementTotal(returnValue);

                RemoveInteractions(interactions, contacts, devices);
            }

            return returnValue;
        }

        private void RemoveInteractions(MongoCursor<BsonDocument> interactions, HashSet<Guid> contacts, HashSet<Guid> devices)
        {
            foreach (BsonDocument interaction in interactions)
            {
                Guid asGuid = interaction["_id"].AsGuid;

                try
                {
                    this.RemoveInteraction(asGuid);

                    BsonValue bsonValue1;
                    if (interaction.TryGetValue("ContactId", out bsonValue1))
                        contacts.Add(bsonValue1.AsGuid);
                    BsonValue bsonValue2;
                    if (interaction.TryGetValue("DeviceId", out bsonValue2))
                        devices.Add(bsonValue2.AsGuid);
                }
                catch (Exception ex)
                {
                    Log.Error("[Analytics Database Manager] Exception was thrown while removing userAgent " + asGuid, ex, this);
                }
                finally
                {
                    IncrementProcessed();
                }
            }
        }

        private bool InteractionsExist(string userAgentName)
        {
            if (userAgentName == null)
                return false;
            return (ulong) Database.GetCollection("Interactions").Find(Query.EQ("UserAgent", (BsonValue)userAgentName)).SetLimit(1).Count() > 0UL;
        }

        private bool InteractionForDeviceExist(Guid deviceId)
        {
            return (ulong) Database.GetCollection("Interactions").Find(Query.EQ("DeviceId", (BsonValue)deviceId)).SetLimit(1).Count() > 0UL;
        }

        private bool IsContactRestricted(BsonDocument contact)
        {
            Assert.IsNotNull(contact, "contact parameter can't be null");

            FilterContactArgs args = new FilterContactArgs(contact);
            FilterContactPipeline.Run(args);
            return !args.RemoveContact;
        }

        private bool IsRobotInteraction(BsonDocument interaction)
        {
            Assert.IsNotNull(interaction, "interaction parameter can't be null");

            BsonValue bsonUserAgent;
            if (interaction.TryGetValue("UserAgent", out bsonUserAgent))
            {
                return IsRobotAgent(bsonUserAgent.ToString());
            }
            
            //Interactions without useragent are bad, mkay
            return true;
        }

        private bool IsRobotAgent(string userAgent)
        {
            if (!string.IsNullOrEmpty(userAgent))
            {
                userAgent = userAgent.ToLower();

                return RobotNameParts.Any(name => userAgent.Contains(name))
                    || RobotNames.Contains(userAgent);
            }

            //Empty useragents are bad, mkay
            return true;
        }

        private bool IsInteractionRestricted(BsonDocument interaction)
        {
            Assert.IsNotNull(interaction, "interaction parameter can't be null");

            BsonDocument contact = null;
            BsonValue bsonValue;
            if (interaction.TryGetValue("ContactId", out bsonValue))
                contact = GetContact(bsonValue.AsGuid);
            FilterInteractionArgs args = new FilterInteractionArgs(interaction, contact, this.Database);
            FilterInteractionPipeline.Run(args);
            return !args.RemoveInteraction;
        }

        private BsonDocument GetContact(Guid contactId)
        {
            return Enumerable.FirstOrDefault<BsonDocument>((IEnumerable<BsonDocument>)Database.GetCollection("Contacts").Find(Query.EQ("_id", (BsonValue)contactId)).SetLimit(1));
        }

        private void RemoveContact(Guid contactId)
        {
            IMongoQuery query = Query.EQ("_id", (BsonValue)contactId);
            this.Database.GetCollection("Contacts").Remove(query);
            this.Database.GetCollection("ClassificationsMap").Remove(query);
        }

        private void RemoveContactWithIdentifier(BsonDocument contact)
        {
            Assert.IsNotNull(contact, "contact parameter can't be null");

            Guid asGuid = contact["_id"].AsGuid;
            RemoveContact(asGuid);
            BsonElement bsonElement;
            BsonValue bsonValue;
            if (!contact.TryGetElement("Identifiers", out bsonElement) || !bsonElement.Value.AsBsonDocument.TryGetValue("Identifier", out bsonValue))
                return;
            this.RemoveIdentifier(bsonValue.AsString.ToUpperInvariant(), asGuid);
        }

        public void RemoveIdentifier(string identifier, Guid contactId)
        {
            Assert.IsNotNull(identifier, "identifier parameter can't be null");

            IMongoQuery query = Query.EQ("_id", (BsonValue)identifier);
            BsonDocument one = Database.GetCollection("Identifiers").FindOne(query);
            BsonValue bsonValue;
            if (!(one != null) || !one.TryGetValue("contact", out bsonValue))
                return;
            if (bsonValue.AsGuid == contactId)
                this.Database.GetCollection("Identifiers").Remove(query);
            else
                Log.Warn("[Analytics Database Manager] The '" + (object)identifier + "' identifier is used by multiple contacts " + (string)(object)bsonValue.AsGuid + " and " + (string)(object)contactId + ". The record is skipped.", (object)this);
        }

        private void RemoveUserAgent(string userAgentName)
        {
            Assert.IsNotNull(userAgentName, "userAgentName parameter can't be null");

            this.Database.GetCollection("UserAgents").Remove(Query.EQ("UserAgentName", (BsonValue)userAgentName));
        }

        private void RemoveDevice(Guid deviceId)
        {
            Database.GetCollection("Devices").Remove(Query.EQ("_id", (BsonValue)deviceId));
        }

        private void RemoveFormData(Guid interactionId)
        {
            Database.GetCollection("FormData").Remove(Query.EQ("InteractionId", (BsonValue)interactionId));
        }

        private void RemoveInteraction(Guid interactionId)
        {
            Database.GetCollection("Interactions").Remove(Query.EQ("_id", (BsonValue)interactionId));
        }

        public long RemoveUserAgents(HashSet<string> userAgents)
        {
            if (userAgents.Count == 0)
                return 0;
            long num = 0;

            foreach (string userAgentName in userAgents)
            {
                //Halt if job stopped from outside
                if (Context.Job.Status.State == Jobs.JobState.Finished)
                {
                    Context.Job.Status.Messages.Add("Removing useragents halted by user.");
                    return num;
                }


                try
                {
                    if (!InteractionsExist(userAgentName))
                    {
                        RemoveUserAgent(userAgentName);
                        ++num;
                    }
                    IncrementProcessed();
                }
                catch (Exception ex)
                {
                    Log.Error("[Analytics Database Manager] Exception was thrown while removing userAgent " + userAgentName, ex, (object)this);
                }
            }
            return num;
        }

        public long RemoveFormData(HashSet<Guid> formData)
        {
            if (formData.Count == 0)
                return 0;
            long num = 0;

            foreach (Guid interactionId in formData)
            {
                //Halt if job stopped from outside
                if (Context.Job.Status.State == Jobs.JobState.Finished)
                {
                    Context.Job.Status.Messages.Add("Removing formData halted by user.");
                    return num;
                }


                try
                {
                    RemoveFormData(interactionId);
                    ++num;
                    IncrementProcessed();
                }
                catch (Exception ex)
                {
                    Log.Error("[Analytics Database Manager] Exception was thrown while removing form data for interaction " + (object)interactionId, ex, (object)this);
                }
            }
            
            return num;
        }

        public long RemoveContacts(HashSet<Guid> contacts, bool filterContacts)
        {
            long num = 0;
            foreach (Guid contactId in contacts)
            {
                //Halt if job stopped from outside
                if (Context.Job.Status.State == Jobs.JobState.Finished)
                {
                    Context.Job.Status.Messages.Add("Removing contacts halted by user.");
                    return num;
                }

                try
                {
                    BsonDocument contact = this.GetContact(contactId);
                    if (contact == (BsonDocument)null)
                        Log.Debug("[Analytics Database Manager] Contact with id " + (object)contactId + " is missing in the database");
                    else if (filterContacts && this.IsContactRestricted(contact))
                    {
                        Log.Debug("[Analytics Database Manager] Contact with id " + (object)contactId + " was not removed due to the filterContact pipeline restrictions");
                    }
                    else
                    {
                        if (!InteractionsExist(contactId))
                        {
                            RemoveContactWithIdentifier(contact);
                            ++num;
                        }
                        IncrementProcessed();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[Analytics Database Manager] Exception was thrown while removing contact " + (object)contactId, ex, (object)this);
                }
            }
            return num;
        }

        public long RemoveDevices(HashSet<Guid> devices)
        {
            if (devices.Count == 0)
                return 0;
            long num = 0;

            foreach (Guid deviceId in devices)
            {
                //Halt if job stopped from outside
                if (Context.Job.Status.State == Jobs.JobState.Finished)
                {
                    Context.Job.Status.Messages.Add("Removing devices halted by user.");
                    return num;
                }

                try
                {
                    if (!InteractionForDeviceExist(deviceId))
                    {
                        RemoveDevice(deviceId);
                        ++num;
                    }
                    IncrementProcessed();
                }
                catch (Exception ex)
                {
                    Log.Error("[Analytics Database Manager] Exception was thrown while removing device " + (object)deviceId, ex, (object)this);
                }
            }
            
            return num;
        }

        public bool FormDataExists()
        {
            if (Database.GetCollection("FormData").Exists())
                return (ulong)Database.GetCollection("FormData").FindAll().SetLimit(1).Count() > 0UL;
            return false;
        }

        public DateTime? GetStartDate()
        {
            BsonDocument bsonDocument = Enumerable.FirstOrDefault<BsonDocument>((IEnumerable<BsonDocument>)this.Database.GetCollection("Interactions").FindAll().SetSortOrder((IMongoSortBy)SortBy.Ascending("StartDateTime")).SetLimit(1));
            if (bsonDocument == null)
                return new DateTime?();
            string index = "StartDateTime";
            return new DateTime?(bsonDocument[index].ToUniversalTime());
        }

        public DateTime? GetEndDate()
        {
            BsonDocument bsonDocument = Enumerable.FirstOrDefault<BsonDocument>((IEnumerable<BsonDocument>)this.Database.GetCollection("Interactions").FindAll().SetSortOrder((IMongoSortBy)SortBy.Descending("StartDateTime")).SetLimit(1));
            if (bsonDocument == null)
                return new DateTime?();
            string index = "StartDateTime";
            return new DateTime?(bsonDocument[index].ToUniversalTime());
        }

        public long GetInteractionsCount()
        {
            return Database.GetCollection("Interactions").FindAll().Count();
        }

        public long GetInteractionsCount(DateTime? startDate, DateTime endDate)
        {
            IMongoQuery query = null;
            if (startDate.HasValue)
                query = Query.And(Query.LT("StartDateTime", endDate), Query.GT("StartDateTime", startDate.Value));
            else
                query = Query.LT("StartDateTime", endDate);


            return Database.GetCollection("Interactions").Find(query).Count();
        }

        public long GetContactsCount()
        {
            return Database.GetCollection("Contacts").Count();
        }

        public long GetDevicesCount()
        {
            return Database.GetCollection("Devices").Count();
        }

        public long GetUserAgentsCount()
        {
            return Database.GetCollection("UserAgents").Count();
        }
    }
}