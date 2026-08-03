using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.Bridge
{
    /// <summary>
    /// Manages the TCP server socket and background I/O for the bridge.
    ///
    /// Responsibilities:
    ///   - Listen on a configurable host:port (default 127.0.0.1:8765)
    ///   - Accept exactly one client at a time
    ///   - Read newline-delimited JSON in a background thread
    ///   - Place complete lines into a thread-safe incoming queue
    ///   - Send response strings back through the socket (from main thread)
    ///
    /// Architecture:
    ///   Network Thread  ──reads──>  ConcurrentQueue[string]  ──dequeued──>  Main Thread
    ///   Main Thread     ──sends──>  NetworkStream (locked)
    ///
    /// This class knows NOTHING about JSON, commands, vehicles, or physics.
    /// It only moves bytes.
    /// </summary>
    public class BridgeConnection : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _maxMessageBytes;

        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _stream;
        private Thread _receiveThread;
        private volatile bool _running;

        private readonly ConcurrentQueue<string> _incomingQueue = new ConcurrentQueue<string>();
        private readonly object _sendLock = new object();

        // --- Events ---

        /// <summary>Fired when a complete JSON line is received.</summary>
        public event Action<string> LineReceived;

        /// <summary>Protected helper for derived classes to fire LineReceived.</summary>
        protected void OnLineReceived(string line)
        {
            LineReceived?.Invoke(line);
        }

        /// <summary>Fired when the client connects.</summary>
        public event Action ClientConnected;

        /// <summary>Fired when the client disconnects or the connection faults.</summary>
        public event Action ClientDisconnected;

        /// <summary>Protected helper for derived classes to fire ClientConnected.</summary>
        protected void OnClientConnected()
        {
            ClientConnected?.Invoke();
        }

        /// <summary>Protected helper for derived classes to fire ClientDisconnected.</summary>
        protected void OnClientDisconnected()
        {
            ClientDisconnected?.Invoke();
        }

        // --- State tracking ---

        public enum ConnectionState
        {
            Disconnected,
            Listening,
            Connected,
            Faulted
        }

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public BridgeConnection(string host, int port, int maxMessageBytes = 65536)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _maxMessageBytes = maxMessageBytes > 0 ? maxMessageBytes : 65536;
        }

        /// <summary>
        /// Start listening for a client connection on the configured host:port.
        /// </summary>
        public void StartListening()
        {
            if (_running) return;

            try
            {
                var ip = IPAddress.Parse(_host);
                _listener = new TcpListener(ip, _port);
                _listener.Start();
                State = ConnectionState.Listening;
                _running = true;

                SimLog.Info($"[BRIDGE] Listening on {_host}:{_port}");

                // Accept one client in background thread
                _receiveThread = new Thread(AcceptAndReceiveLoop)
                {
                    IsBackground = true,
                    Name = "BridgeReceive"
                };
                _receiveThread.Start();
            }
            catch (Exception ex)
            {
                State = ConnectionState.Faulted;
                SimLog.Error($"[BRIDGE] Failed to start listener: {ex.Message}");
            }
        }

        public void Stop()
        {
            _running = false;

            // Close socket to unblock the receive thread
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            try { _listener?.Stop(); } catch { }

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                if (!_receiveThread.Join(2000))
                {
                    _receiveThread.Abort();
                }
            }

            _stream = null;
            _client = null;
            _listener = null;
            State = ConnectionState.Disconnected;
        }

        /// <summary>
        /// Try to dequeue a received line. Returns false if queue is empty.
        /// </summary>
        public virtual bool TryDequeueLine(out string line)
        {
            return _incomingQueue.TryDequeue(out line);
        }

        /// <summary>
        /// Send a JSON string to the connected client. Thread-safe.
        /// </summary>
        public virtual bool Send(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;

            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            return SendBytes(data);
        }

        /// <summary>
        /// Send a JSON header followed by a binary payload atomically (under one lock).
        /// Format: JSON header line (UTF-8) + newline + N raw bytes.
        /// The Python side reads: JSON line, then parses "length" from header, then reads
        /// exactly that many raw bytes.
        /// </summary>
        /// <param name="jsonHeader">JSON header string (without trailing newline — added automatically).</param>
        /// <param name="binaryPayload">Raw binary data to send after the header.</param>
        /// <returns>True if sent successfully.</returns>
        public virtual bool SendJsonWithBinary(string jsonHeader, byte[] binaryPayload)
        {
            if (string.IsNullOrEmpty(jsonHeader)) return false;
            if (binaryPayload == null) return Send(jsonHeader);

            lock (_sendLock)
            {
                try
                {
                    // Send JSON header + newline
                    byte[] headerBytes = Encoding.UTF8.GetBytes(jsonHeader + "\n");
                    _stream.Write(headerBytes, 0, headerBytes.Length);

                    // Send binary payload
                    _stream.Write(binaryPayload, 0, binaryPayload.Length);
                    _stream.Flush();
                    return true;
                }
                catch (Exception ex)
                {
                    SimLog.Warning($"[BRIDGE] SendJsonWithBinary failed: {ex.Message}");
                    HandleDisconnect();
                    return false;
                }
            }
        }

        private bool SendBytes(byte[] data)
        {
            if (_stream == null || !_running) return false;

            lock (_sendLock)
            {
                try
                {
                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();
                    return true;
                }
                catch (Exception ex)
                {
                    SimLog.Warning($"[BRIDGE] Send failed: {ex.Message}");
                    HandleDisconnect();
                    return false;
                }
            }
        }

        private void AcceptAndReceiveLoop()
        {
            while (_running)
            {
                TcpClient newClient = null;

                // Wait for a connection using polling so we can be stopped cleanly
                // and re-accept after a client disconnects (reconnection support).
                while (_running && newClient == null)
                {
                    try
                    {
                        if (_listener.Pending())
                        {
                            newClient = _listener.AcceptTcpClient();
                        }
                        else
                        {
                            Thread.Sleep(50); // Avoid busy-wait
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (_running)
                            SimLog.Warning($"[BRIDGE] Accept error: {ex.Message}");
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break; // Listener was closed
                    }
                }

                if (!_running || newClient == null) break;

                // Reject a second client if one is still active
                if (_client != null && _client.Connected)
                {
                    try
                    {
                        var stream = newClient.GetStream();
                        var errorMsg = new BridgeMessage
                        {
                            Type = "error",
                            Error = new BridgeErrorDetail
                            {
                                Code = "CLIENT_ALREADY_CONNECTED",
                                Message = "Only one control client is allowed at a time"
                            }
                        };
                        byte[] data = Encoding.UTF8.GetBytes(BridgeProtocol.Serialize(errorMsg) + "\n");
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                        stream.Close();
                        newClient.Close();
                        SimLog.Warning("[BRIDGE] Rejected second client: CLIENT_ALREADY_CONNECTED");
                    }
                    catch { }
                    continue;
                }

                // Accept the client
                _client = newClient;
                _client.NoDelay = true;
                _stream = _client.GetStream();

                State = ConnectionState.Connected;
                SimLog.Info($"[BRIDGE] Client connected from {_client.Client.RemoteEndPoint}");
                ClientConnected?.Invoke();

                // Read loop until disconnect
                RunReadLoop();

                HandleDisconnect();
                // Loop back to accept the next client (supports reconnection)
            }
        }

        private void RunReadLoop()
        {
            var buffer = new byte[4096];
            var lineBuilder = new StringBuilder();

            while (_running && _client != null && _client.Connected)
            {
                int bytesRead = 0;
                try
                {
                    bytesRead = _stream.Read(buffer, 0, buffer.Length);
                }
                catch (IOException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }

                if (bytesRead == 0) break; // graceful disconnect

                string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                for (int i = 0; i < chunk.Length; i++)
                {
                    char c = chunk[i];
                    if (c == '\n')
                    {
                        string line = lineBuilder.ToString();
                        lineBuilder.Clear();

                        if (line.Length > 0)
                        {
                            if (line.Length > _maxMessageBytes)
                            {
                                SimLog.Warning($"[BRIDGE] Oversized message ({line.Length} bytes), discarding");
                                continue;
                            }

                            _incomingQueue.Enqueue(line);
                            OnLineReceived(line);
                        }
                    }
                    else if (c != '\r')
                    {
                        lineBuilder.Append(c);
                    }
                }
            }
        }

        private void HandleDisconnect()
        {
            if (State == ConnectionState.Connected || State == ConnectionState.Listening)
            {
                State = ConnectionState.Disconnected;
                SimLog.Info("[BRIDGE] Client disconnected");

                try { _stream?.Close(); } catch { }
                try { _client?.Close(); } catch { }

                _stream = null;
                _client = null;

                OnClientDisconnected();
            }
        }

        public virtual void Dispose()
        {
            Stop();
        }
    }
}
