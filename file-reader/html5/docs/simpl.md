# SIMPL pairing — File Reader

Import [`contracts/file-reader.chd`](../contracts/file-reader.chd). Drop it beside **File Reader v3.3.0**. Control Join Id is **1**. The signal list is [`CONTRACT-MAP.md`](CONTRACT-MAP.md).

The extender faces the panel, so its columns are the reverse of the user module:

- Extender **inputs** are module **outputs** (Exists_FB, Digital_Output_1, and the rest).
- Extender **outputs** are module **inputs** (Read, Digital_Input_1, and the rest).

Wire the same name on both symbols. `Set_Default_Values` is a module output and an extender input.

## Parameters

Set these on the user module. They are not joins.

| Parameter | Role |
|---|---|
| File Location | Folder string from the user-module list: `\\nvram\\`, `\\user\\`, `\\html\\`, `\\rm\\`, or `\\rm2\\` |
| Filename | Used while FileName_Override is empty. No extension |
| Format | Used while the Format analog is outside 1 through 6. Default 4, JSON |
| Default Wait Time | Pause after Set_Default_Values. Default 0.25s |

## What the page sends

| Control | Join | Level or pulse |
|---|---|---|
| Read, Write, Delete | those digitals | Pulse |
| Format buttons | Format_Text through Format_Binary | Pulse. The user module sets the Format analog to 1 through 6 |
| Location buttons | File_Location_Override | Analog, held. 0 uses the File Location parameter. 1 through 5 force `\\nvram\\`, `\\user\\`, `\\html\\`, `\\rm\\`, `\\rm2\\` |
| Format field | Format | Analog, on commit. Outside 1 through 6 leaves the parameter in charge |
| Match partial, Encrypt, Enable backup, Keep backup history, Auto-create | those digitals | Held level |
| Name override | FileName_Override | Serial, on commit. 128 characters. No extension |
| Encryption key | Encryption_Key | Serial, on commit. 256 characters |
| Signal edits | Digital_Input_n, Analog_Input_n, Serial_Input_n | On change. Analogs are 0 through 65535. Serials are 256 characters |
| Restore backup, Delete backup | those digitals | Pulse |

The page does not publish a signal until you change it. On connect it sends the edits you already made, and it does not jam untouched slots to 0.

## What the page shows

Exists_FB, Not_Exist_FB, Busy_FB, Encrypted_FB, Backup_Exists_FB, Set_Default_Values, FileName_FB, Operational_Feedback, Size_FB, Detected_Format_FB, Format_Text_FB through Format_Binary_FB, and Digital_Output_n, Analog_Output_n, Serial_Output_n.

Detected format codes 1 through 6 are Text, CSV, XML, JSON, INI, and Binary. The library uses -1 for undetected and -2 for a failed integrity check. A SIMPL analog is 16-bit, so those arrive as 65535 and 65534. The page shows them as Undetected and Integrity check failed. The Format_*_FB digitals light for codes 1 through 6 only. Detected format updates on a read, not on a write.

A read loads the file onto the outputs. The edit column stays until **Use file values**. A successful write copies inputs to outputs inside the module. Busy_FB disables Read, Write, Delete, Restore, and Delete backup.

## Panel

The lab program uses IP-ID **C1** for the browser. The page defaults to that ID.

| Who is connecting | Symbol |
|---|---|
| Browser (Vite or the processor page) | **XPanel 3.0 Crestron HTML5** at C1 |
| A touchpanel with the `.ch5z` loaded | That panel symbol, on its own IP-ID |

One IP-ID cannot be two symbols at once. Do not put a touchpanel on C1 while this XPanel is the client.

From Vite (`http://localhost:5176`), the token is requested from `https://<processor>/cws/websocket/getWebSocketToken`. The processor address is `?host=` when it is not `192.168.86.200`. Open `https://<processor>` once and accept the certificate. On the console: `webserver allowsharedsession`, with user-page auth and SSL enabled.

Browser URL after deploy:

```
https://<processor>/file-reader/index.html?ipID=C1&authToken=<token>
```

This folder does not add a SIMPL program. Import the `.chd` into the program that has File Reader v3.3.0.
