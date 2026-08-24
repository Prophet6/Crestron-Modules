# Crestron MQTT Integration Project

**Canonical MQTT sources for git / rebuild live in this folder.**
A former sibling `CrestronMqttWrapper/` work tree is not part of this public repository.

## Goal
Integrate the Waveshare ESP32-S3 panel with Home Assistant via MQTT using Crestron 4-Series SIMPL# Pro + SIMPL+.

Expose panel metrics (screen state, brightness targets, internal temp, etc.) to HA.
Receive occupancy from HA to wake the panel.

## Current Setup
- Using M2Mqtt (Crestron port from SSharpM2MqttLibrary) because MQTTnet had compatibility issues with the .NET version.
- SIMPL# Pro library: Crestron2Mqtt (see Crestron2Mqtt\Crestron2Mqtt.csproj and CrestronMqttClient.cs)
- SIMPL+ wrapper: CrestronMqttWrapper.usp (exposes inputs/outputs for SIMPL program)

## Files
- Crestron2Mqtt\ : The SIMPL# Pro project (**build input**: `Crestron2Mqtt\Crestron2Mqtt\CrestronMqttClient.cs`)
- SSharpM2MqttLibrary\ : The Crestron-ported M2Mqtt source (use this to avoid Trace conflict)
- CrestronMqttClient.cs and CrestronMqttWrapper.usp : Root wrapper copies (may drift slightly from the VS project file — prefer the project file when building)
- See the .sln and build the library to get .clz

## How to Build
1. Open Crestron2Mqtt\Crestron2Mqtt.sln in VS2022 (with Crestron SDK).
2. Build the project.
3. Copy the .clz and M2Mqtt.Net.dll to your SIMPL program folder.
4. In SIMPL Windows, reference the library and compile the .usp module.
5. Load to 4-Series processor.

## Integration with HA
- Use MQTT broker (e.g. Mosquitto or HA built-in).
- Publish panel state/brightness from Crestron.
- Subscribe to binary_sensor.esp_room_station_occupancy to wake the panel.

## Status and Next Steps
See RESTART_HERE.md and PROJECT_STATUS_AND_NEXT_STEPS.md

**Note:** Sessions will now focus on only ONE project at a time.

## Hardware Note
This is for the panel's MQTT reporting. The panel itself is ESPHome side (see sibling project).

The SSharpM2MqttLibrary is the key for Crestron M2Mqtt port.

## Links
- SSharpM2MqttLibrary: https://github.com/oznetmaster/SSharpM2MqttLibrary
- Main mod discussion for reference: https://github.com/HASwitchPlate/openHASP/discussions/602
