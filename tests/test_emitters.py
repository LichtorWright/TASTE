"""Tests for the Python and C# emitters."""

import pytest

from taste.ast.nodes import (
    FieldDeclarationNode,
    MethodDeclarationNode,
    ParameterNode,
    ProgramNode,
    TypeDeclarationNode,
    TypeReferenceNode,
)
from taste.emitters.python_emitter import PythonEmitter
from taste.emitters.csharp_emitter import CSharpEmitter


def _ref(name: str) -> TypeReferenceNode:
    return TypeReferenceNode(name=name)


def _field(name: str, type_name: str, visibility: str = "public") -> FieldDeclarationNode:
    return FieldDeclarationNode(name=name, type_ref=_ref(type_name), visibility=visibility)


def _param(name: str, type_name: str) -> ParameterNode:
    return ParameterNode(name=name, type_ref=_ref(type_name))


def _method(
    name: str,
    params: list[ParameterNode] | None = None,
    return_type: str = "void",
    visibility: str = "public",
) -> MethodDeclarationNode:
    return MethodDeclarationNode(
        name=name,
        parameters=params or [],
        return_type=_ref(return_type),
        visibility=visibility,
    )


def _type_decl(
    name: str,
    fields: list[FieldDeclarationNode] | None = None,
    methods: list[MethodDeclarationNode] | None = None,
    base_types: list[TypeReferenceNode] | None = None,
    visibility: str = "public",
) -> TypeDeclarationNode:
    return TypeDeclarationNode(
        name=name,
        visibility=visibility,
        fields=fields or [],
        methods=methods or [],
        base_types=base_types or [],
    )


# ---------------------------------------------------------------------------
# Python emitter tests
# ---------------------------------------------------------------------------


class TestPythonEmitter:
    @pytest.fixture
    def emitter(self) -> PythonEmitter:
        return PythonEmitter()

    def test_empty_program(self, emitter: PythonEmitter) -> None:
        result = emitter.emit(ProgramNode(declarations=[]))
        assert result == ""

    def test_empty_class(self, emitter: PythonEmitter) -> None:
        prog = ProgramNode(declarations=[_type_decl("Widget")])
        result = emitter.emit(prog)
        assert "class Widget:" in result
        assert "pass" in result

    def test_class_with_fields(self, emitter: PythonEmitter) -> None:
        prog = ProgramNode(
            declarations=[
                _type_decl(
                    "Person",
                    fields=[_field("name", "string"), _field("age", "int")],
                )
            ]
        )
        result = emitter.emit(prog)
        assert "class Person:" in result
        assert "def __init__(self, name: str, age: int)" in result
        assert "self.name = name" in result
        assert "self.age = age" in result

    def test_class_with_method(self, emitter: PythonEmitter) -> None:
        prog = ProgramNode(
            declarations=[
                _type_decl(
                    "Greeter",
                    methods=[_method("greet", [_param("to", "string")], "string")],
                )
            ]
        )
        result = emitter.emit(prog)
        assert "def greet(self, to: str) -> str:" in result
        assert "..." in result

    def test_void_return_maps_to_none(self, emitter: PythonEmitter) -> None:
        prog = ProgramNode(
            declarations=[_type_decl("T", methods=[_method("reset")])]
        )
        result = emitter.emit(prog)
        assert "-> None:" in result

    def test_inheritance(self, emitter: PythonEmitter) -> None:
        prog = ProgramNode(
            declarations=[
                _type_decl("Employee", base_types=[_ref("Person")])
            ]
        )
        result = emitter.emit(prog)
        assert "class Employee(Person):" in result

    def test_type_mapping(self, emitter: PythonEmitter) -> None:
        for taste_type, py_type in [
            ("string", "str"),
            ("int", "int"),
            ("integer", "int"),
            ("float", "float"),
            ("double", "float"),
            ("bool", "bool"),
            ("boolean", "bool"),
        ]:
            prog = ProgramNode(
                declarations=[_type_decl("T", fields=[_field("x", taste_type)])]
            )
            result = emitter.emit(prog)
            assert py_type in result, f"Expected {py_type!r} for {taste_type!r}"

    def test_two_types_separated_by_blank_line(self, emitter: PythonEmitter) -> None:
        prog = ProgramNode(
            declarations=[_type_decl("A"), _type_decl("B")]
        )
        result = emitter.emit(prog)
        assert "\n\n" in result
        assert "class A:" in result
        assert "class B:" in result


# ---------------------------------------------------------------------------
# C# emitter tests
# ---------------------------------------------------------------------------


class TestCSharpEmitter:
    @pytest.fixture
    def emitter(self) -> CSharpEmitter:
        return CSharpEmitter()

    def test_empty_program(self, emitter: CSharpEmitter) -> None:
        result = emitter.emit(ProgramNode(declarations=[]))
        assert result == ""

    def test_class_declaration(self, emitter: CSharpEmitter) -> None:
        prog = ProgramNode(declarations=[_type_decl("Widget")])
        result = emitter.emit(prog)
        assert "public class Widget" in result
        assert "{" in result
        assert "}" in result

    def test_field_as_autoproperty(self, emitter: CSharpEmitter) -> None:
        prog = ProgramNode(
            declarations=[
                _type_decl("Person", fields=[_field("name", "string")])
            ]
        )
        result = emitter.emit(prog)
        assert "public string Name { get; set; }" in result

    def test_private_field(self, emitter: CSharpEmitter) -> None:
        prog = ProgramNode(
            declarations=[
                _type_decl("T", fields=[_field("secret", "int", "private")])
            ]
        )
        result = emitter.emit(prog)
        assert "private int Secret { get; set; }" in result

    def test_method_declaration(self, emitter: CSharpEmitter) -> None:
        prog = ProgramNode(
            declarations=[
                _type_decl(
                    "Greeter",
                    methods=[_method("greet", [_param("to", "string")], "string")],
                )
            ]
        )
        result = emitter.emit(prog)
        assert "public string Greet(string to);" in result

    def test_pascal_case_conversion(self, emitter: CSharpEmitter) -> None:
        prog = ProgramNode(
            declarations=[
                _type_decl(
                    "T",
                    fields=[_field("first_name", "string")],
                    methods=[_method("get_full_name", return_type="string")],
                )
            ]
        )
        result = emitter.emit(prog)
        assert "FirstName" in result
        assert "GetFullName" in result

    def test_inheritance(self, emitter: CSharpEmitter) -> None:
        prog = ProgramNode(
            declarations=[
                _type_decl("Employee", base_types=[_ref("Person")])
            ]
        )
        result = emitter.emit(prog)
        assert "public class Employee : Person" in result

    def test_type_mapping(self, emitter: CSharpEmitter) -> None:
        for taste_type, cs_type in [
            ("string", "string"),
            ("int", "int"),
            ("integer", "int"),
            ("float", "float"),
            ("bool", "bool"),
            ("void", "void"),
        ]:
            prog = ProgramNode(
                declarations=[
                    _type_decl("T", methods=[_method("m", return_type=taste_type)])
                ]
            )
            result = emitter.emit(prog)
            assert cs_type in result, f"Expected {cs_type!r} for {taste_type!r}"
