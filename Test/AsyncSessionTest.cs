namespace Test;

using System.Net.Sockets;
using System.Threading.Tasks;
using SuperSocket.ClientEngine;

public class AsyncSessionTest : UnitTestBase
{
    public AsyncSessionTest() : base(new AsyncTcpSession())
    {
    }

    internal override Task<Stream> GetServerStream(Socket clientSocket)
    {
        return Task.FromResult<Stream>(new NetworkStream(clientSocket));
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
