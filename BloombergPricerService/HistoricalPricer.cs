using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Deployment.Application;

namespace BloombergPricerService
{
    public class HistoricalPricer
    {
        BloombergListener historicalListener = new BloombergListener();
        //public Dictionary<BBHistoricalRequest, List<BBHistoricalPrice>> PriceCache;

        public static string CurrentVersion;
        public HistoricalPricer()
        {
            if (ApplicationDeployment.IsNetworkDeployed)
                CurrentVersion = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();

        }

        //public static Queue<BBHistoricalRequest> HistoricalRequestQueue = new Queue<BBHistoricalRequest>();

        public void CheckForNewRequests(object state)
        {
            Logger.WriteLog("Historical Pricer - check for new requests started.", false);
            while (BloombergPricer.PricerRunning)
            {
                try
                {
                    if (DBHandler.DBHeartbeat())
                    {

                        List<BBPriceRequestQueue> priceRequests = DBHandler.RetrieveCurrentPriceRequests();
                        if (priceRequests.Count > 0)
                        {
                            Logger.WriteLog(DateTime.UtcNow + ": Number of new price requests: " + priceRequests.Count() + ". ", false);
                            historicalListener.SendPriceRequests(priceRequests);
                            Thread.Sleep(30000);
                        }

                        List<BBHistoricalRequest> historicalRequests = DBHandler.RetrieveHistoricalRequests();

                        if (historicalRequests.Count > 0)
                        {
                            Logger.WriteLog(DateTime.UtcNow + ": Number of new historical requests: " + historicalRequests.Count() + ". ", false);
                            historicalListener.AddHistoricalRequests(historicalRequests);
                            Thread.Sleep(30000);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLog(DateTime.UtcNow + ": Exception occured in HistoricalPricer.CheckForNewRequests - " + ex.Message + ". Stack: " + ex.StackTrace, false);
                }
                Thread.Sleep(30000);
            }

        }


    }
}



