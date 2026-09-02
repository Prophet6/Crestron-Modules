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
| [ssh-interface-v2](ssh-interface-v2/) | SSH Interface Client v2.0 | **4-Series** | SSH **client** — the processor connects out to a device. Bench mock: [ssh-tcp-mock](https://github.com/Prophet6/ssh-tcp-mock). |
| [dynamic-tcp-client](dynamic-tcp-client/) | TCP Client (Dynamic IP) | **4-Series** | TCP **client** with runtime `Address$` — stock Connect / Connect_FB / TX$ / RX$ / status, no IP table. |

Compile SIMPL+ with SPlusCC — see [COMPILE_SIMPL_PLUS_CLI.md](COMPILE_SIMPL_PLUS_CLI.md).

## Related repositories

| Repo | Relationship |
|------|----------------|
| [ssh-tcp-mock](https://github.com/Prophet6/ssh-tcp-mock) | PC mock SSH server / raw TCP client for bench tests |
| [crestron-cip-poc](https://github.com/Prophet6/crestron-cip-poc) | ESP32 CIP/SCIP proof of concept |
| [esphome-configs](https://github.com/Prophet6/esphome-configs) | ESPHome device configs |
| [esp32-xyte-poc](https://github.com/Prophet6/esp32-xyte-poc) | Separate Xyte ESP32 track |

## Git hygiene

Track source and usable program files. Ignore `SPlsWork/`, `bin/`, `obj/`, autosaves, and compiled program packages. `.smw` / `.umc` are **binary** in `.gitattributes`.
