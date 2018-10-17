using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Bloomberglp.Blpapi;

namespace BloombergPricerService
{
    public static class Logger
    {
        //private static List<String> fileNames = new List<string>() { GetFileName("BBMessages"), GetFileName("PricerActivity") };

        private static string fileNameBB = GetFileName("BBMessages");
        private static string fileNamePricer = GetFileName("PricerActivity");
        private static ReaderWriterLockSlim lock_ = new ReaderWriterLockSlim();
        public static bool Enabled = true;

        public static string GetFileName(string fileType)
        {
            string dir = "S:\\IT_Open\\BBPricer\\Logs";
            return Path.Combine(dir, Environment.MachineName + "_" + fileType + "_" + DateTime.Now.ToString("yyyyMMdd[HHmm]") + ".txt");
        }

        public static void CreateLoggingFile()
        {
            try
            {

                if (!File.Exists(fileNameBB))
                {
                    FileStream myFile = File.Create(fileNameBB);
                    myFile.Close();
                }
                if (!File.Exists(fileNamePricer))
                {
                    FileStream myFile = File.Create(fileNamePricer);
                    myFile.Close();
                }

            }
            catch (UnauthorizedAccessException)
            {
                Enabled = false;
            }
        }

        public static void WriteLog(string logText, bool isBBMessage)
        {
            if (Enabled)
            {
                lock_.EnterWriteLock();
                try
                {
                    string timeStamp = DateTime.UtcNow.ToString("HH:mm:ss");
                    string fileName = fileNamePricer;
                    //if (!isBBMessage)
                    Console.WriteLine(timeStamp + logText);
                    using (StreamWriter streamWriter = File.AppendText(fileName))
                    {
                        lock (streamWriter)
                        {
                            streamWriter.WriteLine(timeStamp + "  -  " + logText);
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    Enabled = false;
                }
                finally
                {
                    lock_.ExitWriteLock();
                }
            }
        }

        public static void WritePriceToFile(Event.EventType bloombergEventType, Message bloombergMessage, bool isBBMessage)
        {
            lock_.EnterWriteLock();
            try
            {
                string timeStamp = DateTime.UtcNow.ToString("HH:mm:ss");
                string fileName = fileNameBB;
                //Console.WriteLine(timeStamp + ":" + bloombergEventType + ":" + bloombergMessage.ToString());
                using (StreamWriter streamWriter = File.AppendText(fileName))
                {
                    lock (streamWriter)
                    {
                        streamWriter.WriteLine(timeStamp + "-" + bloombergEventType + "(" + bloombergMessage.ToString() + ")");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {

            }
            finally
            {
                lock_.ExitWriteLock();
            }
        }

    }

}
