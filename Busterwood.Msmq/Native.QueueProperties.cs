namespace BusterWood.Msmq
{
    partial class Native
    {
        public const int QUEUE_PROPID_BASE = 100;
        public const int QUEUE_PROPID_INSTANCE = QUEUE_PROPID_BASE + 1;           /* VT_CLSID     */
        public const int QUEUE_PROPID_TYPE = QUEUE_PROPID_BASE + 2;               /* VT_CLSID     */
        public const int QUEUE_PROPID_PATHNAME = QUEUE_PROPID_BASE + 3;           /* VT_LPWSTR    */
        public const int QUEUE_PROPID_JOURNAL = QUEUE_PROPID_BASE + 4;            /* VT_UI1       */
        public const int QUEUE_PROPID_QUOTA = QUEUE_PROPID_BASE + 5;              /* VT_UI4       */
        public const int QUEUE_PROPID_BASEPRIORITY = QUEUE_PROPID_BASE + 6;       /* VT_I2        */
        public const int QUEUE_PROPID_JOURNAL_QUOTA = QUEUE_PROPID_BASE + 7;      /* VT_UI4       */
        public const int QUEUE_PROPID_LABEL = QUEUE_PROPID_BASE + 8;              /* VT_LPWSTR    */
        public const int QUEUE_PROPID_CREATE_TIME = QUEUE_PROPID_BASE + 9;        /* VT_I4        */
        public const int QUEUE_PROPID_MODIFY_TIME = QUEUE_PROPID_BASE + 10;       /* VT_I4        */
        public const int QUEUE_PROPID_AUTHENTICATE = QUEUE_PROPID_BASE + 11;      /* VT_UI1       */
        public const int QUEUE_PROPID_PRIV_LEVEL = QUEUE_PROPID_BASE + 12;        /* VT_UI4       */
        public const int QUEUE_PROPID_TRANSACTION = QUEUE_PROPID_BASE + 13;       /* VT_UI1       */
        public const int QUEUE_PROPID_MULTICAST_ADDRESS = QUEUE_PROPID_BASE + 25; /* VT_LPWSTR */

        public const int MANAGEMENT_BASE = 0;
        public const int MANAGEMENT_ACTIVEQUEUES = (MANAGEMENT_BASE + 1);   /* VT_LPWSTR | VT_VECTOR */
        public const int MANAGEMENT_PRIVATEQ = (MANAGEMENT_BASE + 2);      /* VT_LPWSTR | VT_VECTOR  */
        public const int MANAGEMENT_DSSERVER = (MANAGEMENT_BASE + 3);      /* VT_LPWSTR        */
        public const int MANAGEMENT_CONNECTED = (MANAGEMENT_BASE + 4);     /* VT_LPWSTR        */
        public const int MANAGEMENT_TYPE = (MANAGEMENT_BASE + 5);    /* VT_LPWSTR        */
        public const int BYTES_IN_ALL_QUEUES = (MANAGEMENT_BASE + 6);    /* VT_UI8       */
    }
}
