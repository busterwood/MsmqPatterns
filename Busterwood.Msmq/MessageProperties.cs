using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;

namespace BusterWood.Msmq
{
    class MessageProperties
    {
        private const short VT_UNDEFINED = 0;
        public const short VT_EMPTY = short.MaxValue; //this is hack, VT_EMPTY is really 0, but redefining VT_UNDEFINED is risky since 0 is a good semantic default for it
        public const short VT_ARRAY = 0x2000;
        public const short VT_BOOL = 11;
        public const short VT_BSTR = 8;
        public const short VT_CLSID = 72;
        public const short VT_CY = 6;
        public const short VT_DATE = 7;
        public const short VT_I1 = 16;
        public const short VT_I2 = 2;
        public const short VT_I4 = 3;
        public const short VT_I8 = 20;
        public const short VT_LPSTR = 30;
        public const short VT_LPWSTR = 31;
        public const short VT_NULL = 1;
        public const short VT_R4 = 4;
        public const short VT_R8 = 5;
        public const short VT_STREAMED_OBJECT = 68;
        public const short VT_STORED_OBJECT = 69;
        public const short VT_UI1 = 17;
        public const short VT_UI2 = 18;
        public const short VT_UI4 = 19;
        public const short VT_UI8 = 21;
        public const short VT_VECTOR = 0x1000;

        int _maxProperties = 61;
        int _basePropertyId = Native.MESSAGE_PROPID_BASE + 1;

        int _propertyCount;
        int[] _identifiers;
        int[] _statuses;
        MQPROPVARIANTS[] _properties;
        short[] _types;
        object[] _values;
        object[] _handles;
        GCHandle _pinnedProperties;
        GCHandle _pinnedIdentifiers;
        GCHandle _pinnedStatuses;
        MQPROPS _nativeProps;

        internal MessageProperties(int maxProperties, int baseId)
        {
            _nativeProps = new MQPROPS();
            _maxProperties = maxProperties;
            _basePropertyId = baseId;
            _types = new short[_maxProperties];
            _values = new object[_maxProperties];
            _handles = new object[_maxProperties];
        }

        public MessageProperties()
        {
            _nativeProps = new MQPROPS();
            _types = new short[_maxProperties];
            _values = new object[_maxProperties];
            _handles = new object[_maxProperties];
        }

        public bool IsUndefined(int propertyId) => _types[propertyId - _basePropertyId] == VT_UNDEFINED;

        public byte[] GetGuid(int propertyId) => (byte[])_values[propertyId - _basePropertyId];

        public void SetGuid(int propertyId, byte[] value)
        {
            SetPropertyType(propertyId, VT_CLSID);
            _values[propertyId - _basePropertyId] = value;
        }

        private void SetPropertyType(int propertyId, short type)
        {
            if (_types[propertyId - _basePropertyId] == VT_UNDEFINED)
            {
                _types[propertyId - _basePropertyId] = type;
                _propertyCount++;
            }
        }

        public short GetShort(int propertyId) => (short)_values[propertyId - _basePropertyId];

        public void SetShort(int propertyId, short value)
        {
            SetPropertyType(propertyId, VT_I2);
            _values[propertyId - _basePropertyId] = value;
        }

        public int GetInt(int propertyId) => (int)_values[propertyId - _basePropertyId];

        public void SetInt(int propertyId, int value)
        {
            SetPropertyType(propertyId, VT_I4);
            _values[propertyId - _basePropertyId] = value;
        }

        public IntPtr GetStringVectorBasePointer(int propertyId) => (IntPtr)_handles[propertyId - _basePropertyId];

        public uint GetStringVectorLength(int propertyId) => (uint)_values[propertyId - _basePropertyId];

        public byte[] GetString(int propertyId) => (byte[])_values[propertyId - _basePropertyId];

        public void SetString(int propertyId, byte[] value)
        {
            SetPropertyType(propertyId, VT_LPWSTR);
            _values[propertyId - _basePropertyId] = value;
        }

