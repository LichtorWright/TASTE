# TASTE — Transpiling Abstract Syntax Tree Emission

TASTE is a **universal compiler engine** that operates *above* language syntax.
Its job is to understand the *concepts* behind code — not just the words used to
describe them — and translate those concepts into any supported target language.

## Pipeline

```
Source Parser → MAST → AST → Emitter → Target Code
```

| Stage | Description |
|---|---|
| **Source Parser** | Reads TASTE source language and produces a MAST |
| **MAST** | Matrix Assisted Syntax Tree — the conceptual model |
| **AST** | Language-agnostic Abstract Syntax Tree |
| **Emitter** | Generates source code in a target language |

## MAST — Matrix Assisted Syntax Tree

MAST is the **conceptual layer**. Every language has its own vocabulary:

- C# calls it a **class**
- Rust might use a **struct**
- Go might use a **type**

Different words, same idea. MAST captures the *meaning* of constructs
independently of any specific language's syntax.

## TASTE Source Language

```
// Define a type
type Person {
    public name: string
    public age: int
    private secret_id: string

    public fn greet(to: string) -> string
    public fn get_age() -> int
}

// Inheritance
type Employee extends Person {
    public company: string
    public fn get_salary() -> float
}
```

## Quick Start

```python
from taste import TasteEngine

engine = TasteEngine()

# Transpile to Python
python_code = engine.transpile(source, target="python")

# Transpile to C#
csharp_code = engine.transpile(source, target="csharp")

# Available targets
print(engine.available_targets())  # ['cs', 'csharp', 'py', 'python']
```

## Installation

```bash
pip install -e ".[dev]"
```

## Running Tests

```bash
pytest
```

## Project Structure

```
src/taste/
├── mast/           # MAST node hierarchy (conceptual layer)
├── ast/            # AST node hierarchy (structured layer)
├── parsers/        # Source language parsers (source → MAST)
├── pipeline/       # MAST → AST converter
├── emitters/       # Target language emitters (AST → code)
│   ├── python_emitter.py
│   └── csharp_emitter.py
└── engine.py       # Orchestrates the full pipeline
```
