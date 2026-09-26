using System;
using System.Collections.Generic;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronSockets;

namespace EnttecEtherGate
{
    public delegate void EtherGateConnectionHandler(ushort connected, SimplSharpString status);
    public delegate void EtherGateConfigHandler(
        SimplSharpString nodeName,
        SimplSharpString firmware,
        SimplSharpString port1Mode,
        SimplSharpString port2Mode,
        ushort port1Universe,
        ushort port2Universe);
    public delegate void EtherGateLevelHandler(ushort percent);
    public delegate void EtherGatePresetHandler(ushort active);

    public class EtherGateControllerBind
    {
        private EtherGateNode _owner;
        private EtherGateConnectionHandler _connection;
        private EtherGateConfigHandler _config;

        public EtherGateControllerBind()
        {
        }

        public void UseSlot(ushort slot)
        {
            EtherGateNode node = EtherGateNode.Get(slot);
            if (_owner == node)
                return;
            if (_owner != null)
                _owner.DetachController(this);
            _owner = node;
            _owner.AddController(this);
        }

        public EtherGateConnectionHandler OnConnectionChanged
        {
            get { return null; }
            set
            {
                if (value != null)
                    _connection = (EtherGateConnectionHandler)Delegate.Combine(_connection, value);
            }
        }

        public EtherGateConfigHandler OnConfigChanged
        {
            get { return null; }
            set
            {
                if (value != null)
                    _config = (EtherGateConfigHandler)Delegate.Combine(_config, value);
            }
        }

        public void Release()
        {
            _connection = null;
            _config = null;
            if (_owner != null)
                _owner.DetachController(this);
        }

        public void Connect(string address)
        {
            if (_owner != null)
                _owner.Connect(address);
        }

        public void Disconnect()
        {
            if (_owner != null)
                _owner.Disconnect();
        }

        public void SetUniverse(ushort port, ushort universe)
        {
            if (_owner != null)
                _owner.SetUniverse(port, universe);
        }

        internal void RaiseConnection(ushort connected, string status)
        {
            EtherGateConnectionHandler handler = _connection;
            if (handler == null)
                return;
            try { handler(connected, new SimplSharpString(status ?? "")); }
            catch (Exception ex) { ErrorLog.Error("EtherGate controller callback: {0}", ex.Message); }
        }

        internal void RaiseConfig(string name, string firmware, string mode1, string mode2, ushort uni1, ushort uni2)
        {
            EtherGateConfigHandler handler = _config;
            if (handler == null)
                return;
            try
            {
                handler(
                    new SimplSharpString(name ?? ""),
                    new SimplSharpString(firmware ?? ""),
                    new SimplSharpString(mode1 ?? ""),
                    new SimplSharpString(mode2 ?? ""),
                    uni1,
                    uni2);
            }
            catch (Exception ex) { ErrorLog.Error("EtherGate config callback: {0}", ex.Message); }
        }
    }

    public class EtherGateChannelBind
    {
        private EtherGateNode _owner;
        private int _port;
        private int _channel;
        private EtherGateLevelHandler _level;

        public EtherGateChannelBind()
        {
        }

        public void Use(ushort slot, ushort port, ushort channel)
        {
            int p = port == 2 ? 1 : 0;
            int c = channel;
            if (c < 1)
                c = 1;
            if (c > 512)
                c = 512;
            c--;

            EtherGateNode node = EtherGateNode.Get(slot);
            if (_owner == node && _port == p && _channel == c)
                return;
            if (_owner != null)
                _owner.DetachChannel(this);
            _owner = node;
            _port = p;
            _channel = c;
            _owner.AddChannel(this);
        }

        public EtherGateLevelHandler OnLevelChanged
        {
            get { return null; }
            set
            {
                if (value != null)
                    _level = (EtherGateLevelHandler)Delegate.Combine(_level, value);
            }
        }

        public void Release()
        {
            _level = null;
            if (_owner != null)
                _owner.DetachChannel(this);
        }

        public void SetLevel(ushort percent)
        {
            if (_owner != null)
                _owner.SetLevel(_port, _channel, percent, true);
        }

        public void Publish()
        {
            if (_owner != null)
                _owner.PublishChannel(this);
        }

        internal void Raise(ushort percent)
        {
            EtherGateLevelHandler handler = _level;
            if (handler == null)
                return;
            try { handler(percent); }
            catch (Exception ex) { ErrorLog.Error("EtherGate level callback: {0}", ex.Message); }
        }

        internal int Port { get { return _port; } }
        internal int Channel { get { return _channel; } }
    }

    public class EtherGatePresetBind
    {
        private EtherGateNode _owner;
        private int _index;
        private EtherGatePresetHandler _active;

        public EtherGatePresetBind()
        {
        }

        public void Use(ushort slot, ushort preset)
        {
            int index = preset;
            if (index < 1)
                index = 1;
            if (index > 16)
                index = 16;
            index--;

            EtherGateNode node = EtherGateNode.Get(slot);
            if (_owner == node && _index == index && _owner != null)
                return;
            if (_owner != null)
                _owner.DetachPreset(this);
            _owner = node;
            _index = index;
            _owner.AddPreset(this);
        }

        public EtherGatePresetHandler OnActiveChanged
        {
            get { return null; }
            set
            {
                if (value != null)
                    _active = (EtherGatePresetHandler)Delegate.Combine(_active, value);
            }
        }

        public void Release()
        {
            _active = null;
            if (_owner != null)
                _owner.DetachPreset(this);
        }

        public void Save()
        {
            if (_owner != null)
                _owner.SavePreset(_index);
        }

        public void Recall(ushort fadeTenths)
        {
            if (_owner != null)
                _owner.RecallPreset(_index, fadeTenths);
        }

        public void Publish()
        {
            if (_owner != null)
                _owner.PublishPreset(this);
        }

