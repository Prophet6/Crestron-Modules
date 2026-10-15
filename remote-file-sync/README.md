# Remote File Sync v1.0

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

Crestron SIMPL+ / Simpl# module for **4-Series** processors. One symbol checks a remote file, retrieves it onto the processor, or overwrites the remote file with the processor copy.

The remote location is an `http://`, `https://`, or `ftp://` URL. A Windows share (`\\server\share\...`) and a cloud folder (OneDrive, SharePoint, Google Drive, Dropbox) are not paths the processor can open. Publish that folder over HTTP, HTTPS, or FTP first.

This module does not parse the file. File Reader still owns formats, encryption, and backups.

| File | Role |
|------|------|
| `Remote File Sync v1.0.usp` | SIMPL+ symbol. F1 opens the technical manual when that HTML file is beside this symbol. The comment in the `.usp` is version history. |
| `Remote File Sync v1.0 Technical Manual.html` | User manual. Keep it next to the `.usp`. |
| `RemoteFileSync_v1.clz` | Compiled Simpl# library. Keep it next to the `.usp`. |
| `Remote File Sync/` | Simpl# source (Visual Studio 2022). The source file stays `RemoteFileSync10142026.cs`. The solution names the `.clz` `RemoteFileSync_v1`. |

This project is not affiliated with Crestron Electronics.

<!-- screenshots -->
## Screenshots

<img src="Screenshot%201.png" alt="Screenshot 1" width="400">

## Symbol

```
Check                                        Remote_Exists_FB
Retrieve                                     Local_Exists_FB
Overwrite                                    Busy_FB

Allow_Insecure_TLS                           Retrieved
Enable_Pull                                  Overwritten
Enable_Push
Pull_Interval_Override      Pull_Interval
Push_Interval_Override      Push_Interval

Remote_URL_Override         Remote_URL
Local_Path_Override         Local_Path

Remote_User_Override        Remote_User
Remote_Password_Override    Remote_Password

                                             Feedback
```

`Pull_Interval` and `Push_Interval` are times, 0s through 3600s, default 0s. 0s runs once. Each override analog is whole seconds. 0 uses the matching parameter. Any other value replaces it. A non-empty string override replaces the matching parameter. Empty uses the parameter.

| Signal | What it does |
|--------|----------------|
| Check | Asks whether the remote file exists. Does not change the processor file. |
| Retrieve | Downloads the remote file and replaces the processor file. Pulses `Retrieved`. |
| Overwrite | Uploads the processor file and replaces the remote file. Pulses `Overwritten`. |
| Enable_Pull | While high, checks and retrieves. Never uploads. Interval 0 runs once. |
| Enable_Push | While high, uploads the processor file. Never downloads. Interval 0 runs once. |
| Allow_Insecure_TLS | High skips HTTPS host and peer verification. |

A failed retrieve leaves the previous processor file in place. A failed overwrite leaves the processor file in place. A missing remote file does not delete the processor file. Downloads larger than 256 KB are discarded.

`Local_Path` is the full processor path, extension included, for example `/user/boardroom.json`. `\user\file.json` and `\\user\\file.json` are accepted. 4-Series paths are case-sensitive.

Wire `Retrieved` to File Reader `Read` when the file is a config File Reader already understands. File Reader's folder, name, and extension must land on this same path.

## Load

1. Keep `RemoteFileSync_v1.clz` in this folder.
2. Compile SIMPL+ for **4-Series only**:

   ```powershell
   & "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
     \rebuild "Remote File Sync v1.0.usp" `
     \target series4
   ```

   Use the absolute `.usp` path when the compile is started from another folder. The `.clz` is found next to that path.

## Bench

On the PC, in Testing-Tools, double-click `start-remote-file-sync.bat`. That serves one folder over HTTP port 8080, HTTPS port 8443, and FTP port 2121. Use the LAN IP it prints.

| Protocol | Remote_URL |
|----------|------------|
| HTTP | `http://<LAN IP>:8080/present.json` |
| HTTPS | `https://<LAN IP>:8443/present.json` with `Allow_Insecure_TLS` high |
| FTP | `ftp://<LAN IP>:2121/present.json` |

`Local_Path` can be `/user/present.json`. User and password can be empty, or `crestron` / `crestron`. `missing.json` is absent. `large.bin` is 300 KB and Retrieve must refuse it. The walkthrough is in the Testing-Tools README.

## Processor check

The compile does not prove a transfer.

1. Point `Local_Path` at a file already on the processor. `Local_Exists_FB` is high. `Check` a URL that is not there. `Remote_Exists_FB` stays low, and the processor file is unchanged.
2. Serve a known small file over HTTP. `Check` raises `Remote_Exists_FB`. `Retrieve` replaces the processor file and pulses `Retrieved`. Retrieve again from a host that is down. The previous file stays.
3. Edit the processor file. `Overwrite` replaces the remote file. Retrieve it and confirm the edit came back.
4. `Enable_Pull` with interval 0 retrieves once. With a short interval it retrieves again. It does not upload.
5. HTTPS with a self-signed certificate fails while `Allow_Insecure_TLS` is low and succeeds when it is high.
6. Time a `Check` against a host that does not answer. The transfer timeout is passed as 15. If the call returns in about 15 seconds, that argument is seconds. If it returns at once, change `TransferTimeout` in `RemoteFileSync10142026.cs` to 15000 and rebuild the library.
