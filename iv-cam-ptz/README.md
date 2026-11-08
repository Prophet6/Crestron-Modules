# IV-CAM PTZ v1.0

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

Pan, tilt, zoom, and preset store/recall for a Crestron 1 Beyond PTZ camera. The command set is raw VISCA from the [IV-CAM Series manual](https://docs.crestron.com/en-us/9440/Content/Topics/NextGenCameras/Configuration/VISCA-Commands.htm). It covers the IV-CAM-P12, P20, I12, and I20, and older 1 Beyond heads that take the same packets on TCP port 5500.

This module has not been tried on a live camera.

This project is not affiliated with Crestron Electronics.

| File | Role |
|------|------|
| `IV-CAM PTZ v1.0.usp` | SIMPL+ symbol. Press F1 for the HTML manual. |
| `IV-CAM PTZ v1.0.html` | F1 manual. Keep it next to the `.usp`. |
| `IV-CAM PTZ Presets v1.0.usp` | Expandable preset buttons for the PTZ symbol. Pure SIMPL+. |
| `IV-CAM RTSP v1.0.usp` | Builds the RTSP URL from the camera IP address. Pure SIMPL+. |
| `IvCamPtz_v1.clz` | Simpl# TCP client. The symbol opens port 5500 itself. |
| `IV-CAM PTZ/` | Simpl# source. 4-Series. |

## Connection

Set `IP_Address` to the camera. `0.0.0.0` does not open a socket.

`IP_Address_Override` is ignored until a string arrives. After that, the serial is the address, even when it is empty.

Hold `Enable` high to connect and allow control. Low disconnects and ignores the pad. `Connected_FB` is high while the TCP socket is up. A dropped link retries every 2 seconds while `Enable` stays high.

In Camera Manager, leave control on TCP, server mode, port 5500. The OSD protocol must be VISCA, and `Camera_Address` must match the OSD address. The factory address is 1.

Sony VISCA-over-IP modules (port 52381, 8-byte header) do not speak this dialect.

## Controls

Directions and zoom run while the input is high. Releasing the input sends the VISCA stop for that axis. `Stop` sends both a pan/tilt stop and a zoom stop.

`Command` sends the bytes on that input unchanged. Include the address byte and the `FF` terminator. Address 1 home is `81 01 06 04 FF`.

`Preset` is 0 through 255. `Store_Preset` and `Recall_Preset` use that number. `Home` is the VISCA home command, not preset 0.

`Wake` sends power on (`81 01 04 00 02 FF`). `Privacy` toggles. Awake sends standby (`81 01 04 00 03 FF`). Standby sends power on. On connect the module sends `81 09 04 00 FF`. `90 50 02 FF` raises `Awake_FB`. `90 50 03 FF` raises `Privacy_FB`. Both drop when the socket drops. A pan, tilt, zoom, home, or preset press during standby sends power on. That move is sent only if the button is still held when the camera reports awake and one second has passed. A released button leaves the camera awake and does not move.

Pan speed is 1–24, tilt speed is 1–20, and zoom speed is 0–7. Each speed stays on its parameter until a value arrives on the matching override. After that, the override is kept, including 0. Pan and tilt send 0 as speed 1.

## Preset buttons

`IV-CAM PTZ Presets v1.0` is pure SIMPL+ and compiles for 3-Series and 4-Series. It drives the preset pins on `IV-CAM PTZ v1.0`.

`Press[1]` is camera preset 1. A press shorter than `Hold_Time` recalls. Holding for `Hold_Time` stores. The default hold is 3s (0.5s to 10s). The symbol starts with one button and expands to 32. One preset runs at a time. A new press cancels the press already in progress.

`Press_FB[n]` follows the button. After a store it stays low for `Blink_Time` (default 0.5s, 0s to 5s), then follows the button again. At 0s the feedback stays with the button.

Wire `Store` to `Store_Preset`, `Recall` to `Recall_Preset`, and `Preset` to `Preset`. `Store` and `Recall` pulse. `Preset` updates to the preset number before the pulse.

`Preset_Trigger` recalls preset 0 through 255 when the analog changes. The value present at startup waits for the next change. Send a different number before sending the same number again. Preset 0 is a camera memory. `Home` on the PTZ symbol is a different command.

`Hold_Time` and `Blink_Time` are the first two rows. `Store` and `Recall` are the next two. A blank row sits under `Recall`, then `Press[n]` beside `Press_FB[n]`. A blank row sits under the last button, then `Preset_Trigger` beside `Preset`. Adding a button moves that lower blank row and the analog pair down.

## RTSP URL

`IV-CAM RTSP v1.0` is pure SIMPL+ and compiles for 3-Series and 4-Series. It builds the stream URL. It does not open the stream.

`IP_Address` is the camera. With the defaults, `URL` is `rtsp://<ip>:554/2.h264`. Set `Credentials` to Yes, with a password, to write `rtsp://username:password@ip:port/lens.format`. An empty password leaves the username off. `Credentials_Override` is ignored until it changes. After that, its level is kept, including low.

Lens 1 is the PTZ picture. Lens 2 is the fixed reference camera. On an IV-CAM-I12D-B, lens 1 is PTZ 1, lens 2 is PTZ 2, and lens 3 is the reference. H.264 writes `h264`. H.265 writes `h265`.

A serial override is ignored until text arrives. Text already present at startup is used. After that, the serial is kept, even when empty. An analog override is ignored until it changes. A later 0 is kept. Port 0 then leaves the port off.

Leave `Path` empty for the IV-CAM path. A path replaces the lens and the format. A missing leading slash is added. The path is copied as entered. Username and password are percent-encoded.

This camera accepts RTSP ports 554 and 3479 through 7999. Ports 1 through 65535 are still written.

`Credentials` is the first row. Port, lens, format, username, password, and path follow, each beside its override. A blank row sits under `Path`. `IP_Address` is beside `URL` on the last row. `Password` and `Path` default to blank.

The symbol has not been tried in a loaded program.
