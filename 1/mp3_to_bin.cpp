#include <iostream>
#include <fstream>
#include <vector>
#include <cstring>

#define MINIMP3_IMPLEMENTATION
#include "minimp3.h"

int main() {
    const char* inputFile = "input.mp3";
    const char* outputFile = "output.bin";

    std::ifstream inFile(inputFile, std::ios::binary | std::ios::ate);
    if (!inFile) {
        std::cerr << "MP3 dosyasi acilamadi: " << inputFile << std::endl;
        return 1;
    }

    std::streamsize size = inFile.tellg();
    inFile.seekg(0, std::ios::beg);

    std::vector<unsigned char> mp3_data(size);
    if (!inFile.read(reinterpret_cast<char*>(mp3_data.data()), size)) {
        std::cerr << "MP3 dosyasi okunurken hata olustu." << std::endl;
        return 1;
    }
    inFile.close();

    mp3dec_t mp3d;
    mp3dec_init(&mp3d);

    mp3dec_frame_info_t info;
    size_t pos = 0;
    short pcm[MINIMP3_MAX_SAMPLES_PER_FRAME];

    std::ofstream outFile(outputFile, std::ios::binary);
    if (!outFile) {
        std::cerr << "BIN dosyasi acilamadi: " << outputFile << std::endl;
        return 1;
    }

    while (pos < mp3_data.size()) {
        int samples = mp3dec_decode_frame(&mp3d,
                                          mp3_data.data() + pos,
                                          mp3_data.size() - pos,
                                          pcm,
                                          &info);

        if (info.frame_bytes == 0) {
            pos++;
            continue;
        }

        pos += info.frame_bytes;

        if (samples > 0) {
            for (int i = 0; i < samples; i += info.channels) {
                int16_t left = pcm[i];
                int16_t right = (info.channels == 2) ? pcm[i + 1] : left;
                int16_t mono = (left + right) / 2;
                unsigned char pcm8 = static_cast<unsigned char>((mono + 32768) >> 8);
                outFile.write(reinterpret_cast<const char*>(&pcm8), 1);
            }
        }
    }

    outFile.close();
    std::cout << "MP3 -> BIN (8-bit PCM) donusturme tamamlandi: " << outputFile << std::endl;
    return 0;
}
