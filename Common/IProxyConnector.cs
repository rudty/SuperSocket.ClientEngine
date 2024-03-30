namespace SuperSocket.ClientEngine
{
    using System.Threading.Tasks;
    using System.Net;

    public interface IProxyConnector
    {
        Task<ProxyResult> ConnectAsync(EndPoint remoteEndPoint);
    }
}
