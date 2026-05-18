"""TASTE — Transpiling Abstract Syntax Tree Emission.

A universal compiler engine that operates above language syntax by:

1. Parsing source code into a MAST (Matrix Assisted Syntax Tree) — the
   conceptual model.
2. Converting MAST into a language-agnostic AST.
3. Using emitters to generate code in any supported target language.

Pipeline::

    Source Parser → MAST → AST → Emitter → Target Code

Quick start::

    from taste import TasteEngine

    engine = TasteEngine()
    python_code = engine.transpile(source, target="python")
"""

from .engine import TasteEngine
from .parsers.base import ParseError
from .parsers.taste_parser import TasteParser
from .pipeline.converter import MastToAstConverter
from .emitters.python_emitter import PythonEmitter
from .emitters.csharp_emitter import CSharpEmitter

__all__ = [
    "TasteEngine",
    "ParseError",
    "TasteParser",
    "MastToAstConverter",
    "PythonEmitter",
    "CSharpEmitter",
]
