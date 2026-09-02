# TCP Client (Dynamic IP)

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

Crestron SIMPL+ / Simpl# **TCP client** for **4-Series** processors (RMC4, CP4, CP4N, VC-4, and similar). Drop-in for the built-in SIMPL Windows **TCP/IP Client** symbol, except the remote host is a serial input (`Address$`) instead of an IP-table Address parameter. No IP-ID required.

| File | Role |
|------|------|
| `TCP Client (Dynamic IP).usp` | SIMPL+ symbol |
| `Dynamic_TCP_Client.clz` | Compiled Simpl# library (required next to the `.usp`) |
| `Dynamic TCP Client/` | Simpl# source (Visual Studio 2022) |

This is the outbound counterpart to [TCP Server with Authentication](../tcp-server-with-authentication/). This project is not affiliated with Crestron Electronics.

---

## Load

1. Keep `Dynamic_TCP_Client.clz` in this folder.
2. Compile SIMPL+ for **4-Series only**:

   ```powershell
   & "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
     \rebuild "TCP Client (Dynamic IP).usp" `
     \target series4
   ```

   A 3-Series target will fail (`archive does not contain a valid SIMPL# assembly`). That is expected for this NuGet SDK library.

3. In SIMPL Windows, add **TCP Client (Dynamic IP)**.
4. Drive `Address$` with the host (IPv4 or hostname), set **Port** (default 23), hold **Connect** high.

`.usp` files must be saved with **Windows (CRLF)** line endings or SIMPL+ reports Error 1700.

---

## Pins

```
Connect         | Port | Connect_FB
Port_Override   |      | status
Address$        |      |
TX$             |      | RX$
```

| Pin | Role |
|-----|------|
| Connect | Hold high to open the socket; low closes it. Sampled at startup. |
| Address$ | Runtime host. Empty = do not connect. Change while Connect is high reconnects. |
| Port | Parameter, default 23. |
| Port_Override | Analog. `> 0` replaces Port; `0` uses the parameter. |
| TX$ | Bytes to send. Ignored unless connected. No extra CR/LF. |
| Connect_FB | High while the socket is up (stock `Connect-F`). |
| status | Stock TCP/IP Client status analog (0–8). |
| RX$ | Incoming bytes, chunked at 250 characters. |

### status

| Value | Meaning |
|------:|---------|
| 0d | Not connected |
| 1d | Waiting for connection |
| 2d | Connected |
| 3d | Connection failed |
| 4d | Connection broken remotely |
| 5d | Connection broken locally |
| 6d | Performing DNS lookup |
| 7d | DNS lookup failed |
| 8d | DNS lookup resolved |

If the remote end drops while Connect is still high, the client waits 100 ms then retries (same idea as the stock client on CUZ 3.029+).

Each symbol instance has its own socket. Drop as many as you need.

---

## Rebuild the Simpl# library (optional)

```powershell
cd "Dynamic TCP Client"
nuget restore
msbuild "Dynamic TCP Client.sln" /p:Configuration=Release
copy "Dynamic TCP Client\bin\Release\Dynamic_TCP_Client.clz" ..
```

Then recompile the `.usp` file (step 2 above).

---

## License / disclaimer

Use at your own risk on a lab network. Not affiliated with Crestron Electronics, Inc.
