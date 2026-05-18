"""Base emitter interface."""

from abc import ABC, abstractmethod

from ..ast.nodes import ProgramNode


class BaseEmitter(ABC):
    """Abstract base class for all TASTE emitters.

    An emitter takes a :class:`~taste.ast.nodes.ProgramNode` (AST) and
    produces source code in a specific target language.
    """

    @abstractmethod
    def emit(self, program: ProgramNode) -> str:
        """Generate target-language source code from *program*.

        Args:
            program: The top-level AST program node.

        Returns:
            A string containing the generated source code.
        """
