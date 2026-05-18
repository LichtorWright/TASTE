"""Tests for the MAST → AST converter."""

import pytest

from taste.mast.nodes import (
    FieldConcept,
    MethodConcept,
    ParameterConcept,
    ProgramConcept,
    TypeConcept,
    VisibilityConcept,
)
from taste.ast.nodes import (
    FieldDeclarationNode,
    MethodDeclarationNode,
    ParameterNode,
    ProgramNode,
    TypeDeclarationNode,
    TypeReferenceNode,
)
from taste.pipeline.converter import MastToAstConverter


def _pub() -> VisibilityConcept:
    return VisibilityConcept(level="public")


def _priv() -> VisibilityConcept:
    return VisibilityConcept(level="private")


@pytest.fixture
def converter() -> MastToAstConverter:
    return MastToAstConverter()


class TestEmptyProgram:
    def test_empty_program(self, converter: MastToAstConverter) -> None:
        mast = ProgramConcept(types=[])
        ast = converter.convert(mast)
        assert isinstance(ast, ProgramNode)
        assert ast.declarations == []


class TestTypeConversion:
    def test_simple_type(self, converter: MastToAstConverter) -> None:
        mast = ProgramConcept(
            types=[TypeConcept(name="Widget", visibility=_pub())]
        )
        ast = converter.convert(mast)
        assert len(ast.declarations) == 1
        decl = ast.declarations[0]
        assert isinstance(decl, TypeDeclarationNode)
        assert decl.name == "Widget"
        assert decl.visibility == "public"

    def test_type_with_parents(self, converter: MastToAstConverter) -> None:
        mast = ProgramConcept(
            types=[
                TypeConcept(
                    name="Employee",
                    visibility=_pub(),
                    parents=["Person"],
                )
            ]
        )
        ast = converter.convert(mast)
        decl = ast.declarations[0]
        assert len(decl.base_types) == 1
        assert isinstance(decl.base_types[0], TypeReferenceNode)
        assert decl.base_types[0].name == "Person"

    def test_multiple_types(self, converter: MastToAstConverter) -> None:
        mast = ProgramConcept(
            types=[
                TypeConcept(name="A", visibility=_pub()),
                TypeConcept(name="B", visibility=_pub()),
            ]
        )
        ast = converter.convert(mast)
        assert len(ast.declarations) == 2
        assert ast.declarations[0].name == "A"
        assert ast.declarations[1].name == "B"


class TestFieldConversion:
    def test_field_converted(self, converter: MastToAstConverter) -> None:
        field_concept = FieldConcept(name="age", type_name="int", visibility=_pub())
        mast = ProgramConcept(
            types=[TypeConcept(name="T", visibility=_pub(), fields=[field_concept])]
        )
        ast = converter.convert(mast)
        field = ast.declarations[0].fields[0]
        assert isinstance(field, FieldDeclarationNode)
        assert field.name == "age"
        assert isinstance(field.type_ref, TypeReferenceNode)
        assert field.type_ref.name == "int"
        assert field.visibility == "public"

    def test_private_field_visibility(self, converter: MastToAstConverter) -> None:
        field_concept = FieldConcept(
            name="secret", type_name="string", visibility=_priv()
        )
        mast = ProgramConcept(
            types=[TypeConcept(name="T", visibility=_pub(), fields=[field_concept])]
        )
        ast = converter.convert(mast)
        field = ast.declarations[0].fields[0]
        assert field.visibility == "private"


class TestMethodConversion:
    def test_method_no_params(self, converter: MastToAstConverter) -> None:
        method_concept = MethodConcept(
            name="reset",
            parameters=[],
            return_type="void",
            visibility=_pub(),
        )
        mast = ProgramConcept(
            types=[
                TypeConcept(name="T", visibility=_pub(), methods=[method_concept])
            ]
        )
        ast = converter.convert(mast)
        method = ast.declarations[0].methods[0]
        assert isinstance(method, MethodDeclarationNode)
        assert method.name == "reset"
        assert method.parameters == []
        assert method.return_type.name == "void"

    def test_method_with_params(self, converter: MastToAstConverter) -> None:
        param = ParameterConcept(name="x", type_name="int")
        method_concept = MethodConcept(
            name="scale",
            parameters=[param],
            return_type="float",
            visibility=_pub(),
        )
        mast = ProgramConcept(
            types=[
                TypeConcept(name="T", visibility=_pub(), methods=[method_concept])
            ]
        )
        ast = converter.convert(mast)
        method = ast.declarations[0].methods[0]
        assert len(method.parameters) == 1
        p = method.parameters[0]
        assert isinstance(p, ParameterNode)
        assert p.name == "x"
        assert p.type_ref.name == "int"
