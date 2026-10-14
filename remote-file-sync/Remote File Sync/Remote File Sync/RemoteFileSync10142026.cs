using System;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;

namespace RemoteFileSyncLib
{
    public delegate void FinishedHandler(short operation, short code);

    public class RemoteFileSync
    {
        public const short OpCheck = 1;
        public const short OpRetrieve = 2;
        public const short OpOverwrite = 3;
        public const short OpPullDue = 4;
        public const short OpPull = 5;
        public const short OpPushDue = 6;
        public const short OpPush = 7;

        private const int MaxBytes = 256 * 1024;
        private const int HttpTimeoutSeconds = 15;
        /* SDK types this as a ushort and does not name the unit. 15 matches the HTTP timeout.
           A processor check against a host that does not answer confirms seconds versus milliseconds. */
        private const int TransferTimeout = 15;

        private const short Found = 1;
        private const short Absent = 0;
        private const short Failed = -1;
        private const short Saved = 0;
        private const short MissingFile = 1;
        private const short TooBig = -2;

        private readonly object _sync = new object();
        private readonly CTimer _pullTimer;
        private readonly CTimer _pushTimer;
        private readonly CTimer _workTimer;

        private FinishedHandler _handlers;
        private string _feedback = "";
        private Job _job;
        private bool _busy;
        private bool _stopping;

        public FinishedHandler OnFinished
        {
            get { return null; }
            set
            {
                if (value == null)
                    return;
                lock (_sync)
                {
                    _handlers = (FinishedHandler)Delegate.Combine(_handlers, value);
                }
            }
        }

        public string Feedback
        {
            get
            {
                lock (_sync)
                {
                    return _feedback;
                }
            }
        }

        public RemoteFileSync()
        {
            _pullTimer = new CTimer(OnPullTimer, null, Timeout.Infinite);
            _pushTimer = new CTimer(OnPushTimer, null, Timeout.Infinite);
            _workTimer = new CTimer(Worker, null, Timeout.Infinite);
            CrestronEnvironment.ProgramStatusEventHandler += OnProgramStatus;
        }

        public void SetPull(ushort enable, int intervalTicks)
        {
            if (_stopping || enable == 0 || intervalTicks <= 0)
            {
                _pullTimer.Stop();
                return;
            }

            /* SIMPL unitTime is hundredths of a second, the same unit as DELAY. */
            long ms = (long)intervalTicks * 10L;
            _pullTimer.Reset(ms, ms);
        }

        public void SetPush(ushort enable, int intervalTicks)
        {
            if (_stopping || enable == 0 || intervalTicks <= 0)
            {
                _pushTimer.Stop();
                return;
            }

            long ms = (long)intervalTicks * 10L;
            _pushTimer.Reset(ms, ms);
        }