        internal void Raise(ushort active)
        {
            EtherGatePresetHandler handler = _active;
            if (handler == null)
                return;
            try { handler(active); }
            catch (Exception ex) { ErrorLog.Error("EtherGate preset callback: {0}", ex.Message); }
        }

        internal int Index { get { return _index; } }
    }

    public class EtherGateNode
    {
        private const int Ports = 2;
        private const int Channels = 512;
        private const int Presets = 16;
        private const int ArtNetPort = 6454;
        private const int TickMs = 50;
        private const int PollTicks = 600;
        private const int RetryTicks = 100;
        private const int ConnectWaitTicks = 160;
        private const int MaxBody = 12000;

        private static readonly object SlotGate = new object();
        private static readonly EtherGateNode[] Slots = new EtherGateNode[8];

        private readonly object _sync = new object();
        private readonly int _slot;
        private readonly bool _live;
        private readonly byte[,] _dmx = new byte[Ports, Channels];
        private readonly ushort[,] _percent = new ushort[Ports, Channels];
        private readonly int[] _universe = new int[Ports];
        private readonly byte[] _sequence = new byte[Ports];
        private readonly byte[] _packet1 = new byte[18 + Channels];
        private readonly byte[] _packet2 = new byte[18 + Channels];
        private readonly List<EtherGateControllerBind> _controllers = new List<EtherGateControllerBind>();
        private readonly Dictionary<int, List<EtherGateChannelBind>> _channels = new Dictionary<int, List<EtherGateChannelBind>>();
        private readonly List<EtherGatePresetBind>[] _presetBinds = new List<EtherGatePresetBind>[Presets];
        private readonly StoredPreset[] _presets = new StoredPreset[Presets];
        private readonly List<FadeJob> _fades = new List<FadeJob>();

        private CTimer _timer;
        private UDPServer _udp;
        private bool _want;
        private bool _online;
        private int _connectWait;
        private bool _udpReady;
        private bool _udpFaultLogged;
        private string _ip = "";
        private int _pollCountdown;
        private int _artPollCountdown;
        private bool _pollBusy;
        private bool _logBuffer;
        private bool _shellLogged;
        private TCPClient _http;
        private StringBuilder _httpBody;
        private int _pollPhase;
        private bool _httpSettled;
        private byte[] _pendingRequest;
        private string _nodeName = "";
        private string _firmware = "";
        private string _mode1 = "poll pending";
        private string _mode2 = "poll pending";
        private string _postedName;
        private string _postedFirmware;
        private string _postedMode1;
        private string _postedMode2;
        private ushort _polledUni1;
        private ushort _polledUni2;
        private ushort _connected;
        private string _status;

        public EtherGateNode()
        {
        }

        private EtherGateNode(int slot)
        {
            _slot = slot;
            _live = true;
            _universe[0] = 0;
            _universe[1] = 1;
            for (int i = 0; i < Presets; i++)
            {
                _presets[i] = new StoredPreset();
                _presetBinds[i] = new List<EtherGatePresetBind>();
            }
            StampArtNetHeader(_packet1);
            StampArtNetHeader(_packet2);
            _timer = new CTimer(OnTick, null, Crestron.SimplSharp.Timeout.Infinite);
            CrestronEnvironment.ProgramStatusEventHandler += OnProgramStatus;
            LoadPresets();
        }

        internal static EtherGateNode Get(ushort slot)
        {
            int index = slot;
            if (index < 1)
                index = 1;
            if (index > 8)
                index = 8;
            index--;

            lock (SlotGate)
            {
                if (Slots[index] == null)
                    Slots[index] = new EtherGateNode(index + 1);
                return Slots[index];
            }
        }

        internal void AddController(EtherGateControllerBind bind)
        {
            lock (_sync)
            {
                _controllers.Add(bind);
            }
        }

        internal void AddChannel(EtherGateChannelBind bind)
        {
            int key = Key(bind.Port, bind.Channel);
            lock (_sync)
            {
                List<EtherGateChannelBind> list;
                if (!_channels.TryGetValue(key, out list))
                {
                    list = new List<EtherGateChannelBind>();
                    _channels[key] = list;
                }
                list.Add(bind);
            }
        }

        internal void AddPreset(EtherGatePresetBind bind)
        {
            lock (_sync)
            {
                _presetBinds[bind.Index].Add(bind);
            }
        }

        internal void DetachController(EtherGateControllerBind bind)
        {
            lock (_sync)
            {
                _controllers.Remove(bind);
            }
        }

        internal void DetachChannel(EtherGateChannelBind bind)
        {
            int key = Key(bind.Port, bind.Channel);
            lock (_sync)
            {
                List<EtherGateChannelBind> list;
                if (_channels.TryGetValue(key, out list))
                    list.Remove(bind);
            }
        }

        internal void DetachPreset(EtherGatePresetBind bind)
        {
            lock (_sync)
            {
                _presetBinds[bind.Index].Remove(bind);
            }
        }

        internal void PublishChannel(EtherGateChannelBind bind)
        {
            ushort percent;
            lock (_sync)
            {
                percent = _percent[bind.Port, bind.Channel];
            }
            bind.Raise(percent);
        }

        internal void PublishPreset(EtherGatePresetBind bind)
        {
            ushort active;
            lock (_sync)
            {
                active = _presets[bind.Index].Active ? (ushort)1 : (ushort)0;
            }
            bind.Raise(active);
        }

        public void Connect(string address)
        {
            if (!_live)
                return;

            string ip = address == null ? "" : address.Trim();
            bool bad = ip.Length == 0 || ip == "0.0.0.0";
            if (bad)
            {
                Disconnect();
                SetLink(0, "Address not set");
                return;
            }

            lock (_sync)
            {
                _ip = ip;
                _want = true;
                _online = false;
                _udpFaultLogged = false;
                _pollCountdown = RetryTicks;
                _logBuffer = true;
            }

            _timer.Reset(TickMs, TickMs);
            SetLink(0, "Connecting");
            StartPoll();
        }