        public byte GetByte(int propertyId) => (byte)_values[propertyId - _basePropertyId];

        public void SetByte(int propertyId, byte value)
        {
            SetPropertyType(propertyId, VT_UI1);
            _values[propertyId - _basePropertyId] = value;
        }

        public byte[] GetByteArray(int propertyId) => (byte[])_values[propertyId - _basePropertyId];

        public void SetByteArray(int propertyId, byte[] value)
        {
            SetPropertyType(propertyId, VT_VECTOR | VT_UI1);
            _values[propertyId - _basePropertyId] = value;
        }

        public short GetUShort(int propertyId) => (short)_values[propertyId - _basePropertyId];

        public void SetUShort(int propertyId, short value)
        {
            SetPropertyType(propertyId, VT_UI2);
            _values[propertyId - _basePropertyId] = value;
        }

        public int GetUInt(int propertyId) => (int)_values[propertyId - _basePropertyId];

        public void SetUInt(int propertyId, int value)
        {
            SetPropertyType(propertyId, VT_UI4);
            _values[propertyId - _basePropertyId] = value;
        }

        public long GetULong(int propertyId) => (long)_values[propertyId - _basePropertyId];

        public void SetULong(int propertyId, long value)
        {
            SetPropertyType(propertyId, VT_UI8);
            _values[propertyId - _basePropertyId] = value;
        }

        public IntPtr GetIntPtr(int propertyId)
        {
            object obj = _values[propertyId - _basePropertyId];
            return obj is IntPtr ? (IntPtr)obj : IntPtr.Zero;
        }

        public void AdjustSize(int propertyId, int size)
        {
            // temporarily reuse field to store the size temporarily
            _handles[propertyId - _basePropertyId] = (uint)size;
        }

        public void SetUndefined(int propertyId)
        {
            if (_types[propertyId - _basePropertyId] == VT_UNDEFINED)
                return;
            _types[propertyId - _basePropertyId] = VT_UNDEFINED;
            _propertyCount--;
        }        

        public void Remove(int propertyId)
        {
            if (_types[propertyId - _basePropertyId] == VT_UNDEFINED)
                return;
            _types[propertyId - _basePropertyId] = VT_UNDEFINED;
            _values[propertyId - _basePropertyId] = null;
            _handles[propertyId - _basePropertyId] = null;
            _propertyCount--;
        }

        public void SetNull(int propertyId)
        {
            SetPropertyType(_propertyCount, VT_NULL);
            _values[propertyId - _basePropertyId] = null;
        }

        public void SetEmpty(int propertyId)
        {
            SetPropertyType(_propertyCount, VT_EMPTY);
            _values[propertyId - _basePropertyId] = null;
        }

