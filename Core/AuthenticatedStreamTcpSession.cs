

namespace SuperSocket.ClientEngine
{
    using System;
    using System.IO;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class AuthenticatedStreamTcpSession : TcpClientSession
    {
        private AuthenticatedStream m_Stream;
        
        public AuthenticatedStreamTcpSession()
            : base()
        {

        }

        public SecurityOption Security { get; set; }

        protected abstract Task<AuthenticatedStream> StartAuthenticatedStream(Socket client);

        protected override async Task OnConnected()
        {
            try
            {
                m_Stream = await StartAuthenticatedStream(Client);
            }
            catch (Exception exc)
            {
                if (!IsIgnorableException(exc))
                {
                    throw;
                }
            }

            await base.OnConnected();

            if (Buffer.Array == null)
            {
                var receiveBufferSize = ReceiveBufferSize;

                if (receiveBufferSize <= 0)
                    receiveBufferSize = DefaultReceiveBufferSize;

                ReceiveBufferSize = receiveBufferSize;

                Buffer = new ArraySegment<byte>(new byte[receiveBufferSize]);
            }

            ReadAsync();
        }
        
        private async void ReadAsync()
        {
            while (IsConnected)
            {
                var client = Client;

                if (client == null || m_Stream == null)
                    return;
                
                var buffer = Buffer;
                
                var length = 0;
                
                try
                {
                    length = await m_Stream.ReadAsync(buffer.Array, buffer.Offset, buffer.Count, CancellationToken.None);
                }
                catch (Exception e)
                {
                    if (!IsIgnorableReadOrSendException(e))
                        OnError(e);

                    if (EnsureSocketClosed(Client))
                        OnClosed();

                    return;
                }

                if (length == 0)
                {
                    if (EnsureSocketClosed(Client))
                        OnClosed();

                    return;
                }

                OnDataReceived(buffer.Array, buffer.Offset, length);
            }
        }

        protected override bool IsIgnorableException(Exception e)
        {
            if (base.IsIgnorableException(e))
                return true;

            if (e is System.IO.IOException)
            {
                if (e.InnerException is ObjectDisposedException)
                    return true;

                //In mono, some exception is wrapped like IOException -> IOException -> ObjectDisposedException
                if (e.InnerException is System.IO.IOException)
                {
                    if (e.InnerException.InnerException is ObjectDisposedException)
                        return true;
                }
            }

            return false;
        }

        private bool IsIgnorableReadOrSendException(Exception e)
        {
            if (IsIgnorableException(e))
                return true;

            if (e is System.IO.IOException && e.InnerException is SocketException)
            {
                SocketException exc = (SocketException)e.InnerException;
                int errorCode = (int)exc.SocketErrorCode;
                return IsIgnorableSocketError(errorCode);
            }

            return false;
        }
        protected override void SendInternal(PosList<ArraySegment<byte>> items)
        {
            SendInternalAsync(items);
        }
        
        private async void SendInternalAsync(PosList<ArraySegment<byte>> items)
        {
            try
            {
                for (int i = items.Position; i < items.Count; i++)
                {
                    var item = items[i];
                    await m_Stream.WriteAsync(item.Array, item.Offset, item.Count, CancellationToken.None);
                }
                
                m_Stream.Flush();
            }
            catch (Exception e)
            {
                if (!IsIgnorableReadOrSendException(e))
                    OnError(e);

                if (EnsureSocketClosed(Client))
                    OnClosed();
                    
                return;
            }
            
            OnSendingCompleted();
        }
        

        public override void Close()
        {
            var stream = m_Stream;

            if (stream != null)
            {
                stream.Dispose();
                m_Stream = null;
            }

            base.Close();
        }
    }
}
