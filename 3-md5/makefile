CC = gcc
CFLAGS = -Wall -O2 -Wno-deprecated-declarations -I "C:/Users/Mehmet Ali/vcpkg/installed/x64-mingw-dynamic/include"
LDFLAGS = -L "C:/Users/Mehmet Ali/vcpkg/installed/x64-mingw-dynamic/lib" -lssl -lcrypto -lzmq -lprotobuf-c
TARGET = hello_chunk.exe
OBJS = main.o ipc.pb-c.o

all: $(TARGET)

$(TARGET): $(OBJS)
	$(CC) $(OBJS) -o $(TARGET) $(LDFLAGS)

main.o: main.c
	$(CC) $(CFLAGS) -c main.c

ipc.pb-c.o: ipc.pb-c.c ipc.pb-c.h
	$(CC) $(CFLAGS) -c ipc.pb-c.c

clean:
	del $(OBJS) $(TARGET)

re: clean all

.PHONY: all clean re
