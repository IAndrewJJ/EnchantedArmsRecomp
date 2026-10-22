"""Summarize existing local frame logs; does not control or modify the game."""
import argparse
import json
from pathlib import Path
import re
import statistics

INTERVALS = re.compile(r'EA frame intervals \(ms, includes pacing\): mean=([\d.]+), p50=([\d.]+), p95=([\d.]+), p99=([\d.]+), max=([\d.]+), over_50ms=(\d+), intervals=(\d+)')
PACING = re.compile(r'EA frame pacing: requested_limit=(-?\d+), submissions_per_second=([\d.]+)')


def analyze(text):
    windows = [tuple(map(float, match.groups())) for match in INTERVALS.finditer(text)]
    rates = [(int(cap), float(fps)) for cap, fps in PACING.findall(text)]
    if not windows:
        raise ValueError('No completed frame-measurement windows in this log yet')
    count = sum(int(w[6]) for w in windows)
    return {
        'measurement': 'game swap intervals including pacing, not GPU time',
        'windows': len(windows), 'intervals': count,
        'mean_ms': round(sum(w[0]*w[6] for w in windows)/count, 3),
        'median_window_p95_ms': statistics.median(w[2] for w in windows),
        'worst_window_p99_ms': max(w[3] for w in windows),
        'max_ms': max(w[4] for w in windows),
        'over_50ms': sum(int(w[5]) for w in windows),
        'requested_limits': sorted(set(cap for cap, _ in rates)),
        'median_window_fps': statistics.median(fps for _, fps in rates) if rates else None,
        'limits': 'Includes loading and menus. Window percentiles cannot be combined into a session percentile. Compare the same route with the same settings and warm cache.'
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('log', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = analyze(args.log.read_text(encoding='utf-8', errors='replace'))
    rendered = json.dumps(result, indent=2)
    if args.output:
        args.output.write_text(rendered+'\n', encoding='utf-8')
    print(rendered)


if __name__ == '__main__':
    main()
