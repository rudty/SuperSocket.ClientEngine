namespace Test;

using SuperSocket.ClientEngine;
using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

public class SslStreamTcpSessionTest : UnitTestBase
{
    public SslStreamTcpSessionTest() : base(new SslStreamTcpSession
    {
        Security = new SecurityOption
        {
            EnabledSslProtocols = SslProtocols.Tls12,
            AllowUnstrustedCertificate = true,
            //AllowNameMismatchCertificate = true,
        }
    })
    {
    }

    internal override async Task<Stream> GetServerStream(Socket clientSocket)
    {
        var sslStream = new SslStream(new NetworkStream(clientSocket), false, delegate
        {
            return true;
        });

        var opt = new SslServerAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls12,
            AllowTlsResume = false,
            AllowRenegotiation = false,
            ClientCertificateRequired = false,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            ServerCertificate = new X509Certificate2(SslKey.LocalHostBlob, SslKey.LocalHostPassword),
        };

        await sslStream.AuthenticateAsServerAsync(opt, testCancellationTokenSource.Token);

        return sslStream;
    }

    [Fact]
    public async Task Connect()
    {
        var session = await GetClientAndConnectAsync();
        Assert.True(session.IsConnected);
        session.Close();
    }

    [Fact]
    public async Task Send()
    {
        var session = await GetClientAndConnectAsync();
        var sendData = TestUtil.MakeRandomByteArrayPacket();
        session.Send(new ArraySegment<byte>(sendData));
        await Task.Delay(500);
        session.Close();
    }

    [Fact]
    public async Task SendAndReceive()
    {
        var sendData = TestUtil.MakeRandomByteArrayPacket();
        var receiveData = await ConnectSendReceive1(sendData);
        Assert.Equal(sendData, receiveData);
    }

    [Fact]
    public async Task SendAndReceive5()
    {
        const int repeatCount = 5;
        var sendData = TestUtil.MakeRandomByteArrayPacket();
        var receiveData = await ConnectSendReceiveRepeat(sendData, repeatCount);
        Assert.Equal(repeatCount, receiveData.Length);

        for (var i = 0; i < repeatCount; ++i)
        {
            Assert.Equal(sendData, receiveData[i]);
        }
    }
}
