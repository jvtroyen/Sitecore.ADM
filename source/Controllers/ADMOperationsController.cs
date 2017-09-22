using Sitecore;
using Sitecore.Jobs;
using System;
using System.Collections.Specialized;
using System.Web.Http;
using System.Web.Mvc;
using TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Core;
using TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Core.Security;

namespace TheReference.DotNet.Sitecore.AnalyticsDatabaseManager.Controllers
{
    [AuthorizedUserFilter]
    public class ADMOperationsController : ApiController
    {
        private const string JOBNAME_DATABASE = "ADM_Database_Clearing";
        private const string JOBNAME_CONTACTS = "ADM_Contacts_Clearing";
        private const string JOBNAME_DEVICES = "ADM_Devices_Clearing";
        private const string JOBNAME_USERAGENTS = "ADM_UserAgents_Clearing";
        private const string JOBNAME_INDEX = "ADM_Index_Rebuilding";

        [System.Web.Mvc.HttpPost]
        public ActionResult StartClearing(ADMOperationsController.RemoveInfo info)
        {
            if (IsRunning())
                return new JsonResult()
                {
                    Data = new
                    {
                        result = "Failed to start the operation. The job is already running."
                    }
                };

            MongoDatabaseManager mongoDatabaseManager1 = new MongoDatabaseManager();
            string jobName = JOBNAME_DATABASE;
            string category = "ADM";
            string name = Context.Site.Name;
            MongoDatabaseManager mongoDatabaseManager2 = mongoDatabaseManager1;
            string methodName = "RemoveData";

            DataClearingOptions dataClearingOptions = new DataClearingOptions(info.endDate);
            if (info.startDate.HasValue)
            {
                dataClearingOptions.StartDate = info.startDate.Value;
            }
            int num1 = info.removeContacts ? 1 : 0;
            dataClearingOptions.RemoveContacts = num1 != 0;
            int num2 = info.filterContacts ? 1 : 0;
            dataClearingOptions.FilterContacts = num2 != 0;
            int num3 = info.removeFormData ? 1 : 0;
            dataClearingOptions.RemoveFormData = num3 != 0;
            int num4 = info.removeUserAgents ? 1 : 0;
            dataClearingOptions.RemoveUserAgents = num4 != 0;
            int num5 = info.filterInteractions ? 1 : 0;
            dataClearingOptions.FilterInteractions = num5 != 0;
            int num6 = info.removeDevices ? 1 : 0;
            dataClearingOptions.RemoveDevices = num6 != 0;
            int num7 = info.removeRobotsOnly ? 1 : 0;
            dataClearingOptions.RemoveRobotsOnly = num7 != 0;


            object[] parameters = new object[1];
            parameters[0] = dataClearingOptions;

            JobOptions options = new JobOptions(jobName, category, name, (object)mongoDatabaseManager2, methodName, parameters);
            options.AfterLife = TimeSpan.FromHours(1.0);
            int num8 = 0;
            options.EnableSecurity = num8 != 0;

            var count = JobManager.GetJobs().Length;
            var oldJob = JobManager.GetJob(jobName);
            if (oldJob != null)
            {
                
            }

            JobManager.Start(options);
            return new JsonResult()
            {
                Data = new
                {
                    result = "ok"
                }
            };
        }

