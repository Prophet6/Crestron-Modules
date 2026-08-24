# Crestron Panel + Home Assistant Integration Project Status

**PAUSED** – User focusing on ESPHome panel tweaks for now. One project at a time.

**Date:** 2026-05-30 (approx, based on session)
**Board:** Waveshare ESP32-S3-Touch-LCD-4.3 (MIPI RGB, GT911 touch, CH422G expander)
**Current Goal:** 
- Reliable 60-second screen timeout that turns the display off (via CH422G).
- Automatic wake on touch **or** room occupancy (`binary_sensor.esp_room_station_occupancy`).
- Dynamic brightness targets based on `sensor.esp_room_station_room_light` (with 5-6s stabilization to handle jitter).
- Expose useful metrics back to Home Assistant (internal temp, uptime, WiFi, screen state, brightness targets, room light).
- Long-term: Real analog dimming instead of on/off via hardware mod.

## Current State (as of latest YAML)

- **Backlight control:** Still via CH422G pin 2 (`digital_write(2, true/false)`). This is on/off only.
- **Timeout behavior:** After 60s of no interaction → backlight off (and display effectively "sleeping").
- **Wake triggers:**
  - Touchscreen (GT911)
  - Home Assistant occupancy binary sensor (on_press calls `wake_screen`)
- **Brightness logic:** Light sensor drives `full_brightness` and `dim_brightness` globals (used for logging and future PWM). Currently only affects whether we turn the backlight fully on or off.
- **Stabilization:** 6-second delay in `update_brightness_targets` script (with 5% change filter on the sensor).
- **Metrics exposed to HA:**
  - Panel Internal Temperature
  - Panel Uptime
  - Panel WiFi Signal
  - Panel Target Full Brightness (%)
  - Panel Target Dim Brightness (%)
  - Panel Room Light (raw)
  - Panel Screen State (text_sensor: "On" / "Dimmed / Off")
  - Panel Screen On (binary_sensor)
- **Display:** Light blue theme, temp/humidity on separate lines, scrolling disabled.
- **Known issues (pre-mod):**
  - Occasional glitches when turning backlight on/off.
  - Hit-or-miss crashes on wake (I2C/CH422G + GT911 contention on same bus).
  - No true dimming — only on/off.

## Hardware Mod Discussion (Backlight PWM)

You are planning to do the hardware mod tomorrow to get real PWM dimming instead of on/off.

**Goal of the mod:** Move backlight control from CH422G (digital only) to a direct ESP32 PWM pin so you can have smooth, variable brightness levels that respond to ambient light.

**Recommended pin on this board:** GPIO6 (the "Sensor AD" pin on the expansion header).

**Main resource with photos, steps, and component details:**
- https://github.com/HASwitchPlate/openHASP/discussions/602

**Typical mod steps (from community reports):**
1. Remove the resistor between CH422G output (usually EXIO2) and the MP3302 backlight driver EN pin.
2. Remove the capacitor on that line.
3. Solder a wire from ESP32 GPIO6 to the MP3302 EN pad.
4. After mod: Use LEDC PWM on GPIO6 for brightness (0-100%).
5. Continue using CH422G pin 2 as the display enable/DISP pin for proper low-power sleep.

**Post-mod code changes needed (high level):**
- Change the `ledc` output pin from whatever it is now (GPIO15 in some versions) to `GPIO6`.
- Keep using the `backlight` light component for `set_brightness()`.
- In sleep/timeout: Set brightness to 0.0 **and** set CH422G pin 2 low (to disable the panel).
- In wake: Set CH422G pin 2 high first, then apply the calculated brightness.
- The light sensor logic (calculating full/dim targets) stays very similar.

## MQTT / Home Assistant Integration Side (Crestron → HA metrics + occupancy wake)

You decided on the **Community SIMPL# Pro + M2Mqtt** route (instead of Node-RED bridge).

**Why M2Mqtt over MQTTnet:** MQTTnet has compatibility issues with the .NET environment on 4-Series. M2Mqtt is the community standard for Crestron SIMPL# Pro.

