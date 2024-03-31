namespace Test;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using SuperSocket.ClientEngine;

public abstract class UnitTestBase : IDisposable
{
    internal static int ServerPort = 14346;
    internal const int ReceiveBufferSize = 1024;
    
    private readonly Socket serverSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    internal readonly CancellationTokenSource testCancellationTokenSource = new();
    internal readonly IPEndPoint LocalHostEndPoint = new(IPAddress.Loopback, Interlocked.Increment(ref ServerPort));

    internal readonly ClientSession clientSession;
    internal UnitTestBase(ClientSession client)
    {
        ListenAndServe();
        clientSession = client;
    }

    public async Task<ClientSession> GetClientAndConnectAsync()
    {
        if (!clientSession.IsConnected)
        {
            await clientSession.ConnectAsync(LocalHostEndPoint);
        }

        return clientSession;
    }

    internal async Task<ArraySegment<byte>> ConnectSendReceive1(ArraySegment<byte> sendData)
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

    internal async Task<ArraySegment<byte>[]> ConnectSendReceiveRepeat(ArraySegment<byte> sendData, int repeatCount)
    {
        var session = await GetClientAndConnectAsync();

        var queue = new ConcurrentQueue<ArraySegment<byte>>();
        session.DataReceived += (_, args) =>
        {
            for (var i = 0; i < repeatCount; ++i)
            {
                queue.Enqueue(new ArraySegment<byte>(args.Data, args.Offset, args.Length));
            }
        };

        var ret = new List<ArraySegment<byte>>(repeatCount);
        for (var it = 0; it < repeatCount; ++it)
        {
            session.Send(sendData);
            while (queue.Count is 0)
            {
                await Task.Delay(5);
            }

            if (!queue.TryDequeue(out var seg))
            {
                Assert.Fail($"{nameof(queue)}.{nameof(queue.TryDequeue)} fail");
            }

            ret.Add(seg);
        }

        session.Close();
        return ret.ToArray();
    }

    internal void ListenAndServe()
    {
        serverSocket.Bind(LocalHostEndPoint);
        serverSocket.Listen(100);
        _ = AcceptAsync();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                var clientSocket = await serverSocket.AcceptAsync(testCancellationTokenSource.Token).ConfigureAwait(false);
                _ = RunEcho(clientSocket);
            }
        }
        catch
        {
            // ignore
        }
    }

    private async Task RunEcho(Socket clientSocket)
    {
        var b = new byte[ReceiveBufferSize];
        try
        {
            await using var stream = await GetServerStream(clientSocket);
            while (!testCancellationTokenSource.IsCancellationRequested)
            {
                var len = await stream.ReadAsync(b);
                if (len == 0)
                {
                    break;
                }

                await stream.WriteAsync(new ReadOnlyMemory<byte>(b, 0, len), testCancellationTokenSource.Token);
            }
        }
        catch
        {
            // ignore
            
        }

        ShutdownSocket(clientSocket);
    }

    internal abstract Task<Stream> GetServerStream(Socket clientSocket);

    internal static void ShutdownSocket(Socket socket)
    {
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
            // ignore
        }

        try
        {
            socket.Dispose();
        }
        catch
        {
            // ignore
        }
    }

    public void Dispose()
    {
        try
        {
            testCancellationTokenSource.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            testCancellationTokenSource.Dispose();
        }
        catch
        {
            // ignore
        }

        ShutdownSocket(serverSocket);
    }
}