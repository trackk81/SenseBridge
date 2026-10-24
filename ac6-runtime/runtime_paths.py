"""Resolve native dependencies from the application, never a developer path."""
import os
from pathlib import Path

def application_root():
    value = os.environ.get('SENSEBRIDGE_APP_ROOT')
    if not value:
        raise RuntimeError('SenseBridge must launch the AC6 runtime')
    root = Path(value).resolve()
    for name in ('SenseBridge.Events.exe', 'SDL3.dll'):
        if not (root / name).is_file():
            raise RuntimeError('Missing application dependency: ' + name)
    return root
