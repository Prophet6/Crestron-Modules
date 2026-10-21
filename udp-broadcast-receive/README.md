# UDP Broadcast and Receive

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

Crestron UDP modules for **4-Series** processors.

| File | Role |
|------|------|
| `UDP Census v1.0.usp` | One symbol. Answer polls, collect a processor database, or both |
| `UdpCensus_v1.clz` | Simpl# library for the census. Keep it next to the `.usp` |
| `UDP Broadcast and Receive v2.0.usp` | Broadcast `MAC,UID,Data` on a timer. Reply is off unless a new sender must be answered once |
| `UDP Extract Data v2.0.usp` | Latch one peer on IP / MAC / UID and drop it after `Timeout` of silence |

`_archive` holds the previous beacon (`UDP Broadcast and Receive v1.3`) and the previous latch (`UDP Extract Data v1.0`). `Testing UDP Packet Modules.smw` is the bench program. This project is not affiliated with Crestron Electronics.

---

## Load

## UDP Census v1.0

One symbol does either job, or both.

| Mode | Setup |
|------|--------|
| Answer only | `Respond_Mode` Answer polls. `Discover_Mode` Off. Hold **Enable**. |
| Database only | `Discover_Mode` Keep the database. `Respond_Mode` Off. |
| Both | Both modes on. This processor polls, stores the answers, and still answers anyone else's poll. |

```
Enable     | UDP_Port       | Enable_FB
Poll       | Poll_Window    | Polling_FB
Respond    | Respond_Mode   | Answered_FB
Discover   | Discover_Mode  |
Index      | Poll_Interval  | Count
           |                | Processor_IP
Mac        |                | Processor_MAC
UID        |                | Processor_UID
Data       |                | Processor_Data
```

**Poll** broadcasts `?POLL`. Responders unicast `!HERE,MAC,UID,Data` to the sender. The collector inserts itself, then keeps one row per UID. A row that misses the next poll is removed when **Poll_Window** ends (default 3s). **Index** 1 shows the first row. **Count** is how many rows survived. During `Polling_FB`, **Answered_FB** is high only for a row that has already replied.

`Poll_Interval` 0s waits for the **Poll** pulse. Any other period sends the first poll half a second after the socket opens, then repeats. A pulse runs one census even when Discover is off. Leave **Respond** and **Discover** unwired to use the parameters. After either digital changes, that digital stays in charge.

The socket binds the LAN adapter and stays open while replies come in. Up to 512 processors.

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "UdpCensus\UdpCensus.sln" /p:Configuration=Release /v:m

Copy-Item "UdpCensus\UdpCensus\bin\Release\UdpCensus_v1.clz" .

& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
  \rebuild "UDP Census v1.0.usp" `
  \target series4
```

