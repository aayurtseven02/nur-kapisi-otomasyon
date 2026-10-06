"""
Zamanlayıcı: config/schedule.yaml içindeki "programı" tarar, yayın zamanı
yaklaşmış (lead_time_hours içinde) ve durumu "pending" olan görevleri bulur,
pipeline.run_entry ile üretip YouTube'a yükler, sonra durumu günceller.

Not: Videoyu hemen YouTube'da HERKESE AÇIK yapmıyoruz. Video "private" olarak
yüklenir ve status.publishAt = tam istenen saat olarak ayarlanır. Böylece asıl
"saati gelince yayına alma" işini YouTube'un kendi sunucuları yapar; bizim
GitHub Actions görevimizin dakikası dakikasına doğru zamanda çalışması
gerekmez — sadece yayın saatinden ÖNCE (lead_time_hours kadar erken) video
hazır olup YouTube'a yüklenmiş olsun yeterlidir.

Bu script GitHub Actions'ta periyodik (ör. her 30 dakikada bir) çalıştırılır.
İşlenen görevlerin durumu schedule.yaml içinde güncellenir; workflow bu
değişikliği repoya geri commit'ler (bkz .github/workflows/publish.yml).
"""
from __future__ import annotations
import os
import sys
import datetime
import yaml
import traceback

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from src.pipeline import load_settings, run_entry

ROOT = os.path.join(os.path.dirname(__file__), "..")
SCHEDULE_PATH = os.path.join(ROOT, "config", "schedule.yaml")
OUTPUT_DIR = os.path.join(ROOT, "output")

DEFAULT_LEAD_TIME_HOURS = 3


def _load_schedule():
    with open(SCHEDULE_PATH, "r", encoding="utf-8") as f:
        return yaml.safe_load(f)


def _save_schedule(data):
    with open(SCHEDULE_PATH, "w", encoding="utf-8") as f:
        yaml.safe_dump(data, f, allow_unicode=True, sort_keys=False)


def find_due_entries(schedule: dict, lead_time_hours: float = DEFAULT_LEAD_TIME_HOURS):
    now = datetime.datetime.now(datetime.timezone.utc)
    cutoff = now + datetime.timedelta(hours=lead_time_hours)
    due = []
    for entry in schedule.get("videos", []):
        if entry.get("status") != "pending":
            continue
        publish_at = datetime.datetime.fromisoformat(entry["publish_at"])
        if publish_at <= cutoff:
            due.append(entry)
    due.sort(key=lambda e: e["publish_at"])
    return due


def run_once(upload: bool = True, lead_time_hours: float = DEFAULT_LEAD_TIME_HOURS, max_videos: int = 1):
    """Tek seferlik tarama: en fazla max_videos görevi işler (GitHub Actions
    runner süresini aşmamak için varsayılan olarak her çalıştırmada 1 video)."""
    settings = load_settings()
    schedule = _load_schedule()
    due_entries = find_due_entries(schedule, lead_time_hours)

    if not due_entries:
        print("[scheduler] Zamanı gelen görev yok.")
        return []

    results = []
    for entry in due_entries[:max_videos]:
        print(f"[scheduler] İşleniyor: {entry['id']} (yayın zamanı: {entry['publish_at']})")
        try:
            res = run_entry(entry, settings, OUTPUT_DIR, upload=upload,
                             schedule_entries=schedule.get("videos", []))
            entry["status"] = "done"
            if upload and "youtube" in res:
                entry["youtube_video_id"] = res["youtube"].video_id
                entry["youtube_url"] = res["youtube"].url
                print(f"[scheduler] Yüklendi: {res['youtube'].url}")
            else:
                print(f"[scheduler] Yerel üretim tamam: {res['video_path']}")
            results.append((entry["id"], "done"))
        except Exception as e:
            entry["status"] = "failed"
            entry["error"] = str(e)
            print(f"[scheduler] HATA ({entry['id']}): {e}")
            traceback.print_exc()
            results.append((entry["id"], "failed"))

    _save_schedule(schedule)
    return results


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument("--no-upload", action="store_true", help="YouTube'a yüklemeden sadece üret (test)")
    parser.add_argument("--lead-time-hours", type=float, default=DEFAULT_LEAD_TIME_HOURS)
    parser.add_argument("--max-videos", type=int, default=1)
    args = parser.parse_args()

    run_once(upload=not args.no_upload, lead_time_hours=args.lead_time_hours, max_videos=args.max_videos)
