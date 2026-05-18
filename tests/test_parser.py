"""Tests for the TASTE source-language parser."""

import pytest

from taste.parsers.base import ParseError
from taste.parsers.taste_parser import TasteParser
from taste.mast.nodes import (
    FieldConcept,
    MethodConcept,
    ProgramConcept,
    TypeConcept,
)


@pytest.fixture
def parser() -> TasteParser:
    return TasteParser()


class TestEmptyProgram:
    def test_empty_source(self, parser: TasteParser) -> None:
        result = parser.parse("")
        assert isinstance(result, ProgramConcept)
        assert result.types == []

    def test_comment_only(self, parser: TasteParser) -> None:
        result = parser.parse("// just a comment\n")
        assert result.types == []


class TestTypeDeclaration:
    def test_empty_type(self, parser: TasteParser) -> None:
        result = parser.parse("type Widget {}")
        assert len(result.types) == 1
        t = result.types[0]
        assert isinstance(t, TypeConcept)
        assert t.name == "Widget"
        assert t.fields == []
        assert t.methods == []
        assert t.parents == []

    def test_type_with_inheritance(self, parser: TasteParser) -> None:
        result = parser.parse("type Employee extends Person {}")
        t = result.types[0]
        assert t.name == "Employee"
        assert t.parents == ["Person"]

    def test_multiple_types(self, parser: TasteParser) -> None:
        src = "type A {}\ntype B {}"
        result = parser.parse(src)
        assert len(result.types) == 2
        assert result.types[0].name == "A"
        assert result.types[1].name == "B"


class TestFieldParsing:
    def test_public_field(self, parser: TasteParser) -> None:
        result = parser.parse("type T { public name: string }")
        field = result.types[0].fields[0]
        assert isinstance(field, FieldConcept)
        assert field.name == "name"
        assert field.type_name == "string"
        assert field.visibility.level == "public"

    def test_private_field(self, parser: TasteParser) -> None:
        result = parser.parse("type T { private secret: int }")
        field = result.types[0].fields[0]
        assert field.visibility.level == "private"

    def test_default_visibility_is_public(self, parser: TasteParser) -> None:
        result = parser.parse("type T { name: string }")
        field = result.types[0].fields[0]
        assert field.visibility.level == "public"

    def test_multiple_fields(self, parser: TasteParser) -> None:
        src = "type T { public a: int  public b: string  private c: float }"
        result = parser.parse(src)
        fields = result.types[0].fields
        assert len(fields) == 3
        assert [f.name for f in fields] == ["a", "b", "c"]
        assert [f.type_name for f in fields] == ["int", "string", "float"]


class TestMethodParsing:
    def test_void_method_no_params(self, parser: TasteParser) -> None:
        result = parser.parse("type T { public fn reset() }")
        method = result.types[0].methods[0]
        assert isinstance(method, MethodConcept)
        assert method.name == "reset"
        assert method.parameters == []
        assert method.return_type == "void"

    def test_method_with_return_type(self, parser: TasteParser) -> None:
        result = parser.parse("type T { public fn get_age() -> int }")
        method = result.types[0].methods[0]
        assert method.return_type == "int"

    def test_method_with_params(self, parser: TasteParser) -> None:
        result = parser.parse("type T { public fn greet(to: string) -> string }")
        method = result.types[0].methods[0]
        assert len(method.parameters) == 1
        assert method.parameters[0].name == "to"
        assert method.parameters[0].type_name == "string"

    def test_method_with_multiple_params(self, parser: TasteParser) -> None:
        src = "type T { public fn add(a: int, b: int) -> int }"
        result = parser.parse(src)
        method = result.types[0].methods[0]
        assert len(method.parameters) == 2
        assert method.parameters[0].name == "a"
        assert method.parameters[1].name == "b"

    def test_method_visibility(self, parser: TasteParser) -> None:
        result = parser.parse("type T { private fn secret() }")
        method = result.types[0].methods[0]
        assert method.visibility.level == "private"


class TestParseErrors:
    def test_unexpected_token(self, parser: TasteParser) -> None:
        with pytest.raises(ParseError):
            parser.parse("unexpected garbage")

    def test_missing_name(self, parser: TasteParser) -> None:
        with pytest.raises(ParseError):
            parser.parse("type {}")

    def test_missing_brace(self, parser: TasteParser) -> None:
        with pytest.raises(ParseError):
            parser.parse("type T {")

    def test_malformed_field(self, parser: TasteParser) -> None:
        with pytest.raises(ParseError):
            parser.parse("type T { public name }")


class TestFullExample:
    _SOURCE = """
    // A simple person type
    type Person {
        public name: string
        public age: int
        private secret_id: string

        public fn greet(to: string) -> string
        public fn get_age() -> int
    }

    type Employee extends Person {
        public company: string
        public fn get_salary() -> float
    }
    """

    def test_two_types_parsed(self, parser: TasteParser) -> None:
        result = parser.parse(self._SOURCE)
        assert len(result.types) == 2

    def test_person_fields(self, parser: TasteParser) -> None:
        result = parser.parse(self._SOURCE)
        person = result.types[0]
        assert person.name == "Person"
        assert len(person.fields) == 3
        assert len(person.methods) == 2

    def test_employee_inherits(self, parser: TasteParser) -> None:
        result = parser.parse(self._SOURCE)
        employee = result.types[1]
        assert employee.name == "Employee"
        assert employee.parents == ["Person"]
        assert len(employee.fields) == 1
        assert len(employee.methods) == 1
