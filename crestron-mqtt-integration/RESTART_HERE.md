# Crestron MQTT Integration - Restart Here

**SESSION PAUSED** (2026-06-01 ~23:56)
User is tired and wrapping up for the night. Will start fresh tomorrow.
**GOING FORWARD RULE:** Only ONE project per session from now on (either this Crestron MQTT integration OR the ESPHome Waveshare panel – never both in parallel).

This project is for the Crestron 4-Series side MQTT integration to communicate with Home Assistant (expose panel metrics, receive occupancy for wake, etc.).

## Current Files
- Crestron2Mqtt\ : Your SIMPL# Pro project (open the .sln in VS2022).
- SSharpM2MqttLibrary\ : The Crestron-ported M2Mqtt source (use these .cs files in your project to avoid Trace conflict with plain NuGet).
- CrestronMqttClient.cs and CrestronMqttWrapper.usp : The wrapper code (SIMPL# class + SIMPL+ module exposing inputs/outputs).
- README.md : Overview.
- RESTART_HERE.md : This file.
- (Build outputs in SPlsWork or bin if present)

## Status
- Using M2Mqtt via the ported library.
- The .cs has the M2Mqtt client with events.
- The .usp has the wrapper with Broker, Port, Connect, etc.
- Last compile had Trace conflict (fixed by using the SSharp port and the using alias in the .cs).
- Goal: Publish panel state (screen on/off, brightness targets, temp, etc.) to HA via MQTT.
- Subscribe to occupancy to trigger wake.

## How to Build and Use
1. Open Crestron2Mqtt\Crestron2Mqtt.sln in VS2022.
2. Include/reference the source from SSharpM2MqttLibrary (especially MqttClient.cs and supporting files).
3. Build to get .clz (and ensure M2Mqtt.Net.dll is included).
4. In SIMPL Windows, reference the library.
5. Use the .usp module in your program to expose to SIMPL logic.
6. Connect to your MQTT broker (e.g. Mosquitto or HA's) and integrate with HA.

See the main RESTART_HERE.md in the parent for overall context.

The ESPHome panel side is in the sibling `..\ESPHome_Waveshare_Panel\`

## Next (tomorrow, one project only)
- Pick EITHER this Crestron MQTT side OR the ESPHome panel for the session.
- Get the library building cleanly with the port.
- Wire the SIMPL+ to your main SIMPL program.
- Test publish/subscribe.
- The ESPHome panel side is in ..\ESPHome_Waveshare_Panel\ (see its RESTART_HERE.md).

Good luck! The files are ready for when you continue.
