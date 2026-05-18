"""TASTE source-language parser.

This module provides a lexer and recursive-descent parser for the TASTE
source language — a minimal, language-neutral syntax designed to express
type definitions, fields, and methods in a way that TASTE can convert into
its conceptual MAST layer.

TASTE source language grammar (informal)
----------------------------------------

.. code-block:: text

    program     ::= type_decl*
    type_decl   ::= 'type' IDENTIFIER ('extends' IDENTIFIER)? '{' member* '}'
    member      ::= visibility? (field_decl | method_decl)
    visibility  ::= 'public' | 'private' | 'protected' | 'internal'
    field_decl  ::= IDENTIFIER ':' IDENTIFIER
    method_decl ::= 'fn' IDENTIFIER '(' param_list? ')' ('->' IDENTIFIER)?
    param_list  ::= param (',' param)*
    param       ::= IDENTIFIER ':' IDENTIFIER

Example
-------

.. code-block:: text

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

from __future__ import annotations

import re
from typing import List, Optional, Tuple

from ..mast.nodes import (
    FieldConcept,
    MethodConcept,
    ParameterConcept,
    ProgramConcept,
    TypeConcept,
    VisibilityConcept,
)
from .base import BaseParser, ParseError


# ---------------------------------------------------------------------------
# Lexer
# ---------------------------------------------------------------------------

_KEYWORDS = frozenset(
    {"type", "extends", "fn", "public", "private", "protected", "internal"}
)

_TOKEN_PATTERNS: List[Tuple[str, str]] = [
    ("COMMENT", r"//[^\n]*"),
    ("WHITESPACE", r"[ \t\r\n]+"),
    ("ARROW", r"->"),
    ("LBRACE", r"\{"),
    ("RBRACE", r"\}"),
    ("LPAREN", r"\("),
    ("RPAREN", r"\)"),
    ("COLON", r":"),
    ("COMMA", r","),
    ("IDENTIFIER", r"[a-zA-Z_][a-zA-Z0-9_]*"),
]

_MASTER_PATTERN = re.compile(
    "|".join(f"(?P<{name}>{pattern})" for name, pattern in _TOKEN_PATTERNS)
)


class _Token:
    """A single lexical token."""

    __slots__ = ("type", "value", "line", "column")

    def __init__(self, type_: str, value: str, line: int, column: int) -> None:
        self.type = type_
        self.value = value
        self.line = line
        self.column = column

    def __repr__(self) -> str:  # pragma: no cover
        return f"Token({self.type}, {self.value!r}, line={self.line})"


def _tokenize(source: str) -> List[_Token]:
    """Tokenize *source* and return a list of non-whitespace, non-comment tokens."""
    tokens: List[_Token] = []
    for match in _MASTER_PATTERN.finditer(source):
        kind = match.lastgroup
        value = match.group()

        if kind in ("COMMENT", "WHITESPACE"):
            continue

        # Promote keyword identifiers to their own token types.
        if kind == "IDENTIFIER" and value in _KEYWORDS:
            kind = value.upper()

        start = match.start()
        line = source.count("\n", 0, start) + 1
        last_newline = source.rfind("\n", 0, start)
        column = start - last_newline  # 1-based

        tokens.append(_Token(kind, value, line, column))

    return tokens


# ---------------------------------------------------------------------------
# Parser
# ---------------------------------------------------------------------------


class TasteParser(BaseParser):
    """Recursive-descent parser for the TASTE source language.

    Converts TASTE source text into a :class:`~taste.mast.nodes.ProgramConcept`
    (MAST).
    """

    def __init__(self) -> None:
        self._tokens: List[_Token] = []
        self._pos: int = 0

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def parse(self, source: str) -> ProgramConcept:
        """Parse *source* and return the corresponding MAST program concept."""
        self._tokens = _tokenize(source)
        self._pos = 0
        return self._parse_program()

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    def _current(self) -> Optional[_Token]:
        if self._pos < len(self._tokens):
            return self._tokens[self._pos]
        return None

    def _advance(self) -> _Token:
        token = self._tokens[self._pos]
        self._pos += 1
        return token

    def _expect(self, type_: str) -> _Token:
        token = self._current()
        if token is None:
            raise ParseError(f"Expected {type_!r} but reached end of input")
        if token.type != type_:
            raise ParseError(
                f"Expected {type_!r} but got {token.type!r} ({token.value!r})",
                token.line,
                token.column,
            )
        return self._advance()

    # ------------------------------------------------------------------
    # Grammar rules
    # ------------------------------------------------------------------

    def _parse_program(self) -> ProgramConcept:
        types: List[TypeConcept] = []
        while self._current() is not None:
            token = self._current()
            if token.type == "TYPE":
                types.append(self._parse_type())
            else:
                raise ParseError(
                    f"Unexpected token {token.value!r}; expected 'type'",
                    token.line,
                    token.column,
                )
        return ProgramConcept(types=types)

    def _parse_type(self) -> TypeConcept:
        self._expect("TYPE")
        name_token = self._expect("IDENTIFIER")
        name = name_token.value

        parents: List[str] = []
        if self._current() and self._current().type == "EXTENDS":
            self._advance()  # consume 'extends'
            parent_token = self._expect("IDENTIFIER")
            parents.append(parent_token.value)

        self._expect("LBRACE")

        fields: List[FieldConcept] = []
        methods: List[MethodConcept] = []

        while self._current() and self._current().type != "RBRACE":
            member = self._parse_member()
            if isinstance(member, FieldConcept):
                fields.append(member)
            else:
                methods.append(member)

        self._expect("RBRACE")

        return TypeConcept(
            name=name,
            visibility=VisibilityConcept(level="public"),
            fields=fields,
            methods=methods,
            parents=parents,
        )

    def _parse_visibility(self) -> str:
        token = self._current()
        if token and token.type in ("PUBLIC", "PRIVATE", "PROTECTED", "INTERNAL"):
            self._advance()
            return token.value
        return "public"

    def _parse_member(self) -> FieldConcept | MethodConcept:
        visibility_str = self._parse_visibility()
        visibility = VisibilityConcept(level=visibility_str)

        if self._current() and self._current().type == "FN":
            return self._parse_method(visibility)
        return self._parse_field(visibility)

    def _parse_field(self, visibility: VisibilityConcept) -> FieldConcept:
        name_token = self._expect("IDENTIFIER")
        self._expect("COLON")
        type_token = self._expect("IDENTIFIER")
        return FieldConcept(
            name=name_token.value,
            type_name=type_token.value,
            visibility=visibility,
        )

    def _parse_method(self, visibility: VisibilityConcept) -> MethodConcept:
        self._expect("FN")
        name_token = self._expect("IDENTIFIER")
        self._expect("LPAREN")

        parameters: List[ParameterConcept] = []
        while self._current() and self._current().type != "RPAREN":
            param_name = self._expect("IDENTIFIER")
            self._expect("COLON")
            param_type = self._expect("IDENTIFIER")
            parameters.append(
                ParameterConcept(name=param_name.value, type_name=param_type.value)
            )
            if self._current() and self._current().type == "COMMA":
                self._advance()

        self._expect("RPAREN")

        return_type = "void"
        if self._current() and self._current().type == "ARROW":
            self._advance()
            return_type_token = self._expect("IDENTIFIER")
            return_type = return_type_token.value

        return MethodConcept(
            name=name_token.value,
            parameters=parameters,
            return_type=return_type,
            visibility=visibility,
        )
