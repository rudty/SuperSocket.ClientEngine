namespace SuperSocket.ClientEngine
{
    using System;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    public class SslStreamTcpSession : AuthenticatedStreamTcpSession
    {
        protected override async Task<AuthenticatedStream> StartAuthenticatedStream(Socket client)
        {
            var securityOption = Security;

            if (securityOption == null)
            {
                throw new Exception("securityOption was not configured");
            }

            SslStream sslStream;
            try
            {
                sslStream = new SslStream(new NetworkStream(client), false, ValidateRemoteCertificate);
                await sslStream.AuthenticateAsClientAsync(HostName, securityOption.Certificates,
                    securityOption.EnabledSslProtocols, false);
            }
            catch
            {
                EnsureSocketClosed();
                throw;
            }

            return sslStream;
        }

        /// <summary>
        /// Validates the remote certificate.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="certificate">The certificate.</param>
        /// <param name="chain">The chain.</param>
        /// <param name="sslPolicyErrors">The SSL policy errors.</param>
        /// <returns></returns>
        private bool ValidateRemoteCertificate(
            object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            if (Security.AllowNameMismatchCertificate)
            {
                sslPolicyErrors = sslPolicyErrors & (~SslPolicyErrors.RemoteCertificateNameMismatch);
            }

            if (Security.AllowCertificateChainErrors)
            {
                sslPolicyErrors = sslPolicyErrors & (~SslPolicyErrors.RemoteCertificateChainErrors);
            }

            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            if (!Security.AllowUnstrustedCertificate)
            {
                OnError(new Exception(sslPolicyErrors.ToString()));
                return false;
            }

            // not only a remote certificate error
            if (sslPolicyErrors != SslPolicyErrors.None &&
                sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
            {
                OnError(new Exception(sslPolicyErrors.ToString()));
                return false;
            }

            if (chain != null && chain.ChainStatus != null)
            {
                foreach (X509ChainStatus status in chain.ChainStatus)
                {
                    if ((certificate.Subject == certificate.Issuer) &&
                        (status.Status == X509ChainStatusFlags.UntrustedRoot))
                    {
                        // Self-signed certificates with an untrusted root are valid. 
                        continue;
                    }
                    else
                    {
                        if (status.Status != X509ChainStatusFlags.NoError)
                        {
                            OnError(new Exception(sslPolicyErrors.ToString()));
                            // If there are any other errors in the certificate chain, the certificate is invalid,
                            // so the method returns false.
                            return false;
                        }
                    }
                }
            }

            // When processing reaches this line, the only errors in the certificate chain are 
            // untrusted root errors for self-signed certificates. These certificates are valid
            // for default Exchange server installations, so return true.
            return true;
        }
    }
}
