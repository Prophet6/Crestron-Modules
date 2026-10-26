# Date v1.0

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

Pure SIMPL+ date formatter. It reads the processor clock and writes one serial string, `Full_Date`. You choose how the day of week, day, month and year are spelled, the shape of the date, and where the week number goes. The defaults give a full US date such as `Monday, January 3rd, 2026`.

This project is not affiliated with Crestron Electronics.

<!-- screenshots -->
## Screenshots

No screenshots yet.


## Overview

| File | Role |
|------|------|
| `Date v1.0.usp` | SIMPL+ symbol (no Simpl# library). Category Time/Date. |
| `Date v1.0.html` | F1 help: every dropdown value, the date shapes, worked examples and the week-number rules |
| `Testing Date.smw` | Test program |

The module doesn't watch the clock. It writes `Full_Date` at program start, on a rising edge of `Refresh`, and when an override that has already latched changes.

## Requirements

- **4-Series** or **3-Series** processor
- SIMPL Windows + SIMPL+

## Signals

| Input | Role |
|-------|------|
| Refresh | Rising edge rewrites `Full_Date`. Pulse it from a When at 00:00 so the date rolls over. |
| Dow_Format_Override | Analog, optional. Replaces `Dow_Format` |
| Day_Format_Override | Analog, optional. Replaces `Day_Format` |
| Month_Format_Override | Analog, optional. Replaces `Month_Format` |
| Year_Format_Override | Analog, optional. Replaces `Year_Format` |
| Week_Format_Override | Analog, optional. Replaces `Week_Format` |
| Final_Format_Override | Analog, optional. Replaces `Final_Format` |

Each override takes the same numbers as its dropdown, with 0 as the first entry. The dropdown stays in charge until a real value arrives on the input after startup; the 0 of an unwired analog doesn't count. After that the override latches, and a later 0 means Off (or the first shape for `Final_Format`). It does not fall back to the dropdown. A value past the last entry is clamped to the last entry.

| Output | Role |
|--------|------|
| Full_Date | The formatted date string |

| Parameter | Role |
|-----------|------|
| Dow_Format | 0 Off, 1 `M`, 2 `Mo`, 3 `Mon`, 4 `Monday` (default) |
| Day_Format | 0 Off, 1 `3`, 2 `3rd` (default), 3 `Third`, 4 `03` |
| Month_Format | 0 Off, 1 `J`, 2 `Jan`, 3 `January` (default), 4 `1`, 5 `01` |
| Year_Format | 0 Off, 1 `26`, 2 `'26`, 3 `2026` (default) |
| Week_Format | 0 Off (default), 1 `Week 1, <date>`, 2 `<date>, Week 1`, 3 `<date> (Week 1)`, 4 `Week 1: <date>` |
| Final_Format | 0 `Monday, January 3rd, 2026` (default), 1 `Monday, January 3rd 2026`, 2 `Monday, 3rd January 2026`, 3 `Monday, 1/3/2026`, 4 `Monday, 3/1/2026`, 5 `Monday, 2026-01-03` |

`Final_Format` sets only the punctuation and order. The pieces still follow the spelling dropdowns. When a piece is Off, it drops out along with its comma or space. The week is the US calendar week: it starts on Sunday, week 1 contains January 1, and it runs 1 to 53. It is not the ISO week.

## Usage

1. Add **Date v1.0** from Time/Date.
2. Keep the spelling defaults, or set any piece to Off.
3. Pick a date shape in **Final_Format** and, if you want one, a week placement in **Week_Format**.
4. Join **Full_Date** to the touchpanel serial.
5. Pulse **Refresh** from a When at midnight, and after the processor clock is set.
6. Optionally, drive the overrides from an Analog Initialize or program logic to change the format at runtime.
7. Keep `Date v1.0.html` next to the .usp so F1 opens the help page.

| Settings | Full_Date |
|----------|-----------|
| Defaults | `Monday, January 3rd, 2026` |
| Dow `Mon`, month `Jan`, day `3` | `Mon, Jan 3, 2026` |
| Dow Off, month `01`, day `03`, year `26`, shape 3 | `01/03/26` |
| Dow Off, month `01`, day `03`, shape 5 | `2026-01-03` |
| Week 3, other defaults | `Monday, January 3rd, 2026 (Week 1)` |

## Version History

| Version | Date | Changes |
|---------|------|---------|
| v1.0 | 2026-10-26 | Initial module. Six date shapes, separate week placement, a publish at startup, midnight Refresh, and latching analog overrides for every dropdown. |