        public MQPROPS Allocate()
        {
            var propertyValues = new MQPROPVARIANTS[_propertyCount];
            var propertyIds = new int[_propertyCount];
            var newVectorStatus = new int[_propertyCount];
            int used = 0;

            for (int i = 0; i < _maxProperties; ++i)
            {
                short vt = _types[i];
                if (vt == VT_UNDEFINED)
                    continue;

                propertyIds[used] = i + _basePropertyId;
                propertyValues[used].vt = vt; // set type

                if (vt == (VT_VECTOR | VT_UI1)) // byte array
                {
                    if (_handles[i] == null)
                        propertyValues[used].caub.cElems = (uint)((byte[])_values[i]).Length;
                    else
                        propertyValues[used].caub.cElems = (uint)_handles[i];
                    GCHandle handle = GCHandle.Alloc(_values[i], GCHandleType.Pinned);
                    _handles[i] = handle;
                    propertyValues[used].caub.pElems = handle.AddrOfPinnedObject();
                }
                else if (vt == VT_LPWSTR || vt == VT_CLSID)
                {
                    GCHandle handle = GCHandle.Alloc(_values[i], GCHandleType.Pinned);
                    _handles[i] = handle;
                    propertyValues[used].ptr = handle.AddrOfPinnedObject();
                }
                else if (vt == VT_UI1 || vt == VT_I1)
                {
                    propertyValues[used].bVal = (byte)_values[i];
                }
                else if (vt == VT_UI2 || vt == VT_I2)
                {
                    propertyValues[used].iVal = (short)_values[i];
                }
                else if (vt == VT_UI4 || vt == VT_I4)
                {
                    propertyValues[used].lVal = (int)_values[i];
                }
                else if (vt == VT_UI8 || vt == VT_I8)
                {
                    propertyValues[used].hVal = (long)_values[i];
                }
                else if (vt == VT_EMPTY)
                {
                    propertyValues[used].vt = 0; //real value for VT_EMPTY
                }

                used++;

                if (_propertyCount == used)
                    break;
            }

            _identifiers = propertyIds;
            _statuses = newVectorStatus;
            _properties = propertyValues;

            _pinnedIdentifiers = GCHandle.Alloc(propertyIds, GCHandleType.Pinned);
            _pinnedProperties = GCHandle.Alloc(propertyValues, GCHandleType.Pinned);
            _pinnedStatuses = GCHandle.Alloc(newVectorStatus, GCHandleType.Pinned);

            _nativeProps.propertyCount = _propertyCount;
            _nativeProps.propertyIdentifiers = _pinnedIdentifiers.AddrOfPinnedObject();
            _nativeProps.propertyValues = _pinnedProperties.AddrOfPinnedObject();
            _nativeProps.status = _pinnedStatuses.AddrOfPinnedObject();
            return _nativeProps;
        }

        public void Free()
        {
            for (int i = 0; i < _identifiers.Length; ++i)
            {
                var vt = _properties[i].vt;
                var idx = _identifiers[i] - _basePropertyId;
                if (_types[idx] == VT_NULL)
                {
                    if (vt == (VT_VECTOR | VT_UI1) || vt == VT_NULL)
                    {
                        _values[idx] = _properties[i].caub.cElems; //MSMQ allocated this memory

                    }
                    else if (vt == (VT_VECTOR | VT_LPWSTR))
                    {
                        _values[idx] = _properties[i * 4].caub.cElems;
                        _handles[idx] = _properties[i * 4].caub.pElems;
                    }
                    else
                    {
                        _values[idx] = _properties[i].ptr;
                    }
                }
                else if (vt == VT_LPWSTR || vt == VT_CLSID || vt == (VT_VECTOR | VT_UI1))
                {
                    ((GCHandle)_handles[idx]).Free();
                    _handles[idx] = null;
                }
                else if (vt == VT_UI1 || vt == VT_I1)
                {
                    _values[idx] = _properties[i].bVal;
                }
                else if (vt == VT_UI2 || vt == VT_I2)
                {
                    _values[idx] = _properties[i].iVal;
                }
                else if (vt == VT_UI4 || vt == VT_I4)
                {
                    _values[idx] = _properties[i].lVal;
                }
                else if (vt == VT_UI8 || vt == VT_I8)
                {
                    _values[idx] = _properties[i].hVal;
                }
            }

            _pinnedIdentifiers.Free();
            _pinnedProperties.Free();
            _pinnedStatuses.Free();
        }