**Project location (on your machine):**
`C:\Users\proph\CrestronMqttWrapper\`

**Files written so far:**
- `CrestronMqttClient.cs` — Core SIMPL# Pro class using M2Mqtt (with events for connection state and incoming messages).
- `CrestronMqttWrapper.usp` — SIMPL+ module that exposes clean inputs/outputs (Broker, Port, Connect, Publish, Subscribe, etc.) and calls into the SIMPL# class.
- `SSharpM2MqttLibrary\` — Full cloned repo of the Crestron-ported M2Mqtt (https://github.com/oznetmaster/SSharpM2MqttLibrary). Use the files from here instead of the plain NuGet M2Mqtt to avoid the "Trace already defined" compile error.

**Current blockers on the MQTT side (as of last session):**
- 'Trace' conflict when using plain M2Mqtt.Net.dll (the SSharpM2MqttLibrary port should solve this).
- Need to finish wiring the SIMPL+ module events properly to the SIMPL# class.
- Need to handle threading carefully (Crestron hates direct calls from MQTT callbacks into SIMPL+).

**Next logical steps on MQTT side (when you're ready):**
1. Switch the CrestronMqttClient.cs to use the classes from the cloned SSharpM2MqttLibrary.
2. Add the `using Trace = Crestron.SimplSharp.Trace;` alias (and any other fixes from the port).
3. Rebuild the SIMPL# library → get a clean .clz.
4. Wire the .usp properly (the version in the folder has the basic structure).
5. Expose useful things to HA: screen state, current brightness target, last wake reason (touch vs occupancy), etc.
6. (Optional but nice) Add HA MQTT discovery so the panel appears nicely in HA without manual configuration.

## Current "Sleep / Timeout" Behavior (Pre-Mod)

- 60 seconds of no touch/occupancy → `id(ch422g_hub)->digital_write(2, false)` → backlight off + display effectively disabled.
- Any touch or occupancy → `wake_screen` → `digital_write(2, true)` + reset timer.
- Ambient light still drives the `full_brightness` / `dim_brightness` globals (used for logging and future PWM logic).

## Where We Left Off (as of your last message)

- You have a working (on/off) timeout + occupancy wake + light-sensor-driven targets.
- You have the SSharpM2MqttLibrary cloned locally.
- We were in the middle of building the Crestron side MQTT wrapper (SIMPL# + SIMPL+).
- You were planning to do the backlight PWM hardware mod the next morning (using GPIO6 / Sensor AD pin).
- You were leaning toward keeping "always on + LVGL brightness + display sleep" for now, but wanted to explore the mod for real dimming.

## Key Links / Resources

- Backlight PWM mod (the main detailed thread with photos): https://github.com/HASwitchPlate/openHASP/discussions/602
- Waveshare board docs/schematics: https://www.waveshare.com/wiki/ESP32-S3-Touch-LCD-4.3
- SSharpM2MqttLibrary (the Crestron M2Mqtt port we cloned): https://github.com/oznetmaster/SSharpM2MqttLibrary

## Next Time You Come Back — Suggested Order

1. Finish / test the hardware mod (if you still want real dimming) or decide to stay with on/off + LVGL brightness.
2. Finish the Crestron MQTT wrapper using the cloned SSharpM2MqttLibrary (we can iterate on the .cs and .usp).
3. Decide what exact metrics/states you want to expose from the panel to HA (we already have a good starter list).
4. Clean up any remaining crash/glitch issues from CH422G + GT911 contention (lower I2C speed + delays around writes have helped others).

---

**Feel free to come back anytime and paste this file or just say "pick up where we left off"** — I'll have the full context.

You can also ask me to generate an updated full `ux-test-medium.yaml` or the latest version of the Crestron wrapper files whenever you're ready to continue.

Take a break — you've made a ton of progress! 😊

(If you want, I can also create a shorter "one-page summary" version of this status when you return.)