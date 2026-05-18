"""Base parser interface."""

from abc import ABC, abstractmethod

from ..mast.nodes import ProgramConcept


class BaseParser(ABC):
    """Abstract base class for all TASTE source parsers.

    A parser reads source code written in a particular language and produces
    a :class:`~taste.mast.nodes.ProgramConcept` — the conceptual MAST
    representation of that code.
    """

    @abstractmethod
    def parse(self, source: str) -> ProgramConcept:
        """Parse *source* and return the corresponding MAST program concept.

        Args:
            source: Raw source code text.

        Returns:
            The top-level :class:`~taste.mast.nodes.ProgramConcept`.

        Raises:
            :class:`ParseError`: If the source code is syntactically invalid.
        """


class ParseError(Exception):
    """Raised when source code cannot be parsed.

    Attributes:
        message: Human-readable description of the error.
        line: 1-based line number where the error occurred (0 if unknown).
        column: 1-based column number where the error occurred (0 if unknown).
    """

    def __init__(self, message: str, line: int = 0, column: int = 0) -> None:
        location = f" at line {line}, column {column}" if line else ""
        super().__init__(f"Parse error{location}: {message}")
        self.message = message
        self.line = line
        self.column = column
