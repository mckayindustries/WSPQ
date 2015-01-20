using System;
using System.Collections.Generic;
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
        private Dictionary<String, WatchedPrinter> watchedPrinters;

        public PrinterWatcher()
        {
            watchedPrinters = new Dictionary<String, WatchedPrinter>();
            t = new Thread(new ThreadStart(ThreadLoop));
        }

        public void AddPrinter(string name, string shareName)
        {
            watchedPrinters.Add(name, new WatchedPrinter(name, ShareToAddress(shareName)));
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
                    if (!kv.Value.isPrinting)
                        continue;
                }
                Thread.Sleep(1000);
            }
        }

        public bool IsPrinterReady(string name)
        {
            return watchedPrinters[name].IsReady();
        }

        private string ShareToAddress(string shareName)
        {
            return String.Format("{0}.{1}.{2}", shareName[1] == 'E' ? "148.60" : "129.20", shareName.Substring(2, 3), shareName.Substring(5, 3));
        }
    }

    class WatchedPrinter
    {
        public string name;
        public string address;
        public bool isPrinting;
        private SNMPWrapper snmp;

        public WatchedPrinter(string name, string address)
        {
            this.name = name;
            this.address = address;
            snmp = new SNMPWrapper(address);
        }

        public bool IsReady()
        {
            return snmp.IsReady();
        }
    }
}
