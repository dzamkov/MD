#include <gcroot.h>

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Text;
using namespace MD;
using namespace Microsoft::FSharp::Core;

typedef Stream<Byte> ByteStream;

ref class _Context;

/// <summary>
/// read_packet callback for a stream context.
/// </summary>
int read_packet(void* opaque, uint8_t* buf, int buf_size);

/// <summary>
/// Initializes an AVIOContext for a stream.
/// </summary>
AVIOContext* InitStreamContext(ByteStream^ Stream);

/// <summary>
/// Closes an AVIOContext for a stream.
/// </summary>
void CloseStreamContext(AVIOContext* Context);

/// <summary>
/// A context for decoding.
/// </summary>
ref class _Context : Context, IDisposable {
public:
	_Context(array<MD::Content^>^ Content) : Context(Content) {
		this->_Packet = NULL;
	}

	~_Context() {
		this->!_Context();
	}

	!_Context() {
		CloseStreamContext(this->IOContext);
		delete[] this->StreamContent;
		av_free(this->Buffer);
		if (this->_Packet != NULL)
		{
			av_free_packet(this->_Packet);
			delete this->_Packet;
		}
	}

	/// <summary>
	/// Initializes a context.
	/// </summary>
	static _Context^ Initialize(AVIOContext* IOContext, AVFormatContext* FormatContext) {

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
		context->StreamContent = streamcontent;
		context->IOContext = IOContext;
		context->FormatContext = FormatContext;
		context->Buffer = (Byte*)av_malloc(buffersize);
		context->BufferSize = buffersize;

		return context;
	}

	virtual bool NextFrame(int% ContentIndex) override {
		if (this->_Packet == NULL)
			this->_Packet = new AVPacket();
		else
			av_free_packet(this->_Packet);
		while (av_read_frame(this->FormatContext, this->_Packet) >= 0) {
			int streamindex = this->_Packet->stream_index;
			ContentIndex = this->StreamContent[streamindex];
			if (ContentIndex != -1) {
				MD::Content^ content = this->Content[ContentIndex];
				if (!content->Ignore) {
					AVCodecContext* codeccontext = this->FormatContext->streams[streamindex]->codec;
					int framesize = this->BufferSize;

					AudioContent^ audio = dynamic_cast<AudioContent^>(content);
					if (audio != nullptr) {

						// Decode audio
						if (avcodec_decode_audio3(codeccontext, (int16_t*)this->Buffer, &framesize, this->_Packet) >= 0) {
							audio->Data = FSharpOption<MD::Data<Byte>^>::Some(gcnew UnsafeData(this->Buffer, this->Buffer + framesize));
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

	virtual void Finish() override {
		delete this;
	}

	int* StreamContent;
	AVIOContext* IOContext;
	AVFormatContext* FormatContext;
	Byte* Buffer;
	int BufferSize;

private:
	AVPacket* _Packet;
};

/// <summary>
/// A FFmpeg container format.
/// </summary>
ref class _Container : Container {
public:
	_Container(String^ Name) : Container(Name) {
		this->Input = NULL;
		this->Output = NULL;
	}

    virtual FSharpOption<Context^>^ Decode(Stream<Byte>^ Stream) override {
		if (this->Input == NULL)
			return FSharpOption<Context^>::None;

		AVIOContext* io = InitStreamContext(Stream);
		
		// Find stream format information
		AVFormatContext* formatcontext;
		int err = av_open_input_stream(&formatcontext, io, "", this->Input, NULL);
		if (err != 0)
		{
			CloseStreamContext(io);
			return FSharpOption<Context^>::None;
		}

		err = av_find_stream_info(formatcontext);
		if (err < 0)
		{
			av_close_input_stream(formatcontext);
			CloseStreamContext(io);
			return FSharpOption<Context^>::None;
		}

		return FSharpOption<Context^>::Some(_Context::Initialize(io, formatcontext));
	}

    virtual FSharpOption<Stream<Byte>^>^ Encode(Context^ Context) override {
		return FSharpOption<Stream<Byte>^>::None;
	}

	AVInputFormat* Input;
	AVOutputFormat* Output;
};

public ref class Plugin : MD::Plugin {
public:
	Plugin() {

	}

	static bool Initialized = false;

	virtual property String^ Name {
		String^ get() override {
			return "FFmpeg";
		}
	}

	virtual property String^ Description {
		String^ get() override {
			int vers = avcodec_version();
			return "Interface to the FFmpeg audio/video codec collection (version " + vers.ToString() + ").";
		}
	}

	virtual property String^ About {
		String^ get() override {
			const char* license = avcodec_license();
			return "This plugin uses libraries from the FFmpeg project, licensed under " + gcnew String(license) + ".";
		}
	}

	virtual RetractAction^ Load() override {
		RetractAction^ retract = nullptr;

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

			// Register containers
			for each(_Container^ container in _Containers->Values) {
				retract += MD::Container::Register(container);
			}

			// Register load container
			retract += MD::Container::RegisterLoad(gcnew LoadContainerAction(_LoadContainer));
		}

		return retract;
	}

private:
	static Dictionary<String^, _Container^>^ _Containers = nullptr;

	static FSharpOption<Tuple<Container^, Context^>^>^ _LoadContainer(MD::Data<Byte>^ Data, String^ Filename) {
		using namespace Runtime::InteropServices;

		Stream<Byte>^ stream = Data->Read(0, Data->Size);
		AVIOContext* io = InitStreamContext(stream);

		// Get file name if possible
		char* filename = NULL;
		if (Filename != nullptr) {
			filename = static_cast<char*>(Marshal::StringToHGlobalAnsi(Filename).ToPointer());
		}

		// Determine format
		AVInputFormat* iformat;
		int err = av_probe_input_buffer(io, &iformat, filename, NULL, 0, 0);
		if (err != 0)
		{
			CloseStreamContext(io);
			return FSharpOption<Tuple<Container^, Context^>^>::None;
		}

		// Free file name
		if (filename != NULL) {
			Marshal::FreeHGlobal(IntPtr(static_cast<void*>(filename)));
		}
		
		// Find stream format information
		AVFormatContext* formatcontext;
		err = av_open_input_stream(&formatcontext, io, "", iformat, NULL);
		if (err != 0)
		{
			CloseStreamContext(io);
			return FSharpOption<Tuple<Container^, Context^>^>::None;
		}

		err = av_find_stream_info(formatcontext);
		if (err < 0)
		{
			av_close_input_stream(formatcontext);
			CloseStreamContext(io);
			return FSharpOption<Tuple<Container^, Context^>^>::None;
		}

		// Find corresponding managed container
		_Container^ container = nullptr;
		for each(_Container^ it in _Containers->Values) {
			if (it->Input == iformat) {
				container = it;
				break;
			}
		}

		return FSharpOption<Tuple<Container^, Context^>^>::Some(Tuple::Create<Container^, Context^>(container, _Context::Initialize(io, formatcontext)));
	}
};

