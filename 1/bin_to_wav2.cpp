#include <iostream>
#include <fstream>
#include <vector>
#include <thread>
#include <mutex>
#include <queue>
#include <condition_variable>
#include <windows.h>

const size_t BUFFER_SIZE = 2 * 1024ULL * 1024ULL; // 2 MB

std::queue<std::vector<char>> writeQueue;
std::mutex queueMutex;
std::condition_variable queueCV;
bool done = false;

void writerThread(HANDLE hWav) {
    while (true) {
        std::vector<char> buffer;
        {
            std::unique_lock<std::mutex> lock(queueMutex);
            queueCV.wait(lock, [] { return !writeQueue.empty() || done; });
            if (writeQueue.empty() && done) break;
            buffer = std::move(writeQueue.front());
            writeQueue.pop();
        }

        DWORD written;
        WriteFile(hWav, buffer.data(), (DWORD)buffer.size(), &written, NULL);
    }
}

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

    HANDLE hBin = CreateFileA(binFile, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hBin == INVALID_HANDLE_VALUE) return 1;

    LARGE_INTEGER fileSize;
    GetFileSizeEx(hBin, &fileSize);
    size_t total_size = static_cast<size_t>(fileSize.QuadPart);

    HANDLE hMap = CreateFileMappingA(hBin, NULL, PAGE_READONLY, 0, 0, NULL);
    if (!hMap) return 1;
    char* pcm_data = (char*)MapViewOfFile(hMap, FILE_MAP_READ, 0, 0, 0);
    if (!pcm_data) return 1;

    HANDLE hWav = CreateFileA(wavFile, GENERIC_WRITE | GENERIC_READ, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hWav == INVALID_HANDLE_VALUE) return 1;

    std::vector<char> headerBuffer;
    int sample_rate = 44100;
    int channels = 1;
    int total_samples = total_size;
    write_wav_header_to_buffer(headerBuffer, sample_rate, channels, total_samples);

    LARGE_INTEGER freq, start, end;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start);

    DWORD writtenHeader;
    WriteFile(hWav, headerBuffer.data(), (DWORD)headerBuffer.size(), &writtenHeader, NULL);

    std::thread writer(writerThread, hWav);

    size_t pos = 0;
    while (pos < total_size) {
        size_t chunk;
        if (BUFFER_SIZE < total_size - pos) {
            chunk = BUFFER_SIZE;
        } else {
            chunk = total_size - pos;
        }
                
        std::vector<char> buffer(chunk);
        memcpy(buffer.data(), pcm_data + pos, chunk);

        {
            std::unique_lock<std::mutex> lock(queueMutex);
            writeQueue.push(std::move(buffer));
        }
        queueCV.notify_one();
        pos += chunk;
        std::cout << "[INFO] " << pos / (1024 * 1024) << " MB kuyruga eklendi.\n";
    }

    {
        std::unique_lock<std::mutex> lock(queueMutex);
        done = true;
    }
    queueCV.notify_one();
    writer.join();

    // Zaman ölçümünü bitir
    QueryPerformanceCounter(&end);
    double elapsed_sec = static_cast<double>(end.QuadPart - start.QuadPart) / freq.QuadPart;

    UnmapViewOfFile(pcm_data);
    CloseHandle(hMap);
    CloseHandle(hBin);
    CloseHandle(hWav);

    std::cout << "[INFO] Donusum tamamlandi.\n";
    std::cout << "[INFO] Gecen sure: " << elapsed_sec << " saniye\n";
    return 0;
}
