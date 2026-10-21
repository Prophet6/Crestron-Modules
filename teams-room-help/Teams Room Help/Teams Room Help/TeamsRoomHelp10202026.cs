using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Https;

namespace TeamsRoomHelpLib
{
    public delegate void HelpStateHandler(
        ushort active,
        ushort busy,
        ushort fail,
        ushort sent,
        SimplSharpString status,
        SimplSharpString sender,
        SimplSharpString replyTime,
        SimplSharpString replyText,
        SimplSharpString clearAt);

    public class TeamsRoomHelp
    {
        private const int HttpTimeoutSeconds = 8;
        private const int DefaultIntervalMs = 15000;
        private const int MinIntervalMs = 5000;
        private const int MaxIntervalMs = 300000;
        private const int StaleSkewMinutes = 2;

        private readonly object _sync = new object();
        private readonly CTimer _pollTimer;
        private readonly CTimer _workTimer;
        private HelpStateHandler _stateHandlers;

        private bool _stopping;
        private bool _busy;
        private bool _active;
        private bool _autoClear = true;
        private bool _businessOnly;
        private bool _holiday;
        private bool _insecure;
        private int _intervalMs = DefaultIntervalMs;
        private int _clearHours = 9;
        private int _startMinute = HelpSchedule.DefaultStartMinute;
        private int _endMinute = HelpSchedule.DefaultEndMinute;
        private int _queued;

        private string _alertUrl = "";
        private string _replyUrl = "";
        private string _secret = "";
        private string _header = "x-room-help-secret";
        private string _room = "";
        private readonly string[] _issues = new string[8];
        private string _typed = "";
        private string _actionHelp = "help";
        private string _actionCancel = "cancel";
        private string _actionFixed = "fixed";
        private string _fieldRoom = "room";
        private string _fieldAction = "action";
        private string _fieldIssue = "issue";
        private string _fieldMessage = "message";
        private string _fieldSender = "sender";
        private string _fieldText = "text";
        private string _fieldAt = "at";
        private string _templateHelp = "";
        private string _templateCancel = "";
        private string _templateFixed = "";

        private string _status = "";
        private string _sender = "";
        private string _replyTime = "";
        private string _replyText = "";
        private string _clearAt = "";
        private DateTime _sentLocal;
        private DateTime _sentUtc;

        public TeamsRoomHelp()
        {
            _pollTimer = new CTimer(OnPoll, null, Timeout.Infinite);
            _workTimer = new CTimer(OnWork, null, Timeout.Infinite);
            CrestronEnvironment.ProgramStatusEventHandler += OnProgramStatus;
        }

        public HelpStateHandler OnState
        {
            get { return null; }
            set
            {
                if (value != null)
                    _stateHandlers = (HelpStateHandler)Delegate.Combine(_stateHandlers, value);
            }
        }

        public void SetConnection(string alertUrl, string replyUrl, string secret, string header, ushort insecure)
        {
            lock (_sync)
            {
                _alertUrl = alertUrl ?? "";
                _replyUrl = replyUrl ?? "";
                _secret = secret ?? "";
                if (header != null && header.Length > 0)
                    _header = header;
                _insecure = insecure != 0;
            }
        }

        public void SetCopy(
            string room,
            string issue1,
            string issue2,
            string issue3,
            string issue4,
            string issue5,
            string issue6,
            string issue7,
            string issue8,
            string actionHelp,
            string actionCancel,
            string actionFixed,
            string fieldRoom,
            string fieldAction,
            string fieldIssue,
            string fieldMessage,
            string fieldSender,
            string fieldText,
            string fieldAt,
            string templateHelp,
            string templateCancel,
            string templateFixed)
        {
            lock (_sync)
            {
                _room = room ?? "";
                _issues[0] = issue1 ?? "";
                _issues[1] = issue2 ?? "";
                _issues[2] = issue3 ?? "";
                _issues[3] = issue4 ?? "";
                _issues[4] = issue5 ?? "";
                _issues[5] = issue6 ?? "";
                _issues[6] = issue7 ?? "";
                _issues[7] = issue8 ?? "";
                _actionHelp = Or(actionHelp, "help");
                _actionCancel = Or(actionCancel, "cancel");
                _actionFixed = Or(actionFixed, "fixed");
                _fieldRoom = fieldRoom ?? "";
                _fieldAction = fieldAction ?? "";
                _fieldIssue = fieldIssue ?? "";
                _fieldMessage = fieldMessage ?? "";
                _fieldSender = fieldSender ?? "";
                _fieldText = fieldText ?? "";
                _fieldAt = fieldAt ?? "";
                _templateHelp = templateHelp ?? "";
                _templateCancel = templateCancel ?? "";
                _templateFixed = templateFixed ?? "";
            }
        }

