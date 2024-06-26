﻿namespace SuperSocket.ClientEngine
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.ConstrainedExecution;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class TcpClientSession : ClientSession
    {
        protected string HostName { get; private set; }

        private bool m_InConnecting = false;        

        public TcpClientSession()
            : base()
        {

        }

        public override EndPoint LocalEndPoint
        {
            get
            {
                return base.LocalEndPoint;
            }

            set
            {
                if (m_InConnecting || IsConnected)
                    throw new Exception("You cannot set LocalEndPoint after you start the connection.");

                base.LocalEndPoint = value;
            }
        }

        public override int ReceiveBufferSize
        {
            get
            {
                return base.ReceiveBufferSize;
            }

            set
            {
                if (Buffer.Array != null)
                    throw new Exception("ReceiveBufferSize cannot be set after the socket has been connected!");

                base.ReceiveBufferSize = value;
            }
        }

        protected virtual bool IsIgnorableException(Exception e)
        {
            if (e is System.ObjectDisposedException)
                return true;

            if (e is NullReferenceException)
                return true;

            return false;
        }

        protected bool IsIgnorableSocketError(int errorCode)
        {
            const int SocketErrorShutdown = (int)SocketError.Shutdown;
            const int SocketErrorConnectionAborted = (int)SocketError.ConnectionAborted;
            const int SocketErrorConnectionReset = (int)SocketError.ConnectionReset;
            const int SocketErrorOperationAborted = (int)SocketError.OperationAborted;

            switch (errorCode)
            {
                case SocketErrorShutdown:
                case SocketErrorConnectionAborted:
                case SocketErrorConnectionReset:
                case SocketErrorOperationAborted:
                    return true;
            }

            return false;
        }

        public override async Task ConnectAsync(EndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));

            if (remoteEndPoint is DnsEndPoint dnsEndPoint)
            {
                var hostName = dnsEndPoint.Host;

                if (!string.IsNullOrEmpty(hostName))
                    HostName = hostName;
            }

            if (m_InConnecting)
                throw new Exception("The socket is connecting, cannot connect again!");

            if (Client != null)
                throw new Exception("The socket is connected, you needn't connect again!");

            //If there is a proxy set, connect the proxy server by proxy connector
            if (Proxy != null)
            {
                m_InConnecting = true;
                try
                {
                    var proxyResult = await Proxy.ConnectAsync(remoteEndPoint);
                    await Proxy_Completed(proxyResult);
                }
                catch
                {
                    m_InConnecting = false;
                    throw;
                }

                return;
            }

            m_InConnecting = true;

            Socket socket = null;
            try
            {
                var localEndPoint = LocalEndPoint;
                if (localEndPoint != null)
                {
                    socket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    socket.ExclusiveAddressUse = false;
                    socket.Bind(localEndPoint);
                }
                else
                {
                    socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                }

                await socket.ConnectAsync(remoteEndPoint);
                await ProcessConnect(socket, null);
            }
            catch
            {
                m_InConnecting = false;
                if (socket != null)
                {
                    try
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                    catch
                    {
                        // ignore
                    }

                    try
                    {
                        socket.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                throw;
            }
        }

        private Task Proxy_Completed(ProxyResult result)
        {

            if (result.Connected)
            {
                DnsEndPoint endPoint = null;
                if (result.TargetHostName != null)
                {
                    endPoint = new DnsEndPoint(result.TargetHostName, 0);
                }

                return ProcessConnect(result.Socket, endPoint);
            }

            m_InConnecting = false;

            throw new Exception("proxy error", result.Exception);
        }

        protected async Task ProcessConnect(Socket socket, EndPoint remoteEndpoint)
        {
            if (socket == null)
            {
                m_InConnecting = false;
                throw new SocketException((int)SocketError.ConnectionAborted);
            }

            //To walk around a MonoTouch's issue
            //one user reported in some cases the e.SocketError = SocketError.Succes but the socket is not connected in MonoTouch
            if (!socket.Connected)
            {
                m_InConnecting = false;

                SocketError socketError;

                try
                {
                    socketError = (SocketError)socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error);
                }
                catch (Exception)
                {
                    socketError = SocketError.HostUnreachable;
                }                

                throw new SocketException((int)socketError);
            }

            Client = socket;
            m_InConnecting = false;

            try
            {
                // mono may throw an exception here
                LocalEndPoint = socket.LocalEndPoint;
            }
            catch
            {
            }

            var finalRemoteEndPoint = remoteEndpoint ?? socket.RemoteEndPoint;

            if (string.IsNullOrEmpty(HostName))
            {
                HostName = GetHostOfEndPoint(finalRemoteEndPoint);
            }
            else if (finalRemoteEndPoint is DnsEndPoint finalDnsEndPoint)
            {
                var hostName = finalDnsEndPoint.Host;

                if (!string.IsNullOrEmpty(hostName) && !HostName.Equals(hostName, StringComparison.OrdinalIgnoreCase))
                    HostName = hostName;
            }

            try
            {
                //Set keep alive
                Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            }
            catch
            {
            }
            
            await OnConnected();
        }

        private string GetHostOfEndPoint(EndPoint endPoint)
        {
            var dnsEndPoint = endPoint as DnsEndPoint;

            if (dnsEndPoint != null)
            {
                return dnsEndPoint.Host;
            }

            var ipEndPoint = endPoint as IPEndPoint;

            if (ipEndPoint != null && ipEndPoint.Address != null)
               return ipEndPoint.Address.ToString();

            return string.Empty;
        }

        protected bool EnsureSocketClosed()
        {
            return EnsureSocketClosed(null);
        }

        protected bool EnsureSocketClosed(Socket prevClient)
        {
            var client = Client;

            if (client == null)
                return false;

            var fireOnClosedEvent = true;

            if (prevClient != null && prevClient != client)//originalClient is previous disconnected socket, so we needn't fire event for it
            {
                client = prevClient;
                fireOnClosedEvent = false;
            }
            else
            {
                Client = null;
                m_IsSending = 0;
            }

            try
            {
                client.Shutdown(SocketShutdown.Both);
            }
            catch
            {}
            finally
            {
                try
                {
                    client.Dispose();
                }
                catch
                {}
            }

            return fireOnClosedEvent;
        }

        private bool DetectConnected()
        {
            if (Client != null)
                return true;
            OnError(new SocketException((int)SocketError.NotConnected));
            return false;
        }

        private IBatchQueue<ArraySegment<byte>> m_SendingQueue;

        private IBatchQueue<ArraySegment<byte>> GetSendingQueue()
        {
            if (m_SendingQueue != null)
                return m_SendingQueue;

            lock (this)
            {
                if (m_SendingQueue != null)
                    return m_SendingQueue;

                //Sending queue size must be greater than 3
                m_SendingQueue = new ConcurrentBatchQueue<ArraySegment<byte>>(Math.Max(SendingQueueSize, 1024), (t) => t.Array == null || t.Count == 0);
                return m_SendingQueue;
            }
        }

        private PosList<ArraySegment<byte>> m_SendingItems;

        private PosList<ArraySegment<byte>> GetSendingItems()
        {
            if (m_SendingItems == null)
                m_SendingItems = new PosList<ArraySegment<byte>>();

            return m_SendingItems;
        }

        private int m_IsSending = 0;

        protected bool IsSending
        {
            get { return m_IsSending == 1; }
        }

        public override bool TrySend(ArraySegment<byte> segment)
        {
            if (segment.Array == null || segment.Count == 0)
            {
                throw new Exception("The data to be sent cannot be empty.");
            }

            if (!DetectConnected())
            {
                //may be return false? 
                return true;
            }

            var isEnqueued = GetSendingQueue().Enqueue(segment);

            if (Interlocked.CompareExchange(ref m_IsSending, 1, 0) != 0)
                return isEnqueued;

            DequeueSend();

            return isEnqueued;
        }

        public override bool TrySend(IList<ArraySegment<byte>> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                throw new ArgumentNullException("segments");
            }

            for (var i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                
                if (seg.Count == 0)
                {
                    throw new Exception("The data piece to be sent cannot be empty.");
                }
            }

            if (!DetectConnected())
            {
                //may be return false? 
                return true;
            }

            var isEnqueued = GetSendingQueue().Enqueue(segments);

            if (Interlocked.CompareExchange(ref m_IsSending, 1, 0) != 0)
                return isEnqueued;

            DequeueSend();

            return isEnqueued;
        }

        private void DequeueSend()
        {
            var sendingItems = GetSendingItems();

            if (!m_SendingQueue.TryDequeue(sendingItems))
            {
                m_IsSending = 0;
                return;
            }

            SendInternal(sendingItems);
        }

        protected abstract void SendInternal(PosList<ArraySegment<byte>> items);

        protected void OnSendingCompleted()
        {
            var sendingItems = GetSendingItems();
            sendingItems.Clear();
            sendingItems.Position = 0;

            if (!m_SendingQueue.TryDequeue(sendingItems))
            {
                m_IsSending = 0;
                return;
            }

            SendInternal(sendingItems);
        }

        public override void Close()
        {
            if (EnsureSocketClosed())
                OnClosed();
        }
    }
}
