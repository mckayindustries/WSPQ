using PrinterQueueWatch;
using SnmpSharpNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WSPQ
{
    class PrinterWatcher
    {
        private bool threadRunning = true;
        private Thread t;
        private HttpWrapper http;
        private Dictionary<String, WatchedPrinter> watchedPrinters;
        public delegate void JobEndedEvent(PrinterWatcher w, PrintJobEndArgs e);
        public event JobEndedEvent JobEnded;

        public PrinterWatcher(HttpWrapper http)
        {
            watchedPrinters = new Dictionary<String, WatchedPrinter>();
            this.http = http;
            t = new Thread(new ThreadStart(ThreadLoop));
        }

        public void AddPrinter(string name, PrinterInformation printerInfo)
        {
            if (!string.IsNullOrWhiteSpace(printerInfo.ServerName))
            {
                var printerPath = Path.Combine(printerInfo.ServerName, printerInfo.ShareName);
                watchedPrinters.Add(printerInfo.ShareName, new WatchedPrinter(name, printerInfo.PortName));
            }
        }

        public void Start()
        {
            t.Start();
        }

        public void Stop()
        {
            threadRunning = false;
        }

        public void ThreadLoop()
        {
            while (threadRunning)
            {
                foreach (KeyValuePair<String, WatchedPrinter> kv in watchedPrinters)
                {
                    if (!threadRunning)
                        break;

                    WatchedPrinter p = kv.Value;

                    if (p.awaitingJobs.Count == 0) // Queue is empty
                        continue;

                    if (!p.UpdateStatusAndPageCount()) // Cannot update printer, probably can't reach it
                    {
                        Console.WriteLine(String.Format("|{0} is likely offline ?", p.name));
                        continue;
                    }

                    Console.WriteLine(String.Format("|{0} : {1} - {2} job ({3}/{4})", p.name, p.status.ToString(), p.awaitingJobs.Count, p.runningJobId, p.isSpittingPaper ? "spit" : ""));

                    if (p.status == SnmpStatus.IDLE && !p.hasRunningJob) // Nothing running and printer is ready, let's print
                    {
                        p.RunFirstAwaitingJob();
                        Console.WriteLine(String.Format("|{0} : RunJob ({1})", p.name, p.runningJobId));
                    }

                    if (p.status == SnmpStatus.PRINTING && p.hasRunningJob) // Job is running and printer is doing something, aknowledge printing
                    {
                        //Console.WriteLine(String.Format("Printer {0} : spitPaper ({1})", p.name, p.runningJobId));
                        p.isSpittingPaper = true;
                    }

                    if (p.status == SnmpStatus.IDLE && p.hasRunningJob && p.isSpittingPaper) // Printer was running our job, spat papper and now is ready
                    {
                        Console.WriteLine(String.Format("|{0} : Done ({1})", p.name, p.runningJobId));
                        p.isSpittingPaper = false;

                        PrintJobEndArgs e = new PrintJobEndArgs();
                        e.jobId = p.runningJobId;
                        e.truePageCount = p.pageCount - p.pageCountBefore;
                        e.pageCount = p.currentPrintJob.PagesPrinted;
                        JobEnded(this, e);

                        p.JobEnded();
                    }
                }
                Thread.Sleep(1000);
            }
        }

        public bool EnqueueAwaitingJob(PrintJob job)
        {
            WatchedPrinter printer;
            if (watchedPrinters.TryGetValue(job.PrinterName, out printer))
            {
                return true;
                return printer.AddAwaitingJob(job);
            }
            return false;
        }

        private string ShareToAddress(string shareName)
        {
            return shareName;
            //return String.Format("{0}.{1}.{2}", shareName[1] == 'E' ? "148.60" : "129.20", shareName.Substring(2, 3).TrimStart('0'), shareName.Substring(5, 3).TrimStart('0'));
        }
    }

    class WatchedPrinter
    {
        private static string PAGECOUNT_OID = "1.3.6.1.2.1.43.10.2.1.4.1.1";
        private static string STATUS_OID = "1.3.6.1.2.1.25.3.5.1.1.1";
        
        public string name;
        public string address;
        private SimpleSnmp snmp;
        public SnmpStatus status = SnmpStatus.UNKNOWN;

        public Dictionary<int, PrintJob> awaitingJobs;
        public PrintJob currentPrintJob;
        public int runningJobId = 0;
        public bool hasRunningJob = false;
        public bool isSpittingPaper = false;
        public int pageCount;
        public int pageCountBefore;

        public WatchedPrinter(string name, string address)
        {
            this.name = name;
            this.address = address;
            snmp = new SimpleSnmp(address, "public");
            awaitingJobs = new Dictionary<int, PrintJob>();
        }

        public bool AddAwaitingJob(PrintJob job)
        {
            if (!awaitingJobs.ContainsKey(job.JobId))
            {
                job.Paused = true;
                awaitingJobs.Add(job.JobId, job);
                return true;
            }
            return false;
        }

        public void RunFirstAwaitingJob()
        {
            KeyValuePair<int, PrintJob> kv = awaitingJobs.First();
            currentPrintJob = kv.Value;

            if(currentPrintJob.Deleted || currentPrintJob.Deleting)
            {
                awaitingJobs.Remove(currentPrintJob.JobId);
                return;
            }

            currentPrintJob.Paused = false;
            runningJobId = kv.Key;

            hasRunningJob = true;
            pageCountBefore = pageCount;
        }

        public void JobEnded()
        {
            awaitingJobs.Remove(runningJobId);
            runningJobId = 0;
            currentPrintJob = null;
            hasRunningJob = false;
        }

        public bool UpdateStatusAndPageCount()
        {
            Dictionary<Oid, AsnType> res = snmp.Get(SnmpVersion.Ver1, new String[] { STATUS_OID, PAGECOUNT_OID });

            if (res != null)
            {
                foreach (KeyValuePair<Oid, AsnType> kv in res)
                {
                    string oid = kv.Key.ToString();
                    if (oid == STATUS_OID)
                        status = (SnmpStatus)Enum.Parse(typeof(SnmpStatus), kv.Value.ToString());
                    else if (oid == PAGECOUNT_OID)
                        pageCount = int.Parse(kv.Value.ToString());
                }
                return true;
            }
            return false;
        }
    }

    class PrintJobEndArgs : EventArgs
    {
        public int jobId;
        public int truePageCount;
        public int pageCount;
    }

    enum SnmpStatus
    {
        ERROR = 1,
        UNKNOWN = 2,
        IDLE = 3,
        PRINTING = 4,
        WARMUP = 5
    }
}
