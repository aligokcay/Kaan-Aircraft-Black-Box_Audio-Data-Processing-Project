using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

class Program
{
    [DllImport(@"C:\Users\Mehmet Ali\Desktop\ctech\3\DLL\AudioConverter.dll",
    EntryPoint = "ConvertBinToWav",
    CallingConvention = CallingConvention.Cdecl,
    CharSet = CharSet.Ansi)]
    public static extern int ConvertBinToWav(string binFile, string wavFile, long byteCount);

    // Ses parametreleri
    public const int SampleRate = 44100; // 44.1 kHz
    public const int Channels = 1;       // Mono
    public const int BitsPerSample = 8;  // 8-bit PCM

    public static int TotalHours { get; private set; }

    [STAThread]
    static void Main()
    {
        string binFile = "output.bin";

        if (!File.Exists(binFile))
        {
            Console.WriteLine("BIN dosyası bulunamadı!");
            return;
        }

        // BIN dosyasının boyutu
        long fileSize = new FileInfo(binFile).Length;

        // Süre hesapla
        double totalSeconds = fileSize / (double)(SampleRate * Channels * (BitsPerSample / 8));
        TimeSpan duration = TimeSpan.FromSeconds(totalSeconds);
        TotalHours = (int)Math.Ceiling(duration.TotalHours - 0.0001);

        Console.WriteLine($"BIN dosyasının süresi: {(int)duration.TotalHours} saat {duration.Minutes} dakika {duration.Seconds} saniye");

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1(TotalHours));
    }
}

public class Form1 : Form
{
    private FlowLayoutPanel panel;
    private Button[] hourButtons;
    private int[] hours;
    private int selectedHourIndex = -1;
    private Button btnConvert;
    private Label lblQuestion;
    private Label lblDuration; // *** İşlem süresi için Label

    public Form1(int totalHours)
    {
        this.Size = new Size(600, 300);
        this.Text = "BIN -> WAV Dönüştürücü";
        this.BackColor = Color.White;

        lblQuestion = new Label()
        {
            Text = "İlk kaç saatini dönüştürmek istiyorsunuz?",
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            AutoSize = true,
            Anchor = AnchorStyles.None,
            TextAlign = ContentAlignment.MiddleCenter
        };

        panel = new FlowLayoutPanel()
        {
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0),
            Margin = new Padding(0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None
        };

        btnConvert = new Button()
        {
            Text = "Dönüştür",
            Width = 140,
            Height = 40,
            BackColor = Color.Gray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Anchor = AnchorStyles.None
        };
        btnConvert.FlatAppearance.BorderSize = 0;
        btnConvert.MouseEnter += (s, e) => btnConvert.BackColor = Color.DarkGray;
        btnConvert.MouseLeave += (s, e) => btnConvert.BackColor = Color.Gray;
        btnConvert.Click += BtnConvert_Click;

        lblDuration = new Label()  // *** İşlem süresi label
        {
            Text = "",
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            AutoSize = true,
            Anchor = AnchorStyles.None,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Black,
            Margin = new Padding(0, 10, 0, 0)
        };

        // Saat butonlarını ekle
        var list = new System.Collections.Generic.List<int>();
        for (int i = 2; i < totalHours; i += 2)
            list.Add(i);
        hours = list.ToArray();
        hourButtons = new Button[hours.Length + 1];
        for (int i = 0; i < hours.Length; i++)
            AddHourButton(i, hours[i], i == 0, false);
        AddHourButton(hours.Length, 0, hours.Length == 0, true);

        // TableLayoutPanel
        TableLayoutPanel mainLayout = new TableLayoutPanel()
        {
            Dock = DockStyle.Fill,
            RowCount = 4,  // *** 3 yerine 4
            ColumnCount = 1,
            BackColor = Color.White
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20)); // *** Ek satır

