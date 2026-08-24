// TCP Server with Authentication — Simpl# core
// Compile to TCP_Server_Auth.clz and reference via
// #USER_SIMPLSHARP_LIBRARY "TCP_Server_Auth"
//
// Defaults (Security off, Authentication off) behave like the built-in
// SIMPL Windows TCP/IP Server symbol. TLS uses SecureTCPServer and the
// processor self-signed certificate (ssl self). AUTH is optional.

using System;
using System.Text;

using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharp.Cryptography;

namespace TcpServerAuth
{
    public class TcpAuthServer
    {
        private const int DefaultBufferSize = 4096;
        private const int MaxClientsHardCap = 16;
        private const int MinHandshakeSeconds = 30;

        private static readonly object InitLock = new object();
        private static readonly object IoLock = new object();
        private static readonly byte[] HmacKey = Encoding.UTF8.GetBytes("TcpAuthServer.v1");

        private static TcpAuthServer _instance;
        private static bool _initialized;

        private static TCPServer _plain;
        private static SecureTCPServer _secure;
        private static bool _useTls;
        private static bool _authRequired;
        private static bool _wantListen;
        private static bool _listening;
        private static int _port = 50001;
        private static int _maxClients = 4;
        private static int _sendIndex;
        private static string _username = "user";
        private static string _password = "pass";
        private static int _authTimeoutMs = 10000;

        private static ClientSlot[] _clients;
        private static CMutex _sendMutex;

        private static ServerStateHandler _stateHandlers;
        private static ClientChangeHandler _clientHandlers;
        private static LineReceivedHandler _lineHandlers;
        private static ErrorHandler _errorHandlers;

        public delegate void ServerStateHandler(ushort listening, SimplSharpString msg);
        public delegate void ClientChangeHandler(ushort clientIndex, ushort connected, ushort authenticated, SimplSharpString ip);
        public delegate void LineReceivedHandler(ushort clientIndex, SimplSharpString line);
        public delegate void ErrorHandler(SimplSharpString msg);

        public ServerStateHandler OnServerStateChanged
        {
            get { return null; }
            set
            {
                EnsureInitialized();
                if (value != null)
                    _stateHandlers = (ServerStateHandler)Delegate.Combine(_stateHandlers, value);
            }
        }

        public ClientChangeHandler OnClientChanged
        {
            get { return null; }
            set
            {
                EnsureInitialized();
                if (value != null)
                    _clientHandlers = (ClientChangeHandler)Delegate.Combine(_clientHandlers, value);
            }
        }

        public LineReceivedHandler OnLineReceived
        {
            get { return null; }
            set
            {
                EnsureInitialized();
                if (value != null)
                    _lineHandlers = (LineReceivedHandler)Delegate.Combine(_lineHandlers, value);
            }
        }

        public ErrorHandler OnError
        {
            get { return null; }
            set
            {
                EnsureInitialized();
                if (value != null)
                    _errorHandlers = (ErrorHandler)Delegate.Combine(_errorHandlers, value);
            }
        }

        public TcpAuthServer()
        {
            EnsureInitialized();
            lock (InitLock)
            {
                if (_instance == null)
                    _instance = this;
            }
        }

        public static TcpAuthServer Instance
        {
            get
            {
                EnsureInitialized();
                lock (InitLock)
                {
                    if (_instance == null)
                        _instance = new TcpAuthServer();
                    return _instance;
                }
            }
        }

        public void SetPort(ushort port)
        {
            EnsureInitialized();
            if (port > 0)
                _port = (int)port;
        }

        public void SetMaxClients(ushort count)
        {
            EnsureInitialized();
            int n = (int)count;
            if (n < 1)
                n = 1;
            if (n > MaxClientsHardCap)
                n = MaxClientsHardCap;
            _maxClients = n;
        }

        public void SetSecurity(ushort on)
        {
            EnsureInitialized();
            _useTls = (on != 0);
        }

        public void SetAuthentication(ushort on)
        {
            EnsureInitialized();
            _authRequired = (on != 0);
        }

        public void SetUsername(string user)
        {
            EnsureInitialized();
            _username = user == null ? "" : user;
        }

        public void SetPassword(string pass)
        {
            EnsureInitialized();
            _password = pass == null ? "" : pass;
        }

        public void SetAuthTimeoutSeconds(ushort seconds)
        {
            EnsureInitialized();
            int s = (int)seconds;
            if (s < 1)
                s = 1;
            if (s > 120)
                s = 120;
            _authTimeoutMs = s * 1000;
        }

