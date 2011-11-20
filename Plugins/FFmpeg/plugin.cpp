#include "pch.h"
#include "plugin.h"

AVIOContext* InitStreamContext(ByteStream^ Context) {
	int buffersize = 65536 + FF_INPUT_BUFFER_PADDING_SIZE;
	unsigned char* buf = (unsigned char*)av_malloc(buffersize);
	gcroot<ByteStream^>* context = new gcroot<ByteStream^>(Context);
	return avio_alloc_context(buf, buffersize, 0, context, &read_packet, NULL, NULL);
}

void CloseStreamContext(AVIOContext* Context) {
	delete (gcroot<ByteStream^>*)Context->opaque;
	av_free(Context->buffer);
	av_free(Context);
}

int read_packet(void* opaque, uint8_t* buf, int buf_size) {
	ByteStream^ stream = *(gcroot<ByteStream^>*)opaque;
	return stream->Read((IntPtr)buf, buf_size);
}