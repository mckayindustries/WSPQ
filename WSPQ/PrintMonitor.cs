using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PrinterQueueWatch;
using System.Drawing.Printing;
using System.Printing;

namespace WSPQ
{
    class PrintMonitor
    {
        private StreamWriter log;
        private readonly object logSyncLock = new Object();
        private int printCount = 0;
        private int deleteCount = 0;
        private HttpWrapper http;
        private PrinterWatcher watcher;

        public void MonitorAllPrinters()
        {
            String[] printers = GetMonitorablePrinters();

            if (printers.Length.Equals(0))
            {
                Console.WriteLine("No monitorable printers");
                Environment.Exit(0);
            }

            Console.WriteLine(".Start");
            PrinterMonitorComponent pMon = new PrinterMonitorComponent();
            pMon.MonitorJobSetEvent = false;

            http = new HttpWrapper();
            watcher = new PrinterWatcher(http);

            foreach (string printer in printers)
            {
                Console.WriteLine(printer);
                pMon.AddPrinter(printer);
                watcher.AddPrinter(printer, pMon.get_PrinterInformation(printer));
            }

            // JobAdded may not fire correctly, assume it'll be written at some point before deletion
            pMon.JobAdded += this.OnJobUpdate;
            pMon.JobWritten += this.OnJobUpdate;
            pMon.JobDeleted += this.OnJobDelete;
            watcher.JobEnded += this.OnJobEnded;

            watcher.Start();
            log = new StreamWriter("printjob.log", true, Encoding.Default);
            log.AutoFlush = true;
            log.WriteLine("Started @ {0}", DateTime.Now.ToString());

            Console.WriteLine(".Monit");
            Console.ReadKey();
            lock (logSyncLock)
            {
                log.WriteLine("Ended after {0} prints & {1} cancels @ {2}", printCount, deleteCount, DateTime.Now.ToString());
                log.WriteLine();
                log.Close();
            }
            Console.WriteLine(".Ended");
            watcher.Stop();
            pMon.Disconnect();
        }

        private void OnJobUpdate(object sender, PrintJobEventArgs e)
        {
            PrintJob j = e.PrintJob;
            // Ignore unknown print jobs (shouldn't happen)
            if (j == null)
            {
                lock (logSyncLock) { log.WriteLine("{0} {1}", e.EventTime, e.EventType); }
                return;
            }

            string output = String.Format("{0} {1} :{2}: {3} [{4}] {5} {6}*[{7}/{8}]",
                    e.EventTime, e.EventType, j.JobId, j.PrinterName, j.UserName,
                    j.Document, j.Copies, j.PagesPrinted, j.TotalPages);
            lock (logSyncLock) { log.WriteLine(output); }

            int pages = j.TotalPages > 0 ? j.TotalPages : 1; // Assume 1 page when spooler says 0

            if (watcher.EnqueueAwaitingJob(j))
            {
                Console.WriteLine(String.Format("Printer {0} : Added job {1}", j.PrinterName, j.JobId));
                j.Paused = true;
            }

            if (!http.CanPrint(j.UserName, pages * j.Copies))
                DeleteJob(j);
        }

        private void OnJobDelete(object sender, PrintJobEventArgs e)
        {
            PrintJob j = e.PrintJob;
            // Ignore unknown print jobs (shouldn't happen)
            if (j == null)
            {
                lock (logSyncLock) { log.WriteLine("{0} {1}", e.EventTime, e.EventType); }
                return;
            }

            string output = String.Format("{0} {1} :{2}: {3} [{4}] {5} {6}*[{7}/{8}]",
                    e.EventTime, e.EventType, j.JobId, j.PrinterName, j.UserName,
                    j.Document, j.Copies, j.PagesPrinted, j.TotalPages);
            lock (logSyncLock) { log.WriteLine(output); }

            http.HasPrinted(j.JobId, j.UserName, j.PrinterName, j.Document, j.PagesPrinted);
        }

        private void OnJobEnded(object sender, PrintJobEndArgs e)
        {
            Console.WriteLine(String.Format("OnJobEnded : {0} {1} => {2}", e.jobId, e.pageCount, e.truePageCount));
            //e.jobId;
            //e.pageCount;
            //update pagecount if necessary
        }

        private void DeleteJob(PrintJob j)
        {
            try
            {
                j.Delete();
                Console.WriteLine("Canceled Job {0}", j.JobId);
                deleteCount++;
            }
            catch (Exception)
            {
                Console.WriteLine("Cannot cancel Job {0}", j.JobId);
            }
        }

        protected String[] GetMonitorablePrinters()
        {
            // Remove unmonitorable printers
            String[] ignoredPrinters = {
                "PDFCreator",
                "Microsoft XPS Document Writer",
                "Fax",
                "Envoyer à OneNote 2010"
            };
            List<String> monitoredPrinters = new List<String>();

            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                if (!ignoredPrinters.Contains(printer))
                    monitoredPrinters.Add(printer);
            }
            return monitoredPrinters.ToArray();
        }
    }
}
