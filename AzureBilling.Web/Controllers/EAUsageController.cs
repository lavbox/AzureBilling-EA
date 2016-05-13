using AzureBillingAPI.Data;
using AzureBillingAPI.Web.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IdentityModel.Claims;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace AzureBillingAPI.Web.Controllers
{
    public class EAUsageController : Controller
    {
        #region Configs
        internal static string eaApiUrl = string.Format(@"{0}?month={{1}}&type={{2}}&fmt={{3}}", ConfigurationManager.AppSettings["EA-APIUrl"]);
        internal static string eaEnrollmentNumber = ConfigurationManager.AppSettings["EA-EnrollmentNumber"];
        internal static string eaApiAccessKey = ConfigurationManager.AppSettings["EA-APIAccessKey"];
        internal static string storageConnectionString = ConfigurationManager.AppSettings["StorageConnectionString"];
        #endregion

        public ActionResult GetUsageData(string yyyy, string mm)
        {
            // test get
            //var repo = new EntityRepo<EAUsageSummaryEntity>();
            //var data1 = repo.Get("2016-05",new List<Tuple<string, string>> { new Tuple<string, string>("MeterId", "505db374-df8a-44df-9d8c-13c14b61dee1") }, "2016-05_505db374-df8a-44df-9d8c-13c14b61dee1");
            //foreach (var item in data1)
            //{
            //    var refinedItem = item as EAUsageSummaryEntity;
            //}
            if (IsValidData(yyyy, mm))
            {
                string monthId = string.Format("{0}-{1}", yyyy, mm);
                string detailReportData = DownloadUsageData(monthId, "Detail");
                var runId = "";
                if (detailReportData != String.Empty)
                {
                    runId = Guid.NewGuid().ToString();
                    var data = SaveDetails(detailReportData, monthId, runId);

                    SaveAggregateMeter(monthId, runId, data);
                    SaveAggregateDailyMeter(monthId, runId, data);
                    
                    SaveAggregateBySubscription(monthId, runId, data);
                    SaveDailyBreakupBySubscription(monthId, runId, data);

                    SaveAggregateByAccount(monthId, runId, data);
                    SaveAggregateDailyByAccount(monthId, runId, data);
                }

                // send some identifier
                return Json(new { RunId = runId }, JsonRequestBehavior.AllowGet);
            }

            // return invalid data
            Response.StatusCode = 400;
            return Json(new { Message = "Invalid parameters" }, JsonRequestBehavior.AllowGet);
        }

        private static void SaveAggregateByAccount(string monthId, string runId, List<EAUsageDetailEntity> data)
        {
            var aggregateUsage = from us in data
                                 group us by new
                                 {
                                     us.PartitionKey,
                                     us.AccountName,
                                 }
                              into fus
                                 select new EAUsageAccountSummaryEntity()
                                 {
                                     Amount = fus.Sum(x => (string.IsNullOrEmpty(x.ExtendedCost) ? 0.00 : float.Parse(x.ExtendedCost))),
                                     PartitionKey = monthId,
                                     AccountName = fus.Key.AccountName,
                                     RowKey = monthId + "_" + fus.Key.AccountName,
                                     RunId = runId
                                 };

            EntityRepo<EAUsageAccountSummaryEntity> usageEntityRepoAgg = new EntityRepo<EAUsageAccountSummaryEntity>();
            var aggData = aggregateUsage.ToList();
            usageEntityRepoAgg.Insert(aggData);
        }

        private static void SaveAggregateDailyByAccount(string monthId, string runId, List<EAUsageDetailEntity> data)
        {
            var aggregateUsage = from us in data
                                 group us by new
                                 {
                                     us.PartitionKey,
                                     us.AccountName,
                                     us.Date
                                 }
                              into fus
                                 select new EAUsageAccountDailySummaryEntity()
                                 {
                                     Amount = fus.Sum(x => (string.IsNullOrEmpty(x.ExtendedCost) ? 0.00 : float.Parse(x.ExtendedCost))),
                                     PartitionKey = monthId,
                                     AccountName = fus.Key.AccountName,
                                     RowKey = fus.Key.Date.Replace("/", "-") + "_" + fus.Key.AccountName,
                                     RunId = runId,
                                     Day = fus.Key.Date
                                 };

            EntityRepo<EAUsageAccountDailySummaryEntity> usageEntityRepoAgg = new EntityRepo<EAUsageAccountDailySummaryEntity>();
            var aggData = aggregateUsage.ToList();
            usageEntityRepoAgg.Insert(aggData);
        }

        private static void SaveAggregateBySubscription(string monthId, string runId, List<EAUsageDetailEntity> data)
        {
            var aggregateUsage = from us in data
                                 group us by new
                                 {
                                     us.PartitionKey,
                                     us.SubscriptionId,
                                     us.SubscriptionName
                                 }
                              into fus
                                 select new EAUsageSubscriptionSummaryEntity()
                                 {
                                     Amount = fus.Sum(x => (string.IsNullOrEmpty(x.ExtendedCost) ? 0.00 : float.Parse(x.ExtendedCost))),
                                     PartitionKey = monthId,
                                     SubscriptionId = fus.Key.SubscriptionId,
                                     SubscriptionName = fus.Key.SubscriptionName,
                                     RowKey = monthId + "_" + fus.Key.SubscriptionId,
                                     RunId = runId
                                 };

            EntityRepo<EAUsageSubscriptionSummaryEntity> usageEntityRepoAgg = new EntityRepo<EAUsageSubscriptionSummaryEntity>();
            var aggData = aggregateUsage.ToList();
            usageEntityRepoAgg.Insert(aggData);
        }

        private static void SaveDailyBreakupBySubscription(string monthId, string runId, List<EAUsageDetailEntity>data)
        {
            var aggregateUsage = from us in data
                                 group us by new
                                 {
                                     us.PartitionKey,
                                     us.SubscriptionId,
                                     us.SubscriptionName,
                                     us.Date
                                 }
                             into fus
                                 select new EAUsageSubscriptionDailySummaryEntity()
                                 {
                                     Amount = fus.Sum(x => (string.IsNullOrEmpty(x.ExtendedCost) ? 0.00 : float.Parse(x.ExtendedCost))),
                                     PartitionKey = monthId,
                                     SubscriptionId = fus.Key.SubscriptionId,
                                     SubscriptionName = fus.Key.SubscriptionName,
                                     RowKey = fus.Key.Date.Replace("/", "-") + "_" + fus.Key.SubscriptionId,
                                     RunId = runId,
                                     Day = fus.Key.Date
                                 };

            EntityRepo<EAUsageSubscriptionDailySummaryEntity> usageEntityRepoAgg = new EntityRepo<EAUsageSubscriptionDailySummaryEntity>();
            var aggData = aggregateUsage.ToList();
            usageEntityRepoAgg.Insert(aggData);
        }

        private static void SaveAggregateMeter(string monthId, string runId, List<EAUsageDetailEntity> data)
        {
            var aggregateUsage = from us in data
                                 group us by new
                                 {
                                     us.PartitionKey,
                                     us.MeterId,
                                     us.MeterName,
                                     us.MeterCategory,
                                 }
                              into fus
                                 select new EAUsageMeterSummaryEntity()
                                 {
                                     Amount = fus.Sum(x => (string.IsNullOrEmpty(x.ExtendedCost) ? 0.00 : float.Parse(x.ExtendedCost))),
                                     PartitionKey = monthId,
                                     MeterId = fus.Key.MeterId,
                                     MeterCategory = fus.Key.MeterCategory,
                                     MeterName = fus.Key.MeterName,
                                     RowKey = monthId + "_" + fus.Key.MeterId,
                                     RunId = runId
                                 };

            var aggData = aggregateUsage.ToList();
            EntityRepo<EAUsageMeterSummaryEntity> usageEntityRepoAgg = new EntityRepo<EAUsageMeterSummaryEntity>();
            usageEntityRepoAgg.Insert(aggData);
        }

        private static void SaveAggregateDailyMeter(string monthId, string runId, List<EAUsageDetailEntity> data)
        {
            var aggregateUsage = from us in data
                                 group us by new
                                 {
                                     us.PartitionKey,
                                     us.MeterCategory,
                                     us.Date
                                 }
                              into fus
                                 select new EAUsageMeterDailySummaryEntity()
                                 {
                                     Amount = fus.Sum(x => (string.IsNullOrEmpty(x.ExtendedCost) ? 0.00 : float.Parse(x.ExtendedCost))),
                                     PartitionKey = monthId,
                                     MeterCategory = fus.Key.MeterCategory,
                                     RowKey = fus.Key.Date.Replace("/","-") + "_" + fus.Key.MeterCategory,
                                     RunId = runId,
                                     Day = fus.Key.Date
                                 };

            var aggData = aggregateUsage.ToList();
            EntityRepo<EAUsageMeterDailySummaryEntity> usageEntityRepoAgg = new EntityRepo<EAUsageMeterDailySummaryEntity>();
            usageEntityRepoAgg.Insert(aggData);
        }

        /// <summary>
        /// checks if the date parameters are valid
        /// </summary>
        /// <param name="yyyy">year in YYYY format</param>
        /// <param name="mm">month in mm format</param>
        /// <returns>true if the dates are valid for EA usage</returns>
        private bool IsValidData(string yyyy, string mm)
        {
            bool isDateValid = false;
            if (yyyy != null && mm != null && yyyy.Length == 4 && mm.Length == 2)
            {
                try
                {
                    var uintYYYY = uint.Parse(yyyy, System.Globalization.NumberStyles.Integer);
                    var unitMM = uint.Parse(mm, System.Globalization.NumberStyles.Integer);

                    // year should be > 2012 to current year
                    // month should be between 1 and 12
                    if (uintYYYY > 2012 && uintYYYY <= DateTime.UtcNow.Year && unitMM > 0 && unitMM < 13)
                    {
                        isDateValid = true;
                    }
                }
                catch
                {
                    isDateValid = false;
                }
                
            }
            return isDateValid;
        }
        private string DownloadUsageData(string month, string reportType)
        {
            string url = string.Format(eaApiUrl, eaEnrollmentNumber, month, reportType, "JSON");
            string response = GetResponse(url, eaApiAccessKey);
            return response;
        }

        private string GetResponse(string url, string token)
        {
            WebRequest request = WebRequest.Create(url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Add("authorization", string.Concat("bearer ", token));
                request.Headers.Add("api-version", "1.0");
            }

            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex.Message);
                return String.Empty;
            }

            StreamReader reader = new StreamReader(response.GetResponseStream());
            return reader.ReadToEnd();

        }

        private static List<EAUsageDetailEntity> SaveDetails(string jsonData, string partitionKey,string runId)
        {
            List<EAUsageDetailEntity> usageDetailList = JsonConvert.DeserializeObject<List<EAUsageDetailEntity>>(jsonData);

            foreach (EAUsageDetailEntity entity in usageDetailList)
            {
                var refinedInstanceId = entity.InstanceId.Replace("/", "SPL_CHAR").Replace("\\", "SPL_CHAR").Replace("#", "SPL_CHAR").Replace("?", "SPL_CHAR");
                entity.PartitionKey = partitionKey;
                entity.RowKey = partitionKey + "_" + entity.MeterId + "_" + refinedInstanceId;
                entity.RunId = runId;
            }
            EntityRepo<EAUsageDetailEntity> usageEntityRepoAgg = new EntityRepo<EAUsageDetailEntity>();
            usageEntityRepoAgg.Insert(usageDetailList);

            return usageDetailList;
        }
    }

    

    
}