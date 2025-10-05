using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using K4os.Hash.xxHash;

class Program
{
    static async Task Main()
    {
        using (var receiver = new PullSocket(">tcp://localhost:7500"))
        using (FileStream fs = new FileStream("output.bin", FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            Console.WriteLine("C tarafindan veri bekleniyor...");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            int totalChunks = 0;
            long totalBytes = 0;

            while (true)
            {
                byte[] received = receiver.ReceiveFrameBytes();
                if (received.Length < 16)
                {
                    Console.WriteLine("Gecersiz paket boyutu: " + received.Length);
                    continue;
                }

                int offset = 0;
                int chunkIndex = BitConverter.ToInt32(received, offset);
                offset += sizeof(int);

                long dataLength = BitConverter.ToInt64(received, offset);
                offset += sizeof(long);

                ulong hash = BitConverter.ToUInt64(received, offset);
                offset += sizeof(ulong);

                // Transfer bitti sinyali
                if (chunkIndex == -1)
                {
                    stopwatch.Stop();
                    Console.WriteLine($"Transfer tamamlandi. Toplam {totalChunks} chunk, {totalBytes / (1024 * 1024)} megabyte. Sure: {stopwatch.Elapsed.TotalSeconds:F2} sn");
                    break;
                }

                byte[] chunkData = new byte[dataLength];
                Array.Copy(received, offset, chunkData, 0, (int)dataLength);

                // Hash dogrulama
                ulong computed = XXH64.DigestOf(chunkData);
                if (computed != hash)
                    Console.WriteLine($"[UYARI] Chunk {chunkIndex} hash uyusmadi!");

                await fs.WriteAsync(chunkData, 0, (int)dataLength);
                totalChunks++;
                totalBytes += dataLength;

                // if (chunkIndex % 32 == 0)
                // {
                //     Console.WriteLine($"Chunk {chunkIndex} yazildi. Toplam: {totalBytes/(1024*1024)} megabyte.");
                // }
                Console.WriteLine($"Chunk {chunkIndex} yazildi. Toplam: {totalBytes/(1024*1024)} megabyte.");
            }
        }
    }
}