        public void Disconnect()
        {
            if (!_live)
                return;

            lock (_sync)
            {
                _want = false;
                _online = false;
                _pollBusy = false;
            }

            try { _timer.Stop(); }
            catch (Exception ex) { ErrorLog.Error("EtherGate slot {0} timer stop: {1}", _slot, ex.Message); }

            CloseHttp();
            SetLink(0, "Disconnected");
        }

        public void SetUniverse(ushort port, ushort universe)
        {
            if (!_live)
                return;
            int p = port == 2 ? 1 : 0;
            int u = universe;
            if (u > 32767)
                u = 32767;
            lock (_sync)
            {
                _universe[p] = u;
            }
        }

        internal void SetLevel(int port, int channel, ushort percent, bool fromFader)
        {
            if (!_live)
                return;

            byte dmx = PercentToDmx(percent);
            EtherGateChannelBind[] listeners = null;
            EtherGatePresetBind[] presetListeners = null;
            ushort[] presetActive = null;

            lock (_sync)
            {
                _percent[port, channel] = percent;
                _dmx[port, channel] = dmx;
                CancelFadeLocked(port, channel);
                listeners = CopyChannelsLocked(port, channel);
                if (fromFader)
                    presetListeners = RefreshPresetsLocked(port, channel, out presetActive);
            }

            RaiseLevels(listeners, percent);
            RaisePresets(presetListeners, presetActive);
        }

        internal void SavePreset(int index)
        {
            bool stored = false;
            bool ioFailed = false;
            EtherGatePresetBind[] listeners = null;

            lock (_sync)
            {
                StoredPreset preset = _presets[index];
                preset.Members.Clear();
                foreach (KeyValuePair<int, List<EtherGateChannelBind>> pair in _channels)
                {
                    if (pair.Value == null || pair.Value.Count == 0)
                        continue;
                    int port = pair.Key / Channels;
                    int channel = pair.Key % Channels;
                    PresetMember member = new PresetMember();
                    member.Port = port;
                    member.Channel = channel;
                    member.Percent = _percent[port, channel];
                    member.Dmx = _dmx[port, channel];
                    preset.Members.Add(member);
                }
                preset.Stored = preset.Members.Count > 0;
                preset.Active = preset.Stored;
                stored = preset.Stored;
                listeners = CopyPresetsLocked(index);
            }

            if (!SavePresets())
                ioFailed = true;

            if (listeners != null)
            {
                for (int i = 0; i < listeners.Length; i++)
                    listeners[i].Raise(stored ? (ushort)1 : (ushort)0);
            }

            if (ioFailed)
                SetLink(_connected, "Preset did not persist");
        }

        internal void RecallPreset(int index, ushort fadeTenths)
        {
            int fadeMs = fadeTenths;
            if (fadeMs > 6000)
                fadeMs = 6000;
            fadeMs *= 100;

            List<LevelNote> notes = new List<LevelNote>();
            EtherGatePresetBind[] listeners = null;
            ushort active = 0;

            lock (_sync)
            {
                StoredPreset preset = _presets[index];
                if (!preset.Stored)
                {
                    preset.Active = false;
                    listeners = CopyPresetsLocked(index);
                }
                else
                {
                    int now = Environment.TickCount;
                    for (int i = 0; i < preset.Members.Count; i++)
                    {
                        PresetMember member = preset.Members[i];
                        CancelFadeLocked(member.Port, member.Channel);
                        if (fadeMs <= 0 || _dmx[member.Port, member.Channel] == member.Dmx)
                        {
                            _dmx[member.Port, member.Channel] = member.Dmx;
                            _percent[member.Port, member.Channel] = (ushort)member.Percent;
                        }
                        else
                        {
                            FadeJob job = new FadeJob();
                            job.Port = member.Port;
                            job.Channel = member.Channel;
                            job.Start = _dmx[member.Port, member.Channel];
                            job.Target = member.Dmx;
                            job.EndPercent = (ushort)member.Percent;
                            job.StartTick = now;
                            job.DurationMs = fadeMs;
                            _fades.Add(job);
                        }

                        LevelNote note = new LevelNote();
                        note.Listeners = CopyChannelsLocked(member.Port, member.Channel);
                        note.Percent = _percent[member.Port, member.Channel];
                        notes.Add(note);
                    }
                    preset.Active = fadeMs <= 0 && PresetMatchesLocked(preset);
                    listeners = CopyPresetsLocked(index);
                    active = preset.Active ? (ushort)1 : (ushort)0;
                }
            }

            for (int i = 0; i < notes.Count; i++)
                RaiseLevels(notes[i].Listeners, notes[i].Percent);
            RaisePresets(listeners, active);
        }

        private void OnProgramStatus(eProgramStatusEventType type)
        {
            if (type == eProgramStatusEventType.Stopping)
                Disconnect();
        }

        private void OnTick(object userSpecific)
        {
            if (!_live)
                return;

            try
            {
                bool poll;
                bool giveUp = false;
                lock (_sync)
                {
                    if (!_want)
                        return;
                    _pollCountdown--;
                    poll = _pollCountdown <= 0 && !_pollBusy;
                    if (poll)
                        _pollCountdown = _online ? PollTicks : RetryTicks;
                    if (_pollBusy && !_online)
                    {
                        _connectWait--;
                        if (_connectWait <= 0)
                            giveUp = true;
                    }
                }

                if (giveUp)
                {
                    CloseHttp();
                    FinishPoll(false, "");
                }
                if (_online)
                {
                    SendArtNet();
                    SendArtPoll();
                }
                StepFades();
                if (poll)
                    StartPoll();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("EtherGate slot {0} tick: {1}", _slot, ex.Message);
            }
        }

