from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
import math
import requests

url = 'https://dotnetcli.azureedge.net/dotnet/Sdk/6.0.428/dotnet-sdk-6.0.428-linux-x64.tar.gz'
target = Path('/tmp/dotnet-sdk-6.0.428-linux-x64.tar.gz')
part_dir = Path('/tmp/dotnet-sdk-6.0.428-parts')
workers = 8

head = requests.head(url, timeout=30)
head.raise_for_status()
size = int(head.headers['Content-Length'])
chunk = math.ceil(size / workers)
part_dir.mkdir(parents=True, exist_ok=True)

def fetch(index: int) -> tuple[int, int]:
    start = index * chunk
    end = min(size - 1, start + chunk - 1)
    part = part_dir / f'part-{index:02d}'
    expected = end - start + 1
    current = part.stat().st_size if part.exists() else 0
    if current == expected:
        return index, expected
    if current > expected:
        part.unlink()
        current = 0

    for attempt in range(1, 5):
        resume_from = start + current
        try:
            response = requests.get(url, headers={'Range': f'bytes={resume_from}-{end}'}, stream=True, timeout=90)
            response.raise_for_status()
            with part.open('ab') as output:
                for block in response.iter_content(chunk_size=1024 * 1024):
                    if block:
                        output.write(block)
                        current += len(block)
            if current == expected:
                return index, expected
        except requests.RequestException as exception:
            print(f'part {index}, retry {attempt}: {exception}', flush=True)

    raise RuntimeError(f'part {index}: expected {expected}, got {current}')

with ThreadPoolExecutor(max_workers=workers) as executor:
    futures = [executor.submit(fetch, index) for index in range(workers)]
    for future in as_completed(futures):
        index, bytes_written = future.result()
        print(f'completed part {index}: {bytes_written} bytes', flush=True)

with target.open('wb') as output:
    for index in range(workers):
        output.write((part_dir / f'part-{index:02d}').read_bytes())

if target.stat().st_size != size:
    raise RuntimeError(f'archive size mismatch: expected {size}, got {target.stat().st_size}')
print(f'created {target} ({size} bytes)')