        public void SetClientIndex(ushort index)
        {
            EnsureInitialized();
            _sendIndex = (int)index;
        }

        public void Start()
        {
            EnsureInitialized();
            lock (IoLock)
            {
                _wantListen = true;
                StartListenerUnlocked();
            }
        }

        public void Stop()
        {
            EnsureInitialized();
            lock (IoLock)
            {
                _wantListen = false;
                StopListenerUnlocked("Stopped");
            }
        }

        public void Restart()
        {
            EnsureInitialized();
            lock (IoLock)
            {
                StopListenerUnlocked("Restarting");
                if (_wantListen)
                    StartListenerUnlocked();
            }
        }

        public void Send(string data)
        {
            EnsureInitialized();
            if (data == null)
                data = "";
            byte[] bytes = Encoding.ASCII.GetBytes(data);
            lock (IoLock)
            {
                if (_sendIndex == 0)
                {
                    int i;
                    for (i = 1; i <= _maxClients; i++)
                    {
                        if (_clients[i] != null && _clients[i].Connected && _clients[i].Authenticated)
                            SendToUnlocked((uint)i, bytes);
                    }
                }
                else
                {
                    SendToUnlocked((uint)_sendIndex, bytes);
                }
            }
        }

        public void DisconnectAllClients()
        {
            EnsureInitialized();
            lock (IoLock)
            {
                int i;
                for (i = 1; i <= _maxClients; i++)
                    DropClientUnlocked((uint)i, false);
                if (_plain != null)
                    _plain.DisconnectAll();
                if (_secure != null)
                    _secure.DisconnectAll();
            }
        }

        public void DisconnectClient(ushort index)
        {
            EnsureInitialized();
            lock (IoLock)
            {
                DropClientUnlocked((uint)index, true);
            }
        }

        private static void EnsureInitialized()
        {
            lock (InitLock)
            {
                if (_initialized)
                    return;
                _clients = new ClientSlot[MaxClientsHardCap + 1];
                _sendMutex = new CMutex();
                _initialized = true;
            }
        }

