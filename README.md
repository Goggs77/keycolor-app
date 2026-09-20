# KeyColorApp

A small Windows desktop tool that paints a repeating colour cycle across the keys
of a PopuPiano-family keyboard over MIDI. Point it at the keyboard, choose how
many keys one cycle spans (the *period*) and where the cycle starts, and press
Apply: the colouring is written once and stays until something changes it.

It is a single C# file, targets .NET 8 with WinForms, and needs no packages.

## Requirements

* Windows, and the .NET 8 SDK (or the desktop runtime, to run a build).
* The keyboard connected over USB or bridged BLE MIDI, appearing as a MIDI output
  port whose name contains `Popupiano`. Any class-compliant MIDI port can be
  chosen manually instead.

## Build and run

```powershell
dotnet build src\KeyColorApp.csproj -c Release
src\bin\Release\net8.0-windows\KeyColorApp.exe
```

Restore is offline by design (`src/NuGet.config` clears all package sources), so
the build works without network access.

## Controls

| Field | Meaning |
|---|---|
| MIDI port | output port to use; `Refresh` rescans, `Popupiano` is preselected |
| Period (keys per cycle) | how many keys one repeat of the palette spans, 1..28 |
| Start key (0-52) | the key that takes palette entry 0; the cycle repeats to both sides |
| Hue start (deg) | generated palette: hue of entry 0. File palette: a rotation applied to every loaded colour |
| Hue per key (deg) | hue step between entries in the generated palette (greyed out when a file is loaded) |
| Saturation %, Value % | generated palette: the colours' own S and V. File palette: scale factors applied to each colour's S and V |
| Apply | build the palette and send it, plus the lamp map, to both halves |
| Clear keyboard | turn every key off |
| Load palette... | read colours from a text file and use them instead of the generated ramp |
| Use generated | go back to the generated hue ramp |
| Debug terminal | a window listing every frame the application sends, decoded |

Keys are indexed 0..52 across the whole instrument: 0..23 is the left half, 24..52
the right half, and the two halves are addressed separately so colouring stops at
the seam like the hardware's own effects.

## Palettes

A palette file is plain text holding hex colours, in any of these spellings:

```
["bcb6ad", "909b77", ...]
["#bcb6ad", "#909b77", ...]
["0xbcb6ad", "0x909b77", ...]
```

Separators do not matter - commas, spaces, newlines and brackets are all ignored,
and the file is scanned for six-digit hex values. `palettes/` holds ready-to-load
examples; each is ten colours chosen for a particular look.

| File | Colours |
|---|---|
| `palettes/green_tuned.txt` | muted grey-greens through to near-white |
| `palettes/pres1_tuned.txt` | yellow-green into teal and deep blue |
| `palettes/pres2_tuned.txt` | violet and pink pastels with a deep contrast note |

### How a palette becomes key colours

* The period's first key takes the **first** colour and its last key the **last**
  colour; everything between is interpolated linearly in HSV, with hue taking the
  shortest way round the circle.
* Hue Start then rotates the result; Saturation % and Value % scale each colour's
  S and V (100% leaves them alone, 0% drives them to black).
* Set the period equal to the number of colours for a 1:1 mapping, or anything
  else to stretch or compress the ramp.

## What it sends

Only the two verified LED frames, in the first-generation framing:

```
F0 <component> 1E <n> 01 <R G B>... F7    palette, 7-bit channels, slots 1..28
F0 <component> 20 <n> <key slot>... F7    key lamps, slot 0 = off
```

`component` is `0x03` for the right half and `0x04` for the left. Palette slot 0
is never written: on this hardware that replaces the built-in skin and leaves the
half dark until it is power-cycled. The palette holds 29 entries, which is why the
period is capped at 28.

The wire format is documented in more detail, with provenance for every claim, in
the companion protocol-notes repository.

## License

MIT - see [LICENSE](LICENSE).
