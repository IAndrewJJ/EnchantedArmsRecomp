"""Filesystem paths for deep portable installations without system policy changes."""
import os
from pathlib import Path


def filesystem_path(path):
    path=Path(path).absolute()
    if os.name=='nt':
        text=str(path)
        if not text.startswith('\\\\?\\'):
            text='\\\\?\\UNC\\'+text[2:] if text.startswith('\\\\') else '\\\\?\\'+text
        return Path(text)
    return path