        public void SetTyped(string issue)
        {
            lock (_sync)
            {
                _typed = issue ?? "";
            }
        }

        public void SetSchedule(
            ushort autoClear,
            ushort businessOnly,
            ushort holiday,
            ushort intervalHundredths,
            ushort clearHours,
            string businessStart,
            string businessEnd)
        {
            bool warn = false;
            string warning = "";
            bool publish = false;
            lock (_sync)
            {
                _autoClear = autoClear != 0;
                _businessOnly = businessOnly != 0;
                _holiday = holiday != 0;
                _intervalMs = ClampInterval(intervalHundredths);
                int hours = clearHours;
                if (hours < 1)
                    hours = 1;
                if (hours > 168)
                    hours = 168;
                _clearHours = hours;

                int start;
                int end;
                bool startOk = HelpSchedule.TryParseClock(businessStart, out start);
                bool endOk = HelpSchedule.TryParseClock(businessEnd, out end);
                if (!startOk || !endOk)
                {
                    warn = true;
                    warning = "Business hours need HH:MM";
                }
                else if (_businessOnly && end <= start)
                {
                    warn = true;
                    warning = "Business hours need a start before the end";
                    _startMinute = start;
                    _endMinute = end;
                }
                else
                {
                    _startMinute = start;
                    _endMinute = end;
                }

                if (warn)
                {
                    _status = warning;
                    publish = true;
                }
                RefreshClearAtLocked();
                if (_active)
                {
                    publish = true;
                    ArmPollLocked();
                }
            }
            if (publish)
                Publish(warn, false);
        }

        public void PostPreset(ushort index)
        {
            if (index < 1 || index > 8)
                return;
            Queue(index);
        }

        public void PostTyped()
        {
            Queue(9);
        }

        public void PostCancel()
        {
            Queue(10);
        }

        public void PostFixed()
        {
            Queue(11);
        }

        private void Queue(int kind)
        {
            bool reject = false;
            lock (_sync)
            {
                if (_stopping)
                    return;
                if (_busy || _queued != 0)
                {
                    _status = "Please wait";
                    reject = true;
                }
                else
                {
                    _queued = kind;
                }
            }
            if (reject)
            {
                Publish(true, false);
                return;
            }
            _workTimer.Reset(1, Timeout.Infinite);
        }

        private void OnWork(object unused)
        {
            int kind;
            lock (_sync)
            {
                if (_stopping)
                    return;
                kind = _queued;
                _queued = 0;
                if (kind == 0 || _busy)
                    return;
                _busy = true;
            }
            Publish(false, false);
            try
            {
                RunJob(kind);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Teams Room Help: {0}", SafeError(ex));
                lock (_sync)
                {
                    _status = "Could not reach support";
                }
                Publish(true, false);
            }
            finally
            {
                lock (_sync)
                {
                    _busy = false;
                }
                Publish(false, false);
            }
        }

        private void RunJob(int kind)
        {
            string room;
            string issue;
            string action;
            string template;
            string url;
            string secret;
            string header;
            string body;
            bool insecure;
            bool clearReply;
            lock (_sync)
            {
                room = _room.Trim();
                action = ActionFor(kind);
                issue = IssueFor(kind);
                template = TemplateFor(kind);
                url = _alertUrl.Trim();
                secret = _secret;
                header = _header;
                insecure = _insecure;
                clearReply = kind >= 1 && kind <= 9;
                if (room.Length == 0)
                {
                    _status = "Add the room name";
                    PublishAsyncFail();
                    return;
                }
                if (url.Length == 0)
                {
                    _status = "Add the alert URL";
                    PublishAsyncFail();
                    return;
                }
                if (kind == 9 && issue.Trim().Length == 0)
                {
                    _status = "Type the problem first";
                    PublishAsyncFail();
                    return;
                }
                if (issue.Trim().Length == 0 && kind >= 1 && kind <= 8)
                {
                    _status = "Add the issue text";
                    PublishAsyncFail();
                    return;
                }
                body = HelpSchedule.BuildObject(
                    new string[] { _fieldRoom, _fieldAction, _fieldIssue, _fieldMessage },
                    new string[] { room, action, issue, HelpSchedule.ApplyTemplate(template, room, issue) });
            }

            int code;
            string response;
            bool ok = Post(url, body, secret, header, insecure, out code, out response);
            lock (_sync)
            {
                if (!ok)
                {
                    _status = "Could not reach support";
                    PublishAsyncFail();
                    return;
                }
                if (clearReply)
                {
                    _sender = "";
                    _replyTime = "";
                    _replyText = "";
                    _sentLocal = DateTime.Now;
                    _sentUtc = DateTime.UtcNow;
                    _active = true;
                    _status = "Request sent";
                    RefreshClearAtLocked();
                    ArmPollLocked();
                }
                else
                {
                    _active = false;
                    _clearAt = "";
                    StopPollLocked();
                    _status = kind == 11 ? "Fixed" : "Cancelled";
                }
                _sentPending = true;
            }
        }

