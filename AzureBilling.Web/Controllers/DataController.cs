using AzureBillingAPI.Data;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebApplication4.Controllers
{
    public class DataController : Controller
    {
        public JsonResult SpendingBySubscription(string monthId="")
        {

            if (string.IsNullOrEmpty(monthId))
            {
                monthId = GetMonthId();
            }

            var repo = new EntityRepo<EAUsageSubscriptionSummaryEntity>();
            var data = repo.Get(monthId, new List<Tuple<string, string>> { });
            var array =  data.Select(p => new { name = p.SubscriptionName, y = p.Amount });
            return Json(array.ToList(), JsonRequestBehavior.AllowGet);
        }

        public JsonResult SpendingByAccount(string monthId="")
        {
            if (string.IsNullOrEmpty(monthId))
            {
                monthId = GetMonthId();
            }
            var repo = new EntityRepo<EAUsageAccountSummaryEntity>();
            var data = repo.Get(monthId, new List<Tuple<string, string>> { });
            var array = data.Select(p => new { name = p.AccountName, y = p.Amount  });
            return Json(array.ToList(), JsonRequestBehavior.AllowGet);
        }

        public JsonResult SpendingByService(string monthId = "")
        {
            if (string.IsNullOrEmpty(monthId))
            {
                monthId = GetMonthId();
            }
            var repo = new EntityRepo<EAUsageMeterSummaryEntity>();
            var data = repo.Get(monthId, new List<Tuple<string, string>> { });

            var aggregateUsage = from us in data
                                 group us by new
                                 {
                                     MeterCategory = us.MeterCategory,
                                 }
                            into fus
                                 select new
                                 {
                                     y = fus.Sum(x => x.Amount),
                                     name = fus.Key.MeterCategory,
                                 };
            return Json(aggregateUsage.ToList(), JsonRequestBehavior.AllowGet);
        }

        public JsonResult SpendingByServiceDaily(string monthId = "")
        {
            var dictionary = new Dictionary<string, Dictionary<string,double>>();
            var uniqueDates = new Dictionary<string,string>();
            if (string.IsNullOrEmpty(monthId))
            {
                monthId = GetMonthId();
            }
            var repo = new EntityRepo<EAUsageMeterDailySummaryEntity>();
            var data = repo.Get(monthId, new List<Tuple<string, string>> { });

            var aggregateUsage = data.OrderBy(x=>x.Day).Select(p => new DailyBillInfo{ Amount = p.Amount, Name = p.MeterCategory, Date = p.Day });
            return GetDailyBillSeries(aggregateUsage);
        }

        public JsonResult SpendingBySubscriptionDaily(string monthId = "")
        {
            if (string.IsNullOrEmpty(monthId))
            {
                monthId = GetMonthId();
            }
            var repo = new EntityRepo<EAUsageSubscriptionDailySummaryEntity>();
            var data = repo.Get(monthId, new List<Tuple<string, string>> { });

            var aggregateUsage = data.OrderBy(x => x.Day).Select(p => new DailyBillInfo{ Amount = p.Amount, Name = p.SubscriptionName, Date = p.Day });
            return GetDailyBillSeries(aggregateUsage);
        }

        public JsonResult SpendingByAccountDaily(string monthId = "")
        {

            if (string.IsNullOrEmpty(monthId))
            {
                monthId = GetMonthId();
            }
            var repo = new EntityRepo<EAUsageAccountDailySummaryEntity>();
            var data = repo.Get(monthId, new List<Tuple<string, string>> { });

            var aggregateUsage = data.OrderBy(x => x.Day).Select(p => new DailyBillInfo { Amount = p.Amount, Name = p.AccountName, Date = p.Day });
            return GetDailyBillSeries(aggregateUsage);
        }

        private JsonResult GetDailyBillSeries(IEnumerable<DailyBillInfo> aggregateUsage)
        {
            var dictionary = new Dictionary<string, Dictionary<string, double>>();
            var uniqueDates = new Dictionary<string, string>();
            foreach (var item in aggregateUsage)
            {
                var dateKey = GetDateKey(item.Date);
                if (!uniqueDates.ContainsKey(dateKey))
                {
                    uniqueDates.Add(dateKey, dateKey);
                }

                if (dictionary.ContainsKey(item.Name))
                {
                    var doubleList = dictionary[item.Name];
                    if (doubleList.ContainsKey(dateKey))
                    {
                        doubleList[dateKey] = item.Amount;
                    }
                    else
                    {
                        doubleList.Add(dateKey, item.Amount);
                    }
                }
                else
                {
                    var doubleList = new Dictionary<string, double>();
                    doubleList.Add(dateKey, item.Amount);
                    dictionary.Add(item.Name, doubleList);
                }
            }

            var finalDictionary = new Dictionary<string, List<double>>();
            //populate the entries with date with no data to '0'
            foreach (var categories in dictionary.Keys)
            {
                var dateWiseDictionary = dictionary[categories];
                foreach (var date in uniqueDates.Keys)
                {
                    if (!dateWiseDictionary.ContainsKey(date))
                    {
                        dateWiseDictionary.Add(date, 0.0);
                    }
                }

                // once data is populated sort it based on the date.
                finalDictionary.Add(categories, dateWiseDictionary.OrderBy(p => p.Key).Select(p => p.Value).ToList());
            }
            return Json(new { date = uniqueDates.Keys.OrderBy(p => p), series = finalDictionary.Select(p => new { name = p.Key, data = p.Value }).ToList() }, JsonRequestBehavior.AllowGet);
        }

        private string GetDateKey(string date)
        {
            return date;
        }

        private static string GetMonthId()
        {
            string monthId;
            string month = DateTime.UtcNow.Month < 10 ? "0" + DateTime.UtcNow.Month.ToString() : DateTime.UtcNow.Month.ToString();
            string year = DateTime.UtcNow.Year.ToString();
            monthId = string.Format("{0}-{1}", year, month);
            return monthId;
        }
    }

    public class DailyBillInfo
    {
        public double Amount { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
    }
    
}