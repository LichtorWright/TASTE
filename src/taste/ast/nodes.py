"""AST node definitions.

The AST (Abstract Syntax Tree) is the language-agnostic structured
representation produced by converting a MAST. Emitters consume the AST to
generate source code in a specific target language.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import List


@dataclass
class AstNode:
    """Base class for all AST nodes."""


@dataclass
class TypeReferenceNode(AstNode):
    """A reference to a type by name.

    Attributes:
        name: The type name as it appears in the source concept (e.g.
            ``'string'``, ``'int'``, ``'Person'``). Emitters are responsible
            for mapping this to the target language's type system.
    """

    name: str


@dataclass
class ParameterNode(AstNode):
    """A method / function parameter.

    Attributes:
        name: The parameter name.
        type_ref: Reference to the parameter's type.
    """

    name: str
    type_ref: TypeReferenceNode


@dataclass
class FieldDeclarationNode(AstNode):
    """Declaration of a data field / member.

    Attributes:
        name: The field name.
        type_ref: Reference to the field's type.
        visibility: Access level (``'public'``, ``'private'``, etc.).
    """

    name: str
    type_ref: TypeReferenceNode
    visibility: str


@dataclass
class MethodDeclarationNode(AstNode):
    """Declaration of a method / function.

    Attributes:
        name: The method name.
        parameters: Ordered list of parameter nodes.
        return_type: Reference to the return type.
        visibility: Access level (``'public'``, ``'private'``, etc.).
    """

    name: str
    parameters: List[ParameterNode]
    return_type: TypeReferenceNode
    visibility: str


@dataclass
class TypeDeclarationNode(AstNode):
    """Declaration of an object type (class / struct / record / etc.).

    Attributes:
        name: The type name.
        visibility: Access level for the type itself.
        fields: Field declarations belonging to this type.
        methods: Method declarations belonging to this type.
        base_types: Type references for inherited / composed types.
    """

    name: str
    visibility: str
    fields: List[FieldDeclarationNode] = field(default_factory=list)
    methods: List[MethodDeclarationNode] = field(default_factory=list)
    base_types: List[TypeReferenceNode] = field(default_factory=list)


@dataclass
class ProgramNode(AstNode):
    """Top-level program node containing all declarations.

    Attributes:
        declarations: All type declarations in the program.
    """

    declarations: List[TypeDeclarationNode] = field(default_factory=list)
