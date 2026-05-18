"""Python emitter — generates Python source code from an AST."""

from __future__ import annotations

from ..ast.nodes import (
    FieldDeclarationNode,
    MethodDeclarationNode,
    ProgramNode,
    TypeDeclarationNode,
)
from .base import BaseEmitter

# Mapping from TASTE/source type names to Python type annotations.
_TYPE_MAP: dict[str, str] = {
    "string": "str",
    "str": "str",
    "int": "int",
    "integer": "int",
    "float": "float",
    "double": "float",
    "bool": "bool",
    "boolean": "bool",
    "void": "None",
}


class PythonEmitter(BaseEmitter):
    """Emits Python 3 source code from a TASTE AST.

    Generated output uses type annotations and produces a class with an
    ``__init__`` method for fields and stub methods (``...``) for declared
    methods.
    """

    def emit(self, program: ProgramNode) -> str:
        """Return Python source code for all declarations in *program*."""
        parts = [self._emit_type(decl) for decl in program.declarations]
        return "\n\n".join(parts)

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    def _map_type(self, type_name: str) -> str:
        return _TYPE_MAP.get(type_name.lower(), type_name)

    def _emit_type(self, type_decl: TypeDeclarationNode) -> str:
        lines: list[str] = []

        if type_decl.base_types:
            bases = ", ".join(b.name for b in type_decl.base_types)
            lines.append(f"class {type_decl.name}({bases}):")
        else:
            lines.append(f"class {type_decl.name}:")

        if type_decl.fields:
            lines.extend(self._emit_init(type_decl.fields))
        elif not type_decl.methods:
            lines.append("    pass")

        for method in type_decl.methods:
            lines.append("")
            lines.extend(self._emit_method(method))

        return "\n".join(lines)

    def _emit_init(self, fields: list[FieldDeclarationNode]) -> list[str]:
        params = ", ".join(
            f"{f.name}: {self._map_type(f.type_ref.name)}" for f in fields
        )
        lines = [f"    def __init__(self, {params}) -> None:"]
        for f in fields:
            lines.append(f"        self.{f.name} = {f.name}")
        return lines

    def _emit_method(self, method: MethodDeclarationNode) -> list[str]:
        params = ["self"] + [
            f"{p.name}: {self._map_type(p.type_ref.name)}"
            for p in method.parameters
        ]
        return_type = self._map_type(method.return_type.name)
        signature = f"    def {method.name}({', '.join(params)}) -> {return_type}:"
        return [signature, "        ..."]
