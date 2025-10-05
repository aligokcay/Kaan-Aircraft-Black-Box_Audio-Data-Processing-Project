using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent; // *
using NetMQ;
using NetMQ.Sockets;
using System.Security.Cryptography; // *
using Google.Protobuf; // *

// class Program
class Program
{
    static async Task Main()
    {
        string? expectedFinalMd5 = null; // *
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        using (var receiver = new PullSocket(">tcp://localhost:7500"))
        using (FileStream fs = new FileStream("output.bin", FileMode.Create, FileAccess.Write, FileShare.None, 2 * 1024 * 1024, useAsync: true))
        using (BlockingCollection<MemoryStream> writeQueue = new BlockingCollection<MemoryStream>(boundedCapacity: 8)) // *
        {
            Console.WriteLine("C tarafindan veri bekleniyor...");
            Console.WriteLine(Environment.ProcessorCount);

            // Kuyruk yazıcısı başlasın // *
            var writerTask = Task.Run(async () =>
            {
                foreach (var buffer in writeQueue.GetConsumingEnumerable())
                {
                    await fs.WriteAsync(buffer.GetBuffer(), 0, (int)buffer.Length);
                }
            });

            int totalChunks = 0;
            long totalBytes = 0;
            MemoryStream activeBuffer = new MemoryStream();

            while (true)
            {
                byte[] received = receiver.ReceiveFrameBytes();
                if (received.Length < 16)
                {
                    Console.WriteLine("Gecersiz paket boyutu: " + received.Length);
                    continue;
                }

                // UploadConfig mesajını parse et // *
                UploadConfig uploadConfig = UploadConfig.Parser.ParseFrom(received); // *

                // Transfer bitti sinyali // *
                if (uploadConfig.ChunkIndex == uint.MaxValue) // *
                {
                    stopwatch.Stop();
                    Console.WriteLine($"Transfer tamamlandi. Toplam {totalChunks} chunk, {totalBytes / (1024 * 1024)} MB. Sure: {stopwatch.Elapsed.TotalSeconds:F2} sn");
                    stopwatch.Start();

                    // Son aktif buffer'ı da kuyruğa gönder
                    if (activeBuffer.Length > 0)
                        writeQueue.Add(activeBuffer);

                    writeQueue.CompleteAdding(); // Kuyruğu kapat
                    await writerTask;            // Tüm yazmalar bitsin

                    expectedFinalMd5 = uploadConfig.ChunkMd5; // *

                    break;
                }

                // Chunk verisini byte dizisine çevir // *
                byte[] chunkData = uploadConfig.ChunkData.ToByteArray(); // *

                // MD5 dogrulama // *
                using (var md5 = MD5.Create())
                {
                    byte[] computedMd5 = md5.ComputeHash(chunkData);
                    string computedHex = BitConverter.ToString(computedMd5).Replace("-", "").ToLower();
                    if (computedHex != uploadConfig.ChunkMd5.ToLower())
                        Console.WriteLine($"[UYARI] Chunk {uploadConfig.ChunkIndex} MD5 eslesmedi!");
                }

                activeBuffer.Write(chunkData, 0, chunkData.Length);

                if (activeBuffer.Length >= 2 * 1024 * 1024) // 256 MB dolduğunda // *
                {
                    var fullBuffer = activeBuffer;
                    activeBuffer = new MemoryStream();
                    writeQueue.Add(fullBuffer); // Kuyruğa ekle
                }

                totalChunks++;
                totalBytes += chunkData.Length;

                // if (uploadConfig.ChunkIndex % 32 == 0) // *
                // {
                //     Console.WriteLine($"Chunk {uploadConfig.ChunkIndex} yazildi. Toplam: {totalBytes / (1024 * 1024)} MB.");
                // }
                Console.WriteLine($"Chunk {uploadConfig.ChunkIndex} yazildi. Toplam: {totalBytes / (1024 * 1024)} MB.");
            }
        }

        // * Final kontrol: Dosyanın tamamının MD5'i doğru mu?
        if (!string.IsNullOrWhiteSpace(expectedFinalMd5))
        {
            using var verifyStream = new FileStream("output.bin", FileMode.Open, FileAccess.Read); // ✅ fs kapandıktan sonra açılıyor
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(verifyStream);
            string actualMd5 = BitConverter.ToString(hash).Replace("-", "").ToLower();

            if (actualMd5 == expectedFinalMd5.ToLower())
                Console.WriteLine("[✓] Tum dosya MD5 dogrulandi.");
            else
                Console.WriteLine($"[X] Tum dosya MD5 eslesmedi! Beklenen: {expectedFinalMd5}, Hesaplanan: {actualMd5}");
        }
        stopwatch.Stop();
        Console.WriteLine($"Transfer tamamlandi. Sure: {stopwatch.Elapsed.TotalSeconds:F2} sn");
    }
}
