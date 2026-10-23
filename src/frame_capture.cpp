#include "frame_capture.h"

#include <chrono>
#include <limits>
#if defined(_WIN32)
#include <wincodec.h>
#include <wrl/client.h>
#else
#include <zlib.h>

#include <fstream>
#endif

namespace ea {
bool IsFrameCaptureKey(const rex::ui::KeyEvent& event) {
  return event.virtual_key() == rex::ui::VirtualKey::kF8 && !event.is_shift_pressed() &&
         !event.is_ctrl_pressed() && !event.is_alt_pressed() && !event.is_super_pressed();
}

bool WriteCapturePng(const rex::ui::RawImage& image, const std::filesystem::path& path,
                     std::string& error) {
  error.clear();
  // Validate dimensions and padding before passing UINT-sized lengths to WIC.
  if (!image.width || !image.height || image.width > UINT32_MAX / 4 ||
      image.stride < uint64_t(image.width) * 4 || image.stride > UINT32_MAX ||
      uint64_t(image.height) * image.stride > UINT32_MAX ||
      image.data.size() < uint64_t(image.height - 1) * image.stride + uint64_t(image.width) * 4) {
    error = "Invalid screenshot dimensions or pixel buffer";
    return false;
  }
#if defined(_WIN32)
  using Microsoft::WRL::ComPtr;
  const HRESULT initialized = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
  if (FAILED(initialized) && initialized != RPC_E_CHANGED_MODE) {
    error = "Could not initialize Windows image encoder";
    return false;
  }
  struct Apartment {
    bool release;
    ~Apartment() {
      if (release) CoUninitialize();
    }
  } apartment{SUCCEEDED(initialized)};
  ComPtr<IWICImagingFactory> factory;
  ComPtr<IWICStream> stream;
  ComPtr<IWICBitmapEncoder> encoder;
  ComPtr<IWICBitmapFrameEncode> frame;
  HRESULT hr = CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
                                IID_PPV_ARGS(&factory));
  if (SUCCEEDED(hr)) hr = factory->CreateStream(&stream);
  if (SUCCEEDED(hr)) hr = stream->InitializeFromFilename(path.c_str(), GENERIC_WRITE);
  if (SUCCEEDED(hr)) hr = factory->CreateEncoder(GUID_ContainerFormatPng, nullptr, &encoder);
  if (SUCCEEDED(hr)) hr = encoder->Initialize(stream.Get(), WICBitmapEncoderNoCache);
  if (SUCCEEDED(hr)) hr = encoder->CreateNewFrame(&frame, nullptr);
  if (SUCCEEDED(hr)) hr = frame->Initialize(nullptr);
  if (SUCCEEDED(hr)) hr = frame->SetSize(image.width, image.height);
  WICPixelFormatGUID format = GUID_WICPixelFormat24bppBGR;
  if (SUCCEEDED(hr)) hr = frame->SetPixelFormat(&format);
  if (SUCCEEDED(hr) && format != GUID_WICPixelFormat24bppBGR) hr = E_FAIL;
  const UINT stride = image.width * 3;
  std::vector<BYTE> bgr;
  if (SUCCEEDED(hr)) {
    bgr.resize(size_t(stride) * image.height);
    for (uint32_t y = 0; y < image.height; ++y) {
      for (uint32_t x = 0; x < image.width; ++x) {
        const auto* pixel = image.data.data() + size_t(y) * image.stride + size_t(x) * 4;
        auto* dest = bgr.data() + size_t(y) * stride + size_t(x) * 3;
        dest[0] = pixel[2];
        dest[1] = pixel[1];
        dest[2] = pixel[0];
      }
    }
    hr = frame->WritePixels(image.height, stride, UINT(bgr.size()), bgr.data());
  }
  if (SUCCEEDED(hr)) hr = frame->Commit();
  if (SUCCEEDED(hr)) hr = encoder->Commit();
  if (FAILED(hr)) {
    frame.Reset();
    encoder.Reset();
    stream.Reset();
    std::error_code ignored;
    std::filesystem::remove(path, ignored);
    error = "Windows PNG encoding failed (HRESULT " + std::to_string(uint32_t(hr)) + ")";
    return false;
  }
  return true;
#else
  // PNG rows are RGB, with a filter byte before each row. The GPU's X byte
  // is padding, not opacity; preserve the same opaque output as Windows WIC.
  std::vector<uint8_t> pixels((size_t(image.width) * 3 + 1) * image.height);
  for (uint32_t y = 0; y < image.height; ++y) {
    auto* row = pixels.data() + size_t(y) * (size_t(image.width) * 3 + 1);
    row[0] = 0;
    for (uint32_t x = 0; x < image.width; ++x) {
      const auto* source = image.data.data() + size_t(y) * image.stride + size_t(x) * 4;
      std::copy_n(source, 3, row + 1 + size_t(x) * 3);
    }
  }
  uLongf compressed_size = compressBound(pixels.size());
  std::vector<uint8_t> compressed(compressed_size);
  if (compress2(compressed.data(), &compressed_size, pixels.data(), pixels.size(), Z_BEST_SPEED) !=
          Z_OK ||
      compressed_size > UINT32_MAX - 12) {
    error = "Could not compress screenshot";
    return false;
  }
  compressed.resize(compressed_size);
  std::vector<uint8_t> png = {137, 80, 78, 71, 13, 10, 26, 10};
  auto integer = [](std::vector<uint8_t>& bytes, uint32_t value) {
    for (int shift = 24; shift >= 0; shift -= 8) bytes.push_back(uint8_t(value >> shift));
  };
  auto chunk = [&](const char* name, const std::vector<uint8_t>& data) {
    integer(png, uint32_t(data.size()));
    const size_t start = png.size();
    png.insert(png.end(), name, name + 4);
    png.insert(png.end(), data.begin(), data.end());
    integer(png, uint32_t(crc32(0, png.data() + start, uInt(data.size() + 4))));
  };
  std::vector<uint8_t> header;
  integer(header, image.width);
  integer(header, image.height);
  header.insert(header.end(), {8, 2, 0, 0, 0});
  chunk("IHDR", header);
  chunk("IDAT", compressed);
  chunk("IEND", {});
  std::ofstream output(path, std::ios::binary);
  output.write(reinterpret_cast<const char*>(png.data()), png.size());
  output.close();
  if (!output) {
    std::error_code ignored;
    std::filesystem::remove(path, ignored);
    error = "Could not write screenshot";
    return false;
  }
  return true;
#endif
}

bool CaptureFrame(rex::ui::Presenter& presenter, const std::filesystem::path& directory,
                  std::filesystem::path& saved_path, std::string& error) {
  saved_path.clear();
  rex::ui::RawImage image;
  if (!presenter.CaptureGuestOutput(image)) {
    error = "No guest frame is available to capture";
    return false;
  }
  std::error_code ec;
  std::filesystem::create_directories(directory, ec);
  if (ec) {
    error = "Could not create the captures directory: " + ec.message();
    return false;
  }
  const auto timestamp = std::chrono::duration_cast<std::chrono::microseconds>(
                             std::chrono::system_clock::now().time_since_epoch())
                             .count();
  auto path = directory / ("frame-" + std::to_string(timestamp) + ".png");
  if (!WriteCapturePng(image, path, error)) return false;
  saved_path = std::move(path);
  return true;
}
}  // namespace ea
