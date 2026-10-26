# -*- coding: utf-8 -*-
"""T-CHAR-UVRB: совместимая точка входа; реализация v2 обрабатывает модели, не только персонажей.

Старый destructive алгоритм доступен в истории Git (dd07e70a), но не вызывается автоматически.
Новый контракт и ограничения: docs/Character/TextureRebake/README.md.
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from rebake_model import main

if __name__ == '__main__':
    main()
