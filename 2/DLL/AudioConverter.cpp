#include "AudioConverter.h"
#include <iostream>
#include <fstream>
#include <vector>
#include <windows.h>

const size_t BUFFER_SIZE = 256 * 1024ULL * 1024ULL; // 256 MB buffer

// WAV başlığı yazma fonksiyonu
void write_wav_header_to_buffer(std::vector<char> &buffer, int sample_rate, int channels, int total_samples)
{
    buffer.resize(512, 0); 
    int bits_per_sample = 8;
    int byte_rate = sample_rate * channels * (bits_per_sample / 8);
    int block_align = channels * (bits_per_sample / 8);
    int data_chunk_size = total_samples * channels * (bits_per_sample / 8);
    int riff_chunk_size = 36 + data_chunk_size;

    memcpy(buffer.data() + 0, "RIFF", 4);
    memcpy(buffer.data() + 4, &riff_chunk_size, 4);
    memcpy(buffer.data() + 8, "WAVE", 4);

    memcpy(buffer.data() + 12, "fmt ", 4);
    int subchunk1_size = 16;
    short audio_format = 1;
    memcpy(buffer.data() + 16, &subchunk1_size, 4);
    memcpy(buffer.data() + 20, &audio_format, 2);
    memcpy(buffer.data() + 22, &channels, 2);
    memcpy(buffer.data() + 24, &sample_rate, 4);
    memcpy(buffer.data() + 28, &byte_rate, 4);
    memcpy(buffer.data() + 32, &block_align, 2);
    memcpy(buffer.data() + 34, &bits_per_sample, 2);

    memcpy(buffer.data() + 36, "data", 4);
    memcpy(buffer.data() + 40, &data_chunk_size, 4);
}

extern "C" int ConvertBinToWav(const char* binFile, const char* wavFile, long byteCount)
{
    std::cout << "[INFO] BIN dosyası açılıyor...\n";
    HANDLE hBin = CreateFileA(binFile, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hBin == INVALID_HANDLE_VALUE) {
        std::cerr << "BIN dosyası açılamadı.\n";
        return 1;
    }

    LARGE_INTEGER fileSize;
    if (!GetFileSizeEx(hBin, &fileSize)) {
        std::cerr << "BIN boyutu alınamadı.\n";
        CloseHandle(hBin);
        return 1;
    }

    // ByteCount, dosya boyutunu aşarsa tamamını al
    long total_size = (byteCount > 0 && byteCount < fileSize.QuadPart) 
                            ? byteCount : fileSize.QuadPart;

    double total_size_mb = total_size / (1024.0 * 1024);
    std::cout << "[INFO] İşlenecek boyut: " << total_size_mb << " MB\n";

    HANDLE hMap = CreateFileMappingA(hBin, NULL, PAGE_READONLY, 0, 0, NULL);
    if (!hMap) {
        std::cerr << "CreateFileMapping hatası.\n";
        CloseHandle(hBin);
        return 1;
    }

    char* pcm_data = (char*)MapViewOfFile(hMap, FILE_MAP_READ, 0, 0, 0);
    if (!pcm_data) {
        std::cerr << "MapViewOfFile hatası.\n";
        CloseHandle(hMap);
        CloseHandle(hBin);
        return 1;
    }

    std::cout << "[INFO] WAV dosyası oluşturuluyor...\n";
    HANDLE hWav = CreateFileA(wavFile, GENERIC_WRITE | GENERIC_READ, 0, NULL,
                              CREATE_ALWAYS, FILE_FLAG_NO_BUFFERING | FILE_FLAG_SEQUENTIAL_SCAN, NULL);
    if (hWav == INVALID_HANDLE_VALUE) {
        std::cerr << "WAV dosyası açılamadı.\n";
        UnmapViewOfFile(pcm_data);
        CloseHandle(hMap);
        CloseHandle(hBin);
        return 1;
    }

    // WAV header
    {
        std::vector<char> headerBuffer;
        int sample_rate = 44100;
        int channels = 1;
        int total_samples = static_cast<int>(total_size); // 8-bit PCM, 1 sample = 1 byte
        write_wav_header_to_buffer(headerBuffer, sample_rate, channels, total_samples);
        DWORD writtenHeader = 0;
        WriteFile(hWav, headerBuffer.data(), (DWORD)headerBuffer.size(), &writtenHeader, NULL);
        std::cout << "[INFO] WAV header yazıldı.\n";
    }

    LARGE_INTEGER freq, start, end;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start);

    std::vector<char> bufferA(BUFFER_SIZE);
    std::vector<char> bufferB(BUFFER_SIZE);

    long remaining = total_size;
    size_t pos = 0;
    DWORD written = 0;

    // İlk chunk
    size_t chunk = (remaining > BUFFER_SIZE) ? BUFFER_SIZE : (size_t)remaining;
    memcpy(bufferA.data(), pcm_data + pos, chunk);
    WriteFile(hWav, bufferA.data(), (DWORD)chunk, &written, NULL);
    pos += chunk;
    remaining -= chunk;
    bool toggle = true;

    std::cout << "[INFO] " << pos / (1024 * 1024) << " MB yazıldı.\n";

    while (remaining > 0) {
        chunk = (remaining > BUFFER_SIZE) ? BUFFER_SIZE : (size_t)remaining;

        if (toggle) {
            memcpy(bufferB.data(), pcm_data + pos, chunk);
            WriteFile(hWav, bufferA.data(), (DWORD)chunk, &written, NULL);
        } else {
            memcpy(bufferA.data(), pcm_data + pos, chunk);
            WriteFile(hWav, bufferB.data(), (DWORD)chunk, &written, NULL);
        }

        pos += chunk;
        remaining -= chunk;
        toggle = !toggle;

        double written_mb = pos / (1024.0 * 1024);
        std::cout << "[INFO] " << written_mb << " MB yazıldı.\n";
    }

    // Son padding
    size_t final_padded = ((chunk + 511) / 512) * 512;
    if (final_padded > chunk) {
        if (toggle) memset(bufferB.data() + chunk, 0, final_padded - chunk);
        else        memset(bufferA.data() + chunk, 0, final_padded - chunk);
    }

    if (toggle)
        WriteFile(hWav, bufferB.data(), (DWORD)final_padded, &written, NULL);
    else
        WriteFile(hWav, bufferA.data(), (DWORD)final_padded, &written, NULL);

    QueryPerformanceCounter(&end);
    double elapsed_sec = static_cast<double>(end.QuadPart - start.QuadPart) / freq.QuadPart;
    int elapsed_ms = static_cast<int>(elapsed_sec * 1000.0);

    UnmapViewOfFile(pcm_data);
    CloseHandle(hMap);
    CloseHandle(hBin);
    CloseHandle(hWav);

    double speed = total_size_mb / elapsed_sec;
    std::cout << "[INFO] BIN -> WAV dönüşümü tamamlandı.\n";
    std::cout << "[INFO] Geçen süre: " << elapsed_sec << " saniye\n";
    std::cout << "[INFO] Ortalama hız: " << speed << " MB/s\n";

    return elapsed_ms;
}
