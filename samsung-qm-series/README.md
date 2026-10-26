# Samsung QM Series v1.0

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

RS-232 control for a Samsung QM*C commercial display, including the **QM75C**. The command set is Samsung Multiple Display Control (MDC). This module has not been tried on a live display.

`Samsung QM Series Serial Control v0.1.umc` only paced strings through a COM port. This symbol builds the packets.

This project is not affiliated with Crestron Electronics or Samsung.

| File | Role |
|------|------|
| `Samsung QM Series v1.0.usp` | SIMPL+ symbol. Press F1 on the symbol for the packet layout and command bytes. |

## Connection

9600 baud, 8 data bits, no parity, 1 stop bit, no flow control. Display pin 2 is Tx, pin 3 is Rx, and pin 5 is ground. A Crestron COM port receives on pin 2 and transmits on pin 3, so use a straight-through cable.

`Display_Id` must match the ID in the display menu. It defaults to 1. The older wrapper required ID 1 because these sets are not always shipped as ID 1. Set `Display_Id` to 255 to address every display on the chain.

## What it sends

| Control | Bytes after the header |
|---------|------------------------|
| Power on / off | Command `11`, data `01` or `00` |
| Volume 0–100 | Command `12` |
| Mute on / off | Command `13`, data `01` or `00` |
| HDMI 1 / 2 / 3 | Command `14`, data `21`, `23`, or `31` |
| DisplayPort / MagicInfo | Command `14`, data `25` or `20` |
| Picture size | Command `15`: `00` auto, `01` 16:9, `04` zoom, `0C` wide |
| Volume up / down | Command `62`, data `00` or `01` |
| Any other MDC command | `Raw_Command`, `Raw_Data`, `Send_Raw_Set`, or `Send_Raw_Get` |

`Poll_Enable` queries power, volume, mute, input, and picture size, one query each `Poll_Interval`. A reply starts with `AA FF`. `A` means success and `N` pulses `Nak_FB`.
