namespace Test;

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

    /// <summary>
    /// Get Random byte[]
    /// </summary>
    /// <param name="size">random size</param>
    public static byte[] MakeRandomByteArrayPacket(int size = 10)
    {
        var b = new byte[size];
        Random.Shared.NextBytes(b);

        return b;
    }

    public static byte[] ByteArrayMultiply(ArraySegment<byte> b, int repeatCount)
    {
        var multiplyByteArray = new byte[b.Count * repeatCount];
        for (var r = 0; r < repeatCount; ++r)
        {
            Buffer.BlockCopy(b.Array!, b.Offset, multiplyByteArray, r * b.Count, b.Count);
        }

        return multiplyByteArray;
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