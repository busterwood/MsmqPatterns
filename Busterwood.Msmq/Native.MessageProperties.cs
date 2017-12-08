using System;
using System.Runtime.InteropServices;

namespace BusterWood.Msmq
{
    // messsage properties
    partial class Native
    {
        public const int MESSAGE_PROPID_BASE = 0;
        public const int MESSAGE_PROPID_ACKNOWLEDGE = (MESSAGE_PROPID_BASE + 6);            /* VT_UI1           */
        public const int MESSAGE_PROPID_ADMIN_QUEUE = (MESSAGE_PROPID_BASE + 17);           /* VT_LPWSTR        */
        public const int MESSAGE_PROPID_ADMIN_QUEUE_LEN = (MESSAGE_PROPID_BASE + 18);       /* VT_UI4           */
        public const int MESSAGE_PROPID_APPSPECIFIC = (MESSAGE_PROPID_BASE + 8);            /* VT_UI4           */
        public const int MESSAGE_PROPID_ARRIVEDTIME = (MESSAGE_PROPID_BASE + 32);           /* VT_UI4           */
        public const int MESSAGE_PROPID_AUTHENTICATED = (MESSAGE_PROPID_BASE + 25);         /* VT_UI1           */
        public const int MESSAGE_PROPID_AUTH_LEVEL = (MESSAGE_PROPID_BASE + 24);            /* VT_UI4           */
        public const int MESSAGE_PROPID_BODY = (MESSAGE_PROPID_BASE + 9);                   /* VT_UI1|VT_VECTOR */
        public const int MESSAGE_PROPID_BODY_SIZE = (MESSAGE_PROPID_BASE + 10);             /* VT_UI4           */
        public const int MESSAGE_PROPID_BODY_TYPE = (MESSAGE_PROPID_BASE + 42);             /* VT_UI4           */
        public const int MESSAGE_PROPID_CLASS = (MESSAGE_PROPID_BASE + 1);                  /* VT_UI2           */
        public const int MESSAGE_PROPID_CONNECTOR_TYPE = (MESSAGE_PROPID_BASE + 38);        /* VT_CLSID         */
        public const int MESSAGE_PROPID_CORRELATIONID = (MESSAGE_PROPID_BASE + 3);          /* VT_UI1|VT_VECTOR */
        public const int MESSAGE_PROPID_DELIVERY = (MESSAGE_PROPID_BASE + 5);               /* VT_UI1           */
        public const int MESSAGE_PROPID_DEST_QUEUE = (MESSAGE_PROPID_BASE + 33);            /* VT_LPWSTR        */
        public const int MESSAGE_PROPID_DEST_QUEUE_LEN = (MESSAGE_PROPID_BASE + 34);        /* VT_UI4           */
        public const int MESSAGE_PROPID_DEST_SYMM_KEY = (MESSAGE_PROPID_BASE + 43);         /* VT_UI1|VT_VECTOR */
        public const int MESSAGE_PROPID_DEST_SYMM_KEY_LEN = (MESSAGE_PROPID_BASE + 44);     /* VT_UI4           */
        public const int MESSAGE_PROPID_ENCRYPTION_ALG = (MESSAGE_PROPID_BASE + 27);        /* VT_UI4           */
        public const int MESSAGE_PROPID_EXTENSION = (MESSAGE_PROPID_BASE + 35);             /* VT_UI1|VT_VECTOR */
        public const int MESSAGE_PROPID_EXTENSION_LEN = (MESSAGE_PROPID_BASE + 36);         /* VT_UI4           */
        public const int MESSAGE_PROPID_FIRST_IN_XACT = (MESSAGE_PROPID_BASE + 50);  /* VT_UI1           */
        public const int MESSAGE_PROPID_HASH_ALG = (MESSAGE_PROPID_BASE + 26);              /* VT_UI4           */
        public const int MESSAGE_PROPID_JOURNAL = (MESSAGE_PROPID_BASE + 7);                /* VT_UI1           */
        public const int MESSAGE_PROPID_LABEL = (MESSAGE_PROPID_BASE + 11);                 /* VT_LPWSTR        */
        public const int MESSAGE_PROPID_LABEL_LEN = (MESSAGE_PROPID_BASE + 12);             /* VT_UI4           */
        public const int MESSAGE_PROPID_LAST_IN_XACT = (MESSAGE_PROPID_BASE + 51);  /* VT_UI1           */
        public const int MESSAGE_PROPID_MSGID = (MESSAGE_PROPID_BASE + 2);                  /* VT_UI1|VT_VECTOR */
        public const int MESSAGE_PROPID_PRIORITY = (MESSAGE_PROPID_BASE + 4);               /* VT_UI1           */
        public const int MESSAGE_PROPID_PRIV_LEVEL = (MESSAGE_PROPID_BASE + 23);            /* VT_UI4           */
        public const int MESSAGE_PROPID_PROV_NAME = (MESSAGE_PROPID_BASE + 48);             /* VT_LPWSTR        */
        public const int MESSAGE_PROPID_PROV_NAME_LEN = (MESSAGE_PROPID_BASE + 49);         /* VT_UI4           */
        public const int MESSAGE_PROPID_PROV_TYPE = (MESSAGE_PROPID_BASE + 47);             /* VT_UI4           */
        public const int MESSAGE_PROPID_RESP_QUEUE = (MESSAGE_PROPID_BASE + 15);            /* VT_LPWSTR        */
        public const int MESSAGE_PROPID_RESP_QUEUE_LEN = (MESSAGE_PROPID_BASE + 16);        /* VT_UI4           */
        public const int MESSAGE_PROPID_SECURITY_CONTEXT = (MESSAGE_PROPID_BASE + 37);      /* VT_UI4           */
        public const int MESSAGE_PROPID_SENDERID = (MESSAGE_PROPID_BASE + 20);              /* VT_UI1|VT_VECTOR */
        public const int MESSAGE_PROPID_SENDERID_LEN = (MESSAGE_PROPID_BASE + 21);          /* VT_UI4           */
        public const int MESSAGE_PROPID_SENDERID_TYPE = (MESSAGE_PROPID_BASE + 22);         /* VT_UI4           */
        public const int MESSAGE_PROPID_SENDER_CERT = (MESSAGE_PROPID_BASE + 28);           /* VT_UI1|VT_VECTOR */
        public const int MESSAGE_PROPID_SENDER_CERT_LEN = (MESSAGE_PROPID_BASE + 29);       /* VT_UI4           */
        public const int MESSAGE_PROPID_SENTTIME = (MESSAGE_PROPID_BASE + 31);              /* VT_UI4           */
        public const int MESSAGE_PROPID_SIGNATURE = (MESSAGE_PROPID_BASE + 45);             /* VT_UI1|VT_VECTOR */
        public const int MESSAGE_PROPID_SIGNATURE_LEN = (MESSAGE_PROPID_BASE + 46);         /* VT_UI4           */
        public const int MESSAGE_PROPID_SRC_MACHINE_ID = (MESSAGE_PROPID_BASE + 30);        /* VT_CLSID         */
        public const int MESSAGE_PROPID_TIME_TO_BE_RECEIVED = (MESSAGE_PROPID_BASE + 14);   /* VT_UI4           */
        public const int MESSAGE_PROPID_TIME_TO_REACH_QUEUE = (MESSAGE_PROPID_BASE + 13);   /* VT_UI4           */
        public const int MESSAGE_PROPID_TRACE = (MESSAGE_PROPID_BASE + 41);                 /* VT_UI1           */
        public const int MESSAGE_PROPID_VERSION = (MESSAGE_PROPID_BASE + 19);               /* VT_UI4           */
        public const int MESSAGE_PROPID_XACT_STATUS_QUEUE = (MESSAGE_PROPID_BASE + 39);     /* VT_LPWSTR        */
        public const int MESSAGE_PROPID_XACT_STATUS_QUEUE_LEN = (MESSAGE_PROPID_BASE + 40); /* VT_UI4           */
        public const int MESSAGE_PROPID_XACTID = (MESSAGE_PROPID_BASE + 52); /* VT_UI1|VT_VECTOR */
        public const int MESSAGE_PROPID_LOOKUPID = (MESSAGE_PROPID_BASE + 60);    /* VT_UI8           */
    }
  
}