        public short LocalFileExists(string path)
        {
            string local = NormalizeLocal(path);
            if (local.Length == 0)
                return 0;
            try
            {
                return File.Exists(local) ? (short)1 : (short)0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public void Begin(short operation, string url, string user, string password, string localPath, ushort allowInsecure)
        {
            if (operation != OpCheck && operation != OpRetrieve && operation != OpOverwrite && operation != OpPull && operation != OpPush)
            {
                SetFeedback("Unknown operation", "", "");
                Raise(operation, Failed);
                return;
            }

            Job job = new Job();
            job.Operation = operation;
            job.Url = url == null ? "" : url.Trim();
            job.User = user == null ? "" : user;
            job.Password = password == null ? "" : password;
            job.LocalPath = NormalizeLocal(localPath);
            job.AllowInsecure = allowInsecure;

            lock (_sync)
            {
                if (_stopping || _busy)
                    return;
                _busy = true;
                _job = job;
            }

            _workTimer.Reset(1);
        }

        private void Worker(object unused)
        {
            Job job;
            lock (_sync)
            {
                job = _job;
            }
            if (job == null)
            {
                lock (_sync)
                {
                    _busy = false;
                }
                return;
            }
            short code = Failed;
            try
            {
                if (job.Operation == OpCheck)
                    code = DoCheck(job);
                else if (job.Operation == OpRetrieve)
                    code = DoRetrieve(job);
                else if (job.Operation == OpOverwrite)
                    code = DoOverwrite(job);
                else if (job.Operation == OpPull)
                    code = DoPull(job);
                else if (job.Operation == OpPush)
                    code = DoPush(job);
            }
            catch (Exception ex)
            {
                SetFeedback(ex.Message, job.Password, job.User);
                ErrorLog.Error("Remote File Sync: {0}", Feedback);
                code = Failed;
            }
            finally
            {
                lock (_sync)
                {
                    _busy = false;
                }
                Raise(job.Operation, code);
            }
        }

        private short DoPull(Job job)
        {
            short found = DoCheck(job);
            if (found == Failed)
                return Failed;
            if (found == Absent)
                return Absent;
            if (DoRetrieve(job) == Failed)
                return Failed;
            return Found;
        }

        private short DoPush(Job job)
        {
            return DoOverwrite(job);
        }

        private short DoCheck(Job job)
        {
            int scheme = SchemeOf(job.Url);
            if (scheme < 0)
                return Fail(job, "Remote URL is empty");
            if (scheme == 0)
                return Fail(job, "URL must start with http://, https://, or ftp://");

            if (scheme == 3)
                return Probe(job);

            short head = Head(job, scheme == 2);
            if (head == -2)
                return Probe(job);
            return head;
        }

        private short DoRetrieve(Job job)
        {
            if (!ValidTarget(job, true))
                return Failed;

            string part = job.LocalPath + ".part";
            DeleteQuiet(part);
            short saved = Download(job, part);
            if (saved != Saved)
            {
                DeleteQuiet(part);
                return Failed;
            }

            try
            {
                /* File.Copy onto an existing file returns "Access to the path is denied" on a 4-Series. */
                if (File.Exists(job.LocalPath))
                    File.Delete(job.LocalPath);
                File.Move(part, job.LocalPath);
            }
            catch (Exception ex)
            {
                DeleteQuiet(part);
                return Fail(job, ex.Message);
            }

            SetFeedback("Retrieved", job.Password, job.User);
            return Saved;
        }

        private short DoOverwrite(Job job)
        {
            if (!ValidTarget(job, false))
                return Failed;
            if (!File.Exists(job.LocalPath))
                return Fail(job, "Local file was not found");

            CrestronFileTransferClient client = new CrestronFileTransferClient();
            try
            {
                Prepare(client, job);
                int rc = client.PutFile(job.Url, job.LocalPath);
                if (rc != 0)
                    return Fail(job, HttpsDetail(job, Describe(client, rc)));
            }
            finally
            {
                client.Dispose();
            }

            SetFeedback("Remote file replaced", job.Password, job.User);
            return Saved;
        }

        private short Head(Job job, bool secure)
        {
            if (secure)
                return HeadHttps(job);
            return HeadHttp(job);
        }

        private short HeadHttp(Job job)
        {
            HttpClient client = new HttpClient();
            try
            {
                client.TimeoutEnabled = true;
                client.Timeout = HttpTimeoutSeconds;
                client.AllowAutoRedirect = true;
                if (job.User.Length > 0)
                {
                    client.UserName = job.User;
                    client.Password = job.Password;
                }

                HttpClientRequest request = new HttpClientRequest();
                request.Url.Parse(job.Url);
                request.RequestType = Crestron.SimplSharp.Net.Http.RequestType.Head;

                HttpClientResponse response = client.Dispatch(request);
                try
                {
                    if (response == null)
                        return Fail(job, "No HTTP response");
                    return InterpretStatus(job, response.Code);
                }
                finally
                {
                    if (response != null)
                        response.Dispose();
                }
            }
            catch (Exception ex)
            {
                if (HeadNeedsProbe(ex.Message))
                    return -2;
                return Fail(job, ex.Message);
            }
            finally
            {
                client.Dispose();
            }
        }

        private short HeadHttps(Job job)
        {
            HttpsClient client = new HttpsClient();
            try
            {
                client.TimeoutEnabled = true;
                client.Timeout = HttpTimeoutSeconds;
                client.AllowAutoRedirect = true;
                client.KeepAlive = false;
                client.UseDeviceCA = false;
                bool verify = job.AllowInsecure == 0;
                client.HostVerification = verify;
                client.PeerVerification = verify;
                if (job.User.Length > 0)
                {
                    client.AuthenticationMethod = Crestron.SimplSharp.Net.AuthMethod.BASIC;
                    client.UserName = job.User;
                    client.Password = job.Password;
                }

                HttpsClientRequest request = new HttpsClientRequest();
                request.Url.Parse(job.Url);
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Head;

                HttpsClientResponse response = client.Dispatch(request);
                if (response == null)
                    return Fail(job, "No HTTPS response");
                return InterpretStatus(job, response.Code);
            }
            catch (Exception ex)
            {
                if (HeadNeedsProbe(ex.Message))
                    return -2;
                return Fail(job, HttpsDetail(job, ex.Message));
            }
            finally
            {
                client.Dispose();
            }
        }

        private static string HttpsDetail(Job job, string message)
        {
            if (message == null || message.Length == 0)
                message = "No error";
            if (SchemeOf(job.Url) == 2 && job.AllowInsecure == 0)
                return "HTTPS certificate checks are on. " + message;
            return message;
        }

        private static bool HeadNeedsProbe(string message)
        {
            if (message == null)
                return false;
            string lower = message.ToLower();
            if (lower.IndexOf("end of response") >= 0)
                return true;
            if (lower.IndexOf("unexpected eof") >= 0)
                return true;
            if (lower == "no error")
                return true;
            return false;
        }

        private short InterpretStatus(Job job, int code)
        {
            if (code >= 200 && code <= 299)
            {
                SetFeedback("Remote file found", job.Password, job.User);
                return Found;
            }
            if (code == 404 || code == 410)
            {
                SetFeedback("Remote file not found", job.Password, job.User);
                return Absent;
            }
            if (code == 401 || code == 403)
                return Fail(job, "Login was rejected");
            if (code == 405 || code == 501)
                return -2;
            return Fail(job, "HTTP " + code.ToString());
        }

        private short Probe(Job job)
        {
            if (!ValidTarget(job, true))
                return Failed;

            string probe = job.LocalPath + ".probe";
            short saved = Download(job, probe);
            DeleteQuiet(probe);
            if (saved == MissingFile)
            {
                SetFeedback("Remote file not found", job.Password, job.User);
                return Absent;
            }
            if (saved != Saved)
                return Failed;

            SetFeedback("Remote file found", job.Password, job.User);
            return Found;
        }

        private short Download(Job job, string dest)
        {
            int scheme = SchemeOf(job.Url);
            if (scheme < 0)
                return Fail(job, "Remote URL is empty");
            if (scheme == 0)
                return Fail(job, "URL must start with http://, https://, or ftp://");

            CrestronFileTransferClient client = new CrestronFileTransferClient();
            try
            {
                Prepare(client, job);
                int rc = client.GetFile(job.Url, dest);
                if (rc != 0)
                {
                    DeleteQuiet(dest);
                    if (IsMissing(client))
                    {
                        SetFeedback("Remote file not found", job.Password, job.User);
                        return MissingFile;
                    }
                    return Fail(job, HttpsDetail(job, Describe(client, rc)));
                }
            }
            finally
            {
                client.Dispose();
            }

            try
            {
                FileInfo info = new FileInfo(dest);
                if (info.Length > MaxBytes)
                {
                    DeleteQuiet(dest);
                    return Fail(job, "File is larger than 256 KB");
                }
            }
            catch (Exception ex)
            {
                DeleteQuiet(dest);
                return Fail(job, ex.Message);
            }

            return Saved;
        }

        private bool ValidTarget(Job job, bool needFolder)
        {
            int scheme = SchemeOf(job.Url);
            if (scheme == -1)
            {
                Fail(job, "Remote URL is empty");
                return false;
            }
            if (scheme == 0)
            {
                Fail(job, "URL must start with http://, https://, or ftp://");
                return false;
            }
            if (job.LocalPath.Length == 0)
            {
                Fail(job, "Local path is empty");
                return false;
            }
            if (needFolder && !Directory.Exists(ParentOf(job.LocalPath)))
            {
                Fail(job, "Local folder was not found");
                return false;
            }
            return true;
        }

        private static void Prepare(CrestronFileTransferClient client, Job job)
        {
            client.SetTimeout(TransferTimeout);
            client.SetIPResolve(CrestronFileTransferClient.IPResolveOpt.IPRESOLVE_V4);
            int scheme = SchemeOf(job.Url);
            if (scheme == 2)
            {
                bool verify = job.AllowInsecure == 0;
                client.SetHostVerification(verify);
                client.SetPeerVerification(verify);
                client.SetSslVersion();
            }
            if (job.User.Length > 0)
            {
                client.SetUserName(job.User);
                client.SetPassword(job.Password);
                if (scheme == 1 || scheme == 2)
                    client.SetHttpAuthMethod(CrestronFileTransferClient.FTCAuthMethod.BASIC);
            }
        }

        private static bool IsMissing(CrestronFileTransferClient client)
        {
            int curl = 0;
            string err = "";
            try
            {
                curl = (int)client.GetLastClientError();
                err = client.GetLastClientStrError();
            }
            catch (Exception)
            {
                return false;
            }
            if (curl == 78)
                return true;
            if (err == null)
                return false;
            string lower = err.ToLower();
            if (lower.IndexOf("not found") >= 0)
                return true;
            if (lower.IndexOf("no such") >= 0)
                return true;
            if (lower.IndexOf("550") >= 0)
                return true;
            if (lower.IndexOf("404") >= 0)
                return true;
            return false;
        }

        private string Describe(CrestronFileTransferClient client, int rc)
        {
            string err = "";
            int curl = 0;
            try
            {
                curl = (int)client.GetLastClientError();
                if (rc == 1)
                    err = client.GetLastClientStrError();
            }
            catch (Exception)
            {
                err = "";
            }
            if (err == null)
                err = "";
            if (err.Length == 0 || err.ToLower() == "no error")
                return ExplainCurl(curl, rc);
            return err;
        }

        private static string ExplainCurl(int curl, int rc)
        {
            if (curl == 35 || curl == 59)
                return "HTTPS handshake failed (" + curl.ToString() + ")";
            if (curl == 51 || curl == 60)
                return "HTTPS certificate was rejected (" + curl.ToString() + "). Allow_Insecure_TLS must be high for this bench.";
            if (rc == 87)
                return "Invalid transfer parameter";
            if (rc == -1)
                return "Transfer client was not ready";
            if (curl != 0)
                return "Transfer failed (curl " + curl.ToString() + ")";
            return "Transfer failed (" + rc.ToString() + ")";
        }

        private short Fail(Job job, string message)
        {
            SetFeedback(message, job.Password, job.User);
            ErrorLog.Error("Remote File Sync: {0}", Feedback);
            return Failed;
        }

        private void SetFeedback(string message, string password, string user)
        {
            if (message == null)
                message = "";
            message = StripUserInfo(message);
            if (user != null && user.Length > 0 && password != null && password.Length > 0)
                message = message.Replace(user + ":" + password, user + ":****");
            if (password != null && password.Length > 0)
                message = message.Replace(password, "****");
            if (message.Length > 200)
                message = message.Substring(0, 200);
            lock (_sync)
            {
                _feedback = message;
            }
        }

        private static string StripUserInfo(string message)
        {
            int scheme = message.IndexOf("://");
            while (scheme >= 0)
            {
                int at = message.IndexOf('@', scheme + 3);
                int slash = message.IndexOf('/', scheme + 3);
                if (at > scheme && (slash < 0 || at < slash))
                    message = message.Substring(0, scheme + 3) + message.Substring(at + 1);
                else
                    break;
                scheme = message.IndexOf("://", scheme + 3);
            }
            return message;
        }

        private static int SchemeOf(string url)
        {
            if (url == null || url.Length == 0)
                return -1;
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
                return 3;
            return 0;
        }

        private static string NormalizeLocal(string path)
        {
            if (path == null)
                return "";
            string s = path.Trim();
            if (s.Length == 0)
                return "";
            s = s.Replace('\\', '/');
            while (s.IndexOf("//") >= 0)
                s = s.Replace("//", "/");
            if (s[0] != '/')
                s = "/" + s;
            return s;
        }

        private static string ParentOf(string path)
        {
            int slash = path.LastIndexOf('/');
            if (slash <= 0)
                return "/";
            return path.Substring(0, slash);
        }

        private static void DeleteQuiet(string path)
        {
            try
            {
                if (path != null && path.Length > 0 && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        private void OnPullTimer(object unused)
        {
            if (_stopping)
                return;
            Raise(OpPullDue, 0);
        }

        private void OnPushTimer(object unused)
        {
            if (_stopping)
                return;
            Raise(OpPushDue, 0);
        }

        private void OnProgramStatus(eProgramStatusEventType type)
        {
            if (type != eProgramStatusEventType.Stopping)
                return;
            _stopping = true;
            _pullTimer.Stop();
            _pushTimer.Stop();
            _workTimer.Stop();
        }

        private void Raise(short operation, short code)
        {
            FinishedHandler handlers;
            lock (_sync)
            {
                handlers = _handlers;
            }
            if (handlers == null)
                return;
            try
            {
                handlers(operation, code);
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Remote File Sync: callback: {0}", ex.Message);
            }
        }

        private class Job
        {
            public short Operation;
            public string Url;
            public string User;
            public string Password;
            public string LocalPath;
            public ushort AllowInsecure;
        }
    }
}
