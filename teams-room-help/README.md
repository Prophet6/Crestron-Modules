# Teams Room Help v1.0

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

**Highly experimental. Completely untested.** This module has not been run on a processor and has not been tried against a live Teams webhook. Do not put it in a client program until you have tested it yourself.

One 4-Series symbol posts a room help alert to a Teams Workflow webhook and polls a second webhook for the latest human reply. The processor never holds a Microsoft login.

Replace the example URLs and `change-me` secret before a room goes live. The IT handout in this folder is `Teams Room Help - IT setup.docx`.

| File | Role |
|------|------|
| `Teams Room Help v1.0.usp` | SIMPL+ symbol. F1 opens the HTML manual when that file is beside this symbol. |
| `Teams Room Help v1.0.html` | Programmer manual. Keep it next to the `.usp`. |
| `TeamsRoomHelp_v1.clz` | Compiled Simpl# library. Keep it next to the `.usp`. |
| `Teams Room Help/` | Simpl# source (Visual Studio 2022). |

This project is not affiliated with Crestron Electronics.

## Symbol

Presses on the left, settings down the middle, feedback on the right. Open `Teams Room Help v1.0.usp` for the row alignment. Do not restack it.

Each serial override sits on the same row as the parameter it replaces. `Status`, `Response_Sender`, `Response_Time`, `Response_Text`, and `Clear_At` are the serial outputs under that list.

## Defaults

| Parameter | Default |
|-----------|---------|
| `Room_Name` | `Conference Room` |
| `Alert_URL` | `https://prod-00.westus.logic.azure.com/workflows/EXAMPLE/triggers/manual/paths/invoke` |
| `Reply_URL` | `https://prod-00.westus.logic.azure.com/workflows/EXAMPLE-REPLY/triggers/manual/paths/invoke` |
| `Secret` | `change-me` |
| `Secret_Header` | `x-room-help-secret` |
| `Issue_1` through `Issue_4` | `Audio problem`, `No video`, `Can't join`, `Can't share` |
| `Issue_5` through `Issue_8` | `Issue 5` through `Issue 8`. Rename before those presses are shown. |
| `Action_Help` / `Action_Cancel` / `Action_Fixed` | `help` / `cancel` / `fixed` |
| `Field_Room` / `Field_Action` / `Field_Issue` / `Field_Message` | `room` / `action` / `issue` / `message` |
| `Field_Sender` / `Field_Text` / `Field_At` | `sender` / `text` / `at` |
| `Template_Help` | `Urgent - {room}: {issue}` |
| `Template_Cancel` | `{room} cancelled the request. They figured it out.` |
| `Template_Fixed` | `{room} reports the issue is fixed.` |
| `Poll_Interval` | `15s` |
| `Auto_Clear` | On |
| `Clear_After_Hours` | `9` |
| `Business_Hours_Only` | Off |
| `Business_Start` / `Business_End` | `08:00` / `17:00` |

`{room}` and `{issue}` in a template are replaced before the message is put in the JSON.

An empty JSON field name omits that field. Paste the full webhook URL Microsoft shows, including `sig=`.

## Overrides

- Empty serial override uses the parameter. A non-empty serial replaces it.
- `Poll_Interval_Override` and `Clear_After_Override` are whole seconds and whole hours. `0` uses the parameter. Other values clamp to 5–300 seconds and 1–168 hours.
- `Poll_Interval` is `unitTime`. `Clear_After_Hours` is a count of hours (`unitDecimal`) because several hours of `unitTime` do not fit in a SIMPL+ integer.
- Leave `Auto_Clear_Override` and `Business_Hours_Override` unwired to use the parameters. After either digital changes, that digital stays in charge. A digital already high at startup is in charge.
- Clock times are `HH:MM` in 24-hour form. `0` cannot mean “use the parameter” for a time of day.

`Holiday` high means the processor’s current local day is a holiday. Wire it from a calendar symbol. It is not a parameter.

`Allow_Insecure_TLS` high skips HTTPS host and peer checks. Leave it low for Microsoft.

## When it polls and when it clears

Processor local time. Monday through Friday are business days. `17:00` itself is closed.

`Business_Hours_Only` Off: polls whenever a request is active. `Holiday` does not stop polls. The request clears `Clear_After_Hours` clock hours after it was sent. Nine hours from Tuesday 10:00 is Tuesday 19:00. Nine hours from Friday 16:00 is Saturday 01:00.

`Business_Hours_Only` On: polls only from `Business_Start` inclusive to `Business_End` exclusive, on weekdays that are not holidays. Hours outside that window, weekends, and a holiday do not count toward `Clear_After_Hours`. The request stays active while the gate is closed. With 08:00–17:00 and 9 hours:

| Sent | Holiday | Clears |
|------|---------|--------|
| Tuesday 10:00 | no | Wednesday 10:00 |
| Tuesday 18:30 | no | Wednesday 17:00 |
| Friday 09:00 | no | Monday 09:00 |
| Saturday 12:00 | no | Monday 17:00 |
| Tuesday 10:00 | Tuesday | Wednesday 17:00 |

`Auto_Clear` Off keeps the request up until Cancel or Fixed. The business-hours gate can still suppress polls. `Clear_At` shows the deadline while a request is active and auto-clear is on. It is empty otherwise.

A successful help clears the reply serials and ignores a stored reply whose `at` time is more than two minutes before that help. Cancel and Fixed stop the poll and leave the last reply on screen. Leaving the panel page does not stop the poll.

`Response_Time` is the reply `at` value in processor local time. Today shows `3:04 PM`. An older reply shows `Mon 3:04 PM`.

## Wire contract

`POST`, `Content-Type: application/json`, header `x-room-help-secret` unless `Secret_Header` says otherwise. Timeout 8 seconds, then one retry. Bodies are not logged.

Alert body, with the defaults:

```json
{
  "room": "Conference Room",
  "action": "help",
  "issue": "Audio problem",
  "message": "Urgent - Conference Room: Audio problem"
}
```

`action` is `help`, `cancel`, or `fixed`. Reply request is `{ "room": "Conference Room" }`. Reply response is `{ "sender", "text", "at" }` or `{}`. `at` is ISO-8601 UTC.

## Compile

4-Series only. The project uses the Simpl# SDK already stored under `remote-file-sync`.

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "Teams Room Help\Teams Room Help.sln" /p:Configuration=Release /v:m

& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
  \rebuild "Teams Room Help v1.0.usp" `
  \target series4
```

`//#CATEGORY "0"` stays commented until the symbol is inside a user module.
