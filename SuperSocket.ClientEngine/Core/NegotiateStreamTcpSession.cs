namespace SuperSocket.ClientEngine
{
    using System;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    public class NegotiateStreamTcpSession : AuthenticatedStreamTcpSession
    {
        protected override async Task<AuthenticatedStream> StartAuthenticatedStream(Socket client)
        {
            var securityOption = Security;

            if (securityOption == null)
            {
                throw new Exception("securityOption was not configured");
            }

            var stream = new NegotiateStream(new NetworkStream(client));

            var credential = securityOption.Credential;

            if (credential is null)
            {
                credential = (NetworkCredential)CredentialCache.DefaultCredentials;
            }

            try
            {
                await stream.AuthenticateAsClientAsync(credential, HostName);
            }
            catch
            {
                EnsureSocketClosed();
                throw;
            }

            return stream;
        }
    }
}