        private void PublishAsyncFail()
        {
            /* Called under _sync. The outer OnWork publishes the fail pulse after release. */
            _failPending = true;
        }

        private bool _failPending;
        private bool _sentPending;

        private void OnPoll(object unused)
        {
            bool skip = false;
            bool closed = false;
            lock (_sync)
            {
                if (_stopping || !_active)
                    return;
                if (HelpSchedule.IsExpired(_sentLocal, DateTime.Now, _clearHours, _autoClear, _businessOnly, _startMinute, _endMinute, _holiday))
                {
                    _active = false;
                    _clearAt = "";
                    _status = "Request expired";
                    StopPollLocked();
                    skip = true;
                }
                else if (_busy)
                {
                    return;
                }
                else if (!HelpSchedule.PollAllowed(DateTime.Now, _businessOnly, _startMinute, _endMinute, _holiday))
                {
                    RefreshClearAtLocked();
                    closed = true;
                }
                else
                {
                    _busy = true;
                }
            }
            if (skip || closed)
            {
                Publish(false, false);
                return;
            }

            try
            {
                PollOnce();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Teams Room Help: poll {0}", SafeError(ex));
                lock (_sync)
                {
                    _status = "Could not reach support";
                }
                Publish(true, false);
            }
            finally
            {
                lock (_sync)
                {
                    _busy = false;
                }
                Publish(false, false);
            }
        }

        private void PollOnce()
        {
            string url;
            string room;
            string secret;
            string header;
            string body;
            string senderKey;
            string textKey;
            string atKey;
            bool insecure;
            DateTime sentUtc;
            lock (_sync)
            {
                url = _replyUrl.Trim();
                room = _room.Trim();
                secret = _secret;
                header = _header;
                senderKey = _fieldSender;
                textKey = _fieldText;
                atKey = _fieldAt;
                insecure = _insecure;
                sentUtc = _sentUtc;
                body = HelpSchedule.BuildObject(new string[] { _fieldRoom }, new string[] { room });
                if (url.Length == 0)
                {
                    _status = "Add the reply URL";
                    _failPending = true;
                    return;
                }
            }

            int code;
            string response;
            bool ok = Post(url, body, secret, header, insecure, out code, out response);
            lock (_sync)
            {
                if (!ok)
                {
                    _status = "Could not reach support";
                    _failPending = true;
                    return;
                }
                string text = HelpSchedule.ReadString(response, textKey);
                if (text.Length == 0)
                    return;
                string at = HelpSchedule.ReadString(response, atKey);
                DateTime utc;
                if (HelpSchedule.TryParseUtc(at, out utc))
                {
                    if (utc < sentUtc.AddMinutes(-StaleSkewMinutes))
                        return;
                    _replyTime = HelpSchedule.FormatWhen(utc.ToLocalTime(), DateTime.Now);
                }
                else
                {
                    _replyTime = "";
                }
                _sender = HelpSchedule.ReadString(response, senderKey);
                _replyText = text;
                RefreshClearAtLocked();
            }
        }

