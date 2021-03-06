﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace BusterWood.Msmq
{
    /// <summary>Bundle request details into a class that is keep in the <see cref="Outstanding"/> set during the async call.</summary>
    class QueueAsyncRequest
    {
        const int MQ_INFORMATION_OPERATION_PENDING = unchecked(0x400E0006);

        readonly HashSet<QueueAsyncRequest> Outstanding;
        readonly Message Message;
        readonly uint timeoutMS;
        readonly QueueHandle handle;
        readonly ReadAction action;
        readonly TaskCompletionSource<Message> Tcs;
        public MQPROPS Props;
        readonly CursorHandle cursor;

        public QueueAsyncRequest(Message message, HashSet<QueueAsyncRequest> outstanding, uint timeoutMS, QueueHandle handle, ReadAction action, CursorHandle cursor)
        {
            Contract.Requires(cursor != null);
            this.action = action;
            this.handle = handle;
            this.timeoutMS = timeoutMS;
            Message = message;
            Props = message.Props.Allocate();
            Tcs = new TaskCompletionSource<Message>();
            Outstanding = outstanding;
            this.cursor = cursor;
        }

        public unsafe Task<Message> ReceiveAsync()
        {

            // create overlapped with callback that sets the task complete source
            var overlapped = new Overlapped();
            var nativeOverlapped = overlapped.Pack(EndReceive, null);

            try
            {
                for (;;)
                {
                    // receive, may complete synchronously or call the async callback on the overlapped defined above
                    int res = Native.ReceiveMessage(handle, timeoutMS, action, Props, nativeOverlapped, null, cursor, IntPtr.Zero);

                    // successfully completed synchronously but no enough memory                
                    if (Native.NotEnoughMemory(res))
                    {
                        Message.Props.Free();
                        Message.Props.IncreaseBufferSize();
                        Props = Message.Props.Allocate();
                        continue; // try again
                    }

                    if (Native.IsError(res))
                    {
                        Message.Props.Free();
                        Overlapped.Free(nativeOverlapped);
                        Tcs.TrySetException(new QueueException(unchecked(res))); // we really want Task.FromException...
                        return Tcs.Task;
                    }

                    return Tcs.Task;
                }
            }
            catch (ObjectDisposedException ex)
            {
                Tcs.TrySetException(new QueueException(ErrorCode.OperationCanceled));
                return Tcs.Task;
            }
        }

        public unsafe void EndReceive(uint code, uint bytes, NativeOverlapped* native)
        {
            Overlapped.Free(native);
            Message.Props.Free();

            lock (Outstanding)
                Outstanding.Remove(this);

            if (code == 995) // operation aborted
            {
                Tcs.TrySetException(new QueueException(ErrorCode.OperationCanceled));
                return;
            }

            var result = Native.GetOverlappedResult(native);
            try
            {
                switch (result)
                {
                    case 0:
                        Message.Props.ResizeBody();
                        Tcs.TrySetResult(Message);
                        break;
                    case (int)ErrorCode.InsufficientResources:
                        Tcs.SetException(new OutOfMemoryException("async receive operation reported InsufficientResources"));
                        break;
                    case (int)ErrorCode.IOTimeout:
                        Tcs.TrySetResult(null);
                        break;
                    default:                    
                        // successfully completed but no enough memory                
                        if (Native.NotEnoughMemory(result))
                        {
                            Message.Props.Free();
                            Message.Props.IncreaseBufferSize();
                            Props = Message.Props.Allocate();
                            var overlapped = new Overlapped();
                            var nativeOverlapped = overlapped.Pack(EndReceive, null);
                            int res = Native.ReceiveMessage(handle, timeoutMS, action, Props, nativeOverlapped, null, cursor, IntPtr.Zero);

                            if (res == MQ_INFORMATION_OPERATION_PENDING)    // running asynchronously
                                return;

                            // call completed synchronously
                            Message.Props.Free();
                            Overlapped.Free(nativeOverlapped);

                            if (!Native.IsError(res))
                            {
                                Message.Props.ResizeBody();
                                Tcs.TrySetResult(Message);
                                return;
                            }
                        }

                        // some other error
                        Tcs.TrySetException(new QueueException(unchecked((int)code))); // or do we use the result?
                        break;
                }
            }
            catch (ObjectDisposedException ex)
            {
                Tcs.TrySetException(new QueueException(ErrorCode.OperationCanceled));
            }
        }
    }
}