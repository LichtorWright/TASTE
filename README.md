# taste
taste is essentially a compiler that can compile itself into any language it supports.

## taste — Transpiling Abstract Syntax Tree Emission

taste is a **universal compiler engine**.

It doesn’t belong to any single language — not C#, not Db, not anything else — because it operates at a level *above* language syntax. Its job is to understand the *concepts* behind code, not just the words used to describe them.

At a high level, taste does this:

- Parses source code from a language.
- Converts it into a **MAST** (Matrix Assisted Syntax Tree) — a conceptual model.
- Converts MAST into a language‑agnostic **AST**.
- Uses emitters to generate code in a target language.

taste is a platform for building and translating languages, not just a backend for one language.

---

## MAST — Matrix Assisted Syntax Tree

**MAST is the conceptual layer.**

Every language has its own vocabulary:

- C# calls it a **class**
- C++ calls it a **class**
- Java calls it a **class**
- Rust might use a **struct**
- Go might use a **type**
- Some other language might call it a **CodeHolder**

Different words, same idea.

MAST doesn’t care what a language *calls* something. It cares what the thing *is*.

A MAST node might represent:

- “This is an object type definition”
- “It has fields”
- “It has methods”
- “It has visibility rules”
- “It has inheritance or composition”
- “It has a particular memory layout”

MAST is the **Rosetta Stone** of the system: it captures the *meaning* of constructs in a way that is independent of any specific language.

---

## AST — Abstract Syntax Tree

Once taste understands the concept via MAST, it converts that into an **AST** — a structured, language‑agnostic representation that emitters can work with.

The pipeline looks like this:

```text
Source Parser → MAST → AST → Emitter → Target Code
