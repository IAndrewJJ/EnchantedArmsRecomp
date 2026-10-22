// enchantedarmsrecomp - ReXGlue Recompiled Project
//
// Customize your app by overriding virtual hooks from rex::ReXApp.

#pragma once

#include <rex/rex_app.h>
#include <rex/cvar.h>
#include <rex/filesystem.h>
#include <rex/ui/keybinds.h>
#include <cstdlib>
#include <fstream>
#include "fps_counter.h"
#include "frame_capture.h"

REXCVAR_DECLARE(bool, ea_trace_input);
REXCVAR_DECLARE(int32_t, ea_fps_limit);
REXCVAR_DECLARE(bool, ea_show_fps);

class EnchantedarmsrecompApp : public rex::ReXApp {
 public:
  using rex::ReXApp::ReXApp;

  void OnKeyDown(rex::ui::KeyEvent& event) override {
    if (ea::IsFrameCaptureKey(event)) {
      try {
        if (!event.prev_state() && runtime() && runtime()->graphics_system()) {
          auto* presenter = runtime()->graphics_system()->presenter();
          std::filesystem::path saved;
          std::string error;
          if (presenter && ea::CaptureFrame(*presenter, user_data_root().parent_path() / "captures", saved, error)) {
            REXLOG_INFO("EA screenshot saved: {} (guest output before presenter scaling/overlays)", saved.string());
          } else {
            REXLOG_WARN("EA screenshot failed: {}", presenter ? error : "Presenter is unavailable");
          }
        }
      } catch (const std::exception& error) { REXLOG_WARN("EA screenshot failed: {}", error.what()); }
      event.set_handled(true); return;
    }
    if (ea::IsFpsCounterKey(event)) {
      // SDL reports prev_state for key-repeat: holding F6 toggles only once.
      if (ea::ShouldToggleFpsCounter(event) && imgui_drawer()) {
        if (fps_counter_) fps_counter_.reset();
        else fps_counter_ = std::make_unique<ea::FpsCounterDialog>(imgui_drawer());
        REXLOG_INFO("FPS counter {}", fps_counter_ ? "shown" : "hidden");
      }
      event.set_handled(true);
      return;
    }
    if (REXCVAR_GET(ea_trace_input)) {
      REXLOG_INFO("Host key down: {:02X}", static_cast<uint16_t>(event.virtual_key()));
    }
    // ReXApp's private override delegates directly to this SDK dispatcher.
    rex::ui::ProcessKeyEvent(event);
  }

  void OnKeyUp(rex::ui::KeyEvent& event) override {
    if (ea::IsFpsCounterKey(event) || ea::IsFrameCaptureKey(event)) { event.set_handled(true); return; }
    if (REXCVAR_GET(ea_trace_input)) {
      REXLOG_INFO("Host key up: {:02X}", static_cast<uint16_t>(event.virtual_key()));
    }
  }

  void OnCreateDialogs(rex::ui::ImGuiDrawer* drawer) override {
    if (REXCVAR_GET(ea_show_fps)) fps_counter_ = std::make_unique<ea::FpsCounterDialog>(drawer);
  }

  void OnPreSetup(rex::RuntimeConfig& config) override {
    if (config.gpu_plugin.empty()) {
      config.gpu_plugin = "xenos";
    }
    if (rex::cvar::GetFlagSource("mnk_mode") == rex::cvar::Source::kDefault) {
      rex::cvar::SetFlagByName("mnk_mode", "true");
    }
  }

