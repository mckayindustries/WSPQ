using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Printing;
using System.Threading;

namespace WSPQ
{
    class PrintersWrapper
    {
        PrintQueueCollection pQueues;
        
        public PrintersWrapper()
        {
            PrintServer pServer = new PrintServer();
            pQueues = pServer.GetPrintQueues();
        }

        public PrintSystemJobInfo _GetJob(int id)
        {
            foreach (PrintQueue pQueue in pQueues)
            {
                PrintSystemJobInfo jInfo = pQueue.GetJob(id);
                if(jInfo != null)
                    return jInfo;
            }
            return null;
        }

        public static PrintSystemJobInfo GetJob(int id)
        {
            PrintServer pServer = new PrintServer("localhost");
            PrintQueueCollection pQueues = pServer.GetPrintQueues();
            foreach (PrintQueue pQueue in pQueues)
            {
                PrintSystemJobInfo jInfo = pQueue.GetJob(id);
                if (jInfo != null)
                    return jInfo;
            }
            return null;
        }
    }
}
