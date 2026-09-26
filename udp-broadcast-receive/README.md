# UDP Broadcast and Receive

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

Crestron **SIMPL+** UDP helpers for **3-Series / 4-Series** processors. Pure SIMPL+ — no Simpl# / `.clz`.

| File | Role |
|------|------|
| `UDP Broadcast and Receive v1.3.usp` | Broadcast `MAC,UID,Data` to `255.255.255.255` and parse inbound datagrams on the same port |
| `UDP Extract Data v1.0.usp` | Latch one peer’s IP / MAC / UID / raw Data when the packet matches this instance’s IP and/or MAC and/or UID filter |

This project is not affiliated with Crestron Electronics.

---

## Load

1. Compile SIMPL+ for 3-Series and 4-Series:

   ```powershell
   & "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
     \rebuild "UDP Broadcast and Receive v1.3.usp" `
     \target series3 series4

   & "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
     \rebuild "UDP Extract Data v1.0.usp" `
     \target series3 series4
   ```

2. In SIMPL Windows, add **UDP Broadcast and Receive v1.3**. Optionally add one **UDP Extract Data v1.0** per peer.
3. Set **UDP_Port** (default 50001). Hold **Enable** high.

`.usp` files must be saved with **Windows (CRLF)** line endings or SIMPL+ reports Error 1700.

---

## Pins (UDP Broadcast and Receive v1.3)

```
                | UDP_Port        |
                | Enforce_Format  |
Enable          |                 | Enable_FB
Retrigger       |                 |
                |                 | Received_IP
Mac             |                 | Received_MAC
UID             |                 | Received_UID
Data            |                 | Received_Data
```

| Pin / parameter | Role |
|-----------------|------|
| Enable | Latched. High opens the UDP socket; low closes it. Sampled at startup. |
| Retrigger | Pulse to re-send the current MAC, UID, and Data (even if Data is empty or unchanged). |
| Mac | Local MAC in the outbound packet. 12 hex digits, or 17 bytes with `:` or `-` separators, formatted to colons. |
| UID | Local unique ID (alphanumeric, hyphen, underscore; up to 32 bytes). |
| Data | Outbound data (up to 255 bytes). A change sends a packet when Data is non-empty. |
| Enable_FB | High while the socket is actually open. |
| Received_IP / MAC / UID / Data | Last **accepted** inbound packet. Own broadcasts are never written here. |
| UDP_Port | 1024–65535, default 50001. |
| Enforce_Format | `0d` No enforcement (default). `1d` Enforce MAC,UID,Data. |

---

## Payload

Wire format, no spaces, max 306 bytes:

```text
MAC,UID,Data
00:00:00:00:00:00,123456789,[Type]value
```

### Receive (No enforcement)

1. Clear all four received outputs.
2. Set `Received_IP` when the sender IP is known.
3. Full `MAC,UID,Data` (two commas, field 1 is a MAC, field 2 is a UID) fills MAC, UID, and Data.
4. MAC recognized but UID not → `Received_MAC` plus remainder as Data.
5. Nothing looks like MAC or UID → `Received_Data` is the entire payload.

### Receive (Enforce MAC,UID,Data)

Only a valid full packet updates the outputs. Anything else is ignored; last good values stay.

### Ignore self

Packets whose payload equals what this instance would send, or whose MAC + UID match this instance, are dropped. Always on.

---

## UDP Extract Data v1.0

One instance per peer. Wire the UDP module’s `Received_*` outputs to the same cue names on this symbol. Do not wire `Enable_FB`.

```
Match_IP_Override   | Match_IP     |
Match_MAC_Override  | Match_MAC    |
Match_UID_Override  | Match_UID    |
                    |              |
Received_IP         |              | Peer_IP
Received_MAC        |              | Peer_MAC
Received_UID        |              | Peer_UID
Received_Data       |              | Peer_Data
```

Match parameters default to `[SKIP]` (that field is not used). Any combination of IP / MAC / UID may be set; all **active** fields must match (AND). Exact, case-sensitive.

At boot, locals take the parameters, then any override with length > 0. A `CHANGE` on an override sets that local to the override string; empty means `[SKIP]` from then on (does not fall back to the parameter).

`Peer_UID` is `Received_UID` from the last match (when the filter is IP or MAC). `Peer_Data` is the raw `Received_Data`.

In the **program**, pulse UDP `Retrigger` on `CHANGE Received_IP` so a new talker gets this processor’s announce. Use **50011 or higher** for processor-to-processor TCP. Leave UDP discovery on **50001**.

---

## Notes

- `#ENABLE_TRACE` is commented out in the UDP `.usp`. Uncomment it to print send/receive lines to the console.
- Limited broadcast (`255.255.255.255`) stays on the local subnet. Directed subnet broadcast is not a parameter in this version.
