#pragma once
#include <filesystem>
#include <string>
#include <rex/ui/presenter.h>
#include <rex/ui/ui_event.h>

namespace ea {
bool IsFrameCaptureKey(const rex::ui::KeyEvent& event);
bool WriteCapturePng(const rex::ui::RawImage& image, const std::filesystem::path& path, std::string& error);
bool CaptureFrame(rex::ui::Presenter& presenter, const std::filesystem::path& directory,
                  std::filesystem::path& saved_path, std::string& error);
}  // namespace ea
