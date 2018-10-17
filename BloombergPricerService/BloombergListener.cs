using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Bloomberglp.Blpapi;
using System.Threading;

namespace BloombergPricerService
{
    public class BloombergListener
    {
        public bool SessionRunning = false;

        private Session activeSession;
        private Service referenceService;
        private Bloomberglp.Blpapi.EventHandler eventHandler;

        List<HistoricalRequest> Requests = new List<HistoricalRequest>();
        List<HistoricalRequest> SentRequests = new List<HistoricalRequest>();

        //Queue<HistoricalRequest> Requests = new Queue<HistoricalRequest>();

        List<BBPriceRequestQueue> ImmediateRequests = new List<BBPriceRequestQueue>();

        private HashSet<int> historicalRequestIDSet = new HashSet<int>();


        private static readonly String REF_DATA_SERVICE = "//blp/refdata";
        private static readonly string SERVER_HOST = "localhost";
        private static readonly int SERVER_PORT = 8194;

        public const String HIST_REQ_TYPE = "HistoricalDataRequest";
        public const String REF_REQ_TYPE = "ReferenceDataRequest";
        public const String INTRD_BAR_REQ_TYPE = "IntradayBarRequest";
        public const String INTRD_TICK_REQ_TYPE = "IntradayTickRequest";
        private const String HIST_DATA_RESPONSE = "HistoricalDataResponse";
        private const String REF_DATA_RESPONSE = "ReferenceDataResponse";
        private const String BAR_DATA_RESPONSE = "IntradayBarResponse";
        private const String TICK_DATA_RESPONSE = "IntraDayTickResponse";
        private const String SESSION_STARTED = "SessionStarted";
        private const String SESSION_TERMINATED = "SessionTerminated";
        private const String SESSION_STARTUP_FAILURE = "SessionStartupFailure";
        private const String SESSION_CONN_DOWN = "SessionConnectionDown";
        private const String RESPONSE_ERROR_DB_MSG = "Response Error";
        private const String SECURITY_ERROR_DB_MSG = "Security Error";
        private const String FIELD_ERROR_DB_MSG = "Field Exception";

        private static readonly Name SECURITY_DATA = new Name("securityData");
        private static readonly Name SECURITY = new Name("security");
        private static readonly Name FIELD_DATA = new Name("fieldData");
        private static readonly Name BAR_DATA = new Name("barData");
        private static readonly Name BAR_TICK_DATA = new Name("barTickData");

        private static readonly Name RESPONSE_ERROR = new Name("responseError");
        private static readonly Name SECURITY_ERROR = new Name("securityError");
        private static readonly Name FIELD_EXCEPTIONS = new Name("fieldExceptions");
        private static readonly Name FIELD_ID = new Name("fieldId");
        private static readonly Name ERROR_INFO = new Name("errorInfo");
        private static readonly Name CATEGORY = new Name("category");
        private static readonly Name SUBCATEGORY = new Name("subcategory");
        private static readonly Name MESSAGE = new Name("message");

        private Timer requestBatchTimer;
        Queue<PricesToSave> databaseEntries = new Queue<PricesToSave>();


        public BloombergListener()
        {
            ensureBBSessionRunning();
        }

        static readonly object RequestLock = new object();


        public void SendPriceRequests(List<BBPriceRequestQueue> requests)
        {
            if (SessionRunning)
            {
                Logger.WriteLog("Bloomberg Listener: Sending Current Price Requests. Number of requests: " + requests.Count, false);

                this.ImmediateRequests = requests;
                foreach (BBPriceRequestQueue currentRequest in requests)
                {
                    currentRequest.BBRequest = referenceService.CreateRequest(setRequestType(currentRequest.ServiceType));
                    activeSession.SendRequest(currentRequest.BBRequest, currentRequest.CorrelationID);
                }
                DBHandler.UpdateBloombergPoints(requests.Count);
            }
            else
                ensureBBSessionRunning();
        }

