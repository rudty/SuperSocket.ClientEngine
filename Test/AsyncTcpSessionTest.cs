namespace Test;

using System.Threading.Tasks;
using SuperSocket.ClientEngine;

public class AsyncTcpSessionTest : TcpUnitTestBase
{
    [Fact]
    public async Task Connect()
    {
        var tcpSession = new AsyncTcpSession();
        await tcpSession.ConnectAsync(LocalHostEndPoint);
        Assert.True(tcpSession.IsConnected);
    }

    [Fact]
    public async Task Send()
    {
        var tcpSession = new AsyncTcpSession();
        await tcpSession.ConnectAsync(LocalHostEndPoint);

        var sendData = MakeRandomByteArrayPacket(1, 10);
        tcpSession.Send(new ArraySegment<byte>(sendData));
    }

    private async Task<ArraySegment<byte>> SendAndReceive(ArraySegment<byte> sendData)
    {
        var tcpSession = new AsyncTcpSession();
        await tcpSession.ConnectAsync(LocalHostEndPoint);

        var receiveCompletionSource = new TaskCompletionSource<ArraySegment<byte>>();
        tcpSession.DataReceived += (object? sender, DataEventArgs args) =>
        {
            receiveCompletionSource.SetResult(new ArraySegment<byte>(args.Data, args.Offset, args.Length));
        };
        tcpSession.Send(sendData);

        var receiveData = await receiveCompletionSource.Task;
        return receiveData;
    }
    [Fact]
    public async Task SendAndReceive1()
    {
        var sendData = MakeRandomByteArrayPacket(1, 10);
        var receiveData = await SendAndReceive(sendData);
        Assert.Equal(sendData, receiveData);
    }

    [Fact]
    public async Task SendAndReceive10()
    {
        var sendData = MakeRandomByteArrayPacket(10, 10);
        var receiveData = await SendAndReceive(sendData);
        var expectReceiveData = ByteArrayMultiply(sendData, 10);
        Assert.Equal(expectReceiveData, receiveData);
    }
}
