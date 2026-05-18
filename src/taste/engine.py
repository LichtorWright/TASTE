"""TASTE Engine — orchestrates the full compilation pipeline.

The pipeline is:

.. code-block:: text

    Source Parser → MAST → AST → Emitter → Target Code

Usage::

    from taste import TasteEngine

    engine = TasteEngine()
    python_code = engine.transpile(source, target="python")
    csharp_code = engine.transpile(source, target="csharp")
"""

from __future__ import annotations

from typing import Dict, List, Type

from .ast.nodes import ProgramNode
from .emitters.base import BaseEmitter
from .emitters.csharp_emitter import CSharpEmitter
from .emitters.python_emitter import PythonEmitter
from .mast.nodes import ProgramConcept
from .parsers.taste_parser import TasteParser
from .pipeline.converter import MastToAstConverter

# Registry of available target-language emitters.
_EMITTERS: Dict[str, Type[BaseEmitter]] = {
    "python": PythonEmitter,
    "py": PythonEmitter,
    "csharp": CSharpEmitter,
    "cs": CSharpEmitter,
}


class TasteEngine:
    """Orchestrates the full TASTE compilation pipeline.

    The engine ties together the parser, MAST→AST converter, and emitters so
    that callers only need to provide source text and a target language name.

    Example::

        engine = TasteEngine()
        result = engine.transpile(source_code, target="python")
    """

    def __init__(self) -> None:
        self._parser = TasteParser()
        self._converter = MastToAstConverter()

    # ------------------------------------------------------------------
    # Pipeline stages — exposed individually for inspection / testing
    # ------------------------------------------------------------------

    def parse(self, source: str) -> ProgramConcept:
        """Parse *source* code into a MAST :class:`~taste.mast.nodes.ProgramConcept`.

        This is the first stage of the pipeline.
        """
        return self._parser.parse(source)

    def convert(self, mast: ProgramConcept) -> ProgramNode:
        """Convert a MAST *mast* into a language-agnostic AST
        :class:`~taste.ast.nodes.ProgramNode`.

        This is the second stage of the pipeline.
        """
        return self._converter.convert(mast)

    def emit(self, ast: ProgramNode, target: str) -> str:
        """Emit target-language code from *ast* using the named *target* emitter.

        Args:
            ast: The program AST to emit.
            target: Target language identifier (e.g. ``'python'``, ``'csharp'``).

        Returns:
            Generated source code as a string.

        Raises:
            ValueError: If *target* is not a known emitter.
        """
        emitter_class = _EMITTERS.get(target.lower())
        if emitter_class is None:
            available = ", ".join(sorted(_EMITTERS))
            raise ValueError(
                f"Unknown target {target!r}. Available targets: {available}"
            )
        return emitter_class().emit(ast)

    # ------------------------------------------------------------------
    # Convenience: full pipeline in one call
    # ------------------------------------------------------------------

    def transpile(self, source: str, target: str) -> str:
        """Run the complete pipeline: source → MAST → AST → target code.

        Args:
            source: TASTE source language text.
            target: Target language identifier (e.g. ``'python'``, ``'csharp'``).

        Returns:
            Generated target-language source code.
        """
        mast = self.parse(source)
        ast = self.convert(mast)
        return self.emit(ast, target)

    # ------------------------------------------------------------------
    # Introspection
    # ------------------------------------------------------------------

    @staticmethod
    def available_targets() -> List[str]:
        """Return a sorted list of available target language identifiers."""
        return sorted(_EMITTERS)
