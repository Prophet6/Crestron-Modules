# TCP Client (Dynamic IP)

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

Crestron SIMPL+ / Simpl# **TCP client** for **4-Series** processors (RMC4, CP4, CP4N, VC-4, and similar). Drop-in for the built-in SIMPL Windows **TCP/IP Client** symbol, except the remote host is a serial input (`Address$`) instead of an IP-table Address parameter. No IP-ID required.

| File | Role |
|------|------|
| `TCP Client Dynamic IP v1.1.usp` | SIMPL+ symbol (v1.1) |
| `TCP Client Dynamic IP v1.0.usp` | SIMPL+ symbol (v1.0, unchanged) |
| `DynamicTcp_v1_1.clz` | Simpl# library for v1.1 |
| `Dynamic_TCP_Client.clz` | Simpl# library for v1.0. Do not replace this with the v1.1 build. |
| `Dynamic TCP Client/` | Simpl# source (Visual Studio 2022). Assembly `DynamicTcp_v1_1`. |

This is the outbound counterpart to [TCP Server with Authentication](../tcp-server-with-authentication/). Bench the client from a PC with [Testing-Tools](https://github.com/Prophet6/Testing-Tools) (`start-tcp-server.bat`). This project is not affiliated with Crestron Electronics.

<!-- screenshots -->
## Screenshots

<img src="Screenshot%201.png" alt="Screenshot 1" width="400">


---

## Load

1. Keep `DynamicTcp_v1_1.clz` next to `TCP Client Dynamic IP v1.1.usp`. Leave `Dynamic_TCP_Client.clz` in place for v1.0.
2. Compile SIMPL+ for **4-Series only**:

   ```powershell
   & "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
     \rebuild "TCP Client Dynamic IP v1.1.usp" `
     \target series4
   ```

   A 3-Series target will fail (`archive does not contain a valid SIMPL# assembly`). That is expected for this NuGet SDK library.

3. In SIMPL Windows, add **TCP Client (Dynamic IP) v1.1**. v1.0 stays **TCP Client (Dynamic IP)** and still links `Dynamic_TCP_Client`.
4. Drive `Address$` with the host (IPv4 or hostname), set **Port** (default 23), hold **Connect** high.

`.usp` files must be saved with **Windows (CRLF)** line endings or SIMPL+ reports Error 1700.

---

## Bench (PC mock)

Use [Testing-Tools](https://github.com/Prophet6/Testing-Tools) — this PC **listens**, the processor **connects out**.

1. Clone or download Testing-Tools. TCP server needs stock Python (no extra packages).
2. **Right-click `allow-firewall.bat` → Run as administrator** (once). Windows Firewall blocks inbound TCP 23 until you do this; Python does not get an allow rule on its own.
3. Double-click **`start-tcp-server.bat`**. Note the **LAN IP** it prints (not `127.0.0.1`, not a Hyper-V or Bluetooth address).
4. On the symbol:

   | Pin / parameter | Value |
   |-----------------|--------|
   | Address$ | PC LAN IP |
   | Port | `23` |
   | Connect | hold high |

5. Type in the PC window to send to `RX$`. Data on `TX$` appears as `[RX]`.

---

## Pins

```
Connect         |              | Connect_FB
Port_Override   | Port         |
Address$        |              | Status
                |              |
TX$             |              | RX$
```

| Pin | Role |
|-----|------|
| Connect | Hold high to open the socket; low closes it. Sampled at startup. |
| Address$ | Runtime host. Empty = do not connect. Change while Connect is high reconnects. |
| Port | Parameter, default 23. Shares a row with Port_Override. |
| Port_Override | Analog. `> 0` replaces Port; `0` uses the parameter. |
| TX$ | Bytes to send. Ignored unless connected. No extra CR/LF. |
| Connect_FB | High while the socket is up (stock `Connect-F`). |
| Status | Stock TCP/IP Client status analog (0–8). Shares a row with Address$. |
| RX$ | Incoming bytes, chunked at 250 characters. |

### Status

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

While Connect is held, Status is debounced 1.0s. A dead host retries every 100 ms and would otherwise flicker between 0d and 1d. The output stays on 1d (Waiting) until some other code has sat unchanged for 1.0s. Connected (2d) and a release of Connect update immediately. Connect_FB follows the socket with no debounce.

Each symbol instance has its own socket. Drop as many as you need.

---

## Rebuild the Simpl# library (optional)

```powershell
cd "Dynamic TCP Client"
nuget restore
msbuild "Dynamic TCP Client.sln" /p:Configuration=Release
copy "Dynamic TCP Client\bin\Release\DynamicTcp_v1_1.clz" ..
```

Then recompile the `.usp` file (step 2 above).

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| v1.1 | 2026-10-29 | Debounce Status 1.0s while Connect is held so a dead host does not flicker 0d/1d. Connected and disconnect still update immediately. Simpl# library is `DynamicTcp_v1_1`. v1.0 keeps `Dynamic_TCP_Client`. |
| v1.0 | 2026-09-02 | Initial public module |

## License / disclaimer

Use at your own risk on a lab network. Not affiliated with Crestron Electronics, Inc.
