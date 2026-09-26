# Enttec DIN EtherGate

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

Crestron SIMPL+ / Simpl# control of an **Enttec DIN EtherGate** (SKU 71030) or **DIN EtherGate MK2** (SKU 71031) for **4-Series** processors. The gateway is a two-universe Ethernet-to-DMX node. It does not store presets. This library sends Art-Net and keeps presets on the processor.

Press **F1** on any of the symbols in SIMPL Windows for the full guide: [`EtherGate v1.html`](EtherGate%20v1.html). That file has to stay next to the `.usp` files.

This project is not affiliated with Crestron Electronics or ENTTEC.

## Modules

| Symbol | File | Role |
|--------|------|------|
| Controller | `EtherGate Controller v1.0.usp` | One gateway. Connect, address, Art-Net universes, status. |
| Channel | `EtherGate Channel v1.0.usp` | One DMX load. Percent in and out. |
| Preset | `EtherGate Preset v1.0.usp` | Save and recall the Channel symbols on that gateway. |

`EtherGate_v1.clz` is the compiled Simpl# library. It must sit in this folder, next to the `.usp` files.

Up to eight gateways. Every symbol has a `Controller` parameter (1–8). An override input uses that parameter until a value arrives on the input. After that, the input is in charge, and 0 is a real value.

`Connect` has to be high before the Controller opens a socket. Releasing `Connect` disconnects. `Connected_FB` goes high only after the gateway accepts TCP port 80. Art-Net (UDP 6454) is sent on the processor LAN port while that feedback is high.

The node name and firmware come from the Art-Net poll reply. The MK2 web page at `index.html?config=1` is a script shell, not a settings dump. The DMX buffer request (`buffer1`) returns live channel rows.

Presets are stored at `\user\EtherGate\slotN.presets` on the processor. `Fade_Time` on the symbol is seconds (`0.5s` is half a second). `Fade_Time_Override` is tenths (`5` is 0.5 seconds, `0` is a cut once that input has been driven).

## Load

1. Keep `EtherGate_v1.clz` and `EtherGate v1.html` in this folder.
2. Compile each SIMPL+ module for **4-Series** only:

```powershell
& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
  \rebuild "EtherGate Controller v1.0.usp" `
  \target series4
```

`.usp` files must be **CRLF** or the compiler reports Error 1700.

3. In SIMPL Windows, add one Controller per gateway, one Channel per load, and one Preset per preset. Give them the same `Controller` number.
4. On the gateway, set the driven ports to **DMX Out** and **Art-Net**, and set the Controller universe parameters to those universes. The address is static, or a DHCP reservation that does not move.

## Rebuild the library

Visual Studio 2022 and NuGet package `Crestron.SimplSharp.SDK.Library` **2.21.274**.

From `EtherGate/`:

```powershell
nuget restore EtherGate.sln
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  EtherGate.sln /p:Configuration=Release
```

Copy `EtherGate\EtherGate\bin\Release\EtherGate_v1.clz` up to this folder.

## Where testing stopped

Checked on a CP4N against a DIN EtherGate MK2, with no DMX fixtures on the ports.

Confirmed: `Connect` gates the session, the gateway answers on port 80, Art-Net returns the node name and firmware (`DIN Ethergate MK2`, `1.1`), and the buffer read comes back as `rowN=` channel values. Channel levels and presets have not been checked against a fixture. The web page does not contain the node name; do not parse it for settings.

Work is paused here.
