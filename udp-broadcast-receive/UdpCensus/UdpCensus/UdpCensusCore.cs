using System;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;

namespace UdpCensusLib
{
    public delegate void CensusHandler(ushort enabled, ushort polling, ushort count);
    public delegate void RecordHandler(SimplSharpString ip, SimplSharpString mac, SimplSharpString uid, SimplSharpString data, ushort answered);

    public class UdpCensus
    {
        public const int MaxPeers = 512;
        private const int BufferSize = 512;
        private const int PublishMs = 250;
        private const int MinWindowMs = 500;
        private const int DefaultWindowMs = 3000;
        private const int MaxWindowMs = 30000;

        private sealed class Peer
        {
            public string Key;
            public string Ip;
            public string Mac;
            public string Uid;
            public string Data;
            public bool Seen;
        }

        private readonly object _sync = new object();
        private readonly Peer[] _peers = new Peer[MaxPeers];
        private readonly CTimer _intervalTimer;
        private readonly CTimer _windowTimer;
        private readonly CTimer _publishTimer;

        private CensusHandler _censusHandlers;
        private RecordHandler _recordHandlers;

        private UDPServer _udp;
        private bool _open;
        private bool _stopping;
        private bool _respond;
        private bool _discover;
        private bool _polling;
        private bool _publishQueued;
        private int _port = 50001;
        private int _intervalMs;
        private int _windowMs = DefaultWindowMs;
        private int _count;
        private int _select = 1;
        private string _mac = "";
        private string _uid = "";
        private string _data = "";
        private string _localIp = "";

        public CensusHandler OnCensus
        {
            get { return null; }
            set
            {
                if (value != null)
                    _censusHandlers = (CensusHandler)Delegate.Combine(_censusHandlers, value);
            }
        }

        public RecordHandler OnRecord
        {
            get { return null; }
            set
            {
                if (value != null)
                    _recordHandlers = (RecordHandler)Delegate.Combine(_recordHandlers, value);
            }
        }

        public UdpCensus()
        {
            _intervalTimer = new CTimer(OnInterval, null, Timeout.Infinite);
            _windowTimer = new CTimer(OnWindow, null, Timeout.Infinite);
            _publishTimer = new CTimer(OnPublish, null, Timeout.Infinite);
            CrestronEnvironment.ProgramStatusEventHandler += OnProgramStatus;
        }

        public void Configure(ushort respond, ushort discover, ushort port, ushort intervalHundredths, ushort windowHundredths)
        {
            bool reopen = false;
            lock (_sync)
            {
                if (_stopping)
                    return;
                _respond = respond != 0;
                _discover = discover != 0;
                int nextPort = port;
                if (nextPort < 1024 || nextPort > 65535)
                    nextPort = 50001;
                if (nextPort != _port)
                {
                    _port = nextPort;
                    reopen = _open;
                }
                _intervalMs = HundredthsToMs(intervalHundredths, 0, 600000);
                if (_intervalMs > 0 && _intervalMs < 1000)
                    _intervalMs = 1000;
                int window = HundredthsToMs(windowHundredths, MinWindowMs, MaxWindowMs);
                if (windowHundredths == 0)
                    window = DefaultWindowMs;
                _windowMs = window;
                ArmIntervalLocked();
            }
            if (reopen)
            {
                CloseSocket();
                OpenSocket();
            }
        }

        public void SetIdentity(string mac, string uid, string data)
        {
            lock (_sync)
            {
                _mac = NormalizeMac(mac);
                _uid = Clean(uid, 32).Replace(",", "");
                _data = Clean(data, 128);
            }
        }

        public void Open()
        {
            OpenSocket();
        }

        public void Close()
        {
            CloseSocket();
            PublishNow();
        }

        public void PollNow()
        {
            UDPServer socket;
            int port;
            string localIp;
            string mac;
            string uid;
            string data;
            lock (_sync)
            {
                if (_stopping || !_open || _udp == null)
                    return;
                localIp = RefreshLocalIpLocked();
                mac = _mac;
                uid = _uid;
                data = _data;
                BeginWindowLocked();
                UpsertLocked(KeyFor(uid, mac, localIp), localIp, mac, uid, data, true);
                socket = _udp;
                port = _port;
            }
            Send(socket, Encoding.ASCII.GetBytes("?POLL"), "255.255.255.255", port);
            SchedulePublish();
        }

