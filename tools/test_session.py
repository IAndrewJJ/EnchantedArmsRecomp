"""Frame-log summaries use synthetic windows, not game captures."""
from pathlib import Path
import sys
import unittest
sys.path.insert(0,str(Path(__file__).resolve().parent))
from analyze_session import analyze


class SessionTests(unittest.TestCase):
    def test_weighted_mean_and_window_percentiles(self):
        result=analyze('''EA frame pacing: requested_limit=60, submissions_per_second=60.00
EA frame intervals (ms, includes pacing): mean=16.67, p50=16.66, p95=17.00, p99=18.00, max=22.00, over_50ms=0, intervals=300
EA frame pacing: requested_limit=60, submissions_per_second=30.00
EA frame intervals (ms, includes pacing): mean=33.33, p50=30.00, p95=40.00, p99=55.00, max=70.00, over_50ms=2, intervals=150''')
        self.assertEqual(result['intervals'],450)
        self.assertAlmostEqual(result['mean_ms'],22.223,places=3)
        self.assertEqual(result['max_ms'],70)
        self.assertEqual(result['over_50ms'],2)
        self.assertEqual(result['median_window_fps'],45)
        self.assertEqual(result['median_window_p95_ms'],28.5)
    def test_short_log_needs_completed_windows(self):
        with self.assertRaisesRegex(ValueError,'No completed'):analyze('Starting game')


if __name__=='__main__':unittest.main()
