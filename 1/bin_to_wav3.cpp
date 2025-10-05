#include <iostream>
#include <vector>
#include <windows.h>

void write_wav_header_to_buffer(std::vector<char> &buffer, int sample_rate, int channels, int total_samples) {
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

int main() {
    const char* binFile = "output.bin";
    const char* wavFile = "output.wav";

    std::cout << "[INFO] BIN dosyasi aciliyor...\n";
    HANDLE hBin = CreateFileA(binFile, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hBin == INVALID_HANDLE_VALUE) {
        std::cerr << "BIN dosyasi acilamadi.\n";
        return 1;
    }

    LARGE_INTEGER fileSize;
    if (!GetFileSizeEx(hBin, &fileSize)) {
        std::cerr << "BIN boyutu alinamadi.\n";
        CloseHandle(hBin);
        return 1;
    }
    size_t total_size = static_cast<size_t>(fileSize.QuadPart);
    double total_size_mb = total_size / (1024.0 * 1024.0);
    std::cout << "[INFO] BIN dosyasi boyutu: " << total_size_mb << " MB\n";

    // Input memory-map
    std::cout << "[INFO] CreateFileMapping ile input mmap yapiliyor...\n";
    HANDLE hMapIn = CreateFileMappingA(hBin, NULL, PAGE_READONLY, 0, 0, NULL);
    if (!hMapIn) {
        std::cerr << "CreateFileMapping (input) hatasi.\n";
        CloseHandle(hBin);
        return 1;
    }
    char* pcm_data = (char*)MapViewOfFile(hMapIn, FILE_MAP_READ, 0, 0, 0);
    if (!pcm_data) {
        std::cerr << "MapViewOfFile (input) hatasi.\n";
        CloseHandle(hMapIn);
        CloseHandle(hBin);
        return 1;
    }

    // WAV dosyası oluştur
    HANDLE hWav = CreateFileA(wavFile, GENERIC_READ | GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hWav == INVALID_HANDLE_VALUE) {
        std::cerr << "WAV dosyasi acilamadi.\n";
        UnmapViewOfFile(pcm_data);
        CloseHandle(hMapIn);
        CloseHandle(hBin);
        return 1;
    }

    // WAV header oluştur
    std::vector<char> headerBuffer;
    int sample_rate = 44100;
    int channels = 1;
    int total_samples = total_size;
    write_wav_header_to_buffer(headerBuffer, sample_rate, channels, total_samples);
    size_t wav_total_size = headerBuffer.size() + total_size;

    // Output dosyasının boyutunu belirle
    LARGE_INTEGER newSize;
    newSize.QuadPart = wav_total_size;
    SetFilePointerEx(hWav, newSize, NULL, FILE_BEGIN);
    SetEndOfFile(hWav);

    // Output memory-map
    std::cout << "[INFO] CreateFileMapping ile output mmap yapiliyor...\n";
    HANDLE hMapOut = CreateFileMappingA(hWav, NULL, PAGE_READWRITE, 0, 0, NULL);
    if (!hMapOut) {
        std::cerr << "CreateFileMapping (output) hatasi.\n";
        UnmapViewOfFile(pcm_data);
        CloseHandle(hMapIn);
        CloseHandle(hBin);
        CloseHandle(hWav);
        return 1;
    }
    char* wav_data = (char*)MapViewOfFile(hMapOut, FILE_MAP_WRITE, 0, 0, 0);
    if (!wav_data) {
        std::cerr << "MapViewOfFile (output) hatasi.\n";
        CloseHandle(hMapOut);
        UnmapViewOfFile(pcm_data);
        CloseHandle(hMapIn);
        CloseHandle(hBin);
        CloseHandle(hWav);
        return 1;
    }

    // Zaman ölçümünü başlat
    LARGE_INTEGER freq, start, end;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start);

    // Header + PCM verisini kopyala
    memcpy(wav_data, headerBuffer.data(), headerBuffer.size());
    memcpy(wav_data + headerBuffer.size(), pcm_data, total_size);

    // Zaman ölçümünü bitir
    QueryPerformanceCounter(&end);
    double elapsed_sec = static_cast<double>(end.QuadPart - start.QuadPart) / freq.QuadPart;

    // Temizlik
    //FlushViewOfFile(wav_data, 0);
    UnmapViewOfFile(wav_data);
    CloseHandle(hMapOut);
    UnmapViewOfFile(pcm_data);
    CloseHandle(hMapIn);
    CloseHandle(hBin);
    CloseHandle(hWav);

    double speed = total_size_mb / elapsed_sec;
    std::cout << "[INFO] BIN -> WAV (Memory-Mapped) donusumu tamamlandi.\n";
    std::cout << "[INFO] Gecen sure: " << elapsed_sec << " saniye\n";
    std::cout << "[INFO] Ortalama hiz: " << speed << " MB/s\n";

    return 0;
}
