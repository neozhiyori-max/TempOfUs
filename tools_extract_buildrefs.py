from pathlib import Path
from shutil import rmtree
from zipfile import ZipFile

archive_path = Path('/tmp/tempmod_buildrefs.zip')
target = Path('/home/ubuntu/tempMOD/game_refs/AU')
if target.exists():
    rmtree(target)

with ZipFile(archive_path) as archive:
    for entry in archive.infolist():
        if entry.is_dir():
            continue
        normalized = entry.filename.replace('\\', '/')
        destination = target / normalized
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(archive.read(entry))

required = [
    target / 'BepInEx/core/BepInEx.Core.dll',
    target / 'BepInEx/interop/Assembly-CSharp.dll',
]
for path in required:
    if not path.exists():
        raise RuntimeError(f'Missing expected reference: {path}')
print('REFERENCES_READY')
