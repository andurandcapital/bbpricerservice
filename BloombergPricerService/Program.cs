using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace BloombergPricerService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        static void Main()
        {
#if DEBUG
            BloombergPricer service = new BloombergPricer();
            service.OnDebug();
#else
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new BloombergPricer()
            };
            ServiceBase.Run(ServicesToRun);
#endif
        }
    }
}
