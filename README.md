# Crestron Modules

Public catalog of Crestron **SIMPL+** / **Simpl#** modules.

Each module lives in its own folder. Keep the compiled `.clz` (when the module uses Simpl#) next to the `.usp` so SIMPL Windows can find it.

This project is not affiliated with Crestron Electronics.

## Modules

| Folder | Module | Series | What it does |
|--------|--------|--------|----------------|
| [file-reader](file-reader/) | File Reader v3.2.0 | 3-Series / 4-Series | Read and write digital, analog, and serial values to Text / CSV / XML / JSON / INI / Binary files, with optional AES encryption and backups. |
| [power-shutdown-confirmation](power-shutdown-confirmation/) | Power Shutdown Confirmation v1.0 | 3-Series / 4-Series | Confirm a shutdown (or similar) action before the program acts on it. |
| [crestron-mqtt-integration](crestron-mqtt-integration/) | Crestron MQTT (HA bridge) | **4-Series** | MQTT client (M2Mqtt / SSharp port) for Home Assistant publish and subscribe. |
| [tcp-server-with-authentication](tcp-server-with-authentication/) | TCP Server with Authentication | 3-Series / 4-Series | TCP listener on the processor. Defaults match the built-in **TCP/IP Server** symbol. Optional TLS (`SecureTCPServer`, `ssl self`) and optional `AUTH` username/password. |
| [ssh-interface-v2](ssh-interface-v2/) | SSH Interface Client v2.0 | **4-Series** | SSH **client** — the processor connects out to a device. Bench: [Testing-Tools](https://github.com/Prophet6/Testing-Tools). |
| [dynamic-tcp-client](dynamic-tcp-client/) | TCP Client (Dynamic IP) | **4-Series** | TCP **client** with runtime `Address$` — stock Connect / Connect_FB / TX$ / RX$ / Status, no IP table. Bench: [Testing-Tools](https://github.com/Prophet6/Testing-Tools) `start-tcp-server.bat`. |
| [udp-broadcast-receive](udp-broadcast-receive/) | UDP Broadcast and Receive v1.3 | 3-Series / 4-Series | UDP broadcast send/receive on a configurable port. Payload `MAC,UID,Data`; unstructured packets surface as IP + Data. Latched Enable / Enable_FB. Always ignores own broadcasts. |
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
