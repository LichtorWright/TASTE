"""MAST to AST converter."""

from ..mast.nodes import (
    FieldConcept,
    MethodConcept,
    ParameterConcept,
    ProgramConcept,
    TypeConcept,
)
from ..ast.nodes import (
    FieldDeclarationNode,
    MethodDeclarationNode,
    ParameterNode,
    ProgramNode,
    TypeDeclarationNode,
    TypeReferenceNode,
)


class MastToAstConverter:
    """Converts a MAST (Matrix Assisted Syntax Tree) into an AST.

    The MAST captures *what* constructs mean; the AST expresses *how* they are
    structured in a way that emitters can consume to generate target code.
    """

    def convert(self, program: ProgramConcept) -> ProgramNode:
        """Convert a top-level :class:`~taste.mast.nodes.ProgramConcept` to a
        :class:`~taste.ast.nodes.ProgramNode`.
        """
        declarations = [self._convert_type(t) for t in program.types]
        return ProgramNode(declarations=declarations)

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    def _convert_type(self, type_concept: TypeConcept) -> TypeDeclarationNode:
        fields = [self._convert_field(f) for f in type_concept.fields]
        methods = [self._convert_method(m) for m in type_concept.methods]
        base_types = [TypeReferenceNode(name=p) for p in type_concept.parents]
        return TypeDeclarationNode(
            name=type_concept.name,
            visibility=type_concept.visibility.level,
            fields=fields,
            methods=methods,
            base_types=base_types,
        )

    def _convert_field(self, field_concept: FieldConcept) -> FieldDeclarationNode:
        return FieldDeclarationNode(
            name=field_concept.name,
            type_ref=TypeReferenceNode(name=field_concept.type_name),
            visibility=field_concept.visibility.level,
        )

    def _convert_method(
        self, method_concept: MethodConcept
    ) -> MethodDeclarationNode:
        parameters = [self._convert_parameter(p) for p in method_concept.parameters]
        return MethodDeclarationNode(
            name=method_concept.name,
            parameters=parameters,
            return_type=TypeReferenceNode(name=method_concept.return_type),
            visibility=method_concept.visibility.level,
        )

    def _convert_parameter(self, param_concept: ParameterConcept) -> ParameterNode:
        return ParameterNode(
            name=param_concept.name,
            type_ref=TypeReferenceNode(name=param_concept.type_name),
        )
