#include "pch.h"
#include "plugin.h"

AVIOContext* InitStreamContext(ExclusiveByteStream^ Context) {
	gcroot<ExclusiveByteStream^>* context = new gcroot<ExclusiveByteStream^>(Context);
	return avio_alloc_context(NULL, 0, 0, context, &read_packet, NULL, NULL);
}

void CloseStreamContext(AVIOContext* Context) {
	gcroot<ExclusiveByteStream^>* stream = (gcroot<ExclusiveByteStream^>*)Context->opaque;
	(*stream)->Finish();
	delete stream;
	av_free(Context);
}

int read_packet(void* opaque, uint8_t* buf, int buf_size) {
	ExclusiveByteStream^ stream = *(gcroot<ExclusiveByteStream^>*)opaque;
	return stream->Object->Read((IntPtr)buf, buf_size);
}