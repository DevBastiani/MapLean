import unittest

import bench_readme


def row(method, mean, error, ratio, ratio_sd, allocated="504 B"):
    return {
        "Method": method,
        "Mean": f"{mean} ns",
        "Error": f"{error} ns",
        "StdDev": "1 ns",
        "Ratio": str(ratio),
        "RatioSD": str(ratio_sd),
        "Allocated": allocated,
    }


def rows(lean, auto=(250.0, 5.0, 2.0, 0.05)):
    return [
        row("Manual", 100.0, 2.0, 1.00, 0.00),
        row("MapLean", *lean),
        row("AutoMapper", *auto),
    ]


class SpeedSentence(unittest.TestCase):
    def test_overlapping_is_the_same(self):
        sentence = bench_readme.speed_sentence(rows((101.0, 2.0, 1.01, 0.02)))
        self.assertIn("the same speed as the hand-written map", sentence)

    def test_separated_within_two_ratio_sd_is_the_same(self):
        sentence = bench_readme.speed_sentence(rows((95.0, 1.0, 0.95, 0.04)))
        self.assertIn("the same speed as the hand-written map", sentence)

    def test_clearly_faster(self):
        sentence = bench_readme.speed_sentence(rows((80.0, 1.0, 0.80, 0.02)))
        self.assertIn("faster than the hand-written map", sentence)

    def test_clearly_slower(self):
        sentence = bench_readme.speed_sentence(rows((120.0, 1.0, 1.20, 0.02)))
        self.assertIn("slower than the hand-written map", sentence)

    def test_automapper_round_factor(self):
        sentence = bench_readme.speed_sentence(rows((100.0, 2.0, 1.00, 0.02), (200.0, 2.0, 2.0, 0.02)))
        self.assertTrue(sentence.endswith("about 2x faster than AutoMapper."))

    def test_automapper_other_factor(self):
        sentence = bench_readme.speed_sentence(rows((100.0, 2.0, 1.00, 0.02), (240.0, 2.0, 2.4, 0.02)))
        self.assertIn("2.40 times the speed of AutoMapper", sentence)

    def test_allocation_difference_is_reported(self):
        data = rows((100.0, 2.0, 1.00, 0.02))
        data[1]["Allocated"] = "400 B"
        self.assertIn("Manual allocates 504 B and MapLean allocates 400 B.", bench_readme.speed_sentence(data))


if __name__ == "__main__":
    unittest.main()
