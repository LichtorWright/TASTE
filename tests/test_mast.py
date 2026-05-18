"""Tests for MAST node construction and structure."""

import pytest

from taste.mast.nodes import (
    FieldConcept,
    MethodConcept,
    ParameterConcept,
    ProgramConcept,
    TypeConcept,
    VisibilityConcept,
)


def _public() -> VisibilityConcept:
    return VisibilityConcept(level="public")


def _private() -> VisibilityConcept:
    return VisibilityConcept(level="private")


class TestVisibilityConcept:
    def test_level_stored(self) -> None:
        v = VisibilityConcept(level="public")
        assert v.level == "public"

    def test_different_levels(self) -> None:
        for level in ("public", "private", "protected", "internal"):
            assert VisibilityConcept(level=level).level == level


class TestParameterConcept:
    def test_fields(self) -> None:
        p = ParameterConcept(name="count", type_name="int")
        assert p.name == "count"
        assert p.type_name == "int"


class TestFieldConcept:
    def test_fields(self) -> None:
        vis = _public()
        f = FieldConcept(name="age", type_name="int", visibility=vis)
        assert f.name == "age"
        assert f.type_name == "int"
        assert f.visibility is vis


class TestMethodConcept:
    def test_no_params(self) -> None:
        m = MethodConcept(
            name="get_age",
            parameters=[],
            return_type="int",
            visibility=_public(),
        )
        assert m.name == "get_age"
        assert m.parameters == []
        assert m.return_type == "int"

    def test_with_params(self) -> None:
        param = ParameterConcept(name="to", type_name="string")
        m = MethodConcept(
            name="greet",
            parameters=[param],
            return_type="string",
            visibility=_public(),
        )
        assert len(m.parameters) == 1
        assert m.parameters[0].name == "to"


class TestTypeConcept:
    def test_defaults(self) -> None:
        t = TypeConcept(name="Widget", visibility=_public())
        assert t.name == "Widget"
        assert t.fields == []
        assert t.methods == []
        assert t.parents == []

    def test_with_members(self) -> None:
        field = FieldConcept(name="x", type_name="int", visibility=_public())
        method = MethodConcept(
            name="do_it", parameters=[], return_type="void", visibility=_public()
        )
        t = TypeConcept(
            name="Foo",
            visibility=_public(),
            fields=[field],
            methods=[method],
            parents=["Bar"],
        )
        assert len(t.fields) == 1
        assert len(t.methods) == 1
        assert t.parents == ["Bar"]


class TestProgramConcept:
    def test_empty(self) -> None:
        p = ProgramConcept()
        assert p.types == []

    def test_with_types(self) -> None:
        t = TypeConcept(name="A", visibility=_public())
        p = ProgramConcept(types=[t])
        assert len(p.types) == 1
        assert p.types[0].name == "A"
