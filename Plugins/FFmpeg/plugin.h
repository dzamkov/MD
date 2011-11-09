#include <gcroot.h>

using namespace System;
using namespace System::Collections::Generic;
using namespace MD;
using namespace MD::Data;
using namespace MD::Codec;

typedef Stream<Byte> ByteStream;

ref class _Context;

/// <summary>
/// read_packet callback for a stream context.
/// </summary>
int read_packet(void* opaque, uint8_t* buf, int buf_size);

/// <summary>
/// Initializes an AVIOContext for a stream.
/// </summary>
AVIOContext* InitStreamContext(ByteStream^ Stream, int BufferSize);

/// <summary>
/// Closes an AVIOContext for a stream.
/// </summary>
void CloseStreamContext(AVIOContext* Context);

/// <summary>
/// A context for decoding.
/// </summary>
ref class _Context : Context {
public:
	_Context(array<Codec::Content^>^ Content) : Context(Content) {
		this->_Packet = NULL;
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
				Codec::Content^ content = this->Content[ContentIndex];
				if (!content->Ignore) {
					AVCodecContext* codeccontext = this->FormatContext->streams[streamindex]->codec;
					int framesize = this->BufferSize;

					AudioContent^ audio = dynamic_cast<AudioContent^>(content);
					if (audio != nullptr) {

						// Decode audio
						if (avcodec_decode_audio3(codeccontext, (int16_t*)this->Buffer, &framesize, this->_Packet) >= 0) {
							UnsafeArray^ data = dynamic_cast<UnsafeArray^>(audio->Data);
							if (data == nullptr) {
								data = gcnew UnsafeArray(this->Buffer, this->Buffer + framesize);
								audio->Data = data;
							} else {
								data->Start = this->Buffer;
								data->End = this->Buffer + framesize;
							}
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
		CloseStreamContext(this->IOContext);
		delete[] this->StreamContent;
		av_free(this->Buffer);
		if (this->_Packet != NULL)
		{
			av_free(this->_Packet);
			delete this->_Packet;
		}
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

	}

    virtual Context^ Decode(Stream<Byte>^ Stream) override {
		AVIOContext* io = InitStreamContext(Stream, 4096);
		
		// Find stream format information
		AVFormatContext* formatcontext;
		if (av_open_input_stream(&formatcontext, io, "", this->Input, NULL) != 0 || av_find_stream_info(formatcontext) < 0)
		{
			CloseStreamContext(io);
			return nullptr;
		}

		// Initialize content streams
		List<Content^>^ contents = gcnew List<Content^>(formatcontext->nb_streams);
		int* streamcontent = new int[formatcontext->nb_streams];
		int buffersize = 0;

		for (unsigned int t = 0; t < formatcontext->nb_streams; t++) {
			streamcontent[t] = -1;
			AVCodecContext* codeccontext = formatcontext->streams[t]->codec;
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
		context->IOContext = io;
		context->FormatContext = formatcontext;
		context->Buffer = (Byte*)av_malloc(buffersize);
		context->BufferSize = buffersize;

		return context;
	}

    virtual Stream<Byte>^ Encode(Context^ Context) override {
		return nullptr;
	}

	AVInputFormat* Input;
	AVOutputFormat* Output;
};

public ref class Plugin {
public:

	static String^ Name = "FFmpeg Codecs";
	static bool Initialized = false;

	static RetractHandler^ Load() {
		RetractHandler^ retract = nullptr;

		if (!Initialized)
		{
			Initialized = true;
			avcodec_init();
			av_register_all();
		}

		// Input formats
		AVInputFormat* iformat = av_iformat_next(NULL);
		while (iformat != NULL) {
			_Container^ container = gcnew _Container(gcnew String(iformat->name));
			container->Input = iformat;
			retract += Container::Register(container);
			iformat = av_iformat_next(iformat);
		}

		// Output formats
		AVOutputFormat* oformat = av_oformat_next(NULL);
		while (oformat != NULL) {
			String^ name = gcnew String(oformat->name);
			bool hascontainer = false;
			for each(Container^ container in Container::WithName(name)) {
				_Container^ ncontainer = dynamic_cast<_Container^>(container);
				if (ncontainer != nullptr) {
					ncontainer->Output = oformat;
					hascontainer = true;
					break;
				}
			}
			if (!hascontainer) {
				_Container^ ncontainer = gcnew _Container(name);
				ncontainer->Output = oformat;
				retract += Container::Register(ncontainer);
			}
			oformat = av_oformat_next(oformat);
		}

		return retract;
	}
};

