#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <zmq.h>
#include <openssl/md5.h>
#include "ipc.pb-c.h" // *

#define CHUNK_SIZE 2097152 // 4194304 // 1048576 // 8388608

// MD5'i 16 byte'lık hash'ten 32 karakterlik hex string'e çevirir
void md5_bytes_to_hex(const unsigned char *md5_bytes, char *output) { 
    for (int i = 0; i < MD5_DIGEST_LENGTH; i++) {
        sprintf(&output[i * 2], "%02x", md5_bytes[i]);
    }
    output[32] = '\0';
}

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
    // size_t headerSize = sizeof(int32_t) + sizeof(uint64_t) + 16;  // *
    // ChunkPacket kullanmıyoruz, buffer kullanıyoruz // *
    unsigned char *buffer = (unsigned char*)malloc(CHUNK_SIZE); // *
    if (!buffer) { // *
        perror("Bellek ayirma hatasi"); // *
        return 1; // *
    } // *

    // Dosya boyutunu hesapla ve totalChunks belirle // *
    fseek(file, 0, SEEK_END); // *
    long totalFileSize = ftell(file); // *
    fseek(file, 0, SEEK_SET); // *
    int totalChunks = (totalFileSize + CHUNK_SIZE - 1) / CHUNK_SIZE; // *

    // Tam dosya MD5 hesaplamak için context
    MD5_CTX full_md5_ctx;
    MD5_Init(&full_md5_ctx);

    int chunkIndex = 0;
    size_t bytesRead;
    while ((bytesRead = fread(buffer, 1, CHUNK_SIZE, file)) > 0) {
        // UploadConfig mesajını doldur // *
        UploadConfig msg = UPLOAD_CONFIG__INIT; // *
        msg.is_chunked = 1; // *
        msg.session_id = "session-123"; // *
        msg.chunk_index = chunkIndex++; // *
        msg.total_chunks = totalChunks; // *
        msg.chunk_data.data = buffer; // *
        msg.chunk_data.len = bytesRead; // *
        msg.chunk_size = (uint32_t)bytesRead; // *
        msg.total_file_size = totalFileSize; // *

        // MD5 hesapla ve chunk_md5 alanına ekle // *
        unsigned char md5_result[MD5_DIGEST_LENGTH]; // *
        MD5(buffer, bytesRead, md5_result); // *
        char md5_str[33]; // *
        md5_bytes_to_hex(md5_result, md5_str); // *
        msg.chunk_md5 = md5_str; // *

        // Genel MD5'e dahil et
        MD5_Update(&full_md5_ctx, buffer, bytesRead);

        // Protobuf serialize // *
        size_t packed_size = upload_config__get_packed_size(&msg); // *
        uint8_t *out = malloc(packed_size); // *
        upload_config__pack(&msg, out); // *

        if (zmq_send(sender, out, packed_size, 0) == -1) { 
            printf("zmq_send hatasi: %s\n", zmq_strerror(zmq_errno()));
            free(out); // *
            break;
        }
        free(out); // *

        // printf("Gonderildi: Chunk %d, Boyut %I64u, MD5: %s\n",
        //        msg.chunk_index,
        //        (unsigned long long)bytesRead,
        //        md5_str);
        // fflush(stdout);
    }
    printf("Chunk aktarma bitti.\n");

    // Tam dosyanın MD5'ini hesapla
    unsigned char final_md5[MD5_DIGEST_LENGTH];
    MD5_Final(final_md5, &full_md5_ctx);
    char final_md5_hex[33];
    md5_bytes_to_hex(final_md5, final_md5_hex);
    printf("Tum dosyanin MD5'i: %s\n", final_md5_hex);

    // Transfer bitti sinyali gönder (ve dosya MD5'sini de ekle)
    UploadConfig end_msg = UPLOAD_CONFIG__INIT;
    end_msg.is_chunked = 1;
    end_msg.session_id = "session-123";
    end_msg.chunk_index = UINT32_MAX;
    end_msg.total_chunks = totalChunks;
    end_msg.chunk_md5 = final_md5_hex; // *

    size_t end_size = upload_config__get_packed_size(&end_msg);
    uint8_t *end_out = malloc(end_size);
    upload_config__pack(&end_msg, end_out);
    zmq_send(sender, end_out, end_size, 0);
    free(end_out);

    free(buffer); // *
    fclose(file);
    zmq_close(sender);
    zmq_ctx_destroy(context);
    printf("Program bitti.\n");
    fflush(stdout);
    return 0;
}
