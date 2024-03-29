namespace SuperSocket.ClientEngine
{
    using System.Threading.Tasks;
    using System.Net;

    public interface IProxyConnector
    {
        Task<ProxyEventArgs> ConnectAsync(EndPoint remoteEndPoint);
    }
}
