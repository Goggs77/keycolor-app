# Generates the scale palettes under palettes/Scales/.
#
# Convention, the same one the files already in that folder follow: one entry per
# step of the tuning system, in step order starting at the tonic. A step that
# belongs to the scale gets a colour, every other step gets "#000000". Load such a
# file in KeyColorApp and set Period to the number of entries to light exactly
# those keys; the pattern repeats every Period keys in both directions.
#
# The device palette holds 29 entries, of which slot 0 is off. Black entries cost
# nothing and repeated colours share one entry, so a file may be as long as the
# keyboard - 53 keys - as long as it holds no more than 28 different colours. Every
# file below stays inside that, and the app checks it before sending.
#
# Families produced:
#
#   <n>EDO/diatonic    the just-intonation degrees 1/1 9/8 5/4 4/3 3/2 5/3 15/8
#                      snapped to the nearest step of n equal divisions of the
#                      octave. n = 12 reproduces the keyboard's own C major, and
#                      n = 10 reproduces the existing 10EDO/functional.txt.
#                      n = 53 is the largest useful one: the keyboard has 53 keys,
#                      so a 53-step octave uses every key exactly once.
#   <n>EDO/pentatonic  the same for 1/1 9/8 5/4 3/2 5/3 (the major pentatonic).
#   12EDO/<name>       ordinary 12-step scales in published forms.
#   24EDO/<name>       Arabic maqam tone rows in the 24-tone notation, where one
#                      step is a quarter tone.
#   9EDO/pelog*        Central Javanese pelog expressed in 9-tone equal
#                      temperament, plus its two common five-tone pathet.
#
# Colours run from deep blue at the tonic to pale cyan at the top of the scale,
# one colour per scale degree; black entries do not consume a colour.
#
# Only the palette files are written. Nothing here talks to the keyboard.
#
# Sources for the tables (consulted 2026-09-23):
#   * just-intonation degrees - the 5-limit major scale, 1/1 9/8 5/4 4/3 3/2 5/3 15/8
#   * equal divisions of the octave - "Equal temperament", en.wikipedia.org
#   * Arabic maqam notation - "Arabic maqam" and "Jins", en.wikipedia.org, checked
#     against the per-maqam scale pages of maqamworld.com
#   * pelog - "Pelog", en.wikipedia.org, after Surjodiningrat et al. (1972) as
#     summarised by M. Braun, "The gamelan pelog scale of Central Java as an
#     example of a non-harmonic musical scale" (2002)

