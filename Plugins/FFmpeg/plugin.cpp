#include "pch.h"
#include "plugin.h"

AVIOContext* InitStreamContext(ByteStream^ Context, int BufferSize) {
	unsigned char* buf = (unsigned char*)av_malloc(BufferSize);
	gcroot<ByteStream^>* context = new gcroot<ByteStream^>(Context);
	return avio_alloc_context(buf, BufferSize, 0, context, &read_packet, NULL, NULL);
}

void CloseStreamContext(AVIOContext* Context) {
	delete (gcroot<ByteStream^>*)Context->opaque;
	av_free(Context->buffer);
	av_free(Context);
}

int read_packet(void* opaque, uint8_t* buf, int buf_size) {
	ByteStream^ stream = *(gcroot<ByteStream^>*)opaque;
	return stream->Read(buf, buf_size);
}