# Crestron Modules

Public catalog of Crestron **SIMPL+** / **Simpl#** modules.

Each module lives in its own folder. Keep the compiled `.clz` (when the module uses Simpl#) next to the `.usp` so SIMPL Windows can find it. [file-reader-standalone](file-reader-standalone/) edits a File Reader JSON file and transfers it over HTTP, HTTPS, or FTP without a SIMPL program.

This project is not affiliated with Crestron Electronics.

## Modules

| Folder | Module | Series | What it does |
|--------|--------|--------|----------------|
| [file-reader](file-reader/) | File Reader v3.3.0 | **4-Series** | Read and write digital, analog, and serial values to Text / CSV / XML / JSON / INI / Binary files, with optional AES encryption and backups. |
| [remote-file-sync](remote-file-sync/) | Remote File Sync v1.0 | **4-Series** | Check, retrieve, or overwrite one file at an `http://`, `https://`, or `ftp://` URL. `Enable_Pull` retrieves on an interval. Pair `Retrieved` with File Reader `Read`. |
| [teams-room-help](teams-room-help/) | Teams Room Help v1.0 | **4-Series** | **Experimental, untested.** Post a room help alert to a Teams Workflow webhook and poll the latest reply. Not run on a processor and not tried against a live Teams webhook. Press F1 for `Teams Room Help v1.0.html`. |
| [planar-urp65](planar-urp65/) | Planar URP65 v1.0 | 3-Series / 4-Series | RS-232 control of a Planar UltraRes P URP65 / URP65-T. Protocol is Planar manual 020-1408-02B. Tested on live displays and working. |
| [samsung-qm-series](samsung-qm-series/) | Samsung QM Series v1.0 | 3-Series / 4-Series | RS-232 MDC control for Samsung QM*C displays, including the QM75C. Tested on live displays and working. |
| [iv-cam-ptz](iv-cam-ptz/) | IV-CAM PTZ v1.0 | **4-Series** | Raw VISCA pan, tilt, zoom, and presets for a Crestron 1 Beyond camera, including the IV-CAM-P12. The module opens TCP port 5500 itself. IV-CAM PTZ Presets v1.0 is the expandable press-to-recall, hold-to-store companion. IV-CAM RTSP v1.0 builds the RTSP URL from the IP address (pure SIMPL+, 3-Series and 4-Series). Tested on a live camera and working. |
| [power-shutdown-confirmation](power-shutdown-confirmation/) | Power Shutdown Confirmation v1.0 | 3-Series / 4-Series | Confirm a shutdown (or similar) action before the program acts on it. |
| [crestron-mqtt-integration](crestron-mqtt-integration/) | Crestron MQTT (HA bridge) | **4-Series** | MQTT client (M2Mqtt / SSharp port) for Home Assistant publish and subscribe. |
| [date](date/) | Date v1.0 | 3-Series / 4-Series | Formats the processor date on `Full_Date`. Spellings can be Off. Final Format is one of six date shapes. Week Format places `Week n`. Press F1 for `Date v1.0.html`. |
| [tcp-server-with-authentication](tcp-server-with-authentication/) | TCP Server with Authentication | 3-Series / 4-Series | TCP listener on the processor. Defaults match the built-in **TCP/IP Server** symbol. Optional TLS (`SecureTCPServer`, `ssl self`) and optional `AUTH` username/password. |
| [ssh-interface-v2](ssh-interface-v2/) | SSH Interface Client v2.0 | **4-Series** | SSH **client** — the processor connects out to a device. Bench: [Testing-Tools](https://github.com/Prophet6/Testing-Tools). |
| [dynamic-tcp-client](dynamic-tcp-client/) | TCP Client Dynamic IP v1.1 | **4-Series** | TCP **client** with runtime `Address$` — stock Connect / Connect_FB / TX$ / RX$ / Status, no IP table. v1.1 debounces Status 1.0s and links `DynamicTcp_v1_1.clz`. v1.0 keeps `Dynamic_TCP_Client.clz`. Bench: [Testing-Tools](https://github.com/Prophet6/Testing-Tools) `start-tcp-server.bat`. |
| [udp-broadcast-receive](udp-broadcast-receive/) | UDP Census v1.0 | **4-Series** | One symbol answers a poll, collects a database of processors, or both. The v2.0 beacon stays in the folder. v1.3 is in `_archive`. |
| [enttec-ethergate-mk2](enttec-ethergate-mk2/) | EtherGate Controller, Channel, and Preset v1.0 | **4-Series** | Art-Net control of an Enttec DIN EtherGate (up to 8 gateways). Press F1 on a symbol for `EtherGate v1.html`. |

Compile SIMPL+ with SPlusCC — see [COMPILE_SIMPL_PLUS_CLI.md](COMPILE_SIMPL_PLUS_CLI.md).

## Related repositories

| Repo | Relationship |
|------|----------------|
| [Testing-Tools](https://github.com/Prophet6/Testing-Tools) | Default PC bench tools repo (TCP server, SSH mock, TCP client). Formerly ssh-tcp-mock. |
| [crestron-cip-poc](https://github.com/Prophet6/crestron-cip-poc) | ESP32 CIP/SCIP proof of concept |
| [esphome-configs](https://github.com/Prophet6/esphome-configs) | ESPHome device configs |
| [esp32-xyte-poc](https://github.com/Prophet6/esp32-xyte-poc) | Separate Xyte ESP32 track |

## Git hygiene

Track source and usable program files. Ignore `SPlsWork/`, `bin/`, `obj/`, autosaves, and compiled program packages. `.smw` / `.umc` are **binary** in `.gitattributes`.
