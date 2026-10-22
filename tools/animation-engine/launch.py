"""Run the installed ComfyUI in a separate TubeFlow runtime/database."""
import argparse
import os
from pathlib import Path
import runpy
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--comfy-root', required=True)
parser.add_argument('--runtime', required=True)
parser.add_argument('--model-paths', required=True)
args = parser.parse_args()
root = Path(args.comfy_root).resolve(strict=True)
models = Path(args.model_paths).resolve(strict=True)
runtime = Path(args.runtime).resolve()
if not (root / 'main.py').is_file():
    raise FileNotFoundError('ComfyUI main.py bulunamadı')
runtime.mkdir(parents=True, exist_ok=True)
for name in ('user', 'input', 'output'):
    (runtime / name).mkdir(exist_ok=True)
log = open(runtime / 'engine.log', 'a', encoding='utf-8', buffering=1)
sys.stdout = log
sys.stderr = log
os.environ.pop('SSLKEYLOGFILE', None)
os.chdir(root)
sys.path.insert(0, str(root))
sys.argv = [str(root / 'main.py'), '--listen', '127.0.0.1', '--port', '8189',
            '--lowvram', '--preview-method', 'none',
            '--user-directory', str(runtime / 'user'),
            '--input-directory', str(runtime / 'input'),
            '--output-directory', str(runtime / 'output'),
            '--database-url', 'sqlite:///' + (runtime / 'engine.db').as_posix(),
            '--extra-model-paths-config', str(models),
            '--disable-all-custom-nodes', '--whitelist-custom-nodes', 'ComfyUI-GGUF']
runpy.run_path(str(root / 'main.py'), run_name='__main__')