  void OnPostSetup() override {
    // The plugin has now registered vsync. Its worker reads it on every tick.
    // Change only its throttle; retain the guest video mode and timebase.
    rex::cvar::SetFlagByName("vsync", REXCVAR_GET(ea_fps_limit) == 0 ? "true" : "false");
    REXLOG_INFO("EA timing/detail: fps_limit={}, vsync={}, video_hz={}, texture_lod_bias={}",
                REXCVAR_GET(ea_fps_limit), rex::cvar::GetFlagByName("vsync"),
                rex::cvar::GetFlagByName("video_mode_refresh_rate"),
                rex::cvar::GetFlagByName("ea_texture_lod_bias"));
    if (REXCVAR_GET(ea_fps_limit) != 0) {
      REXLOG_WARN("Experimental FPS can accelerate Enchanted Arms gameplay. A normal-speed high-FPS engine timing patch is not implemented; use Original for normal play.");
    }
    // GPU flags register when the renderer plugin loads. Record their applied
    // values here so a playtest report can distinguish them from launcher intent.
    REXLOG_INFO("EA renderer flags: scale={}, draw_x={}, draw_y={}, AF={}, target_path={}, "
                "invalid_fetch={}, async_shaders={}, occlusion_queries={}",
                rex::cvar::GetFlagByName("resolution_scale"),
                rex::cvar::GetFlagByName("draw_resolution_scale_x"),
                rex::cvar::GetFlagByName("draw_resolution_scale_y"),
                rex::cvar::GetFlagByName("anisotropic_override"),
                rex::cvar::GetFlagByName("render_target_path_d3d12"),
                rex::cvar::GetFlagByName("gpu_allow_invalid_fetch_constants"),
                rex::cvar::GetFlagByName("async_shader_compilation"),
                rex::cvar::GetFlagByName("occlusion_query_enable"));
    REXLOG_INFO("EA coherency flags: readback_resolve={}, memexport={}, fast_memexport={}, page_state={}",
                rex::cvar::GetFlagByName("readback_resolve"),
                rex::cvar::GetFlagByName("readback_memexport"),
                rex::cvar::GetFlagByName("readback_memexport_fast"),
                rex::cvar::GetFlagByName("clear_memory_page_state"));
    REXLOG_INFO("EA presentation flags: output={}, AA={}, FSR_reduction={}, CAS_sharpness={}, "
                "dither={}, fullscreen={}, letterbox={}, tearing={}, native_2x_msaa={}",
                rex::cvar::GetFlagByName("present_effect"),
                rex::cvar::GetFlagByName("swap_post_effect"),
                rex::cvar::GetFlagByName("present_fsr_sharpness_reduction"),
                rex::cvar::GetFlagByName("present_cas_additional_sharpness"),
                rex::cvar::GetFlagByName("present_dither"),
                rex::cvar::GetFlagByName("fullscreen"),
                rex::cvar::GetFlagByName("present_letterbox"),
                rex::cvar::GetFlagByName("d3d12_allow_variable_refresh_rate_and_tearing"),
                rex::cvar::GetFlagByName("native_2x_msaa"));
  }

  void OnConfigurePaths(rex::PathConfig& paths) override {
    // Staged executables live in bin/; developer builds in .local/build/<config>.
    const auto executable_folder = rex::filesystem::GetExecutableFolder();
    auto project = executable_folder;
    for (int depth = 0; depth < 5; ++depth) {
      if (std::filesystem::is_regular_file(project / "enchantedarmsrecomp_manifest.toml")) {
        break;
      }
      project = project.parent_path();
    }
    const auto game = project / ".local/game";
    if (!std::filesystem::is_regular_file(game / "default.xex")) {
      return;
    }
    const auto local = project / ".local/runtime";
    std::filesystem::create_directories(local);
    if (paths.game_data_root.empty()) {
      paths.game_data_root = game;
    }
    if (rex::cvar::GetFlagByName("user_data_root").empty()) {
      paths.user_data_root = local / "user";
    }
    if (rex::cvar::GetFlagByName("cache_root").empty()) {
      paths.cache_root = local / "cache";
    }
    paths.config_path = local / "settings.toml";
    if (rex::cvar::GetFlagByName("log_file").empty()) {
      rex::cvar::SetFlagByName("log_file", (local / "desktop-boot.log").string());
    }
  }

  // Opt-in local diagnostic: inspect indirect callbacks that static discovery
  // missed. The loaded image stays in a caller-selected private workspace file.
  void OnPostLoadXexImage() override {
    #if defined(_WIN32)
    char* dump_path = nullptr;
    size_t path_size = 0;
    if (_dupenv_s(&dump_path, &path_size, "EA_RECOMP_DUMP_IMAGE") != 0) {
      return;
    }
    std::unique_ptr<char, decltype(&std::free)> owned_path(dump_path, &std::free);
    #else
    const char* dump_path = std::getenv("EA_RECOMP_DUMP_IMAGE");
    #endif
    if (dump_path && *dump_path) {
      std::ofstream output(dump_path, std::ios::binary);
      if (output) {
        const auto* image = runtime()->memory()->TranslateVirtual<const char*>(REX_IMAGE_BASE);
        output.write(image, REX_IMAGE_SIZE);
      }
    }
  }

  static std::unique_ptr<rex::ui::WindowedApp> Create(
      rex::ui::WindowedAppContext& ctx) {
    return std::unique_ptr<EnchantedarmsrecompApp>(new EnchantedarmsrecompApp(ctx, "enchantedarmsrecomp",
        PPCImageConfig));
  }

 private:
  std::unique_ptr<ea::FpsCounterDialog> fps_counter_;

  // Override virtual hooks for customization:
  // void OnPostInitLogging() override {}
  // void OnPreSetup(rex::RuntimeConfig& config) override {}
  // void OnLoadXexImage(std::string& xex_image) override {}
  // void OnPostLoadXexImage() override {}
  // void OnPostSetup() override {}
  // void OnCreateDialogs(rex::ui::ImGuiDrawer* drawer) override {}
  // std::unique_ptr<rex::ui::ImGuiDialog> CreateAchievementsOverlay() override;
  // std::unique_ptr<rex::ui::AchievementNotificationDialog>
  // CreateAchievementNotificationDialog() override;
  // void OnShutdown() override {}
  // void OnConfigurePaths(rex::PathConfig& paths) override {}
};
