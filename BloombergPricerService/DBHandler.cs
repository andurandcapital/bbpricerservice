using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BloombergPricerService
{
    public class DBHandler
    {

        public static bool DBHeartbeat()
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {
                try
                {
                    BBPricerHost pricerHost = currentDataContext.BBPricerHosts.Where(h => h.HostName == Environment.MachineName).FirstOrDefault();
                    if (pricerHost == null)
                    {
                        pricerHost = new BBPricerHost() { HostName = Environment.MachineName, PowerSwitch = false };
                        currentDataContext.BBPricerHosts.InsertOnSubmit(pricerHost);
                    }

                    pricerHost.Heartbeat = DateTime.UtcNow;
                    pricerHost.Version = HistoricalPricer.CurrentVersion;
                    currentDataContext.SubmitChanges();
                    return pricerHost.PowerSwitch;
                }
                catch (Exception ex)
                {
                    Logger.WriteLog("Exception in DBHandler.UpdateBBHistoricalRequest. Ex: " + ex.Message, false);
                    return false;
                }
            }
        }

        public static void UpdateBloombergPoints(int? pointsused)
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {
                try
                {
                    currentDataContext.spBloombergPointsUsedUpdate(Environment.MachineName, pointsused);
                }
                catch (Exception ex)
                {
                    Logger.WriteLog("Exception in DBHandler.UpdateBloombergPoints. Ex: " + ex.Message, false);
                }
            }
        }

        public static List<BBPriceRequestQueue> RetrieveCurrentPriceRequests()
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {
                try
                {
                    List<BBPriceRequestQueue> requests = currentDataContext.BBPriceRequestQueues.ToList();
                    return requests.Select(s => new BBPriceRequestQueue(s)).Take(500).ToList();
                }
                catch (Exception ex)
                {
                    Logger.WriteLog("Exception in DBHandler.RetrieveCurrentPriceRequests. Ex: " + ex.Message, false);
                    return null;
                }
            }
        }

        public static List<BBHistoricalRequest> RetrieveHistoricalRequests()
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {
                try
                {
                    //IEnumerable<BBHistoricalRequest> requests = currentDataContext.BBHistoricalRequests.Where(b => !b.Completed && String.Equals(b.HostReserved, Environment.MachineName));
                    // return requests.Select(s => new HistoricalRequest(s)).ToList();
                    return currentDataContext.BBHistoricalRequests.Where(b => !b.Completed && String.Equals(b.HostReserved, Environment.MachineName)).ToList();
                }
                catch (Exception ex)
                {
                    Logger.WriteLog("Exception in DBHandler.RetrieveHistoricalRequests. Ex: " + ex.Message, false);
                    return null;
                }
            }
        }

        public static void UpdateBBHistoricalRequest(HistoricalRequest request)
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {
                try
                {
                    BBHistoricalRequest dbRecord = currentDataContext.BBHistoricalRequests.Single(s => s.RequestID == request.RequestID);

                    dbRecord.Completed = request.Completed;
                    dbRecord.Error = request.Error;
                    dbRecord.ErrorCategory = request.ErrorCategory;
                    dbRecord.ErrorSubcategory = request.ErrorSubcategory;
                    dbRecord.ErrorMessage = request.ErrorMessage;
                    dbRecord.LastModifiedAt = DateTime.UtcNow;
                    dbRecord.Datapoints = request.Datapoints;
                    currentDataContext.SubmitChanges();
                }
                catch (Exception ex)
                {
                    Logger.WriteLog("Exception in DBHandler.UpdateBBHistoricalRequest. Ex: " + ex.Message, false);
                }
            }
        }

        public static void DeleteBBPriceRequest(BBPriceRequestQueue request)
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {
                try
                {
                    BBPriceRequestQueue dbRequest = currentDataContext.BBPriceRequestQueues.Where(b => b.RequestID == request.RequestID).FirstOrDefault();

                    if (dbRequest != null)
                    {
                        currentDataContext.BBPriceRequestQueues.DeleteOnSubmit(dbRequest);
                        currentDataContext.SubmitChanges();
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLog("Exception in DBHandler.DeleteBBPriceRequest. Ex: " + ex.Message, false);
                }
            }
        }

        public static bool TryInsertUpdateHistoricalPrices(List<BBHistoricalPrice> prices, out DateTime? lastPriceDateInserted, out String errorMessage)
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {
                lastPriceDateInserted = new Nullable<DateTime>();
                errorMessage = null;

                foreach (BBHistoricalPrice priceEntry in prices)
                {
                    try
                    {
                        currentDataContext.spInsertUpdateBBHistoricalPrice(priceEntry.PriceDate, priceEntry.BBTicker, priceEntry.BB, priceEntry.LastUpdatedBy, priceEntry.Last, priceEntry.High, priceEntry.Low, priceEntry.Open, priceEntry.Open_Int, priceEntry.Volume, priceEntry.PX_DISC_BID);
                        lastPriceDateInserted = priceEntry.PriceDate;
                    }
                    catch (Exception ex)
                    {
                        //{"ExecuteNonQuery requires the command to have a transaction when the connection assigned to the command is in a pending local transaction.  The Transaction property of the command has not been initialized."}
                        errorMessage = ex.Message;
                        //return false;
                    }
                }
                return true;

            }
        }


        public static void InsertUpdateHistoricalPrices(List<object> prices)
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {

                foreach (BBHistoricalPrice priceEntry in prices)
                {
                    try
                    {
                        currentDataContext.spInsertUpdateBBHistoricalPrice(priceEntry.PriceDate, priceEntry.BBTicker, priceEntry.BB, priceEntry.LastUpdatedBy, priceEntry.Last, priceEntry.High, priceEntry.Low, priceEntry.Open, priceEntry.Open_Int, priceEntry.Volume, priceEntry.PX_DISC_BID);

                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLog("Exception in DBHandler.InsertUpdateHistoricalPrices. Ex: " + ex.Message, false);
                    }

                }
            }
        }

        public static void InsertUpdateStaticData(List<object> entries)
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {

                foreach (BBReferenceData staticEntry in entries)
                {
                    try
                    {
                        currentDataContext.spInsertUpdateStaticData(staticEntry.BBTicker, staticEntry.BB, staticEntry.LastUpdatedBy, staticEntry.FUT_NOTICE_FIRST, staticEntry.FUT_LAST_TRADE_DT);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLog("Exception in DBHandler.InsertUpdateStaticData. Ex: " + ex.Message, false);
                    }

                }
            }
        }

        public static void InsertUpdateIntradayBarData(List<object> entries)
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {

                foreach (BBIntradayBarPrice barEntry in entries)
                {
                    try
                    {
                        currentDataContext.spInsertUpdateBBIntradayBarPrice(barEntry.PriceDate, barEntry.PriceTime, barEntry.BBTicker, barEntry.BB, barEntry.LastUpdatedBy,
                                                                            barEntry.Open, barEntry.High, barEntry.Low, barEntry.Close, barEntry.NumEvents, barEntry.Volume);
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLog("Exception in DBHandler.InsertUpdateIntradayBarData. Ex: " + ex.Message, false);
                    }

                }
            }
        }

        public static void InsertUpdateIntradayTickData(List<object> entries)
        {
            using (PricingDataContext currentDataContext = new PricingDataContext())
            {

                /*foreach (BBIntradayBarPrice barEntry in entries)
                {
                    try
                    {
                        currentDataContext.spInsertUpdateBBIntradayBarPrice(barEntry.PriceDate, barEntry.PriceTime, barEntry.BBTicker, barEntry.BB, barEntry.LastUpdatedBy,
                                                                            barEntry.Open, barEntry.High, barEntry.Low, barEntry.Close, barEntry.NumEvents, barEntry.Volume);
                    }
                    catch (Exception ex)
                    {

                    }

                }*/
            }
        }

    }
}
