using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SnmpSharpNet;

namespace WSPQ
{
    class SNMPWrapper
    {
        private static string PAGECOUNT_OID = ".1.3.6.1.2.1.43.10.2.1.4.1.1";
        private static string STATUS_OID = ".1.3.6.1.2.1.25.3.5.1.1.1";
        private SimpleSnmp snmp;
        public int pageCount;
        public bool isReady = false;

        public SNMPWrapper(string address)
        {
            snmp = new SimpleSnmp(address, "public");
        }
        
        public int GetPrintedCount()
        {
            Dictionary<Oid, AsnType> res = snmp.Get(SnmpVersion.Ver1, new String[] { PAGECOUNT_OID });

            if (res != null)
            {
                foreach (KeyValuePair<Oid, AsnType> kv in res)
                {
                    if (kv.Key.ToString() == PAGECOUNT_OID)
                    {
                        pageCount = int.Parse(kv.Value.ToString());
                        return pageCount;
                    }
                }
            }
            return -1;
        }

        public bool IsReady()
        {
            Dictionary<Oid, AsnType> res = snmp.Get(SnmpVersion.Ver1, new String[] { STATUS_OID, PAGECOUNT_OID });

            if (res != null)
            {
                foreach (KeyValuePair<Oid, AsnType> kv in res)
                {
                    string oid = kv.Key.ToString();
                    if (oid == STATUS_OID)
                        isReady = int.Parse(kv.Value.ToString()) != 3;
                    else if (oid == PAGECOUNT_OID)
                        pageCount = int.Parse(kv.Value.ToString());
                }
                return isReady;
            }
            return true; // ?
        }
    }
}