param(
    [string]$Root = (Join-Path $PSScriptRoot '..\palettes\Scales')
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- scale tables

# The 5-limit diatonic and pentatonic degrees, in semitones over the octave.
$justDiatonic   = @(0.0, 2.0391, 3.8631, 4.9804, 7.0196, 8.8436, 10.8827)
$justPentatonic = @(0.0, 2.0391, 3.8631, 7.0196, 8.8436)

# Equal divisions of the octave to generate. Seven and above only: below that the
# seven-degree set stops being seven distinct steps. Five is left out on purpose -
# in 5-EDO the scale is the whole tuning, so there is nothing to leave dark.
$edoSystems = 7, 9, 11, 13, 15, 16, 17, 19, 22, 24, 26, 28, 31, 35, 37, 41, 53

# Ordinary 12-step scales, step numbers from the tonic.
$twelveEdo = [ordered]@{
    # the seven modes, one per degree of the diatonic set
    ionian            = @(0, 2, 4, 5, 7, 9, 11)
    dorian            = @(0, 2, 3, 5, 7, 9, 10)
    phrygian          = @(0, 1, 3, 5, 7, 8, 10)
    lydian            = @(0, 2, 4, 6, 7, 9, 11)
    mixolydian        = @(0, 2, 4, 5, 7, 9, 10)
    aeolian           = @(0, 2, 3, 5, 7, 8, 10)
    locrian           = @(0, 1, 3, 5, 6, 8, 10)
    # the altered minors and their near relatives
    harmonic_minor    = @(0, 2, 3, 5, 7, 8, 11)
    melodic_minor     = @(0, 2, 3, 5, 7, 9, 11)
    phrygian_dominant = @(0, 1, 4, 5, 7, 8, 10)
    double_harmonic   = @(0, 1, 4, 5, 7, 8, 11)
    hungarian_minor   = @(0, 2, 3, 6, 7, 8, 11)
    ukrainian_dorian  = @(0, 2, 3, 6, 7, 8, 10)
    # five- and six-note sets
    pentatonic_major  = @(0, 2, 4, 7, 9)
    pentatonic_minor  = @(0, 3, 5, 7, 10)
    blues             = @(0, 3, 5, 6, 7, 10)
    # symmetric scales
    whole_tone        = @(0, 2, 4, 6, 8, 10)
    octatonic_wh      = @(0, 2, 3, 5, 6, 8, 9, 11)   # tone, semitone, ...
    octatonic_hw      = @(0, 1, 3, 4, 6, 7, 9, 10)   # semitone, tone, ...
    augmented         = @(0, 3, 4, 7, 8, 11)
    # Japanese pentatonic sets
    hirajoshi         = @(0, 2, 3, 7, 8)
    in_sen            = @(0, 1, 5, 7, 10)
    iwato             = @(0, 1, 5, 6, 10)
    yo                = @(0, 2, 5, 7, 9)
}

# Arabic maqam tone rows in the 24-tone notation, one step per quarter tone, 24
# steps to the octave. The notation rounds every note to the nearest quarter tone;
# where the sources disagree the notated value is used here. The tone row of each
# maqam is written out next to it, on the tonic the sources use.
#
# The eight most common ajnas are remembered by a mnemonic that runs Saba,
# Nahawand, Ajam, Bayati, Sikah, Hijaz, Rast, Kurd. Ajam is the major scale and
# Sikah starts on a quarter-tone tonic, so neither gets a file: Ajam is already
# 24EDO/diatonic, and Sikah would only restate the diatonic on a half-flat tonic.
$maqam24 = [ordered]@{
    # name       tone row on the standard tonic              steps from the tonic
    rast      = @(0, 4,  7, 10, 14, 18, 21)   # C  D  E-half-flat F G  A  B-half-flat
    bayati    = @(0, 3,  6, 10, 14, 16, 20)   # D  E-half-flat    F G  A  B-flat    C
    hijaz     = @(0, 2,  8, 10, 14, 16, 20)   # D  E-flat F-sharp G A  B-flat    C
    kurd      = @(0, 2,  6, 10, 14, 16, 20)   # D  E-flat F       G A  B-flat    C
    nahawand  = @(0, 4,  6, 10, 14, 16, 22)   # C  D  E-flat      F G  A-flat    B
    saba      = @(0, 3,  6,  8, 14, 16, 20)   # D  E-half-flat    F G-flat A B-flat C
    hijazkar  = @(0, 2,  8, 10, 14, 16, 22)   # C  D-flat E       F G  A-flat    B
}

# Central Javanese pelog. Its seven tones sit on seven of the nine steps of 9-EDO,
# with single steps (133 cents) and double steps (267 cents) alternating: the tone
# sequence bem-gulu-dada-pelog-lima-nem-barang is 1 1 2 1 1 2 1 steps. The two
# five-tone pathet are the usual subsets - nem and lima use tones 1 2 3 5 6, and
# barang uses tones 2 3 5 6 7.
$pelogSteps = [ordered]@{
    pelog               = @(0, 1, 2, 4, 5, 6, 8)
    pelog_pathet_nem    = @(0, 1, 2, 5, 6)
    pelog_pathet_barang = @(1, 2, 5, 6, 8)
}

# ------------------------------------------------------------------- colouring

# Deep blue at the tonic shading to pale cyan at the top of the scale.
function Get-DegreeColour([int]$index, [int]$degrees) {
    $t = if ($degrees -le 1) { 0.0 } else { $index / ($degrees - 1) }
    $hue = 225.0 - 45.0 * $t        # 225 deg blue -> 180 deg cyan
    $value = 0.35 + 0.60 * $t
    return Get-HexColour $hue 0.85 $value
}

function Get-HexColour([double]$h, [double]$s, [double]$v) {
    $c = $v * $s
    $x = $c * (1 - [Math]::Abs((($h / 60.0) % 2) - 1))
    $m = $v - $c
    if     ($h -lt  60) { $r, $g, $b = $c, $x, 0 }
    elseif ($h -lt 120) { $r, $g, $b = $x, $c, 0 }
    elseif ($h -lt 180) { $r, $g, $b = 0, $c, $x }
    elseif ($h -lt 240) { $r, $g, $b = 0, $x, $c }
    elseif ($h -lt 300) { $r, $g, $b = $x, 0, $c }
    else                { $r, $g, $b = $c, 0, $x }
    $red   = [int][Math]::Round(($r + $m) * 255)
    $green = [int][Math]::Round(($g + $m) * 255)
    $blue  = [int][Math]::Round(($b + $m) * 255)
    return '#{0:x2}{1:x2}{2:x2}' -f $red, $green, $blue
}

# -------------------------------------------------------------------- the files

function Write-ScaleFile([string]$system, [string]$name, [int]$steps, [int[]]$inScale) {
    $colours = @()
    for ($i = 0; $i -lt $inScale.Count; $i++) { $colours += Get-DegreeColour $i $inScale.Count }

    $entries = @()
    $next = 0
    for ($step = 0; $step -lt $steps; $step++) {
        if ($inScale -contains $step) { $entries += $colours[$next]; $next++ }
        else                          { $entries += '#000000' }
    }
    if ($next -ne $colours.Count) { throw "${system}/${name}: used $next of $($colours.Count) colours" }

    $text = '[' + (($entries | ForEach-Object { '"' + $_ + '"' }) -join ',') + ']'
    $dir = Join-Path $Root $system
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $path = Join-Path $dir "$name.txt"
    # no BOM and no trailing newline, like the files already in the folder
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
    return "$system/$name.txt  steps [$($inScale -join ' ')]"
}

# Nearest step of an n-EDO to each semitone value, wrapped into the octave.
function Get-NearestSteps([int]$steps, [double[]]$semitones) {
    $set = @()
    foreach ($s in $semitones) {
        $step = [int][Math]::Round($s * $steps / 12.0)
        if ($step -ge $steps) { $step -= $steps }
        if ($set -notcontains $step) { $set += $step }
    }
    return @($set | Sort-Object)
}

foreach ($n in $edoSystems) {
    Write-ScaleFile "${n}EDO" 'diatonic'   $n (Get-NearestSteps $n $justDiatonic)
    Write-ScaleFile "${n}EDO" 'pentatonic' $n (Get-NearestSteps $n $justPentatonic)
}

foreach ($name in $twelveEdo.Keys) {
    Write-ScaleFile '12EDO' $name 12 $twelveEdo[$name]
}

foreach ($name in $maqam24.Keys) {
    Write-ScaleFile '24EDO' $name 24 $maqam24[$name]
}

foreach ($name in $pelogSteps.Keys) {
    Write-ScaleFile '9EDO' $name 9 $pelogSteps[$name]
}
