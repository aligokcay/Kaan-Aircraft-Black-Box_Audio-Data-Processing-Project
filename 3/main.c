#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <zmq.h>
#include "xxhash.h"

#define CHUNK_SIZE 2097152 // 4194304 // 1048576 // 8388608

#pragma pack(push, 1)
typedef struct {
    int32_t chunkIndex;       // 4 byte
    uint64_t dataLength;      // 8 byte
    uint64_t hash;            // 8 byte
    unsigned char data[];     // Flexible array (boyut malloc ile belirlenecek)
} ChunkPacket;
#pragma pack(pop)

int main() {
    printf("Program basladi...\n");
    fflush(stdout);

    void *context = zmq_ctx_new();
    void *sender = zmq_socket(context, ZMQ_PUSH);
    int rc = zmq_bind(sender, "tcp://*:7500");
    if (rc != 0) {
        printf("ZeroMQ bind hatasi: %s\n", zmq_strerror(zmq_errno()));
        return 1;
    }
    printf("ZeroMQ baglandi.\n");
    fflush(stdout);

    FILE *file = fopen("bigfile2.bin", "rb");
    if (!file) {
        perror("Dosya acilamadi");
        return 1;
    }
    printf("Dosya acildi.\n");
    fflush(stdout);

    // ChunkPacket + data için heap bellek ayır
    size_t headerSize = sizeof(int32_t) + sizeof(uint64_t) + sizeof(uint64_t);
    ChunkPacket *packet = (ChunkPacket*)malloc(headerSize + CHUNK_SIZE);
    if (!packet) {
        perror("Bellek ayirma hatasi");
        return 1;
    }

    int chunkIndex = 0;
    size_t bytesRead;
    while ((bytesRead = fread(packet->data, 1, CHUNK_SIZE, file)) > 0) {
        //printf("OKUNDU: %zu byte\n", bytesRead);
        packet->chunkIndex = chunkIndex++;
        packet->dataLength = (uint64_t)bytesRead;
        packet->hash = XXH64(packet->data, bytesRead, 0);

        size_t packetSize = headerSize + bytesRead;
        zmq_send(sender, packet, packetSize, 0);

        // printf("Gonderildi: Chunk %d, Boyut %zu, Hash %08x\n",
        //        packet->chunkIndex, bytesRead, packet->hash);
        // fflush(stdout);
    }

    // Transfer bitti sinyali
    packet->chunkIndex = -1;
    packet->dataLength = 0;
    packet->hash = 0;
    zmq_send(sender, packet, headerSize, 0);

    free(packet);
    fclose(file);
    zmq_close(sender);
    zmq_ctx_destroy(context);
    printf("Program bitti.\n");
    fflush(stdout);
    return 0;
}