        mainLayout.Controls.Add(lblQuestion, 0, 0);
        mainLayout.Controls.Add(panel, 0, 1);
        mainLayout.Controls.Add(btnConvert, 0, 2);
        mainLayout.Controls.Add(lblDuration, 0, 3); // *** Süreyi göster

        Controls.Add(mainLayout);
    }

    private void AddHourButton(int index, int hourValue, bool isFirst, bool isFull)
    {
        string text = isFull ? "Tamamı" : hourValue.ToString();
        Button btn = new Button()
        {
            Text = text,
            Width = isFull ? 80 : 60,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            BackColor = Color.LightGray,
            ForeColor = Color.Black,
            Tag = index,
            Margin = new Padding(0)
        };

        btn.FlatAppearance.BorderSize = 0;
        btn.MouseEnter += (s, e) => HighlightUntilIndex((int)btn.Tag);
        btn.MouseLeave += (s, e) => UpdateSelectionColors();
        btn.Click += (s, e) =>
        {
            selectedHourIndex = (int)btn.Tag;
            UpdateSelectionColors();
        };

        btn.Paint += (s, e) => PaintModernButton((Button)s, e, index == 0, index == hourButtons.Length - 1);

        hourButtons[index] = btn;
        panel.Controls.Add(btn);
    }

    private void HighlightUntilIndex(int idx)
    {
        for (int i = 0; i < hourButtons.Length; i++)
        {
            if (i <= idx)
                hourButtons[i].BackColor = Color.Gray;
            else
                hourButtons[i].BackColor = Color.LightGray;
        }
    }

    private void UpdateSelectionColors()
    {
        for (int i = 0; i < hourButtons.Length; i++)
        {
            hourButtons[i].BackColor = (selectedHourIndex >= 0 && i <= selectedHourIndex)
                ? Color.Gray
                : Color.LightGray;
        }
    }

    private void PaintModernButton(Button btn, PaintEventArgs e, bool leftRound, bool rightRound)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = btn.ClientRectangle;
        int radius = 12;

        using (GraphicsPath path = new GraphicsPath())
        {
            if (leftRound && rightRound)
            {
                path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
                path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
                path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
            }
            else if (leftRound)
            {
                path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                path.AddLine(rect.Right, rect.Y, rect.Right, rect.Bottom);
                path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
            }
            else if (rightRound)
            {
                path.AddLine(rect.X, rect.Y, rect.Right - radius, rect.Y);
                path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
                path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
                path.AddLine(rect.X, rect.Bottom, rect.X, rect.Y);
            }
            else
            {
                path.AddRectangle(rect);
            }

            btn.Region = new Region(path);

            using (LinearGradientBrush lgb = new LinearGradientBrush(rect, btn.BackColor, ControlPaint.Light(btn.BackColor), LinearGradientMode.Vertical))
            {
                g.FillPath(lgb, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                btn.Text,
                btn.Font,
                btn.ClientRectangle,
                btn.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            );
        }
    }

    private void BtnConvert_Click(object sender, EventArgs e)
    {
        if (selectedHourIndex == -1)
        {
            MessageBox.Show("Lütfen bir süre seçin!");
            return;
        }

        int result;
        if (selectedHourIndex == hourButtons.Length - 1)
        {
            long fileSize = new FileInfo("output.bin").Length;
            result = Program.ConvertBinToWav("output.bin", "output_full.wav", fileSize);
            lblDuration.Text = result > 0 ? $"İşlem süresi: {result / 1000.0:F2} saniye" : "Hata oluştu!";
        }
        else
        {
            int selectedHour = hours[selectedHourIndex];
            long byteCount = (long)selectedHour * 3600 * Program.SampleRate * Program.Channels * (Program.BitsPerSample / 8);
            result = Program.ConvertBinToWav("output.bin", $"output_{selectedHour}h.wav", byteCount);
            lblDuration.Text = result > 0 ? $"İşlem süresi: {result / 1000.0:F2} saniye" : "Hata oluştu!";
        }
    }
}