        public void AddHistoricalRequests(List<BBHistoricalRequest> requests)
        {
            if (SessionRunning)
            {

                Logger.WriteLog("Bloomberg Listener: Sending Historical Requests. Number of requests: " + requests.Count, false);

                if (Requests.Count() == 0)
                    StartSchedule();

                List<HistoricalRequest> currentBatch = new List<HistoricalRequest>();
                int? totalPoints = 0;
                foreach (BBHistoricalRequest dbRequest in requests.Where(r => !historicalRequestIDSet.Contains(r.RequestID)))
                {
                    historicalRequestIDSet.Add(dbRequest.RequestID);
                    HistoricalRequest currentRequest = new HistoricalRequest(dbRequest);
                    currentRequest.BBRequest = referenceService.CreateRequest(setRequestType(currentRequest.DataService));
                    currentBatch.Add(currentRequest);
                    totalPoints += currentRequest.Datapoints.HasValue ? currentRequest.Datapoints.Value : 0;
                }

                if (currentBatch.Count > 0)
                    Requests.AddRange(currentBatch);

                DBHandler.UpdateBloombergPoints(totalPoints);
            }
            else
                ensureBBSessionRunning();
        }

        private void StartSchedule()
        {
            try
            {
                requestBatchTimer = (requestBatchTimer == null) ? new System.Threading.Timer(scheduleRequests) : requestBatchTimer;
                requestBatchTimer.Change(15000, 40000);

            }
            catch (Exception ex)
            {
                //Logging.WriteLog("ERROR: RTD_BG : StartReports() --- " + ex.Message);
            }
        }

        private void StopSchedule()
        {
            if (requestBatchTimer != null)
                requestBatchTimer.Change(-1, -1);

        }

        List<HistoricalRequest> currentBatch;
        private void scheduleRequests(object o)
        {
            ensureBBSessionRunning();
            removeCompletedRequests();
            currentBatch = new List<HistoricalRequest>();
            currentBatch.AddRange(Requests.Where(r => !r.SentToBloomberg).Take(100));
            sendHistoricalRequests(currentBatch);

        }

        private void removeCompletedRequests()
        {
            //Monitor.Enter(Requests);
            Requests.RemoveAll(r => r.Completed);
            if (Requests.Count == 0)
            {
                SentRequests.Clear();
                StopSchedule();
            }
        }

        private void ensureBBSessionRunning()
        {
            BloombergProcess.EnsureRunning();

            if (!SessionRunning)
            {
                SessionOptions sessionOptions = new SessionOptions();
                sessionOptions.ServerHost = SERVER_HOST;
                sessionOptions.ServerPort = SERVER_PORT;
                eventHandler = new Bloomberglp.Blpapi.EventHandler(processEvent);
                activeSession = new Session(sessionOptions, eventHandler);

                Logger.WriteLog("Bloomberg Listener: Starting new session.", false);

                activeSession.Start();
                Thread.Sleep(5000);
            }
        }


        private void openReferenceService()
        {
            Logger.WriteLog("Bloomberg Listener: Opening Reference Service.", false);
            SessionRunning = true;
            activeSession.OpenService(REF_DATA_SERVICE);
            referenceService = activeSession.GetService(REF_DATA_SERVICE);
        }

        private void sendHistoricalRequests(IEnumerable<HistoricalRequest> historicalRequests)
        {
            foreach (HistoricalRequest request in historicalRequests)
            {
                activeSession.SendRequest(request.BBRequest, request.CorrelationID);
                request.SentToBloomberg = true;
            }
            SentRequests.AddRange(historicalRequests);
        }


        private string setRequestType(String dataService)
        {
            if (String.Equals(dataService, REF_REQ_TYPE) || dataService.ToLower().Contains("reference"))
                return REF_REQ_TYPE;
            else
            if (String.Equals(dataService, INTRD_BAR_REQ_TYPE) || dataService.ToLower().Contains("bar"))
                return INTRD_BAR_REQ_TYPE;
            else
            if (String.Equals(dataService, INTRD_TICK_REQ_TYPE) || dataService.ToLower().Contains("tick"))
                return INTRD_TICK_REQ_TYPE;
            else
                return HIST_REQ_TYPE;
        }



