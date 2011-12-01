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

_Context::_Context(array<MD::Content^>^ Content) : Context(Content) {
	this->_Packet = NULL;
	this->_Disposed = false;
}

_Context::~_Context() {
	this->!_Context();
}

_Context::!_Context() {
	if (!this->_Disposed) {
		this->_Disposed = true;
		CloseStreamContext(this->_IOContext);
		delete[] this->_StreamContent;
		av_free(this->_Buffer);
		if (this->_Packet != NULL)
		{
			av_free_packet(this->_Packet);
			delete this->_Packet;
		}
	}
}

ExclusiveContext^ _Context::Initialize(AVIOContext* IOContext, AVFormatContext* FormatContext) {

	// Initialize content streams
	List<MD::Content^>^ contents = gcnew List<MD::Content^>(FormatContext->nb_streams);
	int* streamcontent = new int[FormatContext->nb_streams];
	int buffersize = 0;

	for (unsigned int t = 0; t < FormatContext->nb_streams; t++) {
		streamcontent[t] = -1;
		AVCodecContext* codeccontext = FormatContext->streams[t]->codec;
		AVCodec* codec = avcodec_find_decoder(codeccontext->codec_id);
		if (codec != NULL) {
			if (avcodec_open(codeccontext, codec) >= 0) {
				switch (codeccontext->codec_type) {

				// Audio content
				case AVMEDIA_TYPE_AUDIO: {
					AudioFormat format = (AudioFormat)codeccontext->sample_fmt;
					int samplerate = codeccontext->sample_rate;
					int channels = codeccontext->channels;
					int bps = AudioContent::BytesPerSample(format);

					streamcontent[t] = contents->Count;
					contents->Add(gcnew AudioContent(samplerate, channels, format));
					} break;
				default:
					break;
				}
			}
		}
	}

	// Set lower bound on buffer size.
	buffersize = Math::Max(buffersize, AVCODEC_MAX_AUDIO_FRAME_SIZE);

	// Create output context
	_Context^ context = gcnew _Context(contents->ToArray());
	context->_StreamContent = streamcontent;
	context->_IOContext = IOContext;
	context->_FormatContext = FormatContext;
	context->_Buffer = (Byte*)av_malloc(buffersize);
	context->_BufferSize = buffersize;

	return Exclusive::dispose<Context^>(context);
}

bool _Context::NextFrame(int% ContentIndex) {
	if (this->_Packet == NULL)
		this->_Packet = new AVPacket();
	else
		av_free_packet(this->_Packet);
	while (av_read_frame(this->_FormatContext, this->_Packet) >= 0) {
		int streamindex = this->_Packet->stream_index;
		ContentIndex = this->_StreamContent[streamindex];
		if (ContentIndex != -1) {
			MD::Content^ content = this->Content[ContentIndex];
			if (!content->Ignore) {
				AVCodecContext* codeccontext = this->_FormatContext->streams[streamindex]->codec;
				int framesize = this->_BufferSize;

				AudioContent^ audio = dynamic_cast<AudioContent^>(content);
				if (audio != nullptr) {

					// Decode audio
					if (avcodec_decode_audio3(codeccontext, (int16_t*)this->_Buffer, &framesize, this->_Packet) >= 0) {
						audio->Data = FSharpOption<MD::Data<Byte>^>::Some(gcnew UnsafeData<Byte>((IntPtr)this->_Buffer, (IntPtr)(this->_Buffer + framesize)));
						return true;
					}

					continue;
				}
			} else {
				return true;
			}
		}
	}
	return false;
}

