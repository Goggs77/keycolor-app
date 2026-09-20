# Notes for agents working in this repository

This is a small WinForms utility that colours the keys of a MIDI keyboard. Keep it
small: the value of this code is that one file can be read end to end.

## Shape of the code

* Everything lives in `src/Program.cs`. The classes are, in order: `MidiOut`
  (WinMM output), `Hsv` (conversions), `MidiLog` (readable frame log),
  `DebugWindow`, `MainForm`, `Program`.
* Do not add layers. No interfaces, factories, dependency injection, service
  classes, config files or settings persistence unless explicitly asked for. New
  behaviour goes into the class it belongs to.
* New UI means a field, a line in the constructor's layout block, and a small
  method. Keep the two-column label/field layout and its spacing intact - captions
  have overlapped input boxes before.
* Comments explain *why* (hardware quirks, protocol rules), never *what* the next
  line does.

## Build rules

* `net8.0-windows`, `UseWindowsForms`, no package references.
* `src/NuGet.config` clears the package sources on purpose: restore must stay
  offline. Do not add dependencies or a network-dependent build step.
* The build must stay warning-free: `dotnet build src\KeyColorApp.csproj -c Release`.

## Protocol rules

Only the verified LED frames may be sent:

```
F0 <component> 1E <n> <startSlot> <R G B>... F7
F0 <component> 20 <n> <keyIndex> <paletteSlot>... F7
```

* `component` is `0x03` (right half, 29 keys) or `0x04` (left half, 24 keys).
* **Never write palette slot 0.** A palette write starting at slot 0 replaces the
  built-in skin and leaves that half dark with no key feedback until the keyboard
  is power-cycled. Writes starting at slot 1 or higher are safe.
* The palette holds 29 entries, so the period is capped at 28 keys per cycle.
* Channels are 7-bit; `Hsv.ToSevenBit` does the halving and clamping.
* Do not hard-code device addresses, MACs or port indices; select the port by the
  name match (or by the user's choice).
* Never claim support for a frame that has not been exercised on hardware.

The companion protocol-notes repository documents the full command set, the
device filesystem and the built-in ripple. Point to it rather than copying long
tables into this repository.

## Logging

Every send must go through `MidiOut.Send`, which appends to `MidiLog`. The log's
format is deliberate: a fixed description column, then hex wrapped at 20 bytes per
line with continuation lines indented to the same column. Keep that alignment,
keep the frame decoding in `MidiLog.Decode`, and keep the debug window's `Copy
all`, `Clear` and `follow` behaviour.

## Palettes

* Palette files live in `palettes/` as plain text arrays of six-digit hex colours.
  The loader accepts bare, `#`-prefixed and `0x`-prefixed values and ignores
  separators.
* Treat them as content, not code: do not reformat the existing files, and add new
  ones with a descriptive lowercase name.
* A palette file is applied with Hue Start as a rotation over the file's hues and
  with the S and V boxes as scale factors; keep that meaning when extending it.
