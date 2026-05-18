"""MAST node definitions.

MAST (Matrix Assisted Syntax Tree) nodes represent the *conceptual* meaning
of constructs, divorced from any particular language's syntax. Different
languages may use different keywords — C# says ``class``, Rust says ``struct``,
Go says ``type`` — but they all express the same underlying concept: an object
type with fields and methods. MAST captures that shared concept.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import List


@dataclass
class MastNode:
    """Base class for all MAST nodes."""


@dataclass
class VisibilityConcept(MastNode):
    """Concept of visibility / access control.

    Captures the idea that a member or type is accessible at some scope,
    without committing to a specific keyword (``public``, ``pub``, ``export``).

    Attributes:
        level: One of ``'public'``, ``'private'``, ``'protected'``, or
            ``'internal'``.
    """

    level: str


@dataclass
class ParameterConcept(MastNode):
    """Concept of a callable parameter.

    Attributes:
        name: The parameter name.
        type_name: The declared type of the parameter.
    """

    name: str
    type_name: str


@dataclass
class FieldConcept(MastNode):
    """Concept of a data member / field.

    Represents the idea that an object type holds a named piece of data with
    a specific type and access level, regardless of how any particular
    language spells that idea.

    Attributes:
        name: The field name.
        type_name: The declared type of the field.
        visibility: The access-control concept for this field.
    """

    name: str
    type_name: str
    visibility: VisibilityConcept


@dataclass
class MethodConcept(MastNode):
    """Concept of a callable member / method.

    Represents the idea that an object type exposes a named operation that
    accepts zero or more parameters and returns a result.

    Attributes:
        name: The method name.
        parameters: Ordered list of parameter concepts.
        return_type: The name of the return type (``'void'`` when nothing is
            returned).
        visibility: The access-control concept for this method.
    """

    name: str
    parameters: List[ParameterConcept]
    return_type: str
    visibility: VisibilityConcept


@dataclass
class TypeConcept(MastNode):
    """Concept of an object-type definition.

    This is the core MAST concept. Different languages have different words
    for it — *class*, *struct*, *record*, *type*, *CodeHolder* — but the
    underlying idea is the same: a named aggregate that has fields and
    methods.

    Attributes:
        name: The type name.
        visibility: The access-control concept for this type.
        fields: The fields declared on this type.
        methods: The methods declared on this type.
        parents: Names of base types (for inheritance / composition).
    """

    name: str
    visibility: VisibilityConcept
    fields: List[FieldConcept] = field(default_factory=list)
    methods: List[MethodConcept] = field(default_factory=list)
    parents: List[str] = field(default_factory=list)


@dataclass
class ProgramConcept(MastNode):
    """Top-level container for a parsed program's conceptual model.

    Attributes:
        types: The type concepts declared in the program.
    """

    types: List[TypeConcept] = field(default_factory=list)
