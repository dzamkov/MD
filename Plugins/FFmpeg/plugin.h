using namespace System;
using namespace MD;
using namespace MD::Codec;

ref class _Codec : Codec {
public:
	_Codec(AVCodec* Source, String^ Name, String^ Hint, bool CanEncode, bool CanDecode)
		: Codec(Name, Hint, CanEncode, CanDecode)
	{
		this->_Source = Source;
	}

private:
	AVCodec* _Source;
};

public ref class Plugin {
public:

	static String^ Name = "FFmpeg Codecs";

	static RetractHandler^ Load() {
		RetractHandler^ retract = nullptr;

		avcodec_init();
		avcodec_register_all();

		AVCodec* cur = av_codec_next(NULL);
		while (cur != NULL) {
			String^ hint = gcnew String(cur->name);
			_Codec^ codec = gcnew _Codec(cur, "FFmpeg " + hint, hint, cur->encode != NULL, cur->decode != NULL);
			retract += MD::Codec::Codec::Register(codec);
			cur = av_codec_next(cur);
		}

		return retract;
	}
};