        private static void StartListenerUnlocked()
        {
            if (_authRequired)
            {
                if (_username == null || _username.Length == 0 || _password == null || _password.Length == 0)
                {
                    RaiseError("Authentication is on but username or password is empty");
                    _listening = false;
                    RaiseState(0, "Not listening");
                    return;
                }
            }

            StopSocketsUnlocked();
            AllocateClientsUnlocked();

            try
            {
                if (_useTls)
                {
                    _secure = new SecureTCPServer(_port, DefaultBufferSize, EthernetAdapterType.EthernetUnknownAdapter, _maxClients);
                    _secure.HandshakeTimeout = MinHandshakeSeconds;
                    _secure.SocketStatusChange += new SecureTCPServerSocketStatusChangeEventHandler(SecureStatusChanged);
                    SocketErrorCodes err = _secure.WaitForConnectionAsync(SecureConnected);
                    if (err != SocketErrorCodes.SOCKET_OK && err != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                    {
                        RaiseError("Secure listen failed: " + err.ToString() + " (enable SSL on the processor: ssl self)");
                        _listening = false;
                        RaiseState(0, "Listen failed");
                        return;
                    }
                }
                else
                {
                    _plain = new TCPServer("0.0.0.0", _port, DefaultBufferSize, EthernetAdapterType.EthernetUnknownAdapter, _maxClients);
                    _plain.SocketStatusChange += new TCPServerSocketStatusChangeEventHandler(PlainStatusChanged);
                    SocketErrorCodes err = _plain.WaitForConnectionAsync(PlainConnected);
                    if (err != SocketErrorCodes.SOCKET_OK && err != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                    {
                        RaiseError("Listen failed: " + err.ToString());
                        _listening = false;
                        RaiseState(0, "Listen failed");
                        return;
                    }
                }

                _listening = true;
                string mode = _useTls ? "TLS" : "plain TCP";
                string auth = _authRequired ? "AUTH on" : "AUTH off";
                RaiseState(1, "Listening on " + _port.ToString() + " (" + mode + ", " + auth + ")");
                if (_authRequired && !_useTls)
                    RaiseError("Authentication without TLS sends the password in the clear");
            }
            catch (Exception ex)
            {
                string extra = _useTls ? " (enable SSL on the processor: ssl self)" : "";
                RaiseError("Listen exception: " + ex.Message + extra);
                _listening = false;
                RaiseState(0, "Listen failed");
            }
        }

        private static void StopListenerUnlocked(string reason)
        {
            int i;
            for (i = 1; i <= MaxClientsHardCap; i++)
                DropClientUnlocked((uint)i, false);
            StopSocketsUnlocked();
            _listening = false;
            RaiseState(0, reason);
        }

        private static void StopSocketsUnlocked()
        {
            try
            {
                if (_plain != null)
                {
                    _plain.DisconnectAll();
                    _plain.Stop();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: stop plain: {0}", ex.Message);
            }
            _plain = null;

            try
            {
                if (_secure != null)
                {
                    _secure.DisconnectAll();
                    _secure.Stop();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: stop secure: {0}", ex.Message);
            }
            _secure = null;
        }

        private static void AllocateClientsUnlocked()
        {
            int i;
            for (i = 0; i < _clients.Length; i++)
            {
                if (_clients[i] != null)
                    _clients[i].DisposeTimer();
                _clients[i] = null;
            }
            for (i = 1; i <= _maxClients; i++)
                _clients[i] = new ClientSlot();
        }

        private static void PlainConnected(TCPServer server, uint clientIndex)
        {
            HandleConnected(clientIndex, false);
        }

        private static void SecureConnected(SecureTCPServer server, uint clientIndex)
        {
            HandleConnected(clientIndex, true);
        }

        private static void HandleConnected(uint clientIndex, bool tls)
        {
            lock (IoLock)
            {
                if (clientIndex == 0)
                {
                    bool stillListening = false;
                    if (tls && _secure != null)
                        stillListening = (_secure.State & ServerState.SERVER_NOT_LISTENING) == 0;
                    else if (!tls && _plain != null)
                        stillListening = (_plain.State & ServerState.SERVER_NOT_LISTENING) == 0;

                    if (_wantListen && stillListening)
                        RearmAcceptUnlocked(tls);
                    else if (_wantListen)
                    {
                        _listening = false;
                        RaiseState(0, "Server is no longer listening");
                    }
                    return;
                }

                ClientSlot slot = Slot(clientIndex);
                if (slot == null)
                {
                    DisconnectSocketUnlocked(clientIndex);
                    RearmAcceptUnlocked(tls);
                    return;
                }

                slot.Connected = true;
                slot.Authenticated = !_authRequired;
                slot.Buffer.Length = 0;
                slot.Ip = GetClientIpUnlocked(clientIndex, tls);

                RaiseClient((ushort)clientIndex, 1, (ushort)(slot.Authenticated ? 1 : 0), slot.Ip);

                if (_authRequired)
                {
                    SendAsciiUnlocked(clientIndex, "220 TCP Server with Authentication ready\r\n");
                    StartAuthTimerUnlocked(clientIndex, slot);
                }

                RearmReceiveUnlocked(clientIndex, tls);
                RearmAcceptUnlocked(tls);
            }
        }

        private static void PlainReceived(TCPServer server, uint clientIndex, int bytesReceived)
        {
            HandleReceived(clientIndex, bytesReceived, false);
        }

        private static void SecureReceived(SecureTCPServer server, uint clientIndex, int bytesReceived)
        {
            HandleReceived(clientIndex, bytesReceived, true);
        }

        private static void HandleReceived(uint clientIndex, int bytesReceived, bool tls)
        {
            lock (IoLock)
            {
                if (bytesReceived <= 0)
                {
                    DropClientUnlocked(clientIndex, true);
                    if (_wantListen)
                        RearmAcceptUnlocked(tls);
                    return;
                }

                ClientSlot slot = Slot(clientIndex);
                if (slot == null || !slot.Connected)
                    return;

                byte[] buf = GetBufferUnlocked(clientIndex, tls);
                if (buf == null)
                {
                    RearmReceiveUnlocked(clientIndex, tls);
                    return;
                }

                int n = bytesReceived;
                if (n > buf.Length)
                    n = buf.Length;

                if (slot.Authenticated)
                {
                    string payload = Encoding.ASCII.GetString(buf, 0, n);
                    RaiseLine((ushort)clientIndex, payload);
                }
                else
                {
                    slot.Buffer.Append(Encoding.ASCII.GetString(buf, 0, n));
                    ProcessAuthBufferUnlocked(clientIndex, slot);
                }

                if (slot.Connected)
                    RearmReceiveUnlocked(clientIndex, tls);
            }
        }

        private static void ProcessAuthBufferUnlocked(uint clientIndex, ClientSlot slot)
        {
            while (true)
            {
                string all = slot.Buffer.ToString();
                int nl = all.IndexOf('\n');
                if (nl < 0)
                    return;

                string line = all.Substring(0, nl);
                slot.Buffer.Remove(0, nl + 1);
                if (line.EndsWith("\r"))
                    line = line.Substring(0, line.Length - 1);

                HandleAuthLineUnlocked(clientIndex, slot, line);
                if (!slot.Connected || slot.Authenticated)
                    return;
            }
        }

        private static void HandleAuthLineUnlocked(uint clientIndex, ClientSlot slot, string line)
        {
            string cmd = line.Trim();
            bool ok = false;
            if (cmd.Length >= 4 && string.Compare(cmd.Substring(0, 4), "AUTH", true) == 0)
            {
                string rest = cmd.Substring(4).Trim();
                string user = rest;
                string pass = "";
                int sp = rest.IndexOf(' ');
                if (sp >= 0)
                {
                    user = rest.Substring(0, sp);
                    pass = rest.Substring(sp + 1);
                }
                ok = CredentialsMatch(user, pass);
            }

            if (ok)
            {
                slot.DisposeTimer();
                slot.Authenticated = true;
                SendAsciiUnlocked(clientIndex, "230 Authenticated\r\n");
                RaiseClient((ushort)clientIndex, 1, 1, slot.Ip);
            }
            else
            {
                SendAsciiUnlocked(clientIndex, "535 Authentication failed\r\n");
                RaiseError("Authentication failed from " + slot.Ip);
                DropClientUnlocked(clientIndex, true);
            }
        }

        private static bool CredentialsMatch(string user, string pass)
        {
            byte[] offered = Digest(user, pass);
            byte[] expected = Digest(_username, _password);
            return FixedTimeEquals(offered, expected);
        }

        private static byte[] Digest(string user, string pass)
        {
            string material = (user == null ? "" : user) + "\n" + (pass == null ? "" : pass);
            HMACSHA256 hmac = new HMACSHA256(HmacKey);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(material));
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null)
                return false;
            int len = a.Length;
            if (b.Length != len)
                return false;
            int diff = 0;
            int i;
            for (i = 0; i < len; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static void StartAuthTimerUnlocked(uint clientIndex, ClientSlot slot)
        {
            slot.DisposeTimer();
            slot.Timer = new CTimer(AuthTimedOut, (int)clientIndex, _authTimeoutMs);
        }

        private static void AuthTimedOut(object userObj)
        {
            uint clientIndex = 0;
            try
            {
                clientIndex = (uint)(int)userObj;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: auth timer: {0}", ex.Message);
                return;
            }

            lock (IoLock)
            {
                ClientSlot slot = Slot(clientIndex);
                if (slot == null || !slot.Connected || slot.Authenticated)
                    return;
                SendAsciiUnlocked(clientIndex, "535 Authentication timeout\r\n");
                RaiseError("Authentication timeout from " + slot.Ip);
                DropClientUnlocked(clientIndex, true);
            }
        }

        private static void PlainStatusChanged(TCPServer server, uint clientIndex, SocketStatus status)
        {
            if (status != SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                lock (IoLock)
                {
                    DropClientUnlocked(clientIndex, false);
                }
            }
        }

        private static void SecureStatusChanged(SecureTCPServer server, uint clientIndex, SocketStatus status)
        {
            if (status != SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                lock (IoLock)
                {
                    DropClientUnlocked(clientIndex, false);
                }
            }
        }

        private static void DropClientUnlocked(uint clientIndex, bool disconnectSocket)
        {
            ClientSlot slot = Slot(clientIndex);
            if (slot == null)
                return;
            bool wasConnected = slot.Connected;
            string ip = slot.Ip;
            slot.DisposeTimer();
            slot.Connected = false;
            slot.Authenticated = false;
            slot.Buffer.Length = 0;
            if (disconnectSocket)
                DisconnectSocketUnlocked(clientIndex);
            if (wasConnected)
                RaiseClient((ushort)clientIndex, 0, 0, ip);
        }

        private static void DisconnectSocketUnlocked(uint clientIndex)
        {
            try
            {
                if (_plain != null)
                    _plain.Disconnect(clientIndex);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: plain disconnect {0}: {1}", clientIndex, ex.Message);
            }
            try
            {
                if (_secure != null)
                    _secure.Disconnect(clientIndex);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: secure disconnect {0}: {1}", clientIndex, ex.Message);
            }
        }

        private static void SendToUnlocked(uint clientIndex, byte[] bytes)
        {
            ClientSlot slot = Slot(clientIndex);
            if (slot == null || !slot.Connected || !slot.Authenticated)
                return;
            if (bytes == null || bytes.Length == 0)
                return;

            _sendMutex.WaitForMutex();
            try
            {
                if (_plain != null)
                    _plain.SendData(clientIndex, bytes, bytes.Length);
                else if (_secure != null)
                    _secure.SendData(clientIndex, bytes, bytes.Length);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: send {0}: {1}", clientIndex, ex.Message);
            }
            finally
            {
                _sendMutex.ReleaseMutex();
            }
        }

        private static void SendAsciiUnlocked(uint clientIndex, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            _sendMutex.WaitForMutex();
            try
            {
                if (_plain != null)
                    _plain.SendData(clientIndex, bytes, bytes.Length);
                else if (_secure != null)
                    _secure.SendData(clientIndex, bytes, bytes.Length);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: send ascii {0}: {1}", clientIndex, ex.Message);
            }
            finally
            {
                _sendMutex.ReleaseMutex();
            }
        }

        private static void RearmAcceptUnlocked(bool tls)
        {
            try
            {
                if (tls)
                {
                    if (_secure != null && _wantListen)
                        _secure.WaitForConnectionAsync(SecureConnected);
                }
                else
                {
                    if (_plain != null && _wantListen)
                        _plain.WaitForConnectionAsync(PlainConnected);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: re-arm accept: {0}", ex.Message);
            }
        }

        private static void RearmReceiveUnlocked(uint clientIndex, bool tls)
        {
            try
            {
                if (tls)
                {
                    if (_secure != null)
                        _secure.ReceiveDataAsync(clientIndex, SecureReceived);
                }
                else
                {
                    if (_plain != null)
                        _plain.ReceiveDataAsync(clientIndex, PlainReceived);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: re-arm receive {0}: {1}", clientIndex, ex.Message);
            }
        }

        private static byte[] GetBufferUnlocked(uint clientIndex, bool tls)
        {
            try
            {
                if (tls && _secure != null)
                    return _secure.GetIncomingDataBufferForSpecificClient(clientIndex);
                if (!tls && _plain != null)
                    return _plain.GetIncomingDataBufferForSpecificClient(clientIndex);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: get buffer {0}: {1}", clientIndex, ex.Message);
            }
            return null;
        }

        private static string GetClientIpUnlocked(uint clientIndex, bool tls)
        {
            try
            {
                if (tls && _secure != null)
                    return _secure.GetAddressServerAcceptedConnectionFromForSpecificClient(clientIndex);
                if (!tls && _plain != null)
                    return _plain.GetAddressServerAcceptedConnectionFromForSpecificClient(clientIndex);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: get ip {0}: {1}", clientIndex, ex.Message);
            }
            return "";
        }

        private static ClientSlot Slot(uint clientIndex)
        {
            if (clientIndex < 1 || clientIndex >= _clients.Length)
                return null;
            return _clients[clientIndex];
        }

        private static void RaiseState(ushort listening, string msg)
        {
            ServerStateHandler h = _stateHandlers;
            if (h == null)
                return;
            try
            {
                h(listening, new SimplSharpString(msg == null ? "" : msg));
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: OnServerStateChanged: {0}", ex.Message);
            }
        }

        private static void RaiseClient(ushort index, ushort connected, ushort authenticated, string ip)
        {
            ClientChangeHandler h = _clientHandlers;
            if (h == null)
                return;
            try
            {
                h(index, connected, authenticated, new SimplSharpString(ip == null ? "" : ip));
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: OnClientChanged: {0}", ex.Message);
            }
        }

        private static void RaiseLine(ushort index, string line)
        {
            LineReceivedHandler h = _lineHandlers;
            if (h == null)
                return;
            try
            {
                h(index, new SimplSharpString(line == null ? "" : line));
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: OnLineReceived: {0}", ex.Message);
            }
        }

        private static void RaiseError(string msg)
        {
            ErrorLog.Error("TcpAuthServer: {0}", msg);
            ErrorHandler h = _errorHandlers;
            if (h == null)
                return;
            try
            {
                h(new SimplSharpString(msg == null ? "" : msg));
            }
            catch (Exception ex)
            {
                ErrorLog.Error("TcpAuthServer: OnError: {0}", ex.Message);
            }
        }

        private class ClientSlot
        {
            public bool Connected;
            public bool Authenticated;
            public string Ip = "";
            public StringBuilder Buffer = new StringBuilder();
            public CTimer Timer;

            public void DisposeTimer()
            {
                if (Timer != null)
                {
                    try
                    {
                        Timer.Stop();
                    }
                    catch (Exception ex)
                    {
                        ErrorLog.Error("TcpAuthServer: timer stop: {0}", ex.Message);
                    }
                    Timer = null;
                }
            }
        }
    }
}
