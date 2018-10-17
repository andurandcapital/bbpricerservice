using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace BloombergPricerService
{
    public partial class BloombergPricer : ServiceBase
    {
        public static bool PricerRunning = false;
        public BloombergPricer()
        {
            InitializeComponent();
        }

        public void OnDebug()
        {
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            BloombergPricer.PricerRunning = true;
            Logger.CreateLoggingFile();
            Logger.WriteLog("Bloomberg Pricer Service Started", false);
            HistoricalPricer pricer = new HistoricalPricer();
            pricer.CheckForNewRequests(null);
            //ThreadPool.QueueUserWorkItem(new WaitCallback(pricer.CheckForNewRequests));
            Logger.WriteLog("OnStart finished. Service running.", false);
            //DBHandler.SendAlertMail("Ice Trade Capture Report Fix Service started.", true, null);
        }

        protected override void OnStop()
        {
            BloombergPricer.PricerRunning = false;
        }
    }
}
