using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace IvCamPtz
{
    public delegate void IvCamConnectionHandler(ushort connected, SimplSharpString status);
    public delegate void IvCamDataHandler(SimplSharpString data);

    public class IvCamLink
    {
        private const int Port = 5500;
        private const int BufferSize = 1024;
        private const int RetryMs = 2000;

        private readonly object _sync = new object();
        private readonly CTimer _retry;

        private IvCamConnectionHandler _connection;
        private IvCamDataHandler _data;

        private TCPClient _client;
        private bool _want;
        private bool _stopping;
        private bool _connecting;
        private bool _receiveArmed;
        private string _host = "";
        private ushort _up;
        private string _status = "";
        private bool _dirty;
        private ushort _dirtyUp;
        private string _dirtyMsg = "";

        public IvCamLink()
        {
            _retry = new CTimer(RetryCallback, null, Timeout.Infinite);
            CrestronEnvironment.ProgramStatusEventHandler += ProgramStatus;
        }

        public IvCamConnectionHandler OnConnectionChanged
        {
            get { return null; }
            set
            {
                if (value != null)
                    _connection = (IvCamConnectionHandler)Delegate.Combine(_connection, value);
            }
        }

        public IvCamDataHandler OnDataReceived
        {
            get { return null; }
            set
            {
                if (value != null)
                    _data = (IvCamDataHandler)Delegate.Combine(_data, value);
            }
        }

        public void Connect(string address)
        {
            string host = address == null ? "" : address.Trim();
            if (host == "0.0.0.0")
                host = "";

            TCPClient start = null;
            bool begin = false;
            bool failed = false;

            lock (_sync)
            {
                if (_stopping)
                    return;

                if (host.Length == 0)
                {
                    _want = false;
                    _host = "";
                    StopRetry();
                    TearDown();
                    Mark(0, "Not connected");
                }
                else
                {
                    bool same = (host == _host) && (_connecting || IsUp());
                    _host = host;
                    _want = true;
                    if (!same)
                    {
                        StopRetry();
                        TearDown();
                        start = Create();
                        begin = (start != null);
                        if (begin)
                            Mark(0, "Waiting");
                        else
                        {
                            failed = true;
                            Mark(0, "Connection failed");
                        }
                    }
                }
            }

            Flush();
            if (begin)
                BeginConnect(start);
            else if (failed)
            {
                lock (_sync)
                {
                    ScheduleRetry();
                }
            }
        }

        public void Disconnect()
        {
            lock (_sync)
            {
                _want = false;
                _host = "";
                StopRetry();
                TearDown();
                Mark(0, "Not connected");
            }
            Flush();
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
                    byte[] bytes = ToBytes(data);
                    _client.SendData(bytes, bytes.Length);
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("IvCamLink: send: {0}", ex.Message);
                }
            }
        }

        private bool IsUp()
        {
            return _client != null && _client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED;
        }

        private TCPClient Create()
        {
            if (_stopping || !_want || _host.Length == 0)
                return null;

            try
            {
                TCPClient client = new TCPClient(_host, Port, BufferSize);
                client.SocketStatusChange += StatusChanged;
                _client = client;
                _connecting = true;
                _receiveArmed = false;
                return client;
            }
            catch (Exception ex)
            {
                _connecting = false;
                ErrorLog.Error("IvCamLink: create {0}: {1}", _host, ex.Message);
                return null;
            }
        }

        private void BeginConnect(TCPClient client)
        {
            try
            {
                SocketErrorCodes err = client.ConnectToServerAsync(ConnectDone);
                if (err == SocketErrorCodes.SOCKET_OK || err == SocketErrorCodes.SOCKET_OPERATION_PENDING)
                    return;
                ErrorLog.Error("IvCamLink: connect: {0}", err);
                Fail(client);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("IvCamLink: connect: {0}", ex.Message);
                Fail(client);
            }
        }

        private void Fail(TCPClient client)
        {
            lock (_sync)
            {
                if (client != _client)
                    return;
                _connecting = false;
                Mark(0, "Connection failed");
                ScheduleRetry();
            }
            Flush();
        }

        private void ConnectDone(TCPClient client)
        {
            TCPClient arm = null;

            lock (_sync)
            {
                if (_stopping || client != _client)
                    return;
                _connecting = false;
                if (client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    Mark(1, "Connected");
                    if (!_receiveArmed)
                    {
                        _receiveArmed = true;
                        arm = client;
                    }
                }
                else
                {
                    Mark(0, "Connection failed");
                    ScheduleRetry();
                }
            }

            Flush();
            if (arm != null)
                Arm(arm);
        }

        private void StatusChanged(TCPClient client, SocketStatus status)
        {
            TCPClient arm = null;

            lock (_sync)
            {
                if (_stopping || client != _client)
                    return;

                if (status == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    _connecting = false;
                    Mark(1, "Connected");
                    if (!_receiveArmed)
                    {
                        _receiveArmed = true;
                        arm = client;
                    }
                }
                else if (status == SocketStatus.SOCKET_STATUS_WAITING ||
                         status == SocketStatus.SOCKET_STATUS_DNS_LOOKUP ||
                         status == SocketStatus.SOCKET_STATUS_DNS_RESOLVED)
                {
                    Mark(0, "Waiting");
                }
                else
                {
                    _connecting = false;
                    _receiveArmed = false;
                    Mark(0, "Not connected");
                    ScheduleRetry();
                }
            }

            Flush();
            if (arm != null)
                Arm(arm);
        }

        private void ReceiveDone(TCPClient client, int count)
        {
            string text = null;
            TCPClient arm = null;

            lock (_sync)
            {
                if (_stopping || client != _client)
                    return;

                if (count > 0)
                {
                    try
                    {
                        byte[] buf = client.IncomingDataBuffer;
                        if (buf != null)
                        {
                            int n = count;
                            if (n > buf.Length)
                                n = buf.Length;
                            text = FromBytes(buf, n);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorLog.Error("IvCamLink: receive: {0}", ex.Message);
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
                    Mark(0, "Not connected");
                    ScheduleRetry();
                }
            }

            Flush();
            if (text != null && text.Length > 0)
                RaiseData(text);
            if (arm != null)
                Arm(arm);
        }

        private void Arm(TCPClient client)
        {
            try
            {
                if (client == null)
                    return;
                if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                    return;

                SocketErrorCodes err = client.ReceiveDataAsync(ReceiveDone);
                if (err != SocketErrorCodes.SOCKET_OK && err != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                {
                    ErrorLog.Error("IvCamLink: re-arm receive: {0}", err);
                    lock (_sync)
                    {
                        if (client == _client)
                            _receiveArmed = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("IvCamLink: re-arm receive: {0}", ex.Message);
                lock (_sync)
                {
                    if (client == _client)
                        _receiveArmed = false;
                }
            }
        }

        private void RetryCallback(object state)
        {
            TCPClient start = null;
            bool begin = false;

            lock (_sync)
            {
                if (_stopping || !_want)
                    return;
                if (IsUp() || _connecting)
                    return;
                TearDown();
                start = Create();
                begin = (start != null);
                if (!begin)
                    Mark(0, "Connection failed");
            }

            Flush();
            if (begin)
                BeginConnect(start);
            else
            {
                lock (_sync)
                {
                    ScheduleRetry();
                }
            }
        }

        private void ScheduleRetry()
        {
            if (_stopping || !_want)
                return;
            try
            {
                _retry.Reset(RetryMs);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("IvCamLink: retry: {0}", ex.Message);
            }
        }

        private void StopRetry()
        {
            try
            {
                _retry.Stop();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("IvCamLink: stop retry: {0}", ex.Message);
            }
        }

        private void TearDown()
        {
            TCPClient client = _client;
            _client = null;
            _connecting = false;
            _receiveArmed = false;
            if (client == null)
                return;

            try
            {
                client.SocketStatusChange -= StatusChanged;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("IvCamLink: unsubscribe: {0}", ex.Message);
            }

            try
            {
                client.DisconnectFromServer();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("IvCamLink: disconnect: {0}", ex.Message);
            }

            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("IvCamLink: dispose: {0}", ex.Message);
            }
        }

        private void ProgramStatus(eProgramStatusEventType type)
        {
            if (type != eProgramStatusEventType.Stopping)
                return;

            lock (_sync)
            {
                _stopping = true;
                _want = false;
                StopRetry();
                TearDown();
            }
        }

        private void Mark(ushort up, string message)
        {
            if (message == null)
                message = "";
            if (up == _up && message == _status)
                return;
            _up = up;
            _status = message;
            _dirtyUp = up;
            _dirtyMsg = message;
            _dirty = true;
        }

        private void Flush()
        {
            ushort up;
            string message;
            IvCamConnectionHandler handler;
            lock (_sync)
            {
                if (!_dirty)
                    return;
                _dirty = false;
                up = _dirtyUp;
                message = _dirtyMsg;
                handler = _connection;
            }
            if (handler == null)
                return;
            try
            {
                handler(up, new SimplSharpString(message));
            }
            catch (Exception ex)
            {
                ErrorLog.Error("IvCamLink: connection callback: {0}", ex.Message);
            }
        }

        private void RaiseData(string text)
        {
            IvCamDataHandler handler = _data;
            if (handler == null)
                return;
            try
            {
                handler(new SimplSharpString(text));
            }
            catch (Exception ex)
            {
                ErrorLog.Error("IvCamLink: data callback: {0}", ex.Message);
            }
        }

        private static string FromBytes(byte[] buf, int n)
        {
            char[] chars = new char[n];
            int i;
            for (i = 0; i < n; i++)
                chars[i] = (char)buf[i];
            return new string(chars);
        }

        private static byte[] ToBytes(string data)
        {
            byte[] bytes = new byte[data.Length];
            int i;
            for (i = 0; i < data.Length; i++)
                bytes[i] = (byte)(data[i] & 0xFF);
            return bytes;
        }
    }
}