With one 4-Series processor, run [Testing-Tools](https://github.com/Prophet6/Testing-Tools) `start-udp-census.bat` on a PC on the same subnet. It answers `?POLL` as `pc-1` and can send a poll of its own. Leave the v2 beacon off so both are not bound to port `50001`.

## Beacon modules

Compile SIMPL+ for 4-Series:

```powershell
& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
  \rebuild "UDP Broadcast and Receive v2.0.usp" `
  \target series4

& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
  \rebuild "UDP Extract Data v2.0.usp" `
  \target series4
```

In SIMPL Windows, add **UDP Broadcast and Receive v2.0**. Add one **UDP Extract Data v2.0** for each peer this program acts on, not one per processor on the network. Set **UDP_Port** (default 50001). Hold **Enable** high. Put a real MAC on **Mac** before expecting the timed announce. On a network of hundreds of processors, set **Announce_Interval** to 15s or 30s and leave **Reply** off.

`.usp` files must be saved with **Windows (CRLF)** line endings or SIMPL+ reports Error 1700.

Use **50011 or higher** for processor-to-processor TCP. Leave UDP discovery on **50001**.

---

## v2.0 pins

```
                | UDP_Port           |
                | Enforce_Format     |
                | Announce_Interval  |
                | Reply              |
Enable          |                    | Enable_FB
Retrigger       |                    |
                |                    |
                |                    | Packet_Rx
                |                    | Received_IP
Mac             |                    | Received_MAC
UID             |                    | Received_UID
Data            |                    | Received_Data
```

| Pin / parameter | Role |
|-----------------|------|
| Enable | Latched. High opens the socket. Low closes it. If the socket drops while Enable is high, it is opened again. |
| Retrigger | Pulse to send now, even when Data is empty. |
| Mac | Local MAC. 12 hex digits, or 17 bytes with `:` or `-`. The timed announce waits until this is a real MAC. |
| UID | Local id. Letters, digits, hyphen, underscore, up to 32 bytes. |
| Data | Payload, up to 255 bytes. A change sends when Data is non-empty. |
| Enable_FB | High while the listen socket is open on `255.255.255.255`. |
| Packet_Rx | 0.05 s pulse after the four received strings are written. |
| Received_IP / MAC / UID / Data | Last accepted packet. Own packets are never written here. |
| UDP_Port | 1024–65535, default 50001. |
| Enforce_Format | `0d` No enforcement (default). `1d` Enforce MAC,UID,Data. |
| Announce_Interval | Time between announces. `0s` off. Default `5s`. Maximum `600s`. Use 15s or 30s when hundreds of processors share the port. |
| Reply | `0d` Off (default). `1d` Answer each new sender once. |

### Announce

While **Enable** is high and **Announce_Interval** is not `0s`, the module sends `MAC,UID,Data` on that period, including when Data is empty. The first announce goes out as soon as the socket is open and **Mac** is valid. **Retrigger** and a non-empty **Data** change still send immediately.

Every processor receives every announce. Two hundred processors at 5s is about 40 datagrams a second at each one, and each datagram rewrites the received strings. Fifteen or thirty seconds is the setting for that fleet. The broadcast is how peers learn each other. A unicast answer to every announce would be one packet for every pair of processors.

### Reply

**Reply** defaults to off. Turn it on only when a new sender must hear you before your next announce. The module then answers that IP once, remembers it for 60 seconds after the last packet from it, and sends at most one reply every 0.5 seconds. One hundred twenty-eight senders fit in the table. A sender past that still hears the broadcast and gets no unicast.

SIMPL+ keeps one destination on a UDP socket. For the reply, the listen socket is pointed at the sender and then put back on `255.255.255.255`. A datagram that arrives in that gap can be missed.

### Payload

Wire format, no spaces, max 306 bytes:

```text
MAC,UID,Data
00:00:00:00:00:00,123456789,[Type]value
```

No enforcement: clear the four received outputs, then fill what matched. A payload that is not a MAC or UID becomes `Received_IP` plus the whole datagram in `Received_Data`.

Enforce MAC,UID,Data: only a full packet updates the outputs. Anything else leaves the last good values in place.

Own packets are dropped. A payload equal to what this module would send, or a MAC + UID that matches this module, never updates the outputs and never gets a reply.

---

## UDP Extract Data v2.0

One instance per peer. Wire `Packet_Rx` and the four `Received_*` outputs to the same names. Do not wire `Enable_FB`.

```
Packet_Rx           | Timeout    | Online_FB
                    |            |
Match_IP_Override   | Match_IP   |
Match_MAC_Override  | Match_MAC  |
Match_UID_Override  | Match_UID  |
                    |            |
Received_IP         |            | Peer_IP
Received_MAC        |            | Peer_MAC
Received_UID        |            | Peer_UID
Received_Data       |            | Peer_Data
```

Match parameters default to `[SKIP]` (that field is not used). Any combination of IP / MAC / UID may be set. All **active** fields must match (AND). Exact, case-sensitive.

At boot, locals take the parameters, then any override with length > 0. A `CHANGE` on an override stores that string. Empty means `[SKIP]` from then on and does not fall back to the parameter.

`Packet_Rx` matches immediately, after v2.0 has finished writing the four strings. The `Received_*` change path still waits 0.01 s, so Extract v2.0 can also sit on the archived v1.3 beacon, which has no `Packet_Rx`.

A non-match does not clear a held peer. **Timeout** does. `0s` keeps the last match and leaves `Online_FB` high. Any other time (default `15s`, maximum `600s`) drops `Online_FB` and clears the peer outputs when no new match arrives.

`Peer_UID` is `Received_UID` from the matching packet. `Peer_Data` is the raw `Received_Data`.

---

## Archived v1.3

`_archive` has **UDP Broadcast and Receive v1.3**, **UDP Extract Data v1.0**, and the v1.3 symbol screenshot. v1.3 sends only on **Retrigger** or a non-empty **Data** change. It does not answer the sender. Extract v1.0 has no `Online_FB` and holds a peer until a newer match replaces it.

---

## Notes

- `#ENABLE_TRACE` is commented out. Uncomment it to print send and receive lines to the console.
- Limited broadcast (`255.255.255.255`) stays on the local subnet.

## Version History

**UDP Broadcast and Receive**

| Version | Date | Changes |
|---------|------|---------|
| v2.0 | 2026-10-19 | `Announce_Interval` (default 5s, `0s` off). **Reply** off by default; on answers each new sender once, at most every 0.5 s, and forgets an IP after 60 s of silence. `Packet_Rx` after the received strings are written. 4-Series. |
| v1.3 | 2026-09-09 | Latched `Enable` / `Enable_FB`. `Retrigger`. `Enforce_Format`. Receive clears then fills. Always ignores own broadcasts. |
| v1.2 | | Broadcast `MAC,UID,Data` and parse incoming UDP on a configurable port |

**UDP Extract Data**

| Version | Date | Changes |
|---------|------|---------|
| v2.0 | 2026-10-18 | `Packet_Rx` matches immediately. `Online_FB` and `Timeout` (default 15s, `0s` keeps the last match). |
| v1.0 | 2026-09-09 | Initial companion latch / filter |
