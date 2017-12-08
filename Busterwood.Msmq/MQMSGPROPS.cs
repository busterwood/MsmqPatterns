using System;
using System.Runtime.InteropServices;

namespace BusterWood.Msmq
{
    [StructLayout(LayoutKind.Sequential)]
    internal class MQPROPS
    {
        public const int PROPID_M_BASE = 0;
        public const int PROPID_M_CLASS = (PROPID_M_BASE + 1);                  /* VT_UI2           */
        public const int PROPID_M_MSGID = (PROPID_M_BASE + 2);                  /* VT_UI1|VT_VECTOR */
        public const int PROPID_M_CORRELATIONID = (PROPID_M_BASE + 3);          /* VT_UI1|VT_VECTOR */
        public const int PROPID_M_PRIORITY = (PROPID_M_BASE + 4);               /* VT_UI1           */
        public const int PROPID_M_DELIVERY = (PROPID_M_BASE + 5);               /* VT_UI1           */
        public const int PROPID_M_ACKNOWLEDGE = (PROPID_M_BASE + 6);            /* VT_UI1           */
        public const int PROPID_M_JOURNAL = (PROPID_M_BASE + 7);                /* VT_UI1           */
        public const int PROPID_M_APPSPECIFIC = (PROPID_M_BASE + 8);            /* VT_UI4           */
        public const int PROPID_M_BODY = (PROPID_M_BASE + 9);                   /* VT_UI1|VT_VECTOR */
        public const int PROPID_M_BODY_SIZE = (PROPID_M_BASE + 10);             /* VT_UI4           */
        public const int PROPID_M_LABEL = (PROPID_M_BASE + 11);                 /* VT_LPWSTR        */
        public const int PROPID_M_LABEL_LEN = (PROPID_M_BASE + 12);             /* VT_UI4           */
        public const int PROPID_M_TIME_TO_REACH_QUEUE = (PROPID_M_BASE + 13);   /* VT_UI4           */
        public const int PROPID_M_TIME_TO_BE_RECEIVED = (PROPID_M_BASE + 14);   /* VT_UI4           */
        public const int PROPID_M_RESP_QUEUE = (PROPID_M_BASE + 15);            /* VT_LPWSTR        */
        public const int PROPID_M_RESP_QUEUE_LEN = (PROPID_M_BASE + 16);        /* VT_UI4           */
        public const int PROPID_M_ADMIN_QUEUE = (PROPID_M_BASE + 17);           /* VT_LPWSTR        */
        public const int PROPID_M_ADMIN_QUEUE_LEN = (PROPID_M_BASE + 18);       /* VT_UI4           */
        public const int PROPID_M_VERSION = (PROPID_M_BASE + 19);               /* VT_UI4           */
        public const int PROPID_M_SENDERID = (PROPID_M_BASE + 20);              /* VT_UI1|VT_VECTOR */
        public const int PROPID_M_SENDERID_LEN = (PROPID_M_BASE + 21);          /* VT_UI4           */
        public const int PROPID_M_SENDERID_TYPE = (PROPID_M_BASE + 22);         /* VT_UI4           */
        public const int PROPID_M_PRIV_LEVEL = (PROPID_M_BASE + 23);            /* VT_UI4           */
        public const int PROPID_M_AUTH_LEVEL = (PROPID_M_BASE + 24);            /* VT_UI4           */
        public const int PROPID_M_AUTHENTICATED = (PROPID_M_BASE + 25);         /* VT_UI1           */
        public const int PROPID_M_HASH_ALG = (PROPID_M_BASE + 26);              /* VT_UI4           */
        public const int PROPID_M_ENCRYPTION_ALG = (PROPID_M_BASE + 27);        /* VT_UI4           */
        public const int PROPID_M_SENDER_CERT = (PROPID_M_BASE + 28);           /* VT_UI1|VT_VECTOR */
        public const int PROPID_M_SENDER_CERT_LEN = (PROPID_M_BASE + 29);       /* VT_UI4           */
        public const int PROPID_M_SRC_MACHINE_ID = (PROPID_M_BASE + 30);        /* VT_CLSID         */
        public const int PROPID_M_SENTTIME = (PROPID_M_BASE + 31);              /* VT_UI4           */
        public const int PROPID_M_ARRIVEDTIME = (PROPID_M_BASE + 32);           /* VT_UI4           */
        public const int PROPID_M_DEST_QUEUE = (PROPID_M_BASE + 33);            /* VT_LPWSTR        */
        public const int PROPID_M_DEST_QUEUE_LEN = (PROPID_M_BASE + 34);        /* VT_UI4           */
        public const int PROPID_M_EXTENSION = (PROPID_M_BASE + 35);             /* VT_UI1|VT_VECTOR */
        public const int PROPID_M_EXTENSION_LEN = (PROPID_M_BASE + 36);         /* VT_UI4           */
        public const int PROPID_M_SECURITY_CONTEXT = (PROPID_M_BASE + 37);      /* VT_UI4           */
        public const int PROPID_M_CONNECTOR_TYPE = (PROPID_M_BASE + 38);        /* VT_CLSID         */
        public const int PROPID_M_XACT_STATUS_QUEUE = (PROPID_M_BASE + 39);     /* VT_LPWSTR        */
        public const int PROPID_M_XACT_STATUS_QUEUE_LEN = (PROPID_M_BASE + 40); /* VT_UI4           */
        public const int PROPID_M_TRACE = (PROPID_M_BASE + 41);                 /* VT_UI1           */
        public const int PROPID_M_BODY_TYPE = (PROPID_M_BASE + 42);             /* VT_UI4           */
        public const int PROPID_M_DEST_SYMM_KEY = (PROPID_M_BASE + 43);         /* VT_UI1|VT_VECTOR */
        public const int PROPID_M_DEST_SYMM_KEY_LEN = (PROPID_M_BASE + 44);     /* VT_UI4           */
        public const int PROPID_M_SIGNATURE = (PROPID_M_BASE + 45);             /* VT_UI1|VT_VECTOR */
        public const int PROPID_M_SIGNATURE_LEN = (PROPID_M_BASE + 46);         /* VT_UI4           */
        public const int PROPID_M_PROV_TYPE = (PROPID_M_BASE + 47);             /* VT_UI4           */
        public const int PROPID_M_PROV_NAME = (PROPID_M_BASE + 48);             /* VT_LPWSTR        */
        public const int PROPID_M_PROV_NAME_LEN = (PROPID_M_BASE + 49);         /* VT_UI4           */
        public const int PROPID_M_FIRST_IN_XACT = (PROPID_M_BASE + 50);         /* VT_UI1           */
        public const int PROPID_M_LAST_IN_XACT = (PROPID_M_BASE + 51);          /* VT_UI1           */
        public const int PROPID_M_XACTID = (PROPID_M_BASE + 52);                /* VT_UI1|VT_VECTOR */
        public const int PROPID_M_AUTHENTICATED_EX = (PROPID_M_BASE + 53);      /* VT_UI1           */

        public int propertyCount;
        public IntPtr propertyIdentifiers;
        public IntPtr propertyValues;
        public IntPtr status;
    }
}