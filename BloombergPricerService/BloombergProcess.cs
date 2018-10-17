using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace BloombergPricerService
{
    public static class BloombergProcess
    {
        private const string comProcessName = "bbcomm";
        private const string comProcessExecutablePath = @"C:\blp\DAPI\bbcomm.exe";

        public static void StartComProcess()
        {
            ProcessStartInfo processStartInfo = new ProcessStartInfo { CreateNoWindow = true, FileName = comProcessExecutablePath, WindowStyle = ProcessWindowStyle.Hidden };
            Process.Start(processStartInfo);
        }

        public static void EndComProcess()
        {
            using (Process process = Process.GetProcessesByName(comProcessName).FirstOrDefault())
            {
                if (process != null)
                {
                    try { process.Kill(); }
                    catch (System.ComponentModel.Win32Exception) { throw new Exception("Unable to kill " + comProcessName + " process!"); }
                    catch (Exception) { }
                }

            }
        }

        public static bool IsRunning
        {
            get
            {
                return Process.GetProcessesByName(comProcessName).FirstOrDefault() != null;
            }
        }

        public static void EnsureRunning()
        {
            if (!IsRunning)
            {
                try
                {
                    StartComProcess();
                    Thread.Sleep(1000);
                }
                catch (Exception exception)
                {
                    if (exception.HResult == -2147467259) // The system cannot find the file specified
                        throw new Exception("Bloomberg BBCom.exe file is not where it is expected, perhaps due to an updated version of Bloomberg.  Please contact the developers.");
                    else
                        throw exception;
                }
            }

            if (!IsRunning)
            {
                try
                {
                    EndComProcess();
                    StartComProcess();
                    Thread.Sleep(1000);
                }
                catch (Exception exception)
                {
                    throw new Exception("Cannot open Bloomberg.  Please seek assistance from development", exception);
                }
            }

        }
    }
}
