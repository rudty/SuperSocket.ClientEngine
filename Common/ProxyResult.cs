using System;
using System.Text;
using System.Net.Sockets;

namespace SuperSocket.ClientEngine
{
    public class ProxyResult
    {
        public ProxyResult(Socket socket)
            : this(true, socket, null, null)
        {

        }

        public ProxyResult(Socket socket, string targetHostHame)
            : this(true, socket, targetHostHame, null)
        {

        }

        public ProxyResult(Exception exception)
            : this(false, null, null, exception)
        {

        }

        public ProxyResult(bool connected, Socket socket, string targetHostName, Exception exception)
        {
            Connected = connected;
            Socket = socket;
            TargetHostName = targetHostName;
            Exception = exception;
        }

        public bool Connected { get; private set; }

        public Socket Socket { get; private set; }

        public Exception Exception { get; private set; }

        public string TargetHostName { get; private set; }
    }
}
