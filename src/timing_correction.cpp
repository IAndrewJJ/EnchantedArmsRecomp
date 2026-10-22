#include "timing_correction.h"

#include <rex/cvar.h>
#include <rex/hook.h>

#include <array>
#include <atomic>
#include <chrono>
#include <cmath>

#include "frame_step.h"
#include "generated/default/enchantedarmsrecomp_init.h"

REXCVAR_DECLARE(int32_t, ea_fps_limit);
REXCVAR_DEFINE_BOOL(
    ea_timing_correction, true, "Enchanted Arms",
    "Correct engine and animated-surface timing for high FPS; campaign validation is ongoing")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);
REXCVAR_DEFINE_BOOL(ea_trace_timing, false, "Enchanted Arms",
                    "Log bounded engine-step measurements for local timing comparisons")
    .lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

namespace {
std::atomic<double> frame_step{0.0};
thread_local unsigned step_depth = 0;
thread_local unsigned script_depth = 0;

enum class Boundary { World, Manager, Script, Timer, Exploration, Interface, Background, Count };
constexpr std::array<const char*, size_t(Boundary::Count)> boundary_names = {
    "world", "manager", "script", "timer", "exploration", "interface", "background"};

double StepScale() {
  const int limit = REXCVAR_GET(ea_fps_limit);
  if (!REXCVAR_GET(ea_timing_correction) || limit == 0 || limit == 30) return 1.0;
  const double current = frame_step.load(std::memory_order_relaxed);
  return current > 0.0 ? current : (limit > 0 ? 30.0 / limit : 1.0);
}

void CorrectFloatArgument(double& argument, bool outer) {
  if (!outer) return;
  const double scale = StepScale();
  // Preserve Original/disabled mode bit-for-bit, including double arguments.
  if (scale != 1.0) argument = double(float(argument * scale));
}

void TraceStep(Boundary boundary, double original, double applied, uint32_t caller) {
  if (!REXCVAR_GET(ea_trace_timing)) return;
  // Sampling is bounded per engine boundary, independent of the number of NPCs.
  struct Sample {
    uint32_t calls = 0;
    double sum = 0.0;
  };
  static thread_local std::array<Sample, size_t(Boundary::Count)> samples{};
  const size_t index = size_t(boundary);
  Sample& sample = samples[index];
  ++sample.calls;
  sample.sum += applied;
  if (sample.calls <= 4 || (sample.calls % 300) == 0) {
    REXLOG_INFO(
        "EA engine step: boundary={}, input={}, applied={}, caller={:08X}, calls={}, "
        "cumulative_steps={}",
        boundary_names[index], original, applied, caller, sample.calls, sample.sum);
    rex::FlushLogging();
  }
}
}  // namespace

namespace ea {
void AdvanceEngineFrameStep() {
  using Clock = std::chrono::steady_clock;
  static thread_local Clock::time_point previous;
  const auto now = Clock::now();
  const double elapsed =
      previous == Clock::time_point{} ? 0.0 : std::chrono::duration<double>(now - previous).count();
  previous = now;
  frame_step.store(FrameStepClock{}.Advance(elapsed, REXCVAR_GET(ea_fps_limit)),
                   std::memory_order_relaxed);
}
}  // namespace ea

// Called by the generated instruction hook after the exploration update's
// fixed-step load, before it dispatches the loaded scenes and camera updates.
void EAExplorationFrameStep(PPCRegister& step) {
  const double input = step.f64;
  CorrectFloatArgument(step.f64, true);
  TraceStep(Boundary::Exploration, input, step.f64, 0x820EE10C);
}

void EAInterfaceFrameStep(PPCRegister& step) {
  const double input = step.f64;
  CorrectFloatArgument(step.f64, true);
  TraceStep(Boundary::Interface, input, step.f64, 0x821B1254);
}

void EAMenuSceneFrameStep(PPCRegister& step) {
  const double input = step.f64;
  CorrectFloatArgument(step.f64, true);
  TraceStep(Boundary::Background, input, step.f64, 0x8223CA0C);
}

void EATextureScrollStep(PPCVRegister& velocity, PPCRegister& model) {
  const double scale = StepScale();
  ea::ScaleTextureScroll(velocity.f32, scale);
  if (REXCVAR_GET(ea_trace_timing) && (velocity.f32[0] != 0.0f || velocity.f32[1] != 0.0f ||
                                       velocity.f32[2] != 0.0f || velocity.f32[3] != 0.0f)) {
    using Clock = std::chrono::steady_clock;
    static thread_local auto previous = Clock::time_point{};
    const auto now = Clock::now();
    if (previous == Clock::time_point{} || now - previous >= std::chrono::seconds(5)) {
      REXLOG_INFO(
          "EA texture scroll: model={:08X}, step_scale={}, corrected_velocity=({}, {}, {}, {})",
          model.u32, scale, velocity.f32[0], velocity.f32[1], velocity.f32[2], velocity.f32[3]);
      previous = now;
    }
  }
}

