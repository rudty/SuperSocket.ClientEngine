namespace Test;

using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Buffer = System.Buffer;

public class TcpUnitTestBase : IDisposable
{
    internal static int ServerPort = 14346;
    
    private readonly Socket serverSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private readonly CancellationTokenSource testCancellationTokenSource = new();
    internal readonly IPEndPoint LocalHostEndPoint = new(IPAddress.Loopback, Interlocked.Increment(ref ServerPort));
    internal TcpUnitTestBase()
    {
        ListenAndServe();
    }

    /// <summary>
    /// Get Random byte[]
    /// </summary>
    /// <param name="repeatCount">server response count</param>
    /// <param name="size">random size</param>
    public static byte[] MakeRandomByteArrayPacket(int repeatCount, int size)
    {
        var b = new byte[size];
        Random.Shared.NextBytes(b);
        b[0] = (byte)repeatCount;

        return b;
    }

    public static byte[] ByteArrayMultiply(byte[] b, int repeatCount)
    {
        var multiplyByteArray = new byte[b.Length * repeatCount];
        for (var r = 0; r < repeatCount; ++r)
        {
            Buffer.BlockCopy(b, 0, multiplyByteArray, r * b.Length, b.Length);
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
                _ = DoEcho(clientSocket);
            }
        }
        catch
        {
            // ignore
        }
    }

    internal async Task DoEcho(Socket clientSocket) 
    {
        var b = new byte[1024];
        try
        {
            while (!testCancellationTokenSource.IsCancellationRequested)
            {
                var len = await clientSocket.ReceiveAsync(b, testCancellationTokenSource.Token).ConfigureAwait(false);
                var repeatCount = (int)b[0];
                var sendList = new List<ArraySegment<byte>>();
                for (var i = 0; i < repeatCount; i++)
                {
                    sendList.Add(new ArraySegment<byte>(b, 0, len));
                }

                await clientSocket.SendAsync(sendList, SocketFlags.None).ConfigureAwait(false);
            }
        }
        catch
        {
            ShutdownSocket(clientSocket);
        }
    }

    private void ShutdownSocket(Socket socket)
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