        public void AdjustMemory()
        {
            if (!IsUndefined(Native.MESSAGE_PROPID_ADMIN_QUEUE_LEN))
            {
                int size = GetUInt(Native.MESSAGE_PROPID_ADMIN_QUEUE_LEN);
                var existing = GetString(Native.MESSAGE_PROPID_ADMIN_QUEUE);
                if (size * 2 > existing.Length)
                    SetString(Native.MESSAGE_PROPID_ADMIN_QUEUE, new byte[size * 2]);
            }

            if (!IsUndefined(Native.MESSAGE_PROPID_BODY))
            {
                int size = GetUInt(Native.MESSAGE_PROPID_BODY_SIZE);
                var existing = GetByteArray(Native.MESSAGE_PROPID_BODY_SIZE);
                if (size > existing.Length)
                    SetByteArray(Native.MESSAGE_PROPID_BODY, new byte[size]);
            }
            
            if (!IsUndefined(Native.MESSAGE_PROPID_DEST_QUEUE))
                {
                int size = GetUInt(Native.MESSAGE_PROPID_DEST_QUEUE_LEN);
                var existing = GetString(Native.MESSAGE_PROPID_DEST_QUEUE);
                if (size * 2 > existing.Length)
                    SetString(Native.MESSAGE_PROPID_DEST_QUEUE, new byte[size * 2]);
            }

            if (!IsUndefined(Native.MESSAGE_PROPID_EXTENSION))
            {
                int size = GetUInt(Native.MESSAGE_PROPID_EXTENSION_LEN);
                var existing = GetByteArray(Native.MESSAGE_PROPID_EXTENSION);
                if (size > existing.Length)
                    SetByteArray(Native.MESSAGE_PROPID_EXTENSION, new byte[size]);
            }

            //if (filter.TransactionStatusQueue)
            //{
            //    int size = GetUInt(Native.MESSAGE_PROPID_XACT_STATUS_QUEUE_LEN);
            //    if (size > Message.DefaultQueueNameSize)
            //        SetString(Native.MESSAGE_PROPID_XACT_STATUS_QUEUE, new byte[size * 2]);
            //}

            if (!IsUndefined(Native.MESSAGE_PROPID_LABEL))
            {
                int size = GetUInt(Native.MESSAGE_PROPID_LABEL_LEN);
                var existing = GetString(Native.MESSAGE_PROPID_LABEL);
                if (size * 2 > existing.Length)
                    SetString(Native.MESSAGE_PROPID_LABEL, new byte[size * 2]);
            }

            if (!IsUndefined(Native.MESSAGE_PROPID_RESP_QUEUE))
            {
                int size = GetUInt(Native.MESSAGE_PROPID_RESP_QUEUE_LEN);
                var existing = GetString(Native.MESSAGE_PROPID_RESP_QUEUE);
                if (size * 2 > existing.Length)
                    SetString(Native.MESSAGE_PROPID_RESP_QUEUE, new byte[size * 2]);
            }

            //if (filter.SenderId)
            //{
            //    int size = GetUInt(Native.MESSAGE_PROPID_SENDERID_LEN);
            //    if (size > Message.DefaultSenderIdSize)
            //        SetByteArray(Native.MESSAGE_PROPID_SENDERID, new byte[size]);
            //}            
        }
    }