        public void Select(ushort index)
        {
            lock (_sync)
            {
                _select = index;
            }
            PublishNow();
        }

        private void OpenSocket()
        {
            UDPServer previous = null;
            UDPServer created = null;
            int port;
            lock (_sync)
            {
                if (_stopping)
                    return;
                if (_open && _udp != null)
                    return;
                port = _port;
                previous = _udp;
                _udp = null;
                _open = false;
            }
            Disable(previous);
            try
            {
                created = new UDPServer("0.0.0.0", port, BufferSize, EthernetAdapterType.EthernetLANAdapter, port);
                SocketErrorCodes err = created.EnableUDPServer();
                if (err != SocketErrorCodes.SOCKET_OK && err != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                {
                    ErrorLog.Error("UdpCensus: UDP enable failed ({0})", err);
                    Disable(created);
                    created = null;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("UdpCensus: UDP enable: {0}", ex.Message);
                created = null;
            }

            lock (_sync)
            {
                if (_stopping || created == null)
                {
                    _open = false;
                    _udp = null;
                }
                else
                {
                    _udp = created;
                    _open = true;
                    _localIp = RefreshLocalIpLocked();
                    try { created.ReceiveDataAsync(OnUdp); }
                    catch (Exception ex) { ErrorLog.Error("UdpCensus: receive arm: {0}", ex.Message); }
                    ArmIntervalLocked();
                }
            }
            if (created != null && !_open)
                Disable(created);
            PublishNow();
        }

        private void CloseSocket()
        {
            UDPServer socket;
            lock (_sync)
            {
                _open = false;
                _polling = false;
                _publishQueued = false;
                socket = _udp;
                _udp = null;
                try { _intervalTimer.Stop(); } catch (Exception) { }
                try { _windowTimer.Stop(); } catch (Exception) { }
                try { _publishTimer.Stop(); } catch (Exception) { }
            }
            Disable(socket);
        }

        private void OnUdp(UDPServer server, int bytes)
        {
            string from = "";
            int fromPort = 0;
            string text = "";
            bool mine = false;
            lock (_sync)
            {
                mine = server == _udp && _open;
            }
            if (!mine)
                return;
            try
            {
                if (bytes > 0 && server.IncomingDataBuffer != null)
                {
                    int n = bytes;
                    if (n > server.IncomingDataBuffer.Length)
                        n = server.IncomingDataBuffer.Length;
                    from = server.IPAddressLastMessageReceivedFrom ?? "";
                    fromPort = server.IPPortLastMessageReceivedFrom;
                    text = Encoding.ASCII.GetString(server.IncomingDataBuffer, 0, n);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("UdpCensus: receive: {0}", ex.Message);
            }
            Rearm(server);
            if (text.Length > 0)
                HandlePacket(text, from, fromPort);
        }

        private void HandlePacket(string raw, string from, int fromPort)
        {
            string text = Clean(raw, 400);
            if (text.Length == 0)
                return;
            if (IsPoll(text))
            {
                bool respond;
                string packetIp;
                lock (_sync)
                {
                    respond = _respond && _open;
                    packetIp = _localIp;
                }
                if (!respond)
                    return;
                if (from.Length > 0 && string.Compare(from, packetIp, StringComparison.Ordinal) == 0)
                    return;
                SendHere(from, fromPort);
                return;
            }
            if (!text.StartsWith("!HERE,", StringComparison.Ordinal))
                return;

            string mac;
            string uid;
            string data;
            SplitHere(text, out mac, out uid, out data);
            bool changed = false;
            lock (_sync)
            {
                if (!_polling)
                    return;
                string ip = Clean(from, 45);
                changed = UpsertLocked(KeyFor(uid, mac, ip), ip, mac, uid, data, true);
            }
            if (changed)
                SchedulePublish();
        }

        private void SendHere(string to, int toPort)
        {
            UDPServer socket;
            int port;
            byte[] payload;
            lock (_sync)
            {
                if (!_open || _udp == null)
                    return;
                socket = _udp;
                port = toPort > 0 ? toPort : _port;
                string body = "!HERE," + _mac + "," + _uid + "," + _data;
                payload = Encoding.ASCII.GetBytes(body);
            }
            if (to == null || to.Length == 0)
                return;
            Send(socket, payload, to, port);
        }

        private void BeginWindowLocked()
        {
            for (int i = 0; i < _count; i++)
            {
                if (_peers[i] != null)
                    _peers[i].Seen = false;
            }
            _polling = true;
            try { _windowTimer.Reset(_windowMs); }
            catch (Exception ex) { ErrorLog.Error("UdpCensus: window timer: {0}", ex.Message); }
        }

        private void OnWindow(object state)
        {
            lock (_sync)
            {
                if (!_polling)
                    return;
                CompactLocked();
                _polling = false;
            }
            PublishNow();
        }

        private void OnInterval(object state)
        {
            bool run;
            lock (_sync)
            {
                run = _open && _discover && _intervalMs > 0 && !_stopping;
            }
            if (run)
                PollNow();
        }

        private void OnPublish(object state)
        {
            lock (_sync)
            {
                _publishQueued = false;
            }
            PublishNow();
        }

        private void SchedulePublish()
        {
            bool start = false;
            lock (_sync)
            {
                if (!_publishQueued)
                {
                    _publishQueued = true;
                    start = true;
                }
            }
            if (!start)
                return;
            try { _publishTimer.Reset(PublishMs); }
            catch (Exception) { PublishNow(); }
        }

        private void PublishNow()
        {
            ushort enabled;
            ushort polling;
            ushort count;
            ushort answered;
            string ip;
            string mac;
            string uid;
            string data;
            CensusHandler census;
            RecordHandler record;
            lock (_sync)
            {
                enabled = _open ? (ushort)1 : (ushort)0;
                polling = _polling ? (ushort)1 : (ushort)0;
                count = (ushort)_count;
                answered = 0;
                ip = "";
                mac = "";
                uid = "";
                data = "";
                int index = _select - 1;
                if (index >= 0 && index < _count && _peers[index] != null)
                {
                    Peer row = _peers[index];
                    ip = row.Ip;
                    mac = row.Mac;
                    uid = row.Uid;
                    data = row.Data;
                    answered = row.Seen ? (ushort)1 : (ushort)0;
                }
                census = _censusHandlers;
                record = _recordHandlers;
            }
            RaiseCensus(census, enabled, polling, count);
            RaiseRecord(record, ip, mac, uid, data, answered);
        }

        private static void RaiseCensus(CensusHandler handler, ushort enabled, ushort polling, ushort count)
        {
            if (handler == null)
                return;
            try { handler(enabled, polling, count); }
            catch (Exception ex) { ErrorLog.Error("UdpCensus: OnCensus: {0}", ex.Message); }
        }

        private static void RaiseRecord(RecordHandler handler, string ip, string mac, string uid, string data, ushort answered)
        {
            if (handler == null)
                return;
            try
            {
                handler(new SimplSharpString(ip ?? ""), new SimplSharpString(mac ?? ""), new SimplSharpString(uid ?? ""), new SimplSharpString(data ?? ""), answered);
            }
            catch (Exception ex) { ErrorLog.Error("UdpCensus: OnRecord: {0}", ex.Message); }
        }

        private bool UpsertLocked(string key, string ip, string mac, string uid, string data, bool seen)
        {
            if (key == null || key.Length == 0)
                return false;
            for (int i = 0; i < _count; i++)
            {
                Peer row = _peers[i];
                if (row != null && row.Key == key)
                {
                    row.Ip = ip ?? "";
                    row.Mac = mac ?? "";
                    row.Uid = uid ?? "";
                    row.Data = data ?? "";
                    row.Seen = seen;
                    return true;
                }
            }
            if (_count >= MaxPeers)
                return false;
            Peer created = new Peer();
            created.Key = key;
            created.Ip = ip ?? "";
            created.Mac = mac ?? "";
            created.Uid = uid ?? "";
            created.Data = data ?? "";
            created.Seen = seen;
            _peers[_count] = created;
            _count++;
            return true;
        }

        private void CompactLocked()
        {
            int write = 0;
            for (int i = 0; i < _count; i++)
            {
                Peer row = _peers[i];
                if (row != null && row.Seen)
                {
                    _peers[write] = row;
                    write++;
                }
            }
            for (int i = write; i < _count; i++)
                _peers[i] = null;
            _count = write;
        }

        private void ArmIntervalLocked()
        {
            try
            {
                if (_open && _discover && _intervalMs > 0)
                    _intervalTimer.Reset(500, _intervalMs);
                else
                    _intervalTimer.Stop();
            }
            catch (Exception ex) { ErrorLog.Error("UdpCensus: interval timer: {0}", ex.Message); }
        }

        private string RefreshLocalIpLocked()
        {
            try
            {
                short adapter = (short)CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter);
                string ip = CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, adapter);
                _localIp = Clean(ip, 45);
            }
            catch (Exception)
            {
                _localIp = "";
            }
            return _localIp;
        }

        private void Rearm(UDPServer server)
        {
            bool arm;
            lock (_sync)
            {
                arm = _open && server == _udp;
            }
            if (!arm)
                return;
            try { server.ReceiveDataAsync(OnUdp); }
            catch (Exception ex) { ErrorLog.Error("UdpCensus: rearm: {0}", ex.Message); }
        }

        private static void Send(UDPServer socket, byte[] payload, string address, int port)
        {
            if (socket == null || payload == null || address == null || address.Length == 0)
                return;
            try
            {
                SocketErrorCodes err = socket.SendData(payload, payload.Length, address, port);
                if (err != SocketErrorCodes.SOCKET_OK && err != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                    ErrorLog.Error("UdpCensus: send to {0}:{1} failed ({2})", address, port, err);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("UdpCensus: send: {0}", ex.Message);
            }
        }

        private static void Disable(UDPServer socket)
        {
            if (socket == null)
                return;
            try { socket.DisableUDPServer(); }
            catch (Exception) { }
        }

        private void OnProgramStatus(eProgramStatusEventType status)
        {
            if (status != eProgramStatusEventType.Stopping)
                return;
            lock (_sync)
            {
                _stopping = true;
            }
            CloseSocket();
        }

        private static bool IsPoll(string text)
        {
            return text == "?POLL" || text.StartsWith("?POLL", StringComparison.Ordinal);
        }

        private static void SplitHere(string text, out string mac, out string uid, out string data)
        {
            mac = "";
            uid = "";
            data = "";
            string rest = text.Substring(6);
            int comma1 = rest.IndexOf(',');
            if (comma1 < 0)
            {
                mac = NormalizeMac(rest);
                return;
            }
            mac = NormalizeMac(rest.Substring(0, comma1));
            string after = rest.Substring(comma1 + 1);
            int comma2 = after.IndexOf(',');
            if (comma2 < 0)
            {
                uid = Clean(after, 32);
                return;
            }
            uid = Clean(after.Substring(0, comma2), 32);
            data = Clean(after.Substring(comma2 + 1), 128);
        }

        private static string KeyFor(string uid, string mac, string ip)
        {
            if (uid != null && uid.Length > 0)
                return "U:" + uid;
            if (mac != null && mac.Length > 0 && mac != "00:00:00:00:00:00")
                return "M:" + mac;
            return "I:" + (ip ?? "");
        }

        private static string NormalizeMac(string value)
        {
            string text = Clean(value, 17).Replace(",", "").ToUpper();
            if (text.Length == 12 && IsHex(text))
                return Pair(text, 0) + ":" + Pair(text, 2) + ":" + Pair(text, 4) + ":" + Pair(text, 6) + ":" + Pair(text, 8) + ":" + Pair(text, 10);
            if (text.Length == 17)
            {
                char sep = text[2];
                if ((sep == ':' || sep == '-') && text[5] == sep && text[8] == sep && text[11] == sep && text[14] == sep)
                {
                    string hex = text.Replace(sep.ToString(), "");
                    if (hex.Length == 12 && IsHex(hex))
                        return Pair(hex, 0) + ":" + Pair(hex, 2) + ":" + Pair(hex, 4) + ":" + Pair(hex, 6) + ":" + Pair(hex, 8) + ":" + Pair(hex, 10);
                }
            }
            return text;
        }

        private static string Pair(string hex, int index)
        {
            return hex.Substring(index, 2);
        }

        private static bool IsHex(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool digit = c >= '0' && c <= '9';
                bool upper = c >= 'A' && c <= 'F';
                bool lower = c >= 'a' && c <= 'f';
                if (!digit && !upper && !lower)
                    return false;
            }
            return true;
        }

        private static string Clean(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            int nul = value.IndexOf('\0');
            if (nul >= 0)
                value = value.Substring(0, nul);
            value = value.Trim();
            if (max > 0 && value.Length > max)
                value = value.Substring(0, max);
            return value;
        }

        private static int HundredthsToMs(ushort hundredths, int minMs, int maxMs)
        {
            int ms = hundredths * 10;
            if (ms < minMs)
                return minMs;
            if (ms > maxMs)
                return maxMs;
            return ms;
        }
    }
}
