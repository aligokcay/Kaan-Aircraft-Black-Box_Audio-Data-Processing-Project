CC = gcc
CFLAGS = -Wall -O2 -I "C:/Users/Mehmet Ali/vcpkg/installed/x64-mingw-dynamic/include"
LDFLAGS = -L "C:/Users/Mehmet Ali/vcpkg/installed/x64-mingw-dynamic/lib" -lzmq
TARGET = hello_chunk.exe
OBJS = main.o xxhash.o

all: $(TARGET)

$(TARGET): $(OBJS)
	$(CC) $(OBJS) -o $(TARGET) $(LDFLAGS)

main.o: main.c
	$(CC) $(CFLAGS) -c main.c

xxhash.o: xxhash.c
	$(CC) $(CFLAGS) -c xxhash.c

clean:
	del $(OBJS) $(TARGET)

re: clean all

.PHONY: all clean re