from pathlib import Path
from PIL import Image

source = Path('/home/ubuntu/tempMOD/assets/tempofus_logo_source.png')
target = Path('/home/ubuntu/tempMOD/assets/tempofus_logo.png')

image = Image.open(source).convert('RGBA')
pixels = image.load()
for y in range(image.height):
    for x in range(image.width):
        red, green, blue, alpha = pixels[x, y]
        # 着色された文字だけを残し、白背景・灰色の圧縮ノイズ・薄い影は透明化する。
        saturation = max(red, green, blue) - min(red, green, blue)
        if saturation < 28:
            pixels[x, y] = (red, green, blue, 0)
        elif saturation < 55:
            coverage = int((saturation - 28) / 27 * 255)
            pixels[x, y] = (red, green, blue, max(0, min(alpha, coverage)))

image.save(target)
print(f'Created {target} ({image.width}x{image.height})')
