# Power Shutdown Confirmation v1.0

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

Pure SIMPL+ page manager for an automatic shutdown warning. When the auto-shutdown sequence starts, it raises a confirmation page and runs a countdown (serial, analog, or both). The user can cancel or confirm. If the timer runs out, the module shuts down as if confirmed.

This project is not affiliated with Crestron Electronics.

<!-- screenshots -->
## Screenshots

![Screenshot 1](Screenshot%201.png)


## Overview

| File | Role |
|------|------|
| `Power Shutdown Confirmation v1.0.usp` | SIMPL+ symbol (no Simpl# library) |

The touchpanel page usually shows a "system is about to shut down" message with **Cancel** and **Confirm** buttons and the countdown.

## Requirements

- **4-Series** or **3-Series** processor
- SIMPL Windows + SIMPL+

## Signals

| Input | Role |
|-------|------|
| Initiate | Starts the warning: raises the page and starts the countdown. Pressing again restarts the count. |
| Cancel | Drops the page and resets the counter. No shutdown. |
| Confirm | Pulses `Shutdown_OS`, drops the page, resets the counter |
| Time_Override | Analog, optional. Countdown length in seconds (1-65535) |

| Output | Role |
|--------|------|
| Warning_Page_FB | High while the confirmation page is up (countdown running) |
| Shutdown_OS | Pulse on Confirm or when the countdown reaches 0 |
| Serial_Count_FB | Time remaining, formatted by `String_Format` |
| Analog_Count_FB | Whole seconds remaining |

| Parameter | Role |
|-----------|------|
| Countdown_Time | Default countdown, 1s-600s (default 30s). Use `Time_Override` for longer. |
| String_Format | 1 Minimal `42` (default), 2 Minutes `0:42`, 3 Hours and minutes `0:00:42`, 4 Leading-zero minutes `00:42`, 5 Leading-zero hours `00:00:42` |

## Usage

1. Pulse **Initiate** when the auto-shutdown sequence begins.
2. Drive the confirmation page or subpage from **Warning_Page_FB**.
3. Show **Serial_Count_FB** and/or **Analog_Count_FB** as the on-screen timer.
4. Wire the panel's Cancel and Confirm buttons to **Cancel** and **Confirm**.
5. Wire **Shutdown_OS** into the real power-down / system-off logic.
6. Put the static warning text on the VTPro page (or a separate serial join).

## Version History

| Version | Date | Changes |
|---------|------|---------|
| v1.0 | 2026-07-15 | Repurposed from Serial/Analog Countdown v2.0. Geared to power-shutdown confirmation. |