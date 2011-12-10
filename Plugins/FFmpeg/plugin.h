#include <gcroot.h>

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Text;
using namespace MD;
using namespace Microsoft::FSharp::Core;

typedef Exclusive<Context^> ExclusiveContext;
typedef Stream<Byte> ByteStream;
typedef Exclusive<ByteStream^> ExclusiveByteStream;
typedef Data<Byte> ByteData;
typedef Exclusive<ByteData^> ExclusiveByteData;

ref class _Context;

/// <summary>
/// read_packet callback for a stream context.
/// </summary>
int read_packet(void* opaque, uint8_t* buf, int buf_size);

/// <summary>
/// Initializes an AVIOContext for a stream.
/// </summary>
AVIOContext* InitStreamContext(ExclusiveByteStream^ Stream);

/// <summary>
/// Closes an AVIOContext for a stream.
/// </summary>
void CloseStreamContext(AVIOContext* Context);

/// <summary>
/// A context for decoding.
/// </summary>
ref class _Context : Context, IDisposable {
public:
	_Context(array<MD::Content^>^ Content);
	~_Context();
	!_Context();

	/// <summary>
	/// Initializes a context.
	/// </summary>
	static ExclusiveContext^ Initialize(AVIOContext* IOContext, AVFormatContext* FormatContext);

	virtual bool NextFrame(int% ContentIndex) override;

private:
	int* _StreamContent;
	AVIOContext* _IOContext;
	AVFormatContext* _FormatContext;
	Byte* _Buffer;
	int _BufferSize;
	volatile bool _Disposed;
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

    virtual FSharpOption<ExclusiveContext^>^ Decode(ExclusiveByteStream^ Stream) override;
    virtual FSharpOption<ExclusiveByteStream^>^ Encode(ExclusiveContext^ Context) override;

	AVInputFormat* Input;
	AVOutputFormat* Output;
};

public ref class Plugin : MD::Plugin {
public:
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

	virtual Retract^ Load() override;

private:
	static Dictionary<String^, _Container^>^ _Containers = nullptr;

	static FSharpOption<Tuple<Container^, ExclusiveContext^>^>^ _LoadContainer(ExclusiveByteData^ Data, String^ Filename);
};