REX_HOOK_RAW(sub_820EE028) {
  ea::FrameStepScope scope(step_depth);
  __imp__sub_820EE028(ctx, base);
}

// An alternate event update forwards its incoming step. The exploration path
// above instead loads its own constant and needs an instruction hook.
REX_HOOK_RAW(sub_82185E08) {
  ea::FrameStepScope scope(step_depth);
  const double input = ctx.f1.f64;
  CorrectFloatArgument(ctx.f1.f64, scope.outer());
  TraceStep(Boundary::World, input, ctx.f1.f64, ctx.lr);
  __imp__sub_82185E08(ctx, base);
}

// Generic scene manager updates also run outside a world update (menus/events).
REX_HOOK_RAW(sub_821378D0) {
  ea::FrameStepScope scope(step_depth);
  const double input = ctx.f1.f64;
  CorrectFloatArgument(ctx.f1.f64, scope.outer());
  TraceStep(Boundary::Manager, input, ctx.f1.f64, ctx.lr);
  if (REXCVAR_GET(ea_trace_timing)) {
    static thread_local uint32_t previous_target = 0xFFFFFFFF;
    const bool active = REX_LOAD_U32(ctx.r3.u32 + 196) != 0;
    const uint32_t table = REX_LOAD_U32(ctx.r3.u32);
    const uint32_t target = active ? REX_LOAD_U32(table + 8) : 0;
    if (target != previous_target) {
      REXLOG_INFO("EA scene manager: object={:08X}, active={}, target={:08X}, caller={:08X}",
                  ctx.r3.u32, active, target, ctx.lr);
      previous_target = target;
    }
  }
  __imp__sub_821378D0(ctx, base);
}

// Script updates supply a fixed 1000 units per 30 Hz frame. Their returned
// fractional step is subsequently forwarded to scene updates by the caller.
REX_HOOK_RAW(sub_821A5FB8) {
  ea::FrameStepScope frame_scope(step_depth);
  ea::FrameStepScope script_scope(script_depth);
  __imp__sub_821A5FB8(ctx, base);
}

REX_HOOK_RAW(sub_821A40F0) {
  if (script_depth != 0) {
    const int32_t input = ctx.r4.s32;
    const double scale = StepScale();
    if (scale != 1.0) ctx.r4.s64 = static_cast<int32_t>(std::llround(double(input) * scale));
    TraceStep(Boundary::Script, double(input) / 1000.0, double(ctx.r4.s32) / 1000.0, ctx.lr);
  }
  __imp__sub_821A40F0(ctx, base);
}

REX_HOOK_RAW(sub_820D45C8) {
  ea::FrameStepScope scope(step_depth);
  const uint64_t input_bits = ctx.f1.u64;
  const double input = ctx.f1.f64;
  CorrectFloatArgument(ctx.f1.f64, scope.outer());
  TraceStep(Boundary::Timer, input, ctx.f1.f64, ctx.lr);
  __imp__sub_820D45C8(ctx, base);
  // This leaf originally preserves f1. Its caller reuses it for a sibling
  // update, so leaking our scaled argument would apply the correction twice.
  ctx.f1.u64 = input_bits;
}

// Local diagnostics for the shared per-frame dispatchers. Discover active
// virtual targets before deciding whether an effect needs a timing correction.
REX_HOOK_RAW(sub_822F67D8) {
  if (REXCVAR_GET(ea_trace_timing)) {
    static thread_local std::array<uint32_t, 8> previous{};
    for (unsigned i = 0; i < previous.size(); ++i) {
      const uint32_t object = REX_LOAD_U32(ctx.r3.u32 + i * 4);
      uint32_t target = 0;
      if (object != 0 && REX_LOAD_U32(object + 4) != 0) {
        const uint32_t table = REX_LOAD_U32(object);
        target = REX_LOAD_U32(table + 16);
      }
      if (target != previous[i]) {
        REXLOG_INFO("EA shared update: slot={}, object={:08X}, target={:08X}, caller={:08X}", i,
                    object, target, ctx.lr);
        previous[i] = target;
      }
    }
  }
  __imp__sub_822F67D8(ctx, base);
}
