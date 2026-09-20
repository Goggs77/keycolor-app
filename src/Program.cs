// KeyColorApp - paint a repeating HSV colour cycle across the keys.
//
// "Period" is spatial: period = 12 means twelve palette colours, one per key,
// starting at the chosen start key and repeating every twelve keys to both sides.
// Applying writes the palette and the lamp map once; the colouring is persistent
// until something else changes it (or you press Clear).
//
// Frames (verified on hardware, see PROTOCOL_NOTES.md):
//   palette  F0 <addr> 1E <count> 01 <R G B>... F7    7-bit channels, slots 1..28
//   lamps    F0 <addr> 20 <n> <keyIndex paletteSlot>... F7    slot 0 = off
//   addr 3 = right half (29 keys, global 24..52), addr 4 = left half (0..23)
//
// Slot 0 is never written - on this hardware that replaces the built-in skin.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeyColor
{
    // ------------------------------------------------------------------ MIDI out

    internal sealed class MidiOut : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MidiHeader
        {
            public IntPtr Data;
            public uint BufferLength;
            public uint BytesRecorded;
            public IntPtr User;
            public uint Flags;
            public IntPtr Next;
            public IntPtr Reserved;
            public uint Offset;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public IntPtr[] Reserved2;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Caps
        {
            public ushort Manufacturer, Product;
            public uint DriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
            public ushort Technology, Voices, Notes, ChannelMask;
            public uint Support;
        }

        [DllImport("winmm.dll")] private static extern uint midiOutGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int midiOutGetDevCaps(IntPtr id, ref Caps caps, uint size);
        [DllImport("winmm.dll")] private static extern int midiOutOpen(out IntPtr h, uint id, IntPtr cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] private static extern int midiOutClose(IntPtr h);
        [DllImport("winmm.dll")] private static extern int midiOutReset(IntPtr h);
        [DllImport("winmm.dll")] private static extern int midiOutPrepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] private static extern int midiOutUnprepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] private static extern int midiOutLongMsg(IntPtr h, IntPtr hdr, uint size);

        private static readonly int HeaderSize = Marshal.SizeOf(typeof(MidiHeader));
        private IntPtr handle = IntPtr.Zero;

        public static string[] PortNames()
        {
            uint count = midiOutGetNumDevs();
            string[] names = new string[count];
            for (uint i = 0; i < count; i++)
            {
                Caps caps = new Caps();
                names[i] = midiOutGetDevCaps((IntPtr)i, ref caps, (uint)Marshal.SizeOf(typeof(Caps))) == 0
                    ? caps.Name : "<error>";
            }
            return names;
        }

        public void Open(int portIndex)
        {
            if (midiOutOpen(out handle, (uint)portIndex, IntPtr.Zero, IntPtr.Zero, 0) != 0)
                throw new InvalidOperationException("could not open MIDI port " + portIndex);
        }

        public void Send(byte[] data)
        {
            IntPtr dataPtr = Marshal.AllocHGlobal(data.Length);
            IntPtr headerPtr = Marshal.AllocHGlobal(HeaderSize);
            try
            {
                Marshal.Copy(data, 0, dataPtr, data.Length);
                MidiHeader header = new MidiHeader { Data = dataPtr, BufferLength = (uint)data.Length, Reserved2 = new IntPtr[8] };
                Marshal.StructureToPtr(header, headerPtr, false);
                if (midiOutPrepareHeader(handle, headerPtr, (uint)HeaderSize) != 0) return;
                if (midiOutLongMsg(handle, headerPtr, (uint)HeaderSize) != 0)
                {
                    midiOutUnprepareHeader(handle, headerPtr, (uint)HeaderSize);
                    return;
                }
                for (int i = 0; i < 200; i++)   // wait for the driver to release the buffer
                {
                    MidiHeader current = (MidiHeader)Marshal.PtrToStructure(headerPtr, typeof(MidiHeader));
                    if ((current.Flags & 1) != 0) break;
                    Thread.Sleep(2);
                }
                midiOutUnprepareHeader(handle, headerPtr, (uint)HeaderSize);
                MidiLog.Add(data);
            }
            finally
            {
                Marshal.FreeHGlobal(dataPtr);
                Marshal.FreeHGlobal(headerPtr);
            }
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                midiOutReset(handle);
                midiOutClose(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    // ------------------------------------------------------------------ HSV helper

    internal static class Hsv
    {
        /// <summary>h in degrees (any range), s and v in 0..1. Returns an 8-bit colour.</summary>
        public static Color FromHsv(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            s = Math.Max(0, Math.Min(1, s));
            v = Math.Max(0, Math.Min(1, v));

            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double r, g, b;

            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromArgb(
                (int)Math.Round((r + m) * 255),
                (int)Math.Round((g + m) * 255),
                (int)Math.Round((b + m) * 255));
        }

        /// <summary>The keyboard takes 7-bit colour channels.</summary>
        public static byte ToSevenBit(int eightBit)
        {
            int v = (int)Math.Round(eightBit / 2.0);
            return (byte)(v < 0 ? 0 : v > 127 ? 127 : v);
        }

        /// <summary>Colour -> HSV. h in degrees, s and v in 0..1.</summary>
        public static void ToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;

            v = max;
            s = max <= 0 ? 0 : d / max;
            if (d <= 0) { h = 0; return; }

            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * ((b - r) / d + 2);
            else h = 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
        }

        /// <summary>Linear HSV blend; hue takes the shortest way round the circle.</summary>
        public static Color Lerp(Color a, Color b, double t)
        {
            double ha, sa, va, hb, sb, vb;
            ToHsv(a, out ha, out sa, out va);
            ToHsv(b, out hb, out sb, out vb);

            double dh = hb - ha;
            if (dh > 180) dh -= 360;
            if (dh < -180) dh += 360;

            return FromHsv(ha + dh * t, sa + (sb - sa) * t, va + (vb - va) * t);
        }

        public static double Clamp01(double v)
        {
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }
    }

    // --------------------------------------------------------------- MIDI monitor

    /// <summary>Every frame the app sends is logged here, in readable form.</summary>
    internal static class MidiLog
    {
        public static readonly List<string> Lines = new List<string>();
        public static event Action<string> Added;

        private const int DescriptionWidth = 30;   // fixed column so the hex always starts in the same place
        private const int HexBytesPerLine = 20;    // hex wraps by hand, so continuation lines stay aligned

        public static void Add(byte[] frame)
        {
            string line = Describe(frame);
            Lines.Add(line);
            Action<string> handler = Added;
            if (handler != null) handler(line);
        }

        /// <summary>
        /// "HH:mm:ss.fff  side  description (fixed column)  hex", with the hex wrapped at
        /// a fixed byte count and continuation lines indented to the same column.
        /// </summary>
        private static string Describe(byte[] f)
        {
            string target = f.Length > 1
                ? (f[1] == 3 ? "right" : f[1] == 4 ? "left " : "0x" + f[1].ToString("X2"))
                : "?    ";
            string text = f.Length < 3 || f[0] != 0xF0 ? "raw" : Decode(f);
            string header = string.Format("{0}  {1}  {2} ",
                DateTime.Now.ToString("HH:mm:ss.fff"), target, Fit(text, DescriptionWidth));
            string indent = new string(' ', header.Length);

            var lines = new List<string>();
            for (int offset = 0; offset < f.Length; offset += HexBytesPerLine)
            {
                int count = Math.Min(HexBytesPerLine, f.Length - offset);
                string chunk = Hex(f, offset, count);
                lines.Add(offset == 0 ? header + chunk : indent + chunk);
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static string Decode(byte[] f)
        {
            string text;
            switch (f[2])
            {
                case 0x1E:
                    text = string.Format("palette   {0,3} colours into slot {1}", f[3], f.Length > 4 ? f[4].ToString() : "?");
                    break;
                case 0x20:
                    text = string.Format("lamps     {0,3} keys", f[3]);
                    break;
                case 0x06:
                    text = string.Format("file open {0}  \"{1}\"", f.Length > 3 && f[3] != 0 ? "write" : "read ",
                        f.Length > 5 ? Ascii(f, 5, f[4]) : "");
                    break;
                case 0x07:
                    text = "file close";
                    break;
                case 0x08:
                    text = string.Format("file data {0}  length byte {1}",
                        f.Length > 3 && f[3] != 0 ? "write" : "read ", f.Length > 4 ? f[4].ToString() : "?");
                    break;
                default:
                    text = "cmd 0x" + f[2].ToString("X2");
                    break;
            }
            return text;
        }

        /// <summary>Pad to the description column, clipping anything that would push the hex right.</summary>
        private static string Fit(string text, int width)
        {
            if (text.Length == width) return text;
            if (text.Length < width) return text.PadRight(width);
            return text.Substring(0, width - 1) + "~";
        }

        private static string Ascii(byte[] data, int start, int length)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = start; i < data.Length - 1 && i < start + length; i++) sb.Append((char)data[i]);
            return sb.ToString();
        }

        public static string Hex(byte[] data)
        {
            return Hex(data, 0, data.Length);
        }

        public static string Hex(byte[] data, int start, int count)
        {
            var sb = new System.Text.StringBuilder(count * 3);
            for (int i = start; i < start + count && i < data.Length; i++) sb.Append(data[i].ToString("X2")).Append(' ');
            return sb.ToString().Trim();
        }
    }

    /// <summary>A small window that shows what the application is sending.</summary>
    internal sealed class DebugWindow : Form
    {
        private readonly TextBox output;
        private readonly CheckBox follow;

        public DebugWindow()
        {
            Text = "MIDI debug terminal";
            ClientSize = new Size(880, 460);
            Font = new Font("Consolas", 9f);

            var bar = new Panel { Dock = DockStyle.Top, Height = 34 };
            var copy = new Button { Text = "Copy all", Location = new Point(6, 4), Width = 80 };
            var clear = new Button { Text = "Clear", Location = new Point(92, 4), Width = 70 };
            follow = new CheckBox { Text = "follow", Location = new Point(172, 8), Width = 80, Checked = true };
            copy.Click += delegate { if (MidiLog.Lines.Count > 0) Clipboard.SetText(string.Join(Environment.NewLine, MidiLog.Lines)); };
            clear.Click += delegate { MidiLog.Lines.Clear(); output.Clear(); };
            bar.Controls.Add(copy);
            bar.Controls.Add(clear);
            bar.Controls.Add(follow);

            output = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 16, 18),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.None
            };
            output.Text = string.Join(Environment.NewLine, MidiLog.Lines);
            if (output.TextLength > 0) { output.SelectionStart = output.TextLength; output.ScrollToCaret(); }

            Controls.Add(output);
            Controls.Add(bar);
            MidiLog.Added += OnLineAdded;
            FormClosed += delegate { MidiLog.Added -= OnLineAdded; };
        }

        private void OnLineAdded(string line)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(OnLineAdded), line); return; }
            output.AppendText(line + Environment.NewLine);
            if (follow.Checked) { output.SelectionStart = output.TextLength; output.ScrollToCaret(); }
        }
    }

    // -------------------------------------------------------------------- the app

    internal sealed class MainForm : Form
    {
        // key layout: address, key count, global index of the half's first key
        private readonly (byte Address, int Keys, int Base)[] halves =
        {
            (3, 29, 24),   // right half  -> global keys 24..52
            (4, 24, 0)     // left half   -> global keys  0..23
        };

        private const int TotalKeys = 53;

        private readonly MidiOut midi = new MidiOut();
        private Color[] palette = new Color[0];
        private Color[] fileColors;          // palette loaded from a text file (null = generated)
        private string fileSource = "generated";

        private ComboBox portBox;
        private NumericUpDown periodBox, startKeyBox, hueStartBox, hueStepBox, satBox, valBox;
        private Panel preview;
        private Label status, sourceLabel, hueStepLabel;

        public MainForm()
        {
            Text = "KeyPainter";
            ClientSize = new Size(680, 470);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            // Two columns of "label -> field", with the label columns wide enough that
            // nothing overlaps when the captions grow.
            int y = 12;
            AddLabel("MIDI port", 12, y);
            portBox = AddCombo(95, y, 240);
            AddButton("Refresh", 350, y - 1, 90, FillPorts);

            y += 34;
            AddLabel("Period (keys per cycle)", 12, y);
            periodBox = AddNumber(185, y, 60, 1, 28, 12);
            AddLabel("Start key (0-52)", 265, y);
            startKeyBox = AddNumber(390, y, 60, 0, 52, 0);

            y += 34;
            AddLabel("Hue start (deg)", 12, y);
            hueStartBox = AddNumber(185, y, 60, 0, 359, 0);
            hueStepLabel = AddLabel("Hue per key (deg)", 265, y);
            hueStepBox = AddNumber(390, y, 60, 0, 180, 30);

            y += 34;
            AddLabel("Saturation % / scale", 12, y);
            satBox = AddNumber(185, y, 60, 0, 300, 100);
            AddLabel("Value % / scale", 265, y);
            valBox = AddNumber(390, y, 60, 0, 300, 100);

            y += 42;
            AddButton("Apply", 12, y, 90, Apply);
            AddButton("Clear keyboard", 110, y, 110, ClearKeyboard);
            AddButton("Load palette...", 228, y, 110, LoadPalette);
            AddButton("Use generated", 346, y, 110, UseGenerated);
            AddButton("Debug terminal", 464, y, 110, ShowDebugTerminal);
            sourceLabel = new Label { Text = "source: generated", Location = new Point(12, y + 28), AutoSize = true };
            Controls.Add(sourceLabel);
            y += 56;

            y += 42;
            preview = new Panel { Location = new Point(12, y), Size = new Size(650, 130), BorderStyle = BorderStyle.FixedSingle };
            preview.Paint += (s, e) => DrawPreview(e.Graphics);
            Controls.Add(preview);

            y += 140;
            status = new Label { Location = new Point(12, y), Size = new Size(650, 50), Text = "ready" };
            Controls.Add(status);

            FillPorts();
        }

        // --------------------------------------------------------------- UI helpers

        private Label AddLabel(string text, int x, int y)
        {
            var label = new Label { Text = text, Location = new Point(x, y + 3), AutoSize = true };
            Controls.Add(label);
            return label;
        }

        private ComboBox AddCombo(int x, int y, int width)
        {
            var box = new ComboBox { Location = new Point(x, y), Width = width, DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(box);
            return box;
        }

        private NumericUpDown AddNumber(int x, int y, int width, int min, int max, int value)
        {
            var box = new NumericUpDown
            {
                Location = new Point(x, y),
                Width = width,
                Minimum = min,
                Maximum = max,
                Value = value
            };
            box.ValueChanged += (s, e) => { BuildPalette(); preview.Invalidate(); };
            Controls.Add(box);
            return box;
        }

        private Button AddButton(string text, int x, int y, int width, Action onClick)
        {
            var button = new Button { Text = text, Location = new Point(x, y), Width = width };
            button.Click += (s, e) => onClick();
            Controls.Add(button);
            return button;
        }

        private void FillPorts()
        {
            portBox.Items.Clear();
            foreach (string name in MidiOut.PortNames()) portBox.Items.Add(name);
            for (int i = 0; i < portBox.Items.Count; i++)
            {
                if (portBox.Items[i].ToString().IndexOf("popupiano", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    portBox.SelectedIndex = i;
                    break;
                }
            }
            if (portBox.SelectedIndex < 0 && portBox.Items.Count > 0) portBox.SelectedIndex = 0;
        }

        // ------------------------------------------------------------------ colour

        /// <summary>
        /// Build the period's palette.
        ///
        /// Generated mode  : period entries, hue stepping by "hue per key"; the S and V
        ///                   boxes are the colour's own saturation and value.
        /// File mode       : the loaded colours, with S and V used as *scale factors*
        ///                   (S' = S * box/100, V' = V * box/100), then interpolated
        ///                   linearly in HSV so the first colour lands on the first key
        ///                   and the last colour on the last key of the period.
        /// </summary>
        private void BuildPalette()
        {
            int period = (int)periodBox.Value;
            double scaleS = (double)satBox.Value / 100.0;
            double scaleV = (double)valBox.Value / 100.0;
            double hueOffset = (double)hueStartBox.Value;

            palette = new Color[period];

            if (fileColors != null && fileColors.Length > 0)
            {
                int n = fileColors.Length;
                for (int i = 0; i < period; i++)
                {
                    double t = period == 1 ? 0 : (double)i / (period - 1);   // first key -> first colour
                    double pos = t * (n - 1);                                // ... last key -> last colour
                    int i0 = (int)Math.Floor(pos);
                    int i1 = Math.Min(n - 1, i0 + 1);
                    Color blended = Hsv.Lerp(fileColors[i0], fileColors[i1], pos - i0);

                    double h, s, v;
                    Hsv.ToHsv(blended, out h, out s, out v);
                    // file mode: Hue Start rotates the file's hues, S and V are scale factors
                    palette[i] = Hsv.FromHsv(h + hueOffset, Hsv.Clamp01(s * scaleS), Hsv.Clamp01(v * scaleV));
                }
                return;
            }

            double hue = (double)hueStartBox.Value;
            double step = (double)hueStepBox.Value;
            for (int i = 0; i < period; i++) palette[i] = Hsv.FromHsv(hue + i * step, scaleS, scaleV);
        }

        // ------------------------------------------------------------- palette file

        /// <summary>Reads hex colours out of a text file: "bcb6ad", "#bcb6ad" or "0xbcb6ad".</summary>
        private void LoadPalette()
        {
            using (var dialog = new OpenFileDialog { Filter = "text (*.txt;*.json)|*.txt;*.json|all files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var found = new List<Color>();
                foreach (System.Text.RegularExpressions.Match m in
                         System.Text.RegularExpressions.Regex.Matches(
                             System.IO.File.ReadAllText(dialog.FileName),
                             @"(?:0x|#)?([0-9A-Fa-f]{6})(?![0-9A-Fa-f])"))
                {
                    string hex = m.Groups[1].Value;
                    found.Add(Color.FromArgb(
                        Convert.ToInt32(hex.Substring(0, 2), 16),
                        Convert.ToInt32(hex.Substring(2, 2), 16),
                        Convert.ToInt32(hex.Substring(4, 2), 16)));
                }

                if (found.Count == 0)
                {
                    status.Text = "no hex colours found in " + System.IO.Path.GetFileName(dialog.FileName);
                    return;
                }

                fileColors = found.ToArray();
                fileSource = string.Format("{0} ({1} colours)", System.IO.Path.GetFileName(dialog.FileName), found.Count);
                sourceLabel.Text = "source: " + fileSource;
                hueStepBox.Enabled = false;                  // hue per key has no meaning for a file
                hueStepLabel.ForeColor = SystemColors.GrayText;

                // one key per loaded colour unless that exceeds the 28-entry palette
                periodBox.Value = Math.Min(28, Math.Max(1, found.Count));
                BuildPalette();
                preview.Invalidate();
                status.Text = string.Format(
                    "loaded {0} colours; Hue Start rotates them, S and V scale them, Hue per key is unused", found.Count);
            }
        }

        private void UseGenerated()
        {
            fileColors = null;
            fileSource = "generated";
            sourceLabel.Text = "source: generated";
            hueStepBox.Enabled = true;
            hueStepLabel.ForeColor = SystemColors.ControlText;
            BuildPalette();
            preview.Invalidate();
            status.Text = "generated palette - S and V are absolute";
        }

        /// <summary>
        /// Palette index for a global key: entry 0 sits on the starting key and the cycle
        /// repeats every "period" keys, extending to both sides.
        /// </summary>
        private int IndexFor(int globalKey)
        {
            int period = palette.Length;
            if (period == 0) return 0;
            int relative = globalKey - (int)startKeyBox.Value;
            return ((relative % period) + period) % period;
        }

        private Color ColorFor(int globalKey)
        {
            if (palette.Length == 0) return Color.Black;
            return palette[IndexFor(globalKey)];
        }

        // ------------------------------------------------------------------ frames

        private void SendPalette(byte address)
        {
            var frame = new List<byte> { 0xF0, address, 0x1E, (byte)palette.Length, 0x01 };
            foreach (Color c in palette)
            {
                frame.Add(Hsv.ToSevenBit(c.R));
                frame.Add(Hsv.ToSevenBit(c.G));
                frame.Add(Hsv.ToSevenBit(c.B));
            }
            frame.Add(0xF7);
            midi.Send(frame.ToArray());
        }

        private void SendLamps(byte address, int keyCount, int globalBase)
        {
            var frame = new List<byte> { 0xF0, address, 0x20, (byte)keyCount };
            for (int k = 0; k < keyCount; k++)
            {
                frame.Add((byte)k);
                frame.Add(palette.Length == 0 ? (byte)0 : (byte)(IndexFor(globalBase + k) + 1));
            }
            frame.Add(0xF7);
            midi.Send(frame.ToArray());
        }

        // ------------------------------------------------------------------ actions

        private void Apply()
        {
            if (portBox.SelectedIndex < 0) { status.Text = "no MIDI port selected"; return; }

            BuildPalette();
            try
            {
                midi.Open(portBox.SelectedIndex);
                foreach (var half in halves)
                {
                    SendPalette(half.Address);
                    Thread.Sleep(40);
                    SendLamps(half.Address, half.Keys, half.Base);
                    Thread.Sleep(40);
                }
            }
            catch (Exception ex)
            {
                status.Text = ex.Message;
                return;
            }
            finally
            {
                midi.Dispose();
            }

            preview.Invalidate();
            status.Text = string.Format(
                "applied - source {0}, period {1} keys from start key {2}, S {3}%, V {4}%",
                fileSource, palette.Length, startKeyBox.Value, satBox.Value, valBox.Value);
        }

        private void ClearKeyboard()
        {
            if (portBox.SelectedIndex < 0) { status.Text = "no MIDI port selected"; return; }
            try { midi.Open(portBox.SelectedIndex); }
            catch (Exception ex) { status.Text = ex.Message; return; }

            try
            {
                foreach (var half in halves)
                {
                    var frame = new List<byte> { 0xF0, half.Address, 0x20, (byte)half.Keys };
                    for (int k = 0; k < half.Keys; k++) { frame.Add((byte)k); frame.Add(0); }
                    frame.Add(0xF7);
                    midi.Send(frame.ToArray());
                    Thread.Sleep(40);
                }
            }
            finally
            {
                midi.Dispose();
            }

            preview.Invalidate();
            status.Text = "keyboard cleared";
        }

        // ------------------------------------------------------------------ preview

        private void ShowDebugTerminal()
        {
            new DebugWindow().Show(this);
        }

        private void DrawPreview(Graphics g)
        {
            if (palette.Length == 0) BuildPalette();
            g.Clear(Color.FromArgb(24, 24, 28));

            // palette swatches
            int swatch = Math.Max(4, 620 / Math.Max(1, palette.Length));
            for (int i = 0; i < palette.Length; i++)
            {
                using (var brush = new SolidBrush(palette[i]))
                    g.FillRectangle(brush, 10 + i * swatch, 8, swatch - 2, 26);
            }

            // the 53 keys as they will look
            int keyWidth = 640 / TotalKeys;
            for (int k = 0; k < TotalKeys; k++)
            {
                using (var brush = new SolidBrush(ColorFor(k)))
                    g.FillRectangle(brush, 10 + k * keyWidth, 48, keyWidth - 1, 44);
            }

            using (var pen = new Pen(Color.DimGray))
                g.DrawLine(pen, 10 + 24 * keyWidth, 44, 10 + 24 * keyWidth, 96);   // seam between halves
            g.DrawString(
                string.Format("keys 0..52 (left 0-23 | right 24-52); entry 0 at start key {0}", startKeyBox.Value),
                SystemFonts.DefaultFont, Brushes.LightGray, 10, 100);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
        }
    }
}
