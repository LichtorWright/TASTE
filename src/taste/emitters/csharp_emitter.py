"""C# emitter — generates C# source code from an AST."""

from __future__ import annotations

from ..ast.nodes import (
    FieldDeclarationNode,
    MethodDeclarationNode,
    ProgramNode,
    TypeDeclarationNode,
)
from .base import BaseEmitter

# Mapping from TASTE/source type names to C# type names.
_TYPE_MAP: dict[str, str] = {
    "string": "string",
    "str": "string",
    "int": "int",
    "integer": "int",
    "float": "float",
    "double": "double",
    "bool": "bool",
    "boolean": "bool",
    "void": "void",
}

_VISIBILITY_MAP: dict[str, str] = {
    "public": "public",
    "private": "private",
    "protected": "protected",
    "internal": "internal",
}


class CSharpEmitter(BaseEmitter):
    """Emits C# source code from a TASTE AST.

    Fields are emitted as auto-properties (``{ get; set; }``). Methods are
    emitted as abstract-style declarations (no body).
    """

    def emit(self, program: ProgramNode) -> str:
        """Return C# source code for all declarations in *program*."""
        parts = [self._emit_type(decl) for decl in program.declarations]
        return "\n\n".join(parts)

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    def _map_type(self, type_name: str) -> str:
        return _TYPE_MAP.get(type_name.lower(), type_name)

    def _map_visibility(self, visibility: str) -> str:
        return _VISIBILITY_MAP.get(visibility, "public")

    @staticmethod
    def _pascal_case(name: str) -> str:
        """Convert ``snake_case`` to ``PascalCase``."""
        return "".join(word.capitalize() for word in name.split("_"))

    def _emit_type(self, type_decl: TypeDeclarationNode) -> str:
        lines: list[str] = []
        vis = self._map_visibility(type_decl.visibility)

        if type_decl.base_types:
            bases = ", ".join(b.name for b in type_decl.base_types)
            lines.append(f"{vis} class {type_decl.name} : {bases}")
        else:
            lines.append(f"{vis} class {type_decl.name}")

        lines.append("{")

        for field in type_decl.fields:
            lines.append(self._emit_field(field))

        if type_decl.fields and type_decl.methods:
            lines.append("")

        for method in type_decl.methods:
            lines.append(self._emit_method(method))

        lines.append("}")
        return "\n".join(lines)

    def _emit_field(self, field: FieldDeclarationNode) -> str:
        vis = self._map_visibility(field.visibility)
        field_type = self._map_type(field.type_ref.name)
        prop_name = self._pascal_case(field.name)
        return f"    {vis} {field_type} {prop_name} {{ get; set; }}"

    def _emit_method(self, method: MethodDeclarationNode) -> str:
        vis = self._map_visibility(method.visibility)
        return_type = self._map_type(method.return_type.name)
        params = ", ".join(
            f"{self._map_type(p.type_ref.name)} {p.name}"
            for p in method.parameters
        )
        method_name = self._pascal_case(method.name)
        return f"    {vis} {return_type} {method_name}({params});"
