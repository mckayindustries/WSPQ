using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PrinterQueueWatch;

namespace WSPQ
{
    class Program
    {
        static void Main(string[] args)
        {
            PrintMonitor pm = new PrintMonitor();
            pm.MonitorAllPrinters();
            //PrintersWrapper pWrap = new PrintersWrapper();
            //pWrap.t();
        }
    }
}
