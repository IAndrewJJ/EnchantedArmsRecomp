#include "generated/default/enchantedarmsrecomp_init.h"
#include "frame_pacer.h"
#include "fps_counter.h"
#include "frame_time_window.h"
#include <rex/cvar.h>
#include <rex/hook.h>

REXCVAR_DEFINE_INT32(ea_fps_limit, 0, "Enchanted Arms",
    "Experimental frame pacing: 0=original, -1=unlimited, positive=maximum submissions per second")
    .range(-1, 360).lifecycle(rex::cvar::Lifecycle::kRequiresRestart);

// Revision-specific D3D swap boundary, identified by its VdSwap call at 822A9860.
// Retain the entire original swap routine and its result/register writes.
REX_HOOK_RAW(sub_822A9680) {
  const int limit = REXCVAR_GET(ea_fps_limit);
  if (limit > 0) {
    static thread_local ea::FramePacer pacer;
    pacer.Wait(limit);
  }
  __imp__sub_822A9680(ctx, base);
  ea::RecordGameFrame();

  struct Metrics {
    ea::FramePacer::Clock::time_point start = ea::FramePacer::Clock::now();
    ea::FramePacer::Clock::time_point last;
    bool have_last = false;
    ea::FrameTimeWindow intervals;
    uint32_t frames = 0;
  };
  static thread_local Metrics metrics;
  ++metrics.frames;
  const auto now = ea::FramePacer::Clock::now();
  if (metrics.have_last) metrics.intervals.Add(std::chrono::duration<double, std::milli>(now - metrics.last).count());
  metrics.last = now; metrics.have_last = true;
  const double elapsed = std::chrono::duration<double>(now - metrics.start).count();
  if (elapsed >= 5.0) {
    REXLOG_INFO("EA frame pacing: requested_limit={}, submissions_per_second={:.2f}, samples={}, elapsed={:.2f}s",
                limit, metrics.frames / elapsed, metrics.frames, elapsed);
    const auto stats = metrics.intervals.Summary();
    REXLOG_INFO("EA frame intervals (ms, includes pacing): mean={:.2f}, p50={:.2f}, p95={:.2f}, p99={:.2f}, max={:.2f}, over_50ms={}, intervals={}, percentile_samples={}",
                stats.mean_ms, stats.p50_ms, stats.p95_ms, stats.p99_ms, stats.max_ms,
                stats.over_50ms, stats.intervals, stats.percentile_samples);
    metrics.intervals.Reset();
    metrics.start = now;
    metrics.frames = 0;
  }
}