        private void processEvent(Event bloombergEvent, Session session)
        {
            foreach (Message bloombergMessage in bloombergEvent.GetMessages())
            {
                Element dataResponse = bloombergMessage.AsElement;

                switch (bloombergEvent.Type)
                {
                    case Bloomberglp.Blpapi.Event.EventType.SESSION_STATUS: handleSessionStatus(bloombergMessage); break;
                    case Bloomberglp.Blpapi.Event.EventType.SERVICE_STATUS: break;
                    /*case Bloomberglp.Blpapi.Event.EventType.SUBSCRIPTION_STATUS: break;
                    case Bloomberglp.Blpapi.Event.EventType.SUBSCRIPTION_DATA:
                       break; */

                    case Bloomberglp.Blpapi.Event.EventType.PARTIAL_RESPONSE:
                    case Bloomberglp.Blpapi.Event.EventType.RESPONSE:
                        handleResponse(dataResponse, bloombergMessage);
                        break;

                    case Bloomberglp.Blpapi.Event.EventType.ADMIN:
                    case Bloomberglp.Blpapi.Event.EventType.AUTHORIZATION_STATUS:
                    case Bloomberglp.Blpapi.Event.EventType.REQUEST:
                    case Bloomberglp.Blpapi.Event.EventType.REQUEST_STATUS:
                    case Bloomberglp.Blpapi.Event.EventType.RESOLUTION_STATUS:
                    case Bloomberglp.Blpapi.Event.EventType.TIMEOUT:
                    case Bloomberglp.Blpapi.Event.EventType.TOKEN_STATUS:
                    case Bloomberglp.Blpapi.Event.EventType.TOPIC_STATUS:
                    default:
                        break;

                }
            }
        }

        private void handleSessionStatus(Message message)
        {
            switch (message.MessageType.ToString())
            {
                case SESSION_STARTED:
                    openReferenceService();
                    //sendServiceRequests();
                    break;
                case SESSION_TERMINATED:
                case SESSION_CONN_DOWN:
                case SESSION_STARTUP_FAILURE:
                    Logger.WriteLog("Bloomberg Listener: Termination Message: " + message.MessageType.ToString(), false);
                    SessionRunning = false;
                    break;
            }
        }


        private void handleResponse(Element dataResponse, Message bloombergMessage)
        {
            switch (dataResponse.Name.ToString())
            {
                case HIST_DATA_RESPONSE:
                    handleHistoricalResponse(bloombergMessage);
                    break;
                case REF_DATA_RESPONSE:
                    handleReferenceResponse(bloombergMessage);
                    break;
                case BAR_DATA_RESPONSE:
                    handleBarResponse(bloombergMessage);
                    break;
                case TICK_DATA_RESPONSE:
                    break;
            }
        }

        private void handleResponseError(Element element, CorrelationID reqId, String errorType)
        {
            Element elementError;
            HistoricalRequest request = Requests.Where(r => r.CorrelationID == reqId).FirstOrDefault();
            if (request != null)
            {
                switch (errorType)
                {
                    case RESPONSE_ERROR_DB_MSG:
                        elementError = element.GetElement(RESPONSE_ERROR);
                        setRequestErrorFields(elementError, errorType, request);
                        break;
                    case SECURITY_ERROR_DB_MSG:
                        elementError = element.GetElement(SECURITY_ERROR);
                        setRequestErrorFields(elementError, errorType, request);
                        break;
                    case FIELD_ERROR_DB_MSG:
                        elementError = element.GetElement(FIELD_EXCEPTIONS);
                        for (int i = 0; i < elementError.NumValues; i++)
                        {
                            Element fieldException = elementError.GetValueAsElement(i);
                            string fieldId = fieldException.HasElement(FIELD_ID) ? fieldException.GetElementAsString(FIELD_ID) : String.Empty;
                            if (fieldException.HasElement(ERROR_INFO))
                                setRequestErrorFields(fieldException.GetElement(ERROR_INFO), errorType + " on " + fieldId, request);
                        }
                        break;
                }
            }
            //Logger.WriteLog("Bloomberg Listener: "  + errorType +  " for RequestId: " + reqId.Value, false);

        }

        private void setRequestErrorFields(Element element, string errorInfo, HistoricalRequest request)
        {
            if (element.HasElement(CATEGORY))
                request.ErrorCategory = element.GetElementAsString(CATEGORY);
            if (element.HasElement(SUBCATEGORY))
                request.ErrorSubcategory = element.GetElementAsString(SUBCATEGORY);
            if (element.HasElement(MESSAGE))
                request.ErrorMessage = String.IsNullOrEmpty(request.ErrorMessage) ? errorInfo + ": " + element.GetElementAsString(MESSAGE)
                                                                                        : request.ErrorMessage + ". " + errorInfo + ": " + element.GetElementAsString(MESSAGE);

            request.Error = true;
            request.Completed = true;
        }

