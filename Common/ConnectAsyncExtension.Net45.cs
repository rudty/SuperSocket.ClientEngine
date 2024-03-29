using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace SuperSocket.ClientEngine
{
    public static partial class ConnectAsyncExtension
    {
        internal static bool PreferIPv4Stack()
        {
            return Environment.GetEnvironmentVariable("PREFER_IPv4_STACK") != null;
        }

        public static void ConnectAsync(this EndPoint remoteEndPoint, EndPoint localEndPoint, ConnectedCallback callback, object state)
        {
            var e = CreateSocketAsyncEventArgs(remoteEndPoint, callback, state);

            var addressFamily = remoteEndPoint.AddressFamily;

            if (localEndPoint != null)
            {
                addressFamily = localEndPoint.AddressFamily;
            }

            var socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                if (localEndPoint != null)
                {
                    socket.ExclusiveAddressUse = false;
                    socket.Bind(localEndPoint);
                }

                bool wasAsync = socket.ConnectAsync(e);

                if (!wasAsync)
                {
                    callback(socket, state, e, null);
                }
            }
            catch (Exception exc)
            {
                callback(null, state, null, exc);
            }
        }
    }
}