        public ActionResult GetRange()
        {
            MongoDatabaseManager mongoDatabaseManager = new MongoDatabaseManager();
            return new JsonResult()
            {
                Data = new
                {
                    startDate = mongoDatabaseManager.GetStartDate(),
                    endDate = mongoDatabaseManager.GetEndDate(),
                    count = mongoDatabaseManager.GetInteractionsCount()
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public ActionResult GetContactsCount()
        {
            MongoDatabaseManager mongoDatabaseManager = new MongoDatabaseManager();
            return new JsonResult()
            {
                Data = new
                {
                    count = mongoDatabaseManager.GetContactsCount()
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public ActionResult GetDevicesCount()
        {
            MongoDatabaseManager mongoDatabaseManager = new MongoDatabaseManager();
            return new JsonResult()
            {
                Data = new
                {
                    count = mongoDatabaseManager.GetDevicesCount()
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public ActionResult GetUserAgentsCount()
        {
            MongoDatabaseManager mongoDatabaseManager = new MongoDatabaseManager();
            return new JsonResult()
            {
                Data = new
                {
                    count = mongoDatabaseManager.GetUserAgentsCount()
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [System.Web.Mvc.HttpGet]
        public ActionResult GetStatus()
        {
            return new JsonResult()
            {
                Data = new
                {
                    databaseJob = GetJobStatus(JOBNAME_DATABASE),
                    contactsJob = GetJobStatus(JOBNAME_CONTACTS),
                    indexJob = GetJobStatus(JOBNAME_INDEX),
                    useragentsJob = GetJobStatus(JOBNAME_USERAGENTS),
                    devicesJob = GetJobStatus(JOBNAME_DEVICES)
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [System.Web.Mvc.HttpPost]
        public ActionResult StopAllJobs()
        {
            HaltJob(JOBNAME_DATABASE);
            HaltJob(JOBNAME_CONTACTS);
            HaltJob(JOBNAME_DEVICES);
            HaltJob(JOBNAME_USERAGENTS);

            return new JsonResult()
            {
                Data = new
                {
                    result = "ok"
                }
            };
        }

        private object GetJobStatus(string jobName)
        {
            var job = JobManager.GetJob(jobName);
            if (job != null)
            {
                return new
                {
                    isRunning = !job.IsDone,
                    messages = job.Status.Messages,
                    processed = job.Status.Processed,
                    total = job.Status.Total
                };
            }

            return new
            {
                isRunning = false,
                messages = new StringCollection(),
                processed = 0L,
                total = 0L
            };

        }

        private bool HaltJob(string jobName)
        {
            var job = JobManager.GetJob(jobName);
            if (job != null)
            {
                if (job.Status.State == JobState.Running)
                {
                    job.Status.State = JobState.Finished;
                    return true;
                }
            }

            return false;
        }

        private bool IsRunning()
        {
            if (!JobManager.IsJobRunning(JOBNAME_DATABASE))
            {
                if (!JobManager.IsJobRunning(JOBNAME_CONTACTS))
                {
                    if (!JobManager.IsJobRunning(JOBNAME_DEVICES))
                    {
                        return JobManager.IsJobRunning(JOBNAME_USERAGENTS);
                    }
                }
                return true;
            }
            return true;
        }

        public ActionResult FormDataExists()
        {
            MongoDatabaseManager mongoDatabaseManager = new MongoDatabaseManager();
            return new JsonResult()
            {
                Data = new
                {
                    exists = mongoDatabaseManager.FormDataExists()
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [System.Web.Mvc.HttpPost]
        public ActionResult RemoveContacts(ADMOperationsController.RemoveInfo info)
        {
            if (IsRunning())
                return new JsonResult()
                {
                    Data = new
                    {
                        result = "Failed to start the operation. The job is already running."
                    }
                };
            JobOptions options = new JobOptions(JOBNAME_CONTACTS, "ADM", Context.Site.Name, (object)new MongoDatabaseManager(), "RemoveContactsWithoutInteractions", new object[1]
            {
                (object)(info.filterContacts ? true : false)
            });
            options.AfterLife = TimeSpan.FromHours(1.0);
            int num = 0;
            options.EnableSecurity = num != 0;
            JobManager.Start(options);

            return new JsonResult()
            {
                Data = new
                {
                    result = "ok"
                }
            };
        }

        [System.Web.Mvc.HttpPost]
        public ActionResult RemoveDevices()
        {
            if (IsRunning())
                return new JsonResult()
                {
                    Data = new
                    {
                        result = "Failed to start the operation. The job is already running."
                    }
                };
            JobOptions options = new JobOptions(JOBNAME_DEVICES, "ADM", Context.Site.Name, (object)new MongoDatabaseManager(), "RemoveDevicesWithoutInteractions");
            options.AfterLife = TimeSpan.FromHours(1.0);
            int num = 0;
            options.EnableSecurity = num != 0;
            JobManager.Start(options);

            return new JsonResult()
            {
                Data = new
                {
                    result = "ok"
                }
            };
        }

        [System.Web.Mvc.HttpPost]
        public ActionResult RemoveUserAgents()
        {
            if (IsRunning())
                return new JsonResult()
                {
                    Data = new
                    {
                        result = "Failed to start the operation. The job is already running."
                    }
                };
            JobOptions options = new JobOptions(JOBNAME_USERAGENTS, "ADM", Context.Site.Name, (object)new MongoDatabaseManager(), "RemoveUserAgentsWithoutInteractions");

            options.AfterLife = TimeSpan.FromHours(1.0);
            int num = 0;
            options.EnableSecurity = num != 0;
            JobManager.Start(options);

            return new JsonResult()
            {
                Data = new
                {
                    result = "ok"
                }
            };
        }

        [System.Web.Mvc.HttpPost]
        public ActionResult RemoveRobotUserAgents()
        {
            if (IsRunning())
                return new JsonResult()
                {
                    Data = new
                    {
                        result = "Failed to start the operation. The job is already running."
                    }
                };
            JobOptions options = new JobOptions(JOBNAME_USERAGENTS, "ADM", Context.Site.Name, (object)new MongoDatabaseManager(), "RemoveRobotUserAgents");
            options.AfterLife = TimeSpan.FromHours(1.0);
            int num = 0;
            options.EnableSecurity = num != 0;
            JobManager.Start(options);

            return new JsonResult()
            {
                Data = new
                {
                    result = "ok"
                }
            };
        }

        [System.Web.Mvc.HttpPost]
        public ActionResult IndexContacts()
        {
            if (JobManager.IsJobRunning(JOBNAME_INDEX))
                return new JsonResult()
                {
                    Data = new
                    {
                        result = "Failed to start the operation. The job is already running."
                    }
                };
            JobOptions options = new JobOptions(JOBNAME_INDEX, "ADM", Context.Site.Name, (object)new AnalyticsIndexManager(), "UpdateContactsInIndex", new object[0]);
            options.AfterLife = TimeSpan.FromHours(1.0);
            int num = 0;
            options.EnableSecurity = num != 0;
            JobManager.Start(options);
            return new JsonResult()
            {
                Data = new
                {
                    result = "ok"
                }
            };
        }

        public class RemoveInfo
        {
            public DateTime? startDate { get; set; }

            public DateTime endDate { get; set; }

            public bool filterInteractions { get; set; }

            public bool removeContacts { get; set; }

            public bool filterContacts { get; set; }

            public bool removeFormData { get; set; }

            public bool removeUserAgents { get; set; }

            public bool removeDevices { get; set; }

            public bool removeRobotsOnly { get; set; }
        }
    }
}