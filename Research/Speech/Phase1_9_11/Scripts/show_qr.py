"""Show a QR code for a review URL: opens PNG viewer + prints compact ASCII.

Usage: python show_qr.py <url> [png_name]
"""
from __future__ import annotations

import sys
import tempfile
from pathlib import Path

import qrcode


def compact_ascii(matrix):
    lines = []
    n = len(matrix)
    for r in range(0, n, 2):
        row = ""
        for c in range(n):
            top = matrix[r][c]
            bot = matrix[r + 1][c] if r + 1 < n else False
            if top and bot:
                row += "\u2588"
            elif top:
                row += "\u2580"
            elif bot:
                row += "\u2584"
            else:
                row += " "
        lines.append(row)
    return "\n".join(lines)


def main():
    url = sys.argv[1]
    name = sys.argv[2] if len(sys.argv) > 2 else "qr.png"
    qr = qrcode.QRCode(border=3, error_correction=qrcode.constants.ERROR_CORRECT_M)
    qr.add_data(url)
    qr.make(fit=True)
    matrix = qr.get_matrix()

    out = Path(tempfile.gettempdir()) / name
    img = qr.make_image(fill_color="black", back_color="white")
    img.save(out)
    print("PNG:", out)
    print("URL:", url)
    print()
    print(compact_ascii(matrix))
    try:
        import os

        os.startfile(str(out))  # noqa: S606
    except Exception as e:
        print("open failed:", e)


if __name__ == "__main__":
    main()
