"""TubeFlow Edge speech adapter. edge-tts runs in its own Python process."""
import asyncio
import json
import os
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent / "packages"))
import edge_tts
from edge_tts import communicate, voices

# Retain certificate/hostname verification and include the operating system's
# trusted roots (for managed networks) in addition to the packaged CA bundle.
for module in (communicate, voices):
    module._SSL_CTX.load_default_certs()

async def main():
    if sys.argv[1] == "--check":
        print(json.dumps({"Ready": True}))
        return
    request = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
    target = Path(sys.argv[2])
    if request.get("List"):
        voices = await edge_tts.list_voices()
        rows = [{"Id": v["ShortName"], "Name": v["FriendlyName"], "Gender": v.get("Gender", "")}
                for v in voices if v.get("Locale") == "tr-TR"]
        target.write_text(json.dumps(rows, ensure_ascii=False), encoding="utf-8")
        return
    text, voice = request["Text"], request["Voice"]
    if not isinstance(text, str) or not 1 <= len(text) <= 30000:
        raise ValueError("Invalid speech text")
    if voice not in ("tr-TR-AhmetNeural", "tr-TR-EmelNeural"):
        raise ValueError("Select a supported Turkish Edge voice")
    speed = float(request.get("Speed", 1))
    if not .5 <= speed <= 2:
        raise ValueError("Invalid speech rate")
    temporary = Path(str(target) + ".partial")
    try:
        speech = edge_tts.Communicate(text, voice, rate=f"{round((speed-1)*100):+d}%", boundary="WordBoundary")
        words = []
        size = 0
        with temporary.open("wb") as output:
            async for event in speech.stream():
                if event["type"] == "audio":
                    size += len(event["data"])
                    if size > 64 * 1024 * 1024:
                        raise ValueError("Speech response too large")
                    output.write(event["data"])
                elif event["type"] == "WordBoundary":
                    words.append({"Word": event["text"], "Start": event["offset"]/10_000_000,
                                  "End": (event["offset"]+event["duration"])/10_000_000})
        if size < 100:
            raise ValueError("No speech audio received")
        os.replace(temporary, target)
        Path(str(target) + ".words.json").write_text(json.dumps(words, ensure_ascii=False), encoding="utf-8")
    finally:
        temporary.unlink(missing_ok=True)

if __name__ == "__main__":
    asyncio.run(main())
