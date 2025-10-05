#pragma once

#ifdef AUDIOCONVERTER_EXPORTS
#define AUDIO_API __declspec(dllexport)
#else
#define AUDIO_API __declspec(dllimport)
#endif

extern "C" AUDIO_API int ConvertBinToWav(const char* binFile, const char* wavFile, long byteCount);