    static partial class Extensions
    {
        /// <summary>
        /// Sets up the <see cref="MsgProps"/> ready to read a message
        /// </summary>
        internal static void SetForRead(this MessageProperties msgProps, Properties read)
        {
            Contract.Requires(msgProps != null);

            if ((read & Properties.Class) != 0)
            {
                msgProps.SetUShort(Native.MESSAGE_PROPID_CLASS, 0);
            }

            if ((read & Properties.AcknowledgementTypes) != 0)
            {
                msgProps.SetByte(Native.MESSAGE_PROPID_ACKNOWLEDGE, 0);
            }

            if ((read & Properties.AdministrationQueue) != 0)
            {
                msgProps.SetString(Native.MESSAGE_PROPID_ADMIN_QUEUE, new byte[255 * 2]);
                msgProps.SetUInt(Native.MESSAGE_PROPID_ADMIN_QUEUE_LEN, 255);
            }

            if ((read & Properties.AppSpecific) != 0)
            {
                msgProps.SetUInt(Native.MESSAGE_PROPID_APPSPECIFIC, 0);
            }

            if ((read & Properties.Body) != 0)
            {
                msgProps.SetByteArray(Native.MESSAGE_PROPID_BODY, new byte[1024]); // use default body size
                msgProps.SetUInt(Native.MESSAGE_PROPID_BODY_SIZE, 1024);
                msgProps.SetUInt(Native.MESSAGE_PROPID_BODY_TYPE, 0);
            }

            if ((read & Properties.CorrelationId) != 0)
            {
                msgProps.SetByteArray(Native.MESSAGE_PROPID_CORRELATIONID, new byte[Message.MessageIdSize]);
            }

            if ((read & Properties.Label) != 0)
            {
                msgProps.SetString(Native.MESSAGE_PROPID_LABEL, new byte[255 * 2]);
                msgProps.SetUInt(Native.MESSAGE_PROPID_LABEL_LEN, 255);
            }

            if ((read & Properties.Id) != 0)
            {
                msgProps.SetByteArray(Native.MESSAGE_PROPID_MSGID, new byte[Message.MessageIdSize]);
            }

            if ((read & Properties.LookupId) != 0)
            {
                msgProps.SetULong(Native.MESSAGE_PROPID_LOOKUPID, 0L);
            }

            if ((read & Properties.ResponseQueue) != 0)
            {
                msgProps.SetString(Native.MESSAGE_PROPID_RESP_QUEUE, new byte[255 * 2]);
                msgProps.SetUInt(Native.MESSAGE_PROPID_RESP_QUEUE_LEN, 255);
            }
            
            if ((read & Properties.Journal) != 0)
            {
                msgProps.SetByte(Native.MESSAGE_PROPID_JOURNAL, 0);
            }

            if ((read & Properties.ArrivedTime) != 0)
            {
                msgProps.SetUInt(Native.MESSAGE_PROPID_ARRIVEDTIME, 0);
            }

            if ((read & Properties.Delivery) != 0)
            {
                msgProps.SetByte(Native.MESSAGE_PROPID_DELIVERY, 0);
            }

            if ((read & Properties.DestinationQueue) != 0)
            {
                msgProps.SetString(Native.MESSAGE_PROPID_DEST_QUEUE, new byte[255 * 2]);
                msgProps.SetUInt(Native.MESSAGE_PROPID_DEST_QUEUE_LEN, 255);
            }

            if ((read & Properties.Extension) != 0)
            {
                msgProps.SetByteArray(Native.MESSAGE_PROPID_EXTENSION, new byte[255]);
                msgProps.SetUInt(Native.MESSAGE_PROPID_EXTENSION_LEN, 255);
            }

            if ((read & Properties.Priority) != 0)
            {
                msgProps.SetByte(Native.MESSAGE_PROPID_PRIORITY, 0);
            }

            //if ((read & Properties.SENDER_ID) != 0)
            //{
            //    MsgProps.SetByteArray(Native.MESSAGE_PROPID_SENDERID, new byte[DefaultSenderIdSize]);
            //    MsgProps.SetUInt(Native.MESSAGE_PROPID_SENDERID_LEN, DefaultSenderIdSize);
            //}

            if ((read & Properties.SentTime) != 0)
            {
                msgProps.SetUInt(Native.MESSAGE_PROPID_SENTTIME, 0);
            }

            //if ((read & Properties.SOURCE_MACHINE) != 0)
            //    MsgProps.SetGuid(Native.MESSAGE_PROPID_SRC_MACHINE_ID, new byte[GenericIdSize]);

            if ((read & Properties.TimeToBeReceived) != 0)
            {
                msgProps.SetUInt(Native.MESSAGE_PROPID_TIME_TO_BE_RECEIVED, 0);
            }

            if ((read & Properties.TimeToReachQueue) != 0)
            {
                msgProps.SetUInt(Native.MESSAGE_PROPID_TIME_TO_REACH_QUEUE, 0);
            }

            //if ((read & Properties.TRANSACTION_ID) != 0)
            //    MsgProps.SetByteArray(Native.MESSAGE_PROPID_XACTID, new byte[MessageIdSize]);
        }

    }
}