FSharpOption<ExclusiveContext^>^ _Container::Decode(ExclusiveByteStream^ Stream) {
	if (this->Input == NULL)
		return FSharpOption<ExclusiveContext^>::None;

	AVIOContext* io = InitStreamContext(Stream);
		
	// Find stream format information
	AVFormatContext* formatcontext = NULL;
	int err = av_open_input_stream(&formatcontext, io, "", this->Input, NULL);
	if (err != 0)
	{
		CloseStreamContext(io);
		return FSharpOption<ExclusiveContext^>::None;
	}

	err = av_find_stream_info(formatcontext);
	if (err < 0)
	{
		av_close_input_stream(formatcontext);
		CloseStreamContext(io);
		return FSharpOption<ExclusiveContext^>::None;
	}

	return FSharpOption<ExclusiveContext^>::Some(_Context::Initialize(io, formatcontext));
}

FSharpOption<ExclusiveByteStream^>^ _Container::Encode(ExclusiveContext^ Context) {
	return FSharpOption<ExclusiveByteStream^>::None;
}

RetractAction^ ::Plugin::Load() {
	if (!Initialized)
	{
		Initialized = true;
		avcodec_init();
		av_register_all();

		// Create containers
		_Containers = gcnew Dictionary<String^, _Container^>();

		// Add input formats
		AVInputFormat* iformat = av_iformat_next(NULL);
		while (iformat != NULL) {
			String^ name = gcnew String(iformat->name);
			_Container^ container = gcnew _Container(name);
			container->Input = iformat;
			_Containers[name] = container;
			iformat = av_iformat_next(iformat);
		}

		// Add output formats
		AVOutputFormat* oformat = av_oformat_next(NULL);
		while (oformat != NULL) {
			String^ name = gcnew String(oformat->name);
			_Container^ container;
			if (!_Containers->TryGetValue(name, container)) {
				container = gcnew _Container(name);
				_Containers->Add(name, container);
			}
			container->Output = oformat;
			oformat = av_oformat_next(oformat);
		}
	}

	RetractAction^ retract = nullptr;

	// Register containers
	for each(_Container^ container in _Containers->Values) {
		retract += MD::Container::Register(container);
	}

	// Register load container
	retract += MD::Container::RegisterLoad(gcnew LoadContainerAction(_LoadContainer));

	return retract;
}

FSharpOption<Tuple<Container^, ExclusiveContext^>^>^ ::Plugin::_LoadContainer(ExclusiveByteData^ Data, String^ Filename) {
	using namespace Runtime::InteropServices;

	AVIOContext* io = InitStreamContext(Data::read(Data->Object));

	// Get file name if possible
	char* filename = NULL;
	if (Filename != nullptr) {
		filename = static_cast<char*>(Marshal::StringToHGlobalAnsi(Filename).ToPointer());
	}

	// Determine format
	AVInputFormat* iformat = NULL;
	int err = av_probe_input_buffer(io, &iformat, filename, NULL, 0, 0);
	if (err != 0)
	{
		CloseStreamContext(io);
		return FSharpOption<Tuple<Container^, ExclusiveContext^>^>::None;
	}

	// Free file name
	if (filename != NULL) {
		Marshal::FreeHGlobal(IntPtr(static_cast<void*>(filename)));
	}
		
	// Find stream format information
	AVFormatContext* formatcontext = NULL;
	err = av_open_input_stream(&formatcontext, io, "", iformat, NULL);
	if (err != 0)
	{
		CloseStreamContext(io);
		return FSharpOption<Tuple<Container^, ExclusiveContext^>^>::None;
	}

	err = av_find_stream_info(formatcontext);
	if (err < 0)
	{
		av_close_input_stream(formatcontext);
		CloseStreamContext(io);
		return FSharpOption<Tuple<Container^, ExclusiveContext^>^>::None;
	}

	// Find corresponding managed container
	_Container^ container = nullptr;
	for each(_Container^ it in _Containers->Values) {
		if (it->Input == iformat) {
			container = it;
			break;
		}
	}

	return FSharpOption<Tuple<Container^, ExclusiveContext^>^>::Some(Tuple::Create<Container^, ExclusiveContext^>(container, _Context::Initialize(io, formatcontext)));
}