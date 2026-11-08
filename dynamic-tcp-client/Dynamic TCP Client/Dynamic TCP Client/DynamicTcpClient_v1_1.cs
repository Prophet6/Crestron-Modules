using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace DynamicTcp_v1_1
{
    public delegate void ConnectionHandler(ushort connected, ushort stat);
    public delegate void DataHandler(SimplSharpString data);

    public class DynamicTcpClient_v1_1
    {
        public const ushort StatusNotConnected = 0;
        public const ushort StatusWaiting = 1;
        public const ushort StatusConnected = 2;
        public const ushort StatusConnectFailed = 3;
        public const ushort StatusBrokenRemotely = 4;
        public const ushort StatusBrokenLocally = 5;
        public const ushort StatusDnsLookup = 6;
        public const ushort StatusDnsFailed = 7;
        public const ushort StatusDnsResolved = 8;

        private const int BufferSize = 4096;
        private const int RxChunk = 250;
        private const int RetryMs = 100;
        // Dead-host retries flap Waiting (1) and Not connected (0) every
        // 100 ms. Hold Status until a non-waiting code sits for 1 s.
        private const int StatusDebounceMs = 1000;

        private readonly object _sync = new object();
        private readonly CTimer _retryTimer;
        private readonly CTimer _statusDebounce;

        private ConnectionHandler _connectionHandlers;
        private DataHandler _dataHandlers;

        private TCPClient _client;
        private bool _wantConnected;
        private bool _programStopping;
        private bool _connecting;
        private bool _receiveArmed;
        private string _host = "";
        private int _port = 23;
        private ushort _status = StatusNotConnected;
        private ushort _publishedStatus = StatusNotConnected;
        private ushort _pendingStatus = StatusNotConnected;
        private bool _statusPublished;
        private bool _statusDebounceArmed;

        public ConnectionHandler OnConnectionChanged
        {
            get { return null; }
            set
            {
                if (value != null)
                    _connectionHandlers = (ConnectionHandler)Delegate.Combine(_connectionHandlers, value);
            }
        }

        public DataHandler OnDataReceived
        {
            get { return null; }
            set
            {
                if (value != null)
                    _dataHandlers = (DataHandler)Delegate.Combine(_dataHandlers, value);
            }
        }

        public DynamicTcpClient_v1_1()
        {
            _retryTimer = new CTimer(RetryCallback, null, Timeout.Infinite);
            _statusDebounce = new CTimer(StatusDebounceCallback, null, Timeout.Infinite);
            CrestronEnvironment.ProgramStatusEventHandler += ProgramStatusHandler;
        }

        public void Connect(string address, ushort port)
        {
            TCPClient startClient = null;
            ushort raiseStatus = 0;
            bool doRaise = false;
            bool start = false;

            lock (_sync)
            {
                if (_programStopping)
                    return;

                string host = address == null ? "" : address.Trim();
                int p = (port == 0) ? 23 : (int)port;
                bool sameEndpoint = (host == _host) && (p == _port);

                _host = host;
                _port = p;

                if (host.Length == 0)
                {
                    _wantConnected = false;
                    StopRetryLocked();
                    TearDownSocketLocked();
                    QueueStatusLocked(StatusNotConnected, true, ref raiseStatus, ref doRaise);
                }
                else if (sameEndpoint && (_connecting || IsUpLocked()))
                {
                    _wantConnected = true;
                    QueueStatusLocked(_status, true, ref raiseStatus, ref doRaise);
                }
                else
                {
                    _wantConnected = true;
                    StopRetryLocked();
                    TearDownSocketLocked();
                    startClient = CreateClientLocked();
                    start = (startClient != null);
                    QueueStatusLocked(StatusWaiting, true, ref raiseStatus, ref doRaise);
                }
            }

            if (doRaise)
                RaiseConnection(raiseStatus);
            if (start)
                BeginConnectAsync(startClient);
        }

        public void Disconnect()
        {
            ushort first = 0;
            ushort second = 0;
            bool raiseFirst = false;
            bool raiseSecond = false;

            lock (_sync)
            {
                _wantConnected = false;
                StopRetryLocked();
                TearDownSocketLocked();
                QueueStatusLocked(StatusBrokenLocally, true, ref first, ref raiseFirst);
                QueueStatusLocked(StatusNotConnected, true, ref second, ref raiseSecond);
            }

            if (raiseFirst)
                RaiseConnection(first);
            if (raiseSecond)
                RaiseConnection(second);
        }

        public void Send(string data)
        {
            if (data == null || data.Length == 0)
                return;

            lock (_sync)
            {
                if (_client == null)
                    return;
                if (_client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                    return;

                try
                {
                    byte[] bytes = SerialToBytes(data);
                    _client.SendData(bytes, bytes.Length);
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("DynamicTcpClient_v1_1: send: {0}", ex.Message);
                }
            }
        }

        public void PublishStatus()
        {
            ushort raiseStatus = 0;
            bool doRaise = false;

            lock (_sync)
            {
                QueueStatusLocked(_status, true, ref raiseStatus, ref doRaise);
            }

            if (doRaise)
                RaiseConnection(raiseStatus);
        }

        private bool IsUpLocked()
        {
            return _client != null && _client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED;
        }

        private TCPClient CreateClientLocked()
        {
            if (_programStopping || !_wantConnected || _host.Length == 0)
                return null;

            try
            {
                TCPClient client = new TCPClient(_host, _port, BufferSize);
                client.SocketStatusChange += SocketStatusChanged;
                _client = client;
                _connecting = true;
                _receiveArmed = false;
                return client;
            }
            catch (Exception ex)
            {
                _connecting = false;
                ErrorLog.Error("DynamicTcpClient_v1_1: create {0}:{1}: {2}", _host, _port, ex.Message);
                return null;
            }
        }

        private void BeginConnectAsync(TCPClient client)
        {
            try
            {
                SocketErrorCodes err = client.ConnectToServerAsync(ConnectCallback);
                if (err == SocketErrorCodes.SOCKET_OK || err == SocketErrorCodes.SOCKET_OPERATION_PENDING)
                    return;

                ErrorLog.Error("DynamicTcpClient_v1_1: connect: {0}", err);
                HandleConnectFailure(client, StatusConnectFailed);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: connect: {0}", ex.Message);
                HandleConnectFailure(client, StatusConnectFailed);
            }
        }

        private void HandleConnectFailure(TCPClient client, ushort status)
        {
            bool retry = false;
            ushort raiseStatus = 0;
            bool doRaise = false;

            lock (_sync)
            {
                if (client != _client)
                    return;
                _connecting = false;
                QueueStatusLocked(status, false, ref raiseStatus, ref doRaise);
                retry = ScheduleRetryLocked();
            }

            if (doRaise)
                RaiseConnection(raiseStatus);
            if (!retry)
                return;
        }

        private void ConnectCallback(TCPClient client)
        {
            TCPClient arm = null;
            bool retry = false;
            ushort raiseStatus = 0;
            bool doRaise = false;

            lock (_sync)
            {
                if (_programStopping || client != _client)
                    return;

                _connecting = false;

                if (client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    QueueStatusLocked(StatusConnected, false, ref raiseStatus, ref doRaise);
                    if (!_receiveArmed)
                    {
                        _receiveArmed = true;
                        arm = client;
                    }
                }
                else
                {
                    QueueStatusLocked(MapStatus(client.ClientStatus), false, ref raiseStatus, ref doRaise);
                    retry = ScheduleRetryLocked();
                }
            }

            if (doRaise)
                RaiseConnection(raiseStatus);
            if (arm != null)
                RearmReceive(arm);
            if (retry)
                return;
        }

        private void SocketStatusChanged(TCPClient client, SocketStatus status)
        {
            TCPClient arm = null;
            bool retry = false;
            ushort raiseStatus = 0;
            bool doRaise = false;

            lock (_sync)
            {
                if (_programStopping || client != _client)
                    return;

                if (status == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    _connecting = false;
                    QueueStatusLocked(StatusConnected, false, ref raiseStatus, ref doRaise);
                    if (!_receiveArmed)
                    {
                        _receiveArmed = true;
                        arm = client;
                    }
                }
                else
                {
                    QueueStatusLocked(MapStatus(status), false, ref raiseStatus, ref doRaise);
                    if (status == SocketStatus.SOCKET_STATUS_DNS_LOOKUP ||
                        status == SocketStatus.SOCKET_STATUS_DNS_RESOLVED ||
                        status == SocketStatus.SOCKET_STATUS_WAITING)
                    {
                        /* in-progress */
                    }
                    else
                    {
                        _connecting = false;
                        _receiveArmed = false;
                        retry = ScheduleRetryLocked();
                    }
                }
            }

            if (doRaise)
                RaiseConnection(raiseStatus);
            if (arm != null)
                RearmReceive(arm);
            if (retry)
                return;
        }

        private void ReceiveCallback(TCPClient client, int bytesReceived)
        {
            string text = null;
            TCPClient arm = null;

            lock (_sync)
            {
                if (_programStopping || client != _client)
                    return;

                if (bytesReceived > 0)
                {
                    try
                    {
                        byte[] buf = client.IncomingDataBuffer;
                        if (buf != null)
                        {
                            int n = bytesReceived;
                            if (n > buf.Length)
                                n = buf.Length;
                            text = BytesToSerial(buf, n);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLog.Error("DynamicTcpClient_v1_1: receive: {0}", ex.Message);
                    }
                }

                if (client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    _receiveArmed = true;
                    arm = client;
                }
                else
                {
                    _receiveArmed = false;
                }
            }

            if (text != null && text.Length > 0)
                RaiseData(text);
            if (arm != null)
                RearmReceive(arm);
        }

        private void RearmReceive(TCPClient client)
        {
            try
            {
                if (client == null)
                    return;
                if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                    return;

                SocketErrorCodes err = client.ReceiveDataAsync(ReceiveCallback);
                if (err != SocketErrorCodes.SOCKET_OK && err != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                {
                    ErrorLog.Error("DynamicTcpClient_v1_1: re-arm receive: {0}", err);
                    lock (_sync)
                    {
                        if (client == _client)
                            _receiveArmed = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: re-arm receive: {0}", ex.Message);
                lock (_sync)
                {
                    if (client == _client)
                        _receiveArmed = false;
                }
            }
        }

        private void RetryCallback(object userobj)
        {
            TCPClient startClient = null;
            ushort raiseStatus = 0;
            bool doRaise = false;
            bool start = false;

            lock (_sync)
            {
                if (_programStopping || !_wantConnected)
                    return;
                if (IsUpLocked() || _connecting)
                    return;

                TearDownSocketLocked();
                startClient = CreateClientLocked();
                start = (startClient != null);
                if (start)
                    QueueStatusLocked(StatusWaiting, false, ref raiseStatus, ref doRaise);
                else
                    QueueStatusLocked(StatusConnectFailed, false, ref raiseStatus, ref doRaise);
            }

            if (doRaise)
                RaiseConnection(raiseStatus);
            if (start)
                BeginConnectAsync(startClient);
            else
            {
                lock (_sync)
                {
                    ScheduleRetryLocked();
                }
            }
        }

        private bool ScheduleRetryLocked()
        {
            if (_programStopping || !_wantConnected)
                return false;
            try
            {
                _retryTimer.Reset(RetryMs);
                return true;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: retry timer: {0}", ex.Message);
                return false;
            }
        }

        private void StopRetryLocked()
        {
            try
            {
                _retryTimer.Stop();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: stop retry: {0}", ex.Message);
            }
        }

        private void TearDownSocketLocked()
        {
            TCPClient client = _client;
            _client = null;
            _connecting = false;
            _receiveArmed = false;
            if (client == null)
                return;

            try
            {
                client.SocketStatusChange -= SocketStatusChanged;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: unsubscribe: {0}", ex.Message);
            }

            try
            {
                client.DisconnectFromServer();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: disconnect: {0}", ex.Message);
            }

            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: dispose: {0}", ex.Message);
            }
        }

        private void ProgramStatusHandler(eProgramStatusEventType type)
        {
            if (type != eProgramStatusEventType.Stopping)
                return;

            lock (_sync)
            {
                _programStopping = true;
                _wantConnected = false;
                StopRetryLocked();
                StopStatusDebounceLocked();
                TearDownSocketLocked();
            }
        }

        private void QueueStatusLocked(ushort status, bool force, ref ushort raiseStatus, ref bool doRaise)
        {
            _status = status;

            if (status == StatusConnected)
            {
                StopStatusDebounceLocked();
                StageStatusLocked(StatusConnected, force, ref raiseStatus, ref doRaise);
                return;
            }

            if (!_wantConnected)
            {
                StopStatusDebounceLocked();
                StageStatusLocked(status, force, ref raiseStatus, ref doRaise);
                return;
            }

            // Connect is held and the socket is down. Leave 2 or 0 at once
            // so the analog does not sit on Connected or flash Not connected.
            if (_publishedStatus == StatusConnected || _publishedStatus == StatusNotConnected)
                StageStatusLocked(StatusWaiting, false, ref raiseStatus, ref doRaise);

            if (status == StatusWaiting)
            {
                StopStatusDebounceLocked();
                if (force)
                    StageStatusLocked(StatusWaiting, true, ref raiseStatus, ref doRaise);
                return;
            }

            if (_publishedStatus == status)
            {
                StopStatusDebounceLocked();
                return;
            }

            ArmStatusDebounceLocked(status);
        }

        private void StageStatusLocked(ushort status, bool force, ref ushort raiseStatus, ref bool doRaise)
        {
            if (!force && _statusPublished && _publishedStatus == status)
                return;

            _publishedStatus = status;
            _statusPublished = true;
            raiseStatus = status;
            doRaise = true;
        }

        private void ArmStatusDebounceLocked(ushort status)
        {
            if (_statusDebounceArmed && _pendingStatus == status)
                return;

            _pendingStatus = status;
            try
            {
                _statusDebounce.Reset(StatusDebounceMs);
                _statusDebounceArmed = true;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: status debounce: {0}", ex.Message);
            }
        }

        private void StopStatusDebounceLocked()
        {
            if (!_statusDebounceArmed)
                return;

            _statusDebounceArmed = false;
            try
            {
                _statusDebounce.Stop();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: status debounce stop: {0}", ex.Message);
            }
        }

        private void StatusDebounceCallback(object userobj)
        {
            ushort raiseStatus = 0;
            bool doRaise = false;

            lock (_sync)
            {
                if (!_statusDebounceArmed)
                    return;

                _statusDebounceArmed = false;
                if (_programStopping || !_wantConnected || IsUpLocked())
                    return;
                if (_status == StatusWaiting || _status != _pendingStatus)
                    return;

                StageStatusLocked(_pendingStatus, false, ref raiseStatus, ref doRaise);
            }

            if (doRaise)
                RaiseConnection(raiseStatus);
        }

        private void RaiseConnection(ushort status)
        {
            ConnectionHandler h = _connectionHandlers;
            if (h == null)
                return;

            ushort connected = (status == StatusConnected) ? (ushort)1 : (ushort)0;
            try
            {
                h(connected, status);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("DynamicTcpClient_v1_1: OnConnectionChanged: {0}", ex.Message);
            }
        }

        private void RaiseData(string text)
        {
            DataHandler h = _dataHandlers;
            if (h == null)
                return;

            int offset = 0;
            while (offset < text.Length)
            {
                int len = text.Length - offset;
                if (len > RxChunk)
                    len = RxChunk;
                string chunk = text.Substring(offset, len);
                offset += len;
                try
                {
                    h(new SimplSharpString(chunk));
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("DynamicTcpClient_v1_1: OnDataReceived: {0}", ex.Message);
                }
            }
        }

        private static ushort MapStatus(SocketStatus status)
        {
            switch (status)
            {
                case SocketStatus.SOCKET_STATUS_NO_CONNECT:
                    return StatusNotConnected;
                case SocketStatus.SOCKET_STATUS_WAITING:
                    return StatusWaiting;
                case SocketStatus.SOCKET_STATUS_CONNECTED:
                    return StatusConnected;
                case SocketStatus.SOCKET_STATUS_CONNECT_FAILED:
                    return StatusConnectFailed;
                case SocketStatus.SOCKET_STATUS_BROKEN_REMOTELY:
                    return StatusBrokenRemotely;
                case SocketStatus.SOCKET_STATUS_BROKEN_LOCALLY:
                    return StatusBrokenLocally;
                case SocketStatus.SOCKET_STATUS_DNS_LOOKUP:
                    return StatusDnsLookup;
                case SocketStatus.SOCKET_STATUS_DNS_FAILED:
                    return StatusDnsFailed;
                case SocketStatus.SOCKET_STATUS_DNS_RESOLVED:
                    return StatusDnsResolved;
                case SocketStatus.SOCKET_STATUS_LINK_LOST:
                    return StatusBrokenRemotely;
                default:
                    return StatusNotConnected;
            }
        }

        private static string BytesToSerial(byte[] buf, int n)
        {
            char[] chars = new char[n];
            int i;
            for (i = 0; i < n; i++)
                chars[i] = (char)buf[i];
            return new string(chars);
        }

        private static byte[] SerialToBytes(string data)
        {
            byte[] bytes = new byte[data.Length];
            int i;
            for (i = 0; i < data.Length; i++)
                bytes[i] = (byte)(data[i] & 0xFF);
            return bytes;
        }
    }
}
