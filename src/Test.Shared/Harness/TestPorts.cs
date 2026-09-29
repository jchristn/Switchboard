namespace Test.Shared.Harness
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Allocates loopback TCP ports for test servers (proxies and origins).
    /// Ports come from <see cref="RangeStart"/> to <see cref="RangeEnd"/>, below the operating
    /// system's ephemeral range (Linux 32768-60999, Windows 49152-65535). Asking the OS for port 0
    /// returns an ephemeral port, which an outbound client connection can claim in the gap between
    /// releasing the probe listener and the server binding it ("Address already in use"); ports
    /// below the ephemeral range are never used for outbound connections. A port is never handed out
    /// twice in one process. Thread-safe.
    /// </summary>
    public static class TestPorts
    {
        /// <summary>
        /// First port (inclusive) considered for allocation.
        /// </summary>
        public const int RangeStart = 20000;

        /// <summary>
        /// Last port (exclusive) considered for allocation.
        /// </summary>
        public const int RangeEnd = 32000;

        private static readonly object _Lock = new object();
        private static readonly HashSet<int> _Issued = new HashSet<int>();
        private static readonly Random _Random = new Random(Environment.ProcessId ^ Environment.TickCount);

        /// <summary>
        /// Return a port in the test range that is currently free on 127.0.0.1 and has not been
        /// returned before in this process.
        /// </summary>
        /// <returns>A free loopback port.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no free port is found after many attempts.</exception>
        public static int Next()
        {
            lock (_Lock)
            {
                for (int attempt = 0; attempt < 1000; attempt++)
                {
                    int port = _Random.Next(RangeStart, RangeEnd);
                    if (_Issued.Contains(port)) continue;
                    if (!IsFree(port)) continue;

                    _Issued.Add(port);
                    return port;
                }
            }

            throw new InvalidOperationException("No free loopback port found between " + RangeStart + " and " + RangeEnd + ".");
        }

        private static bool IsFree(int port)
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, port);
            try
            {
                probe.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                try { probe.Stop(); } catch (SocketException) { }
            }
        }
    }
}