        private void handleHistoricalResponse(Message response)
        {
            Logger.WriteLog(response.ToString(), true);
            List<object> priceEntries = null;

            HistoricalRequest historicalRequest = SentRequests.Count > 0 ? SentRequests.Where(r => r.CorrelationID == response.CorrelationID).FirstOrDefault() : null;
            BBPriceRequestQueue priceRequest = ImmediateRequests.Count > 0 ? ImmediateRequests.Where(r => r.CorrelationID == response.CorrelationID).FirstOrDefault() : null;

            try
            {
                if (response.HasElement(RESPONSE_ERROR))
                    handleResponseError(response.AsElement, response.CorrelationID, "Response Error");
                else
                {
                    Element securityDataArray = response.HasElement(SECURITY_DATA) ? response.GetElement(SECURITY_DATA) : null;
                    String security = securityDataArray.HasElement(SECURITY) ? securityDataArray.GetElementAsString(SECURITY) : String.Empty;
                    //int sequenceNumber = securityDataArray.GetElementAsInt32("sequenceNumber");

                    if (securityDataArray.HasElement(SECURITY_ERROR))
                        handleResponseError(securityDataArray, response.CorrelationID, SECURITY_ERROR_DB_MSG);

                    if (securityDataArray.HasElement(FIELD_EXCEPTIONS))
                        handleResponseError(securityDataArray, response.CorrelationID, FIELD_ERROR_DB_MSG);

                    if (securityDataArray.HasElement(FIELD_DATA))
                    {
                        Element fieldDataArray = securityDataArray.GetElement(FIELD_DATA);

                        priceEntries = new List<object>();
                        for (int i = 0; i < fieldDataArray.NumValues; i++)
                        {
                            Element fieldData = fieldDataArray.GetValueAsElement(i);
                            object newEntry = createPriceEntry(fieldData, historicalRequest, security);
                            if (newEntry != null)
                                priceEntries.Add(newEntry);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                if (historicalRequest != null)
                    historicalRequest.ErrorMessage += ex.Message;
                Logger.WriteLog("Exception in handleHistoricalResponse. Ex: " + ex.Message, false);
            }
            finally
            {
                transferToDatabase(historicalRequest, priceEntries);

                if (historicalRequest == null && priceRequest != null)
                    DBHandler.DeleteBBPriceRequest(priceRequest);

            }


        }

        private void handleReferenceResponse(Message response)
        {
            List<object> priceEntries = null;

            HistoricalRequest request = SentRequests.Where(r => r.CorrelationID == response.CorrelationID).FirstOrDefault();
            try
            {
                if (response.HasElement(RESPONSE_ERROR))
                    handleResponseError(response.AsElement, response.CorrelationID, "Response Error");
                else
                {
                    Element securityDataArray = response.HasElement(SECURITY_DATA) ? response.GetElement(SECURITY_DATA) : null;

                    priceEntries = new List<object>();

                    for (int securityIndex = 0; securityIndex < securityDataArray.NumValues; securityIndex++)
                    {
                        Element securityData = securityDataArray.GetValueAsElement(securityIndex);

                        String security = securityData.HasElement(SECURITY) ? securityData.GetElementAsString(SECURITY) : String.Empty;
                        //int sequenceNumber = securityDataArray.GetElementAsInt32("sequenceNumber");

                        if (securityData.HasElement(SECURITY_ERROR))
                            handleResponseError(securityData, response.CorrelationID, SECURITY_ERROR_DB_MSG);

                        if (securityData.HasElement(FIELD_EXCEPTIONS))
                            handleResponseError(securityData, response.CorrelationID, FIELD_ERROR_DB_MSG);

                        if (securityData.HasElement(FIELD_DATA))
                        {
                            Element fieldDataArray = securityData.GetElement(FIELD_DATA);
                            object newEntry = createStaticEntry(fieldDataArray, request, security);
                            if (newEntry != null)
                                priceEntries.Add(newEntry);

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                request.ErrorMessage += ex.Message;
                Logger.WriteLog("Exception in handleReferenceResponse. Ex: " + ex.Message, false);
            }
            finally
            {
                if (priceEntries != null)
                    transferToDatabase(request, priceEntries);
                //    databaseEntries.Enqueue(new PricesToSave(request, priceEntries));
            }
        }


        private void handleBarResponse(Message response)
        {
            List<object> priceEntries = null;

            HistoricalRequest barRequest = Requests.Count > 0 ? Requests.Where(r => r.CorrelationID == response.CorrelationID).FirstOrDefault() : null;

            try
            {
                if (response.HasElement(RESPONSE_ERROR))
                    handleResponseError(response.AsElement, response.CorrelationID, "Response Error");
                else
                {
                    Element barData = response.HasElement(BAR_DATA) ? response.GetElement(BAR_DATA) : null;
                    Element barTickData = barData.HasElement(BAR_TICK_DATA) ? barData.GetElement(BAR_TICK_DATA) : null;

                    if (barTickData != null)
                    {
                        priceEntries = new List<object>();
                        for (int i = 0; i < barTickData.NumValues; i++)
                        {
                            Element bar = barTickData.GetValueAsElement(i);
                            object newEntry = createBarEntry(bar, barRequest, barRequest.Ticker);
                            if (newEntry != null)
                                priceEntries.Add(newEntry);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                barRequest.ErrorMessage += ex.Message;
                Logger.WriteLog("Exception in handleBarResponse. Ex: " + ex.Message, false);
            }
            finally
            {
                if (priceEntries != null && priceEntries.Count > 0)
                    transferToDatabase(barRequest, priceEntries.Where(p => p != null).ToList());
                // databaseEntries .Add(request, priceEntries);
            }
        }


        private void transferToDatabase(HistoricalRequest request, List<object> entries)
        {
            if (request != null)
            {
                request.Completed = true;
                request.ErrorMessage = (request.ErrorMessage != null) ? request.ErrorMessage : request.ErrorMessage;

                if ((request.ErrorMessage == String.Empty || request.ErrorMessage == null) && entries.Count == 0)
                    request.ErrorMessage = "NO DATA";

                request.Error = !String.IsNullOrEmpty(request.ErrorMessage);

                DBHandler.UpdateBBHistoricalRequest(request);
                historicalRequestIDSet.Remove(request.RequestID);
                //HistoricalPricer.HistoricalRequestSet.RemoveWhere(p => p.RequestID == request.RequestID);
            }
            else if (entries != null)
                DBHandler.InsertUpdateHistoricalPrices(entries);

            if (request != null && entries != null && entries.Count > 0)
            {
                if (request.IsReferenceRequest)
                    DBHandler.InsertUpdateStaticData(entries);
                else
                if (request.IsHistoricalRequest)
                    DBHandler.InsertUpdateHistoricalPrices(entries);
                else
                if (request.IsIntradayBarRequest)
                    DBHandler.InsertUpdateIntradayBarData(entries);
                else
                if (request.IsIntradayTickRequest)
                    DBHandler.InsertUpdateIntradayTickData(entries);
            }

        }

        private BBHistoricalPrice createPriceEntry(Element fieldData, HistoricalRequest request, String security)
        {
            if (security == String.Empty && request == null)
                return null;

            BBHistoricalPrice priceEntry = new BBHistoricalPrice();
            priceEntry.BBTicker = security != String.Empty ? security : request.Ticker;
            priceEntry.BB = request != null ? request.BB : priceEntry.BBTicker.Substring(0, priceEntry.BBTicker.IndexOf(" "));
            priceEntry.UpdateTime = DateTime.UtcNow;
            priceEntry.LastUpdatedBy = Environment.MachineName;

            // Should be able to handle any field from request
            Datetime bbDate = fieldData.GetElementAsDate("date");
            priceEntry.PriceDate = fieldData.HasElement("date") ? new DateTime(bbDate.Year, bbDate.Month, bbDate.DayOfMonth) : DateTime.MinValue;
            priceEntry.Last = fieldData.HasElement("PX_LAST") ? Convert.ToDecimal(fieldData.GetElementAsFloat64("PX_LAST")) : new Nullable<decimal>();
            priceEntry.High = fieldData.HasElement("HIGH") ? Convert.ToDecimal(fieldData.GetElementAsFloat64("HIGH")) : new Nullable<decimal>();
            priceEntry.Low = fieldData.HasElement("LOW") ? Convert.ToDecimal(fieldData.GetElementAsFloat64("LOW")) : new Nullable<decimal>();
            priceEntry.Open = fieldData.HasElement("OPEN") ? Convert.ToDecimal(fieldData.GetElementAsFloat64("OPEN")) : new Nullable<decimal>();
            priceEntry.Open_Int = fieldData.HasElement("OPEN_INT") ? fieldData.GetElementAsInt32("OPEN_INT") : new Nullable<int>();
            priceEntry.Volume = fieldData.HasElement("VOLUME") ? fieldData.GetElementAsInt32("VOLUME") : new Nullable<int>();
            priceEntry.PX_DISC_BID = fieldData.HasElement("PX_DISC_BID") ? Convert.ToDecimal(fieldData.GetElementAsFloat64("PX_DISC_BID")) : new Nullable<decimal>();

            if (priceEntry.Last == new Nullable<decimal>() && priceEntry.High == new Nullable<decimal>() && priceEntry.Low == new Nullable<decimal>() && priceEntry.Open == new Nullable<decimal>() &&
                priceEntry.Open_Int == new Nullable<int>() && priceEntry.Volume == new Nullable<int>() && priceEntry.PX_DISC_BID == new Nullable<decimal>())
                return null;
            else
                return priceEntry;
        }

        private BBReferenceData createStaticEntry(Element fieldData, HistoricalRequest request, String security)
        {
            BBReferenceData staticEntry = new BBReferenceData();
            staticEntry.BBTicker = security != String.Empty ? security : request.Ticker;
            staticEntry.BB = request.BB;
            staticEntry.UpdateTime = DateTime.UtcNow;
            staticEntry.LastUpdatedBy = Environment.MachineName;


            if (fieldData.HasElement("FUT_NOTICE_FIRST"))
            {
                Datetime date = fieldData.GetElementAsDate("FUT_NOTICE_FIRST");
                staticEntry.FUT_NOTICE_FIRST = new DateTime(date.Year, date.Month, date.DayOfMonth);
            }
            if (fieldData.HasElement("FUT_LAST_TRADE_DT"))
            {
                Datetime date = fieldData.GetElementAsDate("FUT_LAST_TRADE_DT");
                staticEntry.FUT_LAST_TRADE_DT = new DateTime(date.Year, date.Month, date.DayOfMonth);
            }

            //staticEntry.FUT_NOTICE_FIRST = fieldData.HasElement("FUT_NOTICE_FIRST") ? new Datetime

            /*switch (fieldData.Name?.ToString())
            {
                case "FUT_NOTICE_FIRST":
                    Datetime dateNotice = fieldData.GetValueAsDate();
                    staticEntry.FUT_NOTICE_FIRST = new DateTime(dateNotice.Year, dateNotice.Month, dateNotice.DayOfMonth);
                    break;
                case "FUT_LAST_TRADE_DT":
                    Datetime dateLast = fieldData.GetValueAsDate();
                    staticEntry.FUT_LAST_TRADE_DT = new DateTime(dateLast.Year, dateLast.Month, dateLast.DayOfMonth);
                    break;

            }*/

            return staticEntry;

        }

        private BBIntradayBarPrice createBarEntry(Element barData, HistoricalRequest request, String security)
        {
            BBIntradayBarPrice barPrice = new BBIntradayBarPrice();
            barPrice.BBTicker = security != String.Empty ? security : request.Ticker;
            barPrice.BB = request.BB;

            Datetime bbDate = barData.GetElementAsDate("time");
            barPrice.PriceDate = new DateTime(bbDate.Year, bbDate.Month, bbDate.DayOfMonth);
            barPrice.PriceTime = new DateTime(bbDate.Year, bbDate.Month, bbDate.DayOfMonth, bbDate.Hour, bbDate.Minute, bbDate.Second);
            //barPrice.UpdateTime = DateTime.UtcNow;
            barPrice.LastUpdatedBy = Environment.MachineName;

            barPrice.Open = barData.HasElement("open") ? Convert.ToDecimal(barData.GetElementAsFloat64("open")) : new Nullable<decimal>();
            barPrice.High = barData.HasElement("high") ? Convert.ToDecimal(barData.GetElementAsFloat64("high")) : new Nullable<decimal>();
            barPrice.Low = barData.HasElement("low") ? Convert.ToDecimal(barData.GetElementAsFloat64("low")) : new Nullable<decimal>();
            barPrice.Close = barData.HasElement("close") ? Convert.ToDecimal(barData.GetElementAsFloat64("close")) : new Nullable<decimal>();

            barPrice.NumEvents = barData.HasElement("numEvents") ? barData.GetElementAsInt32("numEvents") : new Nullable<int>();
            barPrice.Volume = barData.HasElement("volume") ? barData.GetElementAsInt64("volume") : new Nullable<long>();

            return barPrice;
        }


    }

}