        private void SendArtNet()
        {
            string ip;
            int u1;
            int u2;
            lock (_sync)
            {
                if (!_want || _ip.Length == 0)
                    return;
                ip = _ip;
                u1 = _universe[0];
                u2 = _universe[1];
                _sequence[0] = NextSequence(_sequence[0]);
                _sequence[1] = NextSequence(_sequence[1]);
                FillPacket(_packet1, _sequence[0], u1, 0);
                FillPacket(_packet2, _sequence[1], u2, 1);
            }

            if (!_udpReady)
                return;

            try
            {
                _udp.SendData(_packet1, _packet1.Length, ip, ArtNetPort);
                _udp.SendData(_packet2, _packet2.Length, ip, ArtNetPort);
            }
            catch (Exception ex)
            {
                if (!_udpFaultLogged)
                {
                    _udpFaultLogged = true;
                    ErrorLog.Error("EtherGate slot {0} Art-Net send: {1}", _slot, ex.Message);
                }
            }
        }

        private void StepFades()
        {
            List<LevelNote> notes = null;
            List<PresetNote> presets = null;
            int now = Environment.TickCount;

            lock (_sync)
            {
                for (int i = _fades.Count - 1; i >= 0; i--)
                {
                    FadeJob job = _fades[i];
                    int elapsed = unchecked((int)((uint)now - (uint)job.StartTick));
                    bool done = elapsed >= job.DurationMs;
                    byte dmx;
                    ushort percent;
                    if (done)
                    {
                        dmx = job.Target;
                        percent = job.EndPercent;
                        _fades.RemoveAt(i);
                    }
                    else
                    {
                        int delta = job.Target - job.Start;
                        dmx = (byte)(job.Start + (delta * elapsed / job.DurationMs));
                        percent = DmxToPercent(dmx);
                    }

                    if (_dmx[job.Port, job.Channel] == dmx && _percent[job.Port, job.Channel] == percent && !done)
                        continue;

                    _dmx[job.Port, job.Channel] = dmx;
                    _percent[job.Port, job.Channel] = percent;

                    if (notes == null)
                        notes = new List<LevelNote>();
                    LevelNote note = new LevelNote();
                    note.Listeners = CopyChannelsLocked(job.Port, job.Channel);
                    note.Percent = percent;
                    notes.Add(note);

                    ushort[] active;
                    EtherGatePresetBind[] binds = RefreshPresetsLocked(job.Port, job.Channel, out active);
                    if (binds != null)
                    {
                        if (presets == null)
                            presets = new List<PresetNote>();
                        PresetNote presetNote = new PresetNote();
                        presetNote.Listeners = binds;
                        presetNote.Active = active;
                        presets.Add(presetNote);
                    }
                }
            }

            if (notes != null)
            {
                for (int i = 0; i < notes.Count; i++)
                    RaiseLevels(notes[i].Listeners, notes[i].Percent);
            }
            if (presets != null)
            {
                for (int i = 0; i < presets.Count; i++)
                    RaisePresets(presets[i].Listeners, presets[i].Active);
            }
        }

        private void StartPoll()
        {
            lock (_sync)
            {
                if (!_want || _pollBusy || _ip.Length == 0)
                    return;
                _pollBusy = true;
                _pollPhase = 0;
            }
            OpenHttp(BuildRequest("/index.html?config=1"));
        }

        private string BuildRequest(string path)
        {
            return "GET " + path + " HTTP/1.0\r\nHost: " + _ip + "\r\nConnection: close\r\n\r\n";
        }

