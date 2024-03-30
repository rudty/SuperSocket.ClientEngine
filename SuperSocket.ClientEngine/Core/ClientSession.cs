namespace SuperSocket.ClientEngine
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class ClientSession : IBufferSetter
    {
        public const int DefaultReceiveBufferSize = 4096;
        
        public Socket Client { get; protected set; }

        public virtual EndPoint LocalEndPoint { get; set; }

        public bool IsConnected { get; private set; }

        public bool NoDelay { get; set; }

        protected ClientSession()
        {

        }

        public int SendingQueueSize { get; set; }

        public abstract Task ConnectAsync(EndPoint remoteEndPoint);

        public abstract bool TrySend(ArraySegment<byte> segment);

        public abstract bool TrySend(IList<ArraySegment<byte>> segments);

        public void Send(byte[] data, int offset, int length)
        {
            this.Send(new ArraySegment<byte>(data, offset, length));
        }

        public void Send(ArraySegment<byte> segment)
        {
            if (TrySend(segment))
                return;

            var spinWait = new SpinWait();

            while (true)
            {
                spinWait.SpinOnce();

                if (TrySend(segment))
                    return;
            }
        }

        public void Send(IList<ArraySegment<byte>> segments)
        {
            if (TrySend(segments))
                return;

            var spinWait = new SpinWait();

            while (true)
            {
                spinWait.SpinOnce();

                if (TrySend(segments))
                    return;
            }
        }
        public abstract void Close();

        private EventHandler m_Closed;

        public event EventHandler Closed
        {
            add { m_Closed += value; }
            remove { m_Closed -= value; }
        }

        protected virtual void OnClosed()
        {
            IsConnected = false;
            LocalEndPoint =  null;

            var handler = m_Closed;

            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private EventHandler<ErrorEventArgs> m_Error;

        public event EventHandler<ErrorEventArgs> Error
        {
            add { m_Error += value; }
            remove { m_Error -= value; }
        }

        protected virtual void OnError(Exception e)
        {
            var handler = m_Error;
            if (handler == null)
                return;

            handler(this, new ErrorEventArgs(e));
        }

        protected virtual Task OnConnected()
        {
            var client = Client;

            if(client != null)
            {
                try
                {
                    if(client.NoDelay != NoDelay)
                        client.NoDelay = NoDelay;
                }
                catch
                {
                }
            }

            IsConnected = true;
            return Task.CompletedTask;
        }

        private EventHandler<DataEventArgs> m_DataReceived;

        public event EventHandler<DataEventArgs> DataReceived
        {
            add { m_DataReceived += value; }
            remove { m_DataReceived -= value; }
        }

        private DataEventArgs m_DataArgs = new DataEventArgs();

        protected virtual void OnDataReceived(byte[] data, int offset, int length)
        {
            var handler = m_DataReceived;
            if (handler == null)
                return;

            m_DataArgs.Data = data;
            m_DataArgs.Offset = offset;
            m_DataArgs.Length = length;

            handler(this, m_DataArgs);
        }

        public virtual int ReceiveBufferSize { get; set; }

        public IProxyConnector Proxy { get; set; }

        protected ArraySegment<byte> Buffer { get; set; }

        void IBufferSetter.SetBuffer(ArraySegment<byte> bufferSegment)
        {
            SetBuffer(bufferSegment);
        }

        protected virtual void SetBuffer(ArraySegment<byte> bufferSegment)
        {
            Buffer = bufferSegment;
        }
    }
}
