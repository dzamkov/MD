#include "pch.h"
#include "plugin.h"

AVIOContext* InitStreamContext(ExclusiveByteStream^ Context) {
	int buffersize = 65536 + FF_INPUT_BUFFER_PADDING_SIZE;
	unsigned char* buf = (unsigned char*)av_malloc(buffersize);
	gcroot<ExclusiveByteStream^>* context = new gcroot<ExclusiveByteStream^>(Context);
	return avio_alloc_context(buf, buffersize, 0, context, &read_packet, NULL, NULL);
}

void CloseStreamContext(AVIOContext* Context) {
	gcroot<ExclusiveByteStream^>* stream = (gcroot<ExclusiveByteStream^>*)Context->opaque;
	(*stream)->Finish();
	delete stream;
	av_free(Context->buffer);
	av_free(Context);
}

int read_packet(void* opaque, uint8_t* buf, int buf_size) {
	ExclusiveByteStream^ stream = *(gcroot<ExclusiveByteStream^>*)opaque;
	return stream->Object->Read((IntPtr)buf, buf_size);
}