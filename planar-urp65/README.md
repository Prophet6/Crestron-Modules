# Planar URP65 v1.0

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

RS-232 control for a Planar UltraRes P **URP65** or **URP65-T**. The command set is taken from Planar manual **020-1408-02B** (March 2024), which lists those models. This module has not been tried on a live display.

This project is not affiliated with Crestron Electronics or Planar.

| File | Role |
|------|------|
| `Planar URP65 v1.0.usp` | SIMPL+ symbol. Press F1 on the symbol for the pin and command notes. |

## Connection

19200 baud, 8 data bits, no parity, 1 stop bit, no flow control. Straight-through cable to a Crestron COM port. On the display, pin 2 is Tx, pin 3 is Rx, and pin 5 is ground.

RS-232 answers while the display is in standby only when Power Down Mode is Networked Standby or Fast Startup.

## What it sends

| Control | Command |
|---------|---------|
| Power on / off | `DISPLAY.POWER=ON` / `DISPLAY.POWER=OFF` |
| HDMI 1–4 | `SOURCE.SELECT=HDMI.1` through `HDMI.4` |
| DisplayPort / DisplayPort 2 / VGA | `SOURCE.SELECT=DP` / `DP.2` / `VGA` |
| Volume, brightness, contrast | `AUDIO.VOLUME`, `BRIGHTNESS`, `CONTRAST` set to 0–100, or `+` / `-` for volume |
| Mute | `AUDIO.MUTE=ON` / `OFF` |
| Aspect | `ASPECT=AUTO`, `16X9`, `4X3`, `FILL`, `NATIVE`, or `LETTERBOX` |
| Reset user settings | `RESET(USER)` |

`Poll_Enable` asks for power, source, volume, mute, brightness, contrast, and aspect, one query each `Poll_Interval` (default 5 seconds). Replies use a colon, for example `DISPLAY.POWER:ON`.

## Why the other Planar modules do not fit

The QE, UltraRes Gen2, PS, Simplicity, and UltraLux modules in the user database are different products or different command formats. QE and UltraRes Gen2 use the same command language, but their source lists include OPS and they are built around four zones. URP65 has no OPS input. It does have VGA and a second DisplayPort (`DP.2`).
