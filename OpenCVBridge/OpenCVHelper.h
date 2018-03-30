#pragma once

namespace OpenCVBridge
{
	public ref class OpenCVHelper
	{
	public:
		OpenCVHelper() {}
	//private:
		bool GetPointerToPixelData(Windows::Graphics::Imaging::SoftwareBitmap^ bitmap, unsigned char** pPixelData, unsigned int* capacity);
	};
}
