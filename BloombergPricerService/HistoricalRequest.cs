using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bloomberglp.Blpapi;

namespace BloombergPricerService
{
    public class PricesToSave
    {
        public HistoricalRequest Request { get; }
        public List<object> Prices { get; }
        public PricesToSave(HistoricalRequest request, List<object> prices)
        {
            Request = request;
            Prices = prices;
        }
    }

    public partial class BBPriceRequestQueue
    {
        public CorrelationID CorrelationID;

        private Request bbRequest;
        public Request BBRequest
        {
            get { return bbRequest; }
            set { setBBRequest(value); }
        }

        public BBPriceRequestQueue(BBPriceRequestQueue request)
        {
            this.RequestID = request.RequestID;
            this.CorrelationID = new CorrelationID(request.RequestID);
            this.HostReserved = request.HostReserved;
            this.BBTicker = request.BBTicker;
            this.ServiceType = request.ServiceType;
            this.RequestTS = request.RequestTS;
            this.RequestedBy = request.RequestedBy;
            this.Fields = request.Fields;
        }

        private void setBBRequest(Request request)
        {
            this.bbRequest = request;
            //string Fields = "PX_LAST";

            this.bbRequest.Append("securities", this.BBTicker);
            //foreach (String field in Fields.Split(',').ToList())
            this.bbRequest.Append("fields", this.Fields);

            this.bbRequest.Set("startDate", DateTime.Today.ToString("yyyyMMdd"));
            this.bbRequest.Set("endDate", DateTime.Today.ToString("yyyyMMdd"));


            // bars - 1 event per request

        }
    }
    public class HistoricalRequest : BBHistoricalRequest
    {
        public bool IsReferenceRequest { get { return String.Equals(this.DataService.ToUpper(), BloombergListener.REF_REQ_TYPE.ToUpper()); } }
        public bool IsHistoricalRequest { get { return String.Equals(this.DataService.ToUpper(), BloombergListener.HIST_REQ_TYPE.ToUpper()); } }
        public bool IsIntradayTickRequest { get { return String.Equals(this.DataService.ToUpper(), BloombergListener.INTRD_TICK_REQ_TYPE.ToUpper()); } }
        public bool IsIntradayBarRequest { get { return String.Equals(this.DataService.ToUpper(), BloombergListener.INTRD_BAR_REQ_TYPE.ToUpper()); } }

        private bool requestSentToBloomberg = false;
        public bool SentToBloomberg { get { return requestSentToBloomberg; } set { requestSentToBloomberg = value; } }

        public CorrelationID CorrelationID;

        private Request bbRequest;
        public Request BBRequest
        {
            get { return bbRequest; }
            set { setBBRequest(value); }
        }

        // EventTypes: Trade, Bid, Ask, Bid_best, Ask_best, Mid_price, At_Trade, Best_bid, Best_ask

        private void setBBRequest(Request request)
        {
            this.bbRequest = request;

            if (IsHistoricalRequest || IsReferenceRequest)
            {
                this.bbRequest.Append("securities", Ticker);
                foreach (String field in Fields.Split(',').ToList())
                    this.bbRequest.Append("fields", field);
            }

            if (IsHistoricalRequest)
            {
                this.bbRequest.Set("startDate", StartDate.HasValue ? StartDate.Value.ToString("yyyyMMdd") : DateTime.Today.ToString("yyyyMMdd"));
                this.bbRequest.Set("endDate", EndDate.HasValue ? EndDate.Value.ToString("yyyyMMdd") : DateTime.Today.ToString("yyyyMMdd"));
            }

            if (IsIntradayBarRequest || IsIntradayTickRequest)
            {
                Datetime startDateTime = new Datetime(StartDate.Value.Year, StartDate.Value.Month, StartDate.Value.Day, StartDate.Value.Hour, StartDate.Value.Minute, StartDate.Value.Second, 0);
                Datetime endDateTime = new Datetime(EndDate.Value.Year, EndDate.Value.Month, EndDate.Value.Day, EndDate.Value.Hour, EndDate.Value.Minute, EndDate.Value.Second, 0);

                this.bbRequest.Set("security", Ticker);
                this.bbRequest.Set("startDateTime", startDateTime);
                this.bbRequest.Set("endDateTime", endDateTime);
                this.bbRequest.Set("eventType", "TRADE");
            }

            if (IsIntradayBarRequest)
                this.bbRequest.Set("interval", this.BarInterval.HasValue ? this.BarInterval.Value : 1);


            // bars - 1 event per request

        }

        public HistoricalRequest(BBHistoricalRequest request) //(int requestID, string bb,  string bbTicker, DateTime? startDate, DateTime? endDate, String dataService, String fields, int? barInterval, bool completed, bool error, string errorCategory, string errorSubcategory, string errorMessage, int dataPoints)
        {
            this.CorrelationID = new CorrelationID(request.RequestID);

            this.RequestID = request.RequestID;
            this.BB = request.BB;
            this.Ticker = request.Ticker;
            this.StartDate = request.StartDate;
            this.EndDate = request.EndDate;
            this.DataService = request.DataService;
            this.Fields = request.Fields;
            this.Datapoints = request.Datapoints;  // Helper.CountBusinessDays(startDate, endDate) * fields.Split(',').Count();
            this.BarInterval = request.BarInterval;
            this.Completed = request.Completed;
            this.Error = request.Error;
            this.ErrorCategory = request.ErrorCategory == null ? String.Empty : request.ErrorCategory;
            this.ErrorSubcategory = request.ErrorSubcategory == null ? String.Empty : request.ErrorSubcategory;
            this.ErrorMessage = request.ErrorMessage == null ? String.Empty : request.ErrorMessage;

            //HistoricalPricer.HistoricalRequestSet.Add(request);
        }


    }

}
