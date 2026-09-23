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
| Period (keys per cycle) | how many keys one repeat of the palette spans, 1..53 |
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
and the file is scanned for six-digit hex values. `palettes/Colors/` holds
ready-to-load examples; each is ten colours chosen for a particular look.

| File | Colours |
|---|---|
| `palettes/Colors/green_tuned.txt` | muted grey-greens through to near-white |
| `palettes/Colors/pres1_tuned.txt` | yellow-green into teal and deep blue |
| `palettes/Colors/pres2_tuned.txt` | violet and pink pastels with a deep contrast note |

### How a palette becomes key colours

* The period's first key takes the **first** colour and its last key the **last**
  colour; everything between is interpolated linearly in HSV, with hue taking the
  shortest way round the circle.
* Hue Start then rotates the result; Saturation % and Value % scale each colour's
  S and V (100% leaves them alone, 0% drives them to black).
* Set the period equal to the number of colours for a 1:1 mapping, or anything
  else to stretch or compress the ramp.

### Scales

`palettes/Scales/` holds palettes that light a scale instead of a plain ramp. A
file has one entry per step of a tuning system, in step order from the tonic: the
steps the scale uses get a colour, the rest get `#000000`. Load one - the period
is then set to the entry count for you - and exactly those keys light, repeating
every period keys in both directions. Entry 0 is the tonic, so the Start key
moves that tonic onto another key.

Each file covers one octave of its own tuning, which is not the same thing as one
octave of the keyboard: the 24-tone files put two quarter tones on every key, so
their period spans two keyboard octaves, and a 53-tone file puts one step on every
key of the instrument. Nothing here retunes the keyboard; a file only chooses
which keys light. No file has more than 53 entries, and none uses more than eight
palette entries (the octatonics), so all of them fit the 29-entry palette.

| Path | What is in it |
|---|---|
| `Scales/12EDO/modes.txt` | the white-key pattern, which is also `ionian.txt` |
| `Scales/12EDO/` | the seven modes, harmonic and melodic minor, the two pentatonics, blues, whole tone, both octatonics, augmented, phrygian dominant, double harmonic, Hungarian minor, Ukrainian Dorian, and four Japanese pentatonic sets |
| `Scales/<n>EDO/diatonic.txt` | the seven just-intonation degrees (1/1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8) snapped to the nearest step of *n* equal divisions of the octave, for *n* = 7, 9, 11, 13, 15, 16, 17, 19, 22, 24, 26, 28, 31, 35, 37, 41 and 53 |
| `Scales/<n>EDO/pentatonic.txt` | the same for 1/1, 9/8, 5/4, 3/2, 5/3 |
| `Scales/24EDO/` | Arabic maqam tone rows in the 24-tone notation, one step per quarter tone: rast, bayati, hijaz, kurd, nahawand, saba and hijazkar |
| `Scales/9EDO/pelog*.txt` | Central Javanese pelog expressed in 9-tone equal temperament, plus its two common five-tone pathet |

The hand-made `10EDO/functional.txt` that came with this folder is what the
diatonic rule above produces for *n* = 10, and the rule gives the keyboard's own
major scale at *n* = 12. Colours run from deep blue at the tonic to pale cyan at
the top of the scale, one per degree.

Everything under `Scales/` is generated by `tools/make-scales.ps1`, which also
carries the scale tables and the sources they were taken from (Wikipedia on equal
temperament, Arabic maqam, jins and pelog, and the maqamworld scale pages). Change
the tables there and re-run it rather than editing the files.

## What it sends

Only the two verified LED frames, in the first-generation framing:

```
F0 <component> 1E <n> 01 <R G B>... F7    palette, 7-bit channels, slots 1..28
F0 <component> 20 <n> <key slot>... F7    key lamps, slot 0 = off
```

`component` is `0x03` for the right half and `0x04` for the left. Palette slot 0
is never written: on this hardware that replaces the built-in skin and leaves the
half dark until it is power-cycled.

The palette holds 29 entries and slot 0 is off, so what is limited is the number
of **different** colours in a cycle, not its length: entries that share a colour
share a slot, black entries become slot 0 (off), and a cycle of up to 53 keys - the
length of the keyboard - fits as long as it uses no more than 28 different
colours. A generated hue ramp gives every key its own colour, so it needs the
period to stay at 28 or less; a palette file is usually far cheaper. Apply says so
in the status line instead of sending a palette that would wrap.

The wire format is documented in more detail, with provenance for every claim, in
the companion protocol-notes repository.

## License

MIT - see [LICENSE](LICENSE).
