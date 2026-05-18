"""Integration tests for the full TASTE pipeline via TasteEngine."""

import pytest

from taste import TasteEngine
from taste.parsers.base import ParseError
from taste.mast.nodes import ProgramConcept
from taste.ast.nodes import ProgramNode


_PERSON_SOURCE = """
// Person type example
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


@pytest.fixture
def engine() -> TasteEngine:
    return TasteEngine()


class TestPipelineStages:
    def test_parse_returns_mast(self, engine: TasteEngine) -> None:
        mast = engine.parse(_PERSON_SOURCE)
        assert isinstance(mast, ProgramConcept)
        assert len(mast.types) == 2

    def test_convert_returns_ast(self, engine: TasteEngine) -> None:
        mast = engine.parse(_PERSON_SOURCE)
        ast = engine.convert(mast)
        assert isinstance(ast, ProgramNode)
        assert len(ast.declarations) == 2

    def test_emit_python(self, engine: TasteEngine) -> None:
        mast = engine.parse(_PERSON_SOURCE)
        ast = engine.convert(mast)
        result = engine.emit(ast, "python")
        assert "class Person:" in result
        assert "class Employee(Person):" in result

    def test_emit_csharp(self, engine: TasteEngine) -> None:
        mast = engine.parse(_PERSON_SOURCE)
        ast = engine.convert(mast)
        result = engine.emit(ast, "csharp")
        assert "public class Person" in result
        assert "public class Employee : Person" in result

    def test_unknown_target_raises(self, engine: TasteEngine) -> None:
        mast = engine.parse("type T {}")
        ast = engine.convert(mast)
        with pytest.raises(ValueError, match="Unknown target"):
            engine.emit(ast, "cobol")


class TestTranspile:
    def test_transpile_to_python(self, engine: TasteEngine) -> None:
        result = engine.transpile(_PERSON_SOURCE, target="python")
        # Fields should be constructor parameters
        assert "def __init__(self, name: str, age: int, secret_id: str)" in result
        # Methods should be stubs
        assert "def greet(self, to: str) -> str:" in result
        assert "def get_age(self" in result
        # Employee inherits from Person
        assert "class Employee(Person):" in result

    def test_transpile_to_csharp(self, engine: TasteEngine) -> None:
        result = engine.transpile(_PERSON_SOURCE, target="csharp")
        assert "public string Name { get; set; }" in result
        assert "public int Age { get; set; }" in result
        assert "public string Greet(string to);" in result
        assert "public class Employee : Person" in result

    def test_transpile_alias_py(self, engine: TasteEngine) -> None:
        result = engine.transpile("type A {}", target="py")
        assert "class A:" in result

    def test_transpile_alias_cs(self, engine: TasteEngine) -> None:
        result = engine.transpile("type A {}", target="cs")
        assert "class A" in result

    def test_parse_error_propagates(self, engine: TasteEngine) -> None:
        with pytest.raises(ParseError):
            engine.transpile("this is not valid", target="python")


class TestAvailableTargets:
    def test_returns_list(self, engine: TasteEngine) -> None:
        targets = TasteEngine.available_targets()
        assert isinstance(targets, list)
        assert "python" in targets
        assert "csharp" in targets

    def test_list_is_sorted(self, engine: TasteEngine) -> None:
        targets = TasteEngine.available_targets()
        assert targets == sorted(targets)