        private bool Post(string url, string body, string secret, string header, bool insecure, out int code, out string response)
        {
            code = 0;
            response = "";
            Exception last = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    if (TryDispatch(url, body, secret, header, insecure, out code, out response))
                        return true;
                }
                catch (Exception ex)
                {
                    last = ex;
                    code = 0;
                }
            }
            if (last != null)
                ErrorLog.Error("Teams Room Help: post {0}", SafeError(last));
            else if (code != 0)
                ErrorLog.Error("Teams Room Help: post HTTP {0}", code);
            return false;
        }

        private bool TryDispatch(string url, string body, string secret, string header, bool insecure, out int code, out string response)
        {
            code = 0;
            response = "";
            HttpsClient client = new HttpsClient();
            try
            {
                client.TimeoutEnabled = true;
                client.Timeout = HttpTimeoutSeconds;
                client.KeepAlive = false;
                client.AllowAutoRedirect = true;
                client.UseDeviceCA = false;
                bool verify = !insecure;
                client.HostVerification = verify;
                client.PeerVerification = verify;

                HttpsClientRequest request = new HttpsClientRequest();
                request.Url.Parse(url);
                request.RequestType = RequestType.Post;
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.Header.SetHeaderValue(header, secret ?? "");
                request.ContentString = body ?? "";

                HttpsClientResponse result = client.Dispatch(request);
                if (result == null)
                    return false;
                code = result.Code;
                response = result.ContentString ?? "";
                return code >= 200 && code <= 299;
            }
            finally
            {
                client.Dispose();
            }
        }

        private void OnProgramStatus(eProgramStatusEventType type)
        {
            if (type != eProgramStatusEventType.Stopping)
                return;
            lock (_sync)
            {
                _stopping = true;
                _active = false;
                StopPollLocked();
            }
            try { _workTimer.Stop(); } catch (Exception) { }
        }

        private void ArmPollLocked()
        {
            _pollTimer.Reset(_intervalMs, _intervalMs);
        }

        private void StopPollLocked()
        {
            try { _pollTimer.Stop(); } catch (Exception) { }
        }

        private void RefreshClearAtLocked()
        {
            if (!_active || !_autoClear)
            {
                _clearAt = "";
                return;
            }
            DateTime when;
            if (!HelpSchedule.TryClearAt(_sentLocal, DateTime.Now, _clearHours, true, _businessOnly, _startMinute, _endMinute, _holiday, out when))
            {
                _clearAt = "";
                return;
            }
            _clearAt = HelpSchedule.FormatWhen(when, DateTime.Now);
        }

        private string ActionFor(int kind)
        {
            if (kind == 10)
                return _actionCancel;
            if (kind == 11)
                return _actionFixed;
            return _actionHelp;
        }

        private string IssueFor(int kind)
        {
            if (kind >= 1 && kind <= 8)
                return _issues[kind - 1];
            if (kind == 9)
                return _typed;
            return "";
        }

        private string TemplateFor(int kind)
        {
            if (kind == 10)
                return _templateCancel;
            if (kind == 11)
                return _templateFixed;
            return _templateHelp;
        }

        private void Publish(bool fail, bool sent)
        {
            bool failPulse = fail;
            bool sentPulse = sent;
            ushort active;
            ushort busy;
            string status;
            string sender;
            string replyTime;
            string replyText;
            string clearAt;
            lock (_sync)
            {
                if (_failPending)
                {
                    failPulse = true;
                    _failPending = false;
                }
                if (_sentPending)
                {
                    sentPulse = true;
                    _sentPending = false;
                }
                active = _active ? (ushort)1 : (ushort)0;
                busy = _busy ? (ushort)1 : (ushort)0;
                status = _status ?? "";
                sender = _sender ?? "";
                replyTime = _replyTime ?? "";
                replyText = _replyText ?? "";
                clearAt = _clearAt ?? "";
            }
            HelpStateHandler handlers = _stateHandlers;
            if (handlers == null)
                return;
            try
            {
                handlers(
                    active,
                    busy,
                    failPulse ? (ushort)1 : (ushort)0,
                    sentPulse ? (ushort)1 : (ushort)0,
                    status,
                    sender,
                    replyTime,
                    replyText,
                    clearAt);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Teams Room Help: OnState {0}", SafeError(ex));
            }
        }

        private static int ClampInterval(ushort hundredths)
        {
            int ms = hundredths * 10;
            if (ms <= 0)
                return DefaultIntervalMs;
            if (ms < MinIntervalMs)
                return MinIntervalMs;
            if (ms > MaxIntervalMs)
                return MaxIntervalMs;
            return ms;
        }

        private static string Or(string value, string fallback)
        {
            if (value == null || value.Length == 0)
                return fallback;
            return value;
        }

        private static string SafeError(Exception ex)
        {
            string message = ex == null ? "failed" : ex.Message;
            if (message == null || message.Length == 0)
                message = "failed";
            int http = message.IndexOf("http", StringComparison.OrdinalIgnoreCase);
            if (http >= 0)
                message = "request failed";
            if (message.Length > 120)
                message = message.Substring(0, 120);
            return message;
        }
    }
}
