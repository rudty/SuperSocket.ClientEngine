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

        var sendData = MakeRandomByteArrayPacket();
        session.Send(new ArraySegment<byte>(sendData));
        await Task.Delay(500);
        session.Close();
    }

    private async Task<ArraySegment<byte>> SendAndReceive(ArraySegment<byte> sendData)
    {
        var session = await GetClientAndConnectAsync();

        var receiveCompletionSource = new TaskCompletionSource<ArraySegment<byte>>();
        session.DataReceived += (object? sender, DataEventArgs args) =>
        {
            receiveCompletionSource.SetResult(new ArraySegment<byte>(args.Data, args.Offset, args.Length));
        };
        session.Send(sendData);

        var receiveData = await receiveCompletionSource.Task;
        session.Close();
        return receiveData;
    }

    [Fact]
    public async Task SendAndReceive1()
    {
        var sendData = MakeRandomByteArrayPacket();
        var receiveData = await SendAndReceive(sendData);
        Assert.Equal(sendData, receiveData);
    }
}
