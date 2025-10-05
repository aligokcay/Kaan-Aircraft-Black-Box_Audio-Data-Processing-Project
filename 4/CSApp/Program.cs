using System;
using System.IO; // ***
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks; // ***
using System.Collections.Concurrent; // *
using NetMQ; // ***
using NetMQ.Sockets; // ***
using System.Diagnostics;
using System.Security.Cryptography; // *
using Google.Protobuf; // *

class Program
{
    [DllImport(@"C:\Users\Mehmet Ali\Desktop\ctech\3\DLL\AudioConverter.dll",
    EntryPoint = "ConvertBinToWav",
    CallingConvention = CallingConvention.Cdecl,
    CharSet = CharSet.Ansi)]
    public static extern int ConvertBinToWav(string binFile, string wavFile, long byteCount);

    public const int SampleRate = 44100; // 44.1 kHz
    public const int Channels = 1;       // Mono
    public const int BitsPerSample = 8;  // 8-bit PCM

    public static int TotalHours { get; set; }

    [STAThread]
    static void Main()
    {
        string binFile = "output.bin";

        if (!File.Exists(binFile)) // ***
        {
            Console.WriteLine("BIN dosyasi bulunamadi, ZeroMQ uzerinden alinabilir."); // ***
            ApplicationConfiguration.Initialize(); // ***
            Application.Run(new Form1(0)); // ***
            return; // ***
        }

        long fileSize = new FileInfo(binFile).Length;

        double totalSeconds = fileSize / (double)(SampleRate * Channels * (BitsPerSample / 8));
        TimeSpan duration = TimeSpan.FromSeconds(totalSeconds);
        TotalHours = (int)Math.Ceiling(duration.TotalHours - 0.0001);

        Console.WriteLine($"BIN dosyasinin suresi: {(int)duration.TotalHours} saat {duration.Minutes} dakika {duration.Seconds} saniye");

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
    private Button btnFetchBin; // ***
    private ProgressBar progressBar; // ***
    private Label lblQuestion;
    private Label lblDuration;
    private Label lblFetchStatus; // ***

    private string binFile = "output.bin"; // ***

    public Form1(int totalHours)
    {
        this.Size = new Size(800, 500);
        this.Text = "BIN -> WAV Donusturucu";
        this.BackColor = Color.White;

        btnFetchBin = new Button() // ***
        {
            Text = "BIN Dosyasini Al (C'den)", // ***
            Width = 180, // ***
            Height = 40, // ***
            BackColor = Color.Gray, // ***
            ForeColor = Color.White, // ***
            FlatStyle = FlatStyle.Flat, // ***
            Font = new Font("Segoe UI", 10, FontStyle.Bold), // ***
            Anchor = AnchorStyles.None // ***
        };

        btnFetchBin.FlatAppearance.BorderSize = 0; // ***
        btnFetchBin.Padding = new Padding(0);
        btnFetchBin.TextAlign = ContentAlignment.MiddleCenter;
        btnFetchBin.MouseEnter += (s, e) => btnFetchBin.BackColor = Color.DarkGray;
        btnFetchBin.MouseLeave += (s, e) => btnFetchBin.BackColor = Color.Gray;
        btnFetchBin.Click += BtnFetchBin_Click; // ***

        lblFetchStatus = new Label() // ***
        {
            Text = File.Exists(binFile) ? "Mevcut BIN bulundu." : "BIN dosyasi yok.", // ***
            Font = new Font("Segoe UI", 10, FontStyle.Regular), // ***
            AutoSize = true, // ***
            Anchor = AnchorStyles.None, // ***
            TextAlign = ContentAlignment.MiddleCenter // ***
        };

        progressBar = new ProgressBar()
        {
            Dock = DockStyle.Fill,
            Height = 25,
            Minimum = 0,
            Maximum = 100,
            Value = 0
        };

        lblQuestion = new Label()
        {
            Text = "Ilk kac saatini donusturmek istiyorsunuz?",
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
            Text = "Donustur",
            Width = 140,
            Height = 40,
            BackColor = Color.Gray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Anchor = AnchorStyles.None,
            Enabled = totalHours > 0 // *** BIN varsa aktif baslat
        };
        btnConvert.FlatAppearance.BorderSize = 0;
        btnConvert.MouseEnter += (s, e) => btnConvert.BackColor = Color.DarkGray;
        btnConvert.MouseLeave += (s, e) => btnConvert.BackColor = Color.Gray;
        btnConvert.Click += BtnConvert_Click;

        lblDuration = new Label()
        {
            Text = "",
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            AutoSize = true,
            Anchor = AnchorStyles.None,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Black,
            Margin = new Padding(0, 10, 0, 0)
        };

        if (totalHours > 0) // ***
        {
            Program.TotalHours = totalHours; // *****
            PrepareHourButtons(); // *****
        }

        TableLayoutPanel mainLayout = new TableLayoutPanel()
        {
            Dock = DockStyle.Fill,
            RowCount = 7,  // ***
            ColumnCount = 1,
            BackColor = Color.White
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10)); // ***
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 15)); // ***
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // ***
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 5));

        mainLayout.Controls.Add(lblFetchStatus, 0, 0); // ***
        mainLayout.Controls.Add(btnFetchBin, 0, 1); // ***
        mainLayout.Controls.Add(progressBar, 0, 2);
        mainLayout.Controls.Add(lblQuestion, 0, 3);
        mainLayout.Controls.Add(panel, 0, 4);
        mainLayout.Controls.Add(btnConvert, 0, 5);
        mainLayout.Controls.Add(lblDuration,0, 6); // ***

        Controls.Add(mainLayout);
    }

    private void PrepareHourButtons() // *****
    {
        panel.Controls.Clear(); // *****
        selectedHourIndex = -1;
        var list = new System.Collections.Generic.List<int>(); // *****
        for (int i = 2; i < Program.TotalHours; i += 2) // *****
            list.Add(i); // *****
        hours = list.ToArray(); // *****
        hourButtons = new Button[hours.Length + 1]; // *****
        for (int i = 0; i < hours.Length; i++) // *****
            AddHourButton(i, hours[i], i == 0, false); // *****
        AddHourButton(hours.Length, 0, hours.Length == 0, true); // *****
    }

    private void BtnFetchBin_Click(object sender, EventArgs e) // ***
    {
        lblFetchStatus.Text = "Dosya aliniyor..."; // ***
        btnFetchBin.Enabled = false; // ***
        lblDuration.Text = "";

        Task.Run(() => // ***
        {
            try
            {
                StartReceivingBin(); // ***
                Invoke(new Action(() =>
                {
                    lblFetchStatus.Text = "Dosya alindi: output.bin"; // ***
                    Invoke(new Action(() => progressBar.Value = 0));
                    btnConvert.Enabled = true; // ***

                    long fileSize = new FileInfo(binFile).Length; // *****
                    double totalSeconds = fileSize / (double)(Program.SampleRate * Program.Channels * (Program.BitsPerSample / 8)); // *****
                    Program.TotalHours = (int)Math.Ceiling(totalSeconds / 3600.0 - 0.0001); // *****
                    PrepareHourButtons(); // *****
                }));
            }
            catch (Exception ex)
            {
                Invoke(new Action(() => lblFetchStatus.Text = "Hata: " + ex.Message)); // ***
            }
            finally
            {
                Invoke(new Action(() => btnFetchBin.Enabled = true)); // ***
            }
        });
    }

    private async Task StartReceivingBin()
    {
        string? expectedFinalMd5 = null;
        using (var receiver = new PullSocket(">tcp://localhost:7500"))
        using (FileStream fs = new FileStream(binFile, FileMode.Create, FileAccess.Write, FileShare.None, 2 * 1024 * 1024, useAsync: true))
        using (BlockingCollection<MemoryStream> writeQueue = new BlockingCollection<MemoryStream>(boundedCapacity: 8))
        {
            long totalBytes = 0;
            long estimatedTotal = 1400L * 1024 * 1024;
            Stopwatch stopwatch = Stopwatch.StartNew();

            // ✅ Async writer task
            var writerTask = Task.Run(async () =>
            {
                foreach (var buffer in writeQueue.GetConsumingEnumerable())
                {
                    await fs.WriteAsync(buffer.GetBuffer(), 0, (int)buffer.Length);
                }
            });

            MemoryStream activeBuffer = new MemoryStream();

            while (true)
            {
                byte[] received = receiver.ReceiveFrameBytes();
                if (received.Length < 16) continue;

                UploadConfig uploadConfig = UploadConfig.Parser.ParseFrom(received);

                if (uploadConfig.ChunkIndex == uint.MaxValue)
                {
                    if (activeBuffer.Length > 0)
                        writeQueue.Add(activeBuffer);

                    writeQueue.CompleteAdding();
                    await writerTask;

                    expectedFinalMd5 = uploadConfig.ChunkMd5;
                    break;
                }

                byte[] chunkData = uploadConfig.ChunkData.ToByteArray();

                // ✅ MD5 doğrulama
                using (var md5 = MD5.Create())
                {
                    var computed = md5.ComputeHash(chunkData);
                    string computedHex = BitConverter.ToString(computed).Replace("-", "").ToLowerInvariant();
                    if (computedHex != uploadConfig.ChunkMd5.ToLowerInvariant())
                        Console.WriteLine($"[UYARI] Chunk {uploadConfig.ChunkIndex} MD5 eslesmedi!");
                }

                activeBuffer.Write(chunkData, 0, chunkData.Length);
                totalBytes += chunkData.Length;

                if (activeBuffer.Length >= 2 * 1024 * 1024)
                {
                    var fullBuffer = activeBuffer;
                    activeBuffer = new MemoryStream();
                    writeQueue.Add(fullBuffer);
                }

                Invoke(new Action(() =>
                {
                    int percent = (int)Math.Min((totalBytes * 100) / estimatedTotal, 100);
                    progressBar.Value = percent;
                }));
            }

            stopwatch.Stop();
            Console.WriteLine($"Transfer tamamlandi. Toplam boyut: {totalBytes / (1024 * 1024)} MB, Sure: {stopwatch.Elapsed.TotalSeconds:F2} sn");
        }

        // ✅ MD5 final kontrol
        if (!string.IsNullOrWhiteSpace(expectedFinalMd5))
        {
            using var verify = new FileStream(binFile, FileMode.Open, FileAccess.Read);
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(verify);
            string actualMd5 = BitConverter.ToString(hash).Replace("-", "").ToLower();

            if (actualMd5 == expectedFinalMd5.ToLower())
                Console.WriteLine("[✓] Tum dosya MD5 dogrulandi.");
            else
                Console.WriteLine($"[X] MD5 hatasi! Beklenen: {expectedFinalMd5}, Hesaplanan: {actualMd5}");
        }
    }

    private void AddHourButton(int index, int hourValue, bool isFirst, bool isFull)
    {
        string text = isFull ? "Tamami" : hourValue.ToString();
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
            MessageBox.Show("Lutfen bir sure secin!");
            return;
        }

        int result;
        if (selectedHourIndex == hourButtons.Length - 1)
        {
            long fileSize = new FileInfo(binFile).Length; // ***
            result = Program.ConvertBinToWav(binFile, "output_full.wav", fileSize); // ***
            lblDuration.Text = result > 0 ? $"Islem suresi: {result / 1000.0:F2} saniye" : "Hata olustu!";
        }
        else
        {
            int selectedHour = hours[selectedHourIndex];
            long byteCount = (long)selectedHour * 3600 * Program.SampleRate * Program.Channels * (Program.BitsPerSample / 8);
            result = Program.ConvertBinToWav(binFile, $"output_{selectedHour}h.wav", byteCount); // ***
            lblDuration.Text = result > 0 ? $"Islem suresi: {result / 1000.0:F2} saniye" : "Hata olustu!";
        }
    }
}



    