        private void OpenHttp(string request)
        {
            TCPClient client = null;
            try
            {
                CloseHttp();
                client = new TCPClient(_ip, 80, 8192);
                _http = client;
                _httpBody = new StringBuilder();
                _httpSettled = false;
                _connectWait = ConnectWaitTicks;
                _http.SocketStatusChange += HttpStatus;
                SocketErrorCodes err = client.ConnectToServerAsync(HttpConnected);
                if (err != SocketErrorCodes.SOCKET_OK && err != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                {
                    ErrorLog.Error("EtherGate slot {0} HTTP connect: {1}", _slot, err);
                    FinishPoll(false, "");
                }
                else
                {
                    _pendingRequest = Encoding.ASCII.GetBytes(request);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("EtherGate slot {0} HTTP connect: {1}", _slot, ex.Message);
                FinishPoll(false, "");
            }
        }

        private void HttpConnected(TCPClient client)
        {
            try
            {
                if (client != _http)
                    return;
                if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    FinishPoll(false, "");
                    return;
                }

                MarkOnline();
                byte[] request = _pendingRequest;
                if (request != null)
                    client.SendData(request, request.Length);
                client.ReceiveDataAsync(HttpReceived);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("EtherGate slot {0} HTTP send: {1}", _slot, ex.Message);
                FinishPoll(false, "");
            }
        }

        private void HttpStatus(TCPClient client, SocketStatus status)
        {
            if (client != _http)
                return;
            if (status == SocketStatus.SOCKET_STATUS_CONNECTED ||
                status == SocketStatus.SOCKET_STATUS_WAITING ||
                status == SocketStatus.SOCKET_STATUS_DNS_LOOKUP ||
                status == SocketStatus.SOCKET_STATUS_DNS_RESOLVED)
                return;
            string sofar = _httpBody == null ? "" : _httpBody.ToString();
            FinishPoll(sofar.Length > 0, sofar);
        }

        private void HttpReceived(TCPClient client, int bytesReceived)
        {
            try
            {
                if (client != _http)
                    return;

                if (bytesReceived > 0 && client.IncomingDataBuffer != null && _httpBody != null)
                {
                    int n = bytesReceived;
                    if (n > client.IncomingDataBuffer.Length)
                        n = client.IncomingDataBuffer.Length;
                    if (_httpBody.Length < MaxBody)
                    {
                        int room = MaxBody - _httpBody.Length;
                        if (n > room)
                            n = room;
                        _httpBody.Append(Encoding.ASCII.GetString(client.IncomingDataBuffer, 0, n));
                    }
                }

                if (client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED && bytesReceived > 0)
                {
                    client.ReceiveDataAsync(HttpReceived);
                    return;
                }

                FinishPoll(true, _httpBody == null ? "" : _httpBody.ToString());
            }
            catch (Exception ex)
            {
                ErrorLog.Error("EtherGate slot {0} HTTP receive: {1}", _slot, ex.Message);
                FinishPoll(false, _httpBody == null ? "" : _httpBody.ToString());
            }
        }

        private void FinishPoll(bool ok, string raw)
        {
            int phase;
            bool wantBuffer;
            lock (_sync)
            {
                if (_httpSettled || !_pollBusy)
                    return;
                _httpSettled = true;
                phase = _pollPhase;
                wantBuffer = _logBuffer;
                if (!ok && (raw == null || raw.Length == 0))
                {
                    _pollBusy = false;
                    _logBuffer = false;
                    phase = phase == 0 ? -1 : -2;
                }
            }

            CloseHttp();

            if (phase < 0)
            {
                if (_want && phase == -1)
                    MarkOffline("No response");
                return;
            }

            string body = ExtractBody(raw);
            if (phase == 0 && IsWebShell(body))
            {
                if (!_shellLogged)
                {
                    _shellLogged = true;
                    ErrorLog.Notice("EtherGate slot {0} config is the web page, not a settings dump", _slot);
                }
            }
            else if (phase == 0)
            {
                string clipped = body.Length > 400 ? body.Substring(0, 400) : body;
                ErrorLog.Notice("EtherGate slot {0} config: {1}", _slot, clipped);
            }
            else if (wantBuffer)
            {
                string clipped = body.Length > 400 ? body.Substring(0, 400) : body;
                ErrorLog.Notice("EtherGate slot {0} buffer1: {1}", _slot, clipped);
            }

            if (phase == 0)
            {
                ApplyConfig(body);
                if (wantBuffer && _want)
                {
                    lock (_sync)
                    {
                        _pollPhase = 1;
                        _logBuffer = false;
                    }
                    OpenHttp(BuildRequest("/index.html?buffer1"));
                    return;
                }
            }

            lock (_sync)
            {
                _pollBusy = false;
            }
        }

        private void ApplyConfig(string body)
        {
            bool shell = IsWebShell(body);
            string name = shell ? null : ReadName(body);
            string firmware = shell ? null : ReadFirmware(body);
            string protocol = shell ? null : AfterLabel(body, "Protocol");
            bool parsed = name != null && name.Length > 0;
            string mode = Useful(protocol) ? protocol : (shell ? "web page" : GuessMode(body));
            bool haveName;

            lock (_sync)
            {
                if (parsed)
                    _nodeName = name;
                if (firmware != null && firmware.Length > 0)
                    _firmware = firmware;
                if (!(shell && _nodeName.Length > 0))
                {
                    _mode1 = mode;
                    _mode2 = mode;
                }
                _polledUni1 = 0;
                _polledUni2 = 0;
                haveName = _nodeName.Length > 0;
            }

            if (_want)
                SetLink(1, haveName ? "Online" : "Online, config unparsed");
            RaiseConfig();
        }

        private void MarkOnline()
        {
            string status;
            lock (_sync)
            {
                _online = true;
                _pollCountdown = PollTicks;
                status = _status;
            }
            EnsureUdp();
            if (!_want)
                return;
            if (status == null || status.Length == 0 || status == "Connecting" || status == "No response" || status == "Disconnected" || status == "Address not set")
                SetLink(1, "Online");
            else
                SetLink(1, status);
        }

        private void MarkOffline(string status)
        {
            lock (_sync)
            {
                _online = false;
                _pollCountdown = RetryTicks;
            }
            if (_want)
                SetLink(0, status);
        }

        private static string ReadName(string body)
        {
            string value = FormValue(body, "config_name");
            if (Useful(value))
                return Clean(value, true);
            value = FormValue(body, "node_name");
            if (Useful(value))
                return Clean(value, true);
            value = FormValue(body, "nodeName");
            if (Useful(value))
                return Clean(value, true);
            value = FormValue(body, "deviceName");
            if (Useful(value))
                return Clean(value, true);
            value = InputValue(body, "config_name");
            if (Useful(value))
                return Clean(value, false);
            value = AfterLabel(body, "Node Name");
            if (Useful(value))
                return Clean(value, false);
            value = AfterLabel(body, "Device Name");
            if (Useful(value))
                return Clean(value, false);
            return null;
        }

        private static string ReadFirmware(string body)
        {
            string value = FormValue(body, "firmware");
            if (Useful(value))
                return Clean(value, true);
            value = FormValue(body, "fw_version");
            if (Useful(value))
                return Clean(value, true);
            value = FormValue(body, "firmwareVersion");
            if (Useful(value))
                return Clean(value, true);
            value = FormValue(body, "fwVersion");
            if (Useful(value))
                return Clean(value, true);
            value = AfterLabel(body, "Firmware Version");
            if (Useful(value))
                return Clean(value, false);
            value = AfterLabel(body, "Firmware");
            if (Useful(value))
                return Clean(value, false);
            return null;
        }

        private static string FormValue(string body, string key)
        {
            if (body == null || key == null)
                return null;
            int from = 0;
            while (from < body.Length)
            {
                int i = body.IndexOf(key, from, StringComparison.OrdinalIgnoreCase);
                if (i < 0)
                    return null;
                int after = i + key.Length;
                bool boundary = i == 0 || !char.IsLetterOrDigit(body[i - 1]);
                if (boundary && after < body.Length && (body[after] == '=' || body[after] == ':'))
                    return TakeToken(body, after + 1);
                from = i + 1;
            }
            return null;
        }

        private static string InputValue(string body, string field)
        {
            if (body == null || field == null)
                return null;
            int i = body.IndexOf(field, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
                return null;
            int start = i - 100;
            if (start < 0)
                start = 0;
            int end = i + field.Length + 80;
            if (end > body.Length)
                end = body.Length;
            string window = body.Substring(start, end - start);
            int v = window.IndexOf("value=", StringComparison.OrdinalIgnoreCase);
            if (v < 0)
                return null;
            return TakeToken(window, v + 6);
        }

        private static string AfterLabel(string body, string label)
        {
            if (body == null || label == null)
                return null;
            int i = body.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
                return null;
            i += label.Length;
            int guard = 0;
            while (i < body.Length && guard < 400)
            {
                guard++;
                char c = body[i];
                if (c == '<')
                {
                    int gt = body.IndexOf('>', i);
                    if (gt < 0)
                        return null;
                    i = gt + 1;
                    continue;
                }
                if (c == ':' || c == '=' || char.IsWhiteSpace(c) || c == '"' || c == '\'')
                {
                    i++;
                    continue;
                }
                break;
            }
            return TakeToken(body, i);
        }

        private static string TakeToken(string body, int start)
        {
            if (body == null || start < 0 || start >= body.Length)
                return null;
            while (start < body.Length && (body[start] == '"' || body[start] == '\'' || char.IsWhiteSpace(body[start])))
                start++;
            int end = start;
            while (end < body.Length && end - start < 48)
            {
                char c = body[end];
                if (c == '<' || c == '"' || c == '\'' || c == '&' || c == '\r' || c == '\n')
                    break;
                end++;
            }
            if (end == start)
                return null;
            return body.Substring(start, end - start);
        }

        private static string Clean(string value, bool plusIsSpace)
        {
            if (value == null)
                return null;
            string text = value.Trim();
            if (plusIsSpace)
                text = text.Replace('+', ' ');
            text = text.Replace("&nbsp;", " ").Replace("&amp;", "&").Trim();
            if (!Useful(text) || IsUiLabel(text))
                return null;
            return text;
        }

        private static bool IsWebShell(string body)
        {
            if (body == null)
                return false;
            return body.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0
                || body.IndexOf("<!doctype", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUiLabel(string text)
        {
            return text == "Node Name"
                || text == "Firmware Version"
                || text == "System Uptime"
                || text == "System Last Uptime"
                || text == "Pixel Protocol"
                || text == "Protocol"
                || text == "DHCP Status"
                || text == "IP Address"
                || text == "NetMask"
                || text == "Gateway Address"
                || text == "Mac Address"
                || text == "Link Speed"
                || text == "Universe"
                || text == "Refresh Rate"
                || text == "Output Merging"
                || text == "Serial Number"
                || text == "Device Name";
        }

        private static bool Useful(string value)
        {
            if (value == null)
                return false;
            string text = value.Trim();
            if (text.Length == 0 || text.Length > 48)
                return false;
            if (text.IndexOf('<') >= 0 || text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0)
                return false;
            return true;
        }

        private static string GuessMode(string body)
        {
            if (body == null || body.Length == 0)
                return "unparsed";
            if (body.IndexOf("Art-Net") >= 0 || body.IndexOf("ArtNet") >= 0 || body.IndexOf("artnet") >= 0)
                return "Art-Net, detail unparsed";
            if (body.IndexOf("sACN") >= 0 || body.IndexOf("sacn") >= 0)
                return "sACN, detail unparsed";
            return "unparsed";
        }

        private static string ExtractBody(string raw)
        {
            if (raw == null)
                return "";
            if (raw.StartsWith("HTTP/"))
            {
                int i = raw.IndexOf("\r\n\r\n");
                if (i >= 0)
                    return raw.Substring(i + 4);
                i = raw.IndexOf("\n\n");
                if (i >= 0)
                    return raw.Substring(i + 2);
            }
            return raw;
        }

        private void EnsureUdp()
        {
            if (_udpReady)
                return;
            try
            {
                _udp = new UDPServer("0.0.0.0", ArtNetPort, 1024, EthernetAdapterType.EthernetLANAdapter, ArtNetPort);
                SocketErrorCodes err = _udp.EnableUDPServer();
                if (err != SocketErrorCodes.SOCKET_OK && err != SocketErrorCodes.SOCKET_OPERATION_PENDING)
                {
                    ErrorLog.Error("EtherGate slot {0} UDP LAN enable: {1}", _slot, err);
                    return;
                }
                _udp.ReceiveDataAsync(OnArtNet);
                _udpReady = true;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("EtherGate slot {0} UDP enable: {1}", _slot, ex.Message);
            }
        }

        private void SendArtPoll()
        {
            if (!_udpReady)
                return;
            _artPollCountdown--;
            if (_artPollCountdown > 0 || _ip == null || _ip.Length == 0)
                return;
            _artPollCountdown = 40;

            byte[] poll = new byte[]
            {
                (byte)'A', (byte)'r', (byte)'t', (byte)'-', (byte)'N', (byte)'e', (byte)'t', 0,
                0x00, 0x20, 0x00, 0x0E, 0x02, 0x00
            };
            try
            {
                _udp.SendData(poll, poll.Length, _ip, ArtNetPort);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("EtherGate slot {0} ArtPoll: {1}", _slot, ex.Message);
            }
        }

        private void OnArtNet(UDPServer server, int bytes)
        {
            try
            {
                if (bytes >= 44 && server != null && server.IncomingDataBuffer != null)
                    ReadArtPollReply(server.IncomingDataBuffer, bytes);
                if (_udpReady && server == _udp)
                    server.ReceiveDataAsync(OnArtNet);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("EtherGate slot {0} Art-Net receive: {1}", _slot, ex.Message);
            }
        }

        private void ReadArtPollReply(byte[] packet, int length)
        {
            if (length < 44)
                return;
            if (packet[0] != (byte)'A' || packet[8] != 0x00 || packet[9] != 0x21)
                return;

            string shortName = CString(packet, 26, 18, length);
            string longName = length >= 108 ? CString(packet, 44, 64, length) : null;
            string name = Useful(longName) ? longName : shortName;
            string firmware = null;
            if (length >= 18 && (packet[16] != 0 || packet[17] != 0))
                firmware = packet[16].ToString() + "." + packet[17].ToString();

            bool changed = false;
            lock (_sync)
            {
                if (Useful(name) && name != _nodeName)
                {
                    _nodeName = name;
                    changed = true;
                }
                if (Useful(firmware) && firmware != _firmware)
                {
                    _firmware = firmware;
                    changed = true;
                }
                if (_mode1 == "web page" || _mode1 == "poll pending" || _mode1 == "unparsed")
                {
                    _mode1 = "Art-Net";
                    _mode2 = "Art-Net";
                    changed = true;
                }
            }
            if (!changed)
                return;
            if (_want)
                SetLink(1, "Online");
            RaiseConfig();
        }

        private static string CString(byte[] packet, int offset, int max, int length)
        {
            int count = max;
            if (offset >= length)
                return null;
            if (offset + count > length)
                count = length - offset;
            int n = 0;
            while (n < count && packet[offset + n] != 0)
                n++;
            if (n == 0)
                return null;
            return Encoding.ASCII.GetString(packet, offset, n).Trim();
        }

        private void CloseHttp()
        {
            TCPClient client = _http;
            _http = null;
            if (client == null)
                return;
            try { client.DisconnectFromServer(); }
            catch (Exception ex) { ErrorLog.Error("EtherGate slot {0} HTTP close: {1}", _slot, ex.Message); }
        }

        private void SetLink(ushort connected, string status)
        {
            if (status == null)
                status = "";

            EtherGateControllerBind[] binds = null;
            bool post = false;
            lock (_sync)
            {
                if (status == "Online, config unparsed" && _nodeName != null && _nodeName.Length > 0)
                    status = "Online";
                post = status != _status || connected != _connected;
                _status = status;
                _connected = connected;
                if (post)
                    binds = _controllers.ToArray();
            }

            if (!post || binds == null)
                return;

            for (int i = 0; i < binds.Length; i++)
                binds[i].RaiseConnection(connected, status);
        }

        private void RaiseConfig()
        {
            EtherGateControllerBind[] binds;
            string name;
            string firmware;
            string mode1;
            string mode2;
            ushort uni1;
            ushort uni2;
            bool changed = false;
            lock (_sync)
            {
                name = _nodeName;
                firmware = _firmware;
                mode1 = _mode1;
                mode2 = _mode2;
                uni1 = _polledUni1;
                uni2 = _polledUni2;
                changed = name != _postedName || firmware != _postedFirmware || mode1 != _postedMode1 || mode2 != _postedMode2;
                if (!changed)
                    return;
                _postedName = name;
                _postedFirmware = firmware;
                _postedMode1 = mode1;
                _postedMode2 = mode2;
                binds = _controllers.ToArray();
            }
            for (int i = 0; i < binds.Length; i++)
                binds[i].RaiseConfig(name, firmware, mode1, mode2, uni1, uni2);
        }

        private static void RaiseLevels(EtherGateChannelBind[] listeners, ushort percent)
        {
            if (listeners == null)
                return;
            for (int i = 0; i < listeners.Length; i++)
                listeners[i].Raise(percent);
        }

        private static void RaisePresets(EtherGatePresetBind[] listeners, ushort active)
        {
            if (listeners == null)
                return;
            for (int i = 0; i < listeners.Length; i++)
                listeners[i].Raise(active);
        }

        private static void RaisePresets(EtherGatePresetBind[] listeners, ushort[] active)
        {
            if (listeners == null || active == null)
                return;
            int n = listeners.Length;
            if (active.Length < n)
                n = active.Length;
            for (int i = 0; i < n; i++)
                listeners[i].Raise(active[i]);
        }

        private EtherGateChannelBind[] CopyChannelsLocked(int port, int channel)
        {
            List<EtherGateChannelBind> list;
            if (!_channels.TryGetValue(Key(port, channel), out list) || list.Count == 0)
                return null;
            return list.ToArray();
        }

        private EtherGatePresetBind[] CopyPresetsLocked(int index)
        {
            if (_presetBinds[index].Count == 0)
                return null;
            return _presetBinds[index].ToArray();
        }

        private EtherGatePresetBind[] RefreshPresetsLocked(int port, int channel, out ushort[] active)
        {
            List<EtherGatePresetBind> changed = null;
            List<ushort> flags = null;
            for (int i = 0; i < Presets; i++)
            {
                StoredPreset preset = _presets[i];
                if (!preset.Stored || !PresetHasChannel(preset, port, channel))
                    continue;
                bool now = PresetMatchesLocked(preset);
                if (now == preset.Active)
                    continue;
                preset.Active = now;
                if (_presetBinds[i].Count == 0)
                    continue;
                if (changed == null)
                {
                    changed = new List<EtherGatePresetBind>();
                    flags = new List<ushort>();
                }
                EtherGatePresetBind[] binds = _presetBinds[i].ToArray();
                ushort flag = now ? (ushort)1 : (ushort)0;
                for (int b = 0; b < binds.Length; b++)
                {
                    changed.Add(binds[b]);
                    flags.Add(flag);
                }
            }

            if (changed == null)
            {
                active = null;
                return null;
            }
            active = flags.ToArray();
            return changed.ToArray();
        }

        private static bool PresetHasChannel(StoredPreset preset, int port, int channel)
        {
            for (int i = 0; i < preset.Members.Count; i++)
            {
                if (preset.Members[i].Port == port && preset.Members[i].Channel == channel)
                    return true;
            }
            return false;
        }

        private bool PresetMatchesLocked(StoredPreset preset)
        {
            if (!preset.Stored || preset.Members.Count == 0)
                return false;
            for (int i = 0; i < preset.Members.Count; i++)
            {
                PresetMember member = preset.Members[i];
                if (_dmx[member.Port, member.Channel] != member.Dmx)
                    return false;
            }
            return true;
        }

        private void CancelFadeLocked(int port, int channel)
        {
            for (int i = _fades.Count - 1; i >= 0; i--)
            {
                if (_fades[i].Port == port && _fades[i].Channel == channel)
                    _fades.RemoveAt(i);
            }
        }

        private void FillPacket(byte[] packet, byte sequence, int universe, int port)
        {
            packet[12] = sequence;
            packet[14] = (byte)(universe & 0xFF);
            packet[15] = (byte)((universe >> 8) & 0x7F);
            for (int i = 0; i < Channels; i++)
                packet[18 + i] = _dmx[port, i];
        }

        private static void StampArtNetHeader(byte[] packet)
        {
            packet[0] = (byte)'A';
            packet[1] = (byte)'r';
            packet[2] = (byte)'t';
            packet[3] = (byte)'-';
            packet[4] = (byte)'N';
            packet[5] = (byte)'e';
            packet[6] = (byte)'t';
            packet[7] = 0;
            packet[8] = 0x00;
            packet[9] = 0x50;
            packet[10] = 0x00;
            packet[11] = 0x0E;
            packet[13] = 0;
            packet[16] = 0x02;
            packet[17] = 0x00;
        }

        private static byte NextSequence(byte current)
        {
            current++;
            if (current == 0)
                current = 1;
            return current;
        }

        private static int Key(int port, int channel)
        {
            return (port * Channels) + channel;
        }

        private static byte PercentToDmx(ushort percent)
        {
            return (byte)((percent * 255 + 32767) / 65535);
        }

        private static ushort DmxToPercent(byte dmx)
        {
            return (ushort)(dmx * 65535 / 255);
        }

        private string PresetsPath()
        {
            return "\\user\\EtherGate\\slot" + _slot.ToString() + ".presets";
        }

        private void LoadPresets()
        {
            try
            {
                string path = PresetsPath();
                if (!File.Exists(path))
                    return;

                using (StreamReader reader = new StreamReader(path))
                {
                    string magic = reader.ReadLine();
                    if (magic != "EG1")
                        return;
                    for (int i = 0; i < Presets; i++)
                    {
                        string countLine = reader.ReadLine();
                        if (countLine == null)
                            break;
                        int count;
                        if (!int.TryParse(countLine, out count) || count < 0)
                            count = 0;
                        StoredPreset preset = _presets[i];
                        preset.Members.Clear();
                        for (int m = 0; m < count; m++)
                        {
                            string line = reader.ReadLine();
                            if (line == null)
                                break;
                            string[] parts = line.Split(' ');
                            if (parts.Length < 3)
                                continue;
                            int port;
                            int channel;
                            int percent;
                            if (!int.TryParse(parts[0], out port) || !int.TryParse(parts[1], out channel) || !int.TryParse(parts[2], out percent))
                                continue;
                            if (port < 0 || port > 1 || channel < 0 || channel >= Channels)
                                continue;
                            if (percent < 0)
                                percent = 0;
                            if (percent > 65535)
                                percent = 65535;
                            PresetMember member = new PresetMember();
                            member.Port = port;
                            member.Channel = channel;
                            member.Percent = percent;
                            member.Dmx = PercentToDmx((ushort)percent);
                            preset.Members.Add(member);
                        }
                        preset.Stored = preset.Members.Count > 0;
                        preset.Active = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("EtherGate slot {0} load presets: {1}", _slot, ex.Message);
            }
        }

        private bool SavePresets()
        {
            try
            {
                lock (_sync)
                {
                Directory.CreateDirectory("\\user\\EtherGate");
                using (StreamWriter writer = new StreamWriter(PresetsPath(), false))
                {
                    writer.WriteLine("EG1");
                    for (int i = 0; i < Presets; i++)
                    {
                        StoredPreset preset = _presets[i];
                        writer.WriteLine(preset.Members.Count.ToString());
                        for (int m = 0; m < preset.Members.Count; m++)
                        {
                            PresetMember member = preset.Members[m];
                            writer.WriteLine(member.Port.ToString() + " " + member.Channel.ToString() + " " + member.Percent.ToString());
                        }
                    }
                }
                }
                return true;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("EtherGate slot {0} save presets: {1}", _slot, ex.Message);
                return false;
            }
        }

        private class PresetMember
        {
            public int Port;
            public int Channel;
            public int Percent;
            public byte Dmx;
        }

        private class StoredPreset
        {
            public bool Stored;
            public bool Active;
            public List<PresetMember> Members = new List<PresetMember>();
        }

        private class FadeJob
        {
            public int Port;
            public int Channel;
            public byte Start;
            public byte Target;
            public ushort EndPercent;
            public int StartTick;
            public int DurationMs;
        }

        private struct LevelNote
        {
            public EtherGateChannelBind[] Listeners;
            public ushort Percent;
        }

        private struct PresetNote
        {
            public EtherGatePresetBind[] Listeners;
            public ushort[] Active;
        }
    }
}
