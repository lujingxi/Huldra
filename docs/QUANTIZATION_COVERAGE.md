# Quantization Coverage

## 1. Purpose

This document defines Huldra's quantization coverage strategy.

The immediate goal is to use **Qwen2.5-0.5B** as the Golden Reference Model and progressively expand Huldra's quantization support while keeping the inference results verifiable against a known-good reference.

The quantization system must distinguish between:

- Actual tensor quantization formats.
- Mixed-quantization schemes that combine multiple tensor formats.
- GGUF model variants that use those schemes.

The scope of this document is the **actual tensor storage formats**, not every possible GGUF model packaging variant.

---

## 2. Golden Reference Model

Huldra uses **Qwen2.5-0.5B** as the initial Golden Reference Model.

Reasons:

1. It is small enough to make repeated correctness and performance testing practical.
2. It is a widely used model family.
3. A large number of GGUF quantized variants are available.
4. Its architecture is already supported by Huldra.
5. The model is sufficiently representative for validating the inference engine's quantization pipeline.

The reference model should be used consistently when adding and validating new quantization formats.

---

## 3. Quantization Categories

Huldra should distinguish between three concepts.

### 3.1 Base Quantization Formats

These are actual tensor storage formats implemented by a quantization scheme.

Examples include:

- `Q4_0`
- `Q4_1`
- `Q5_0`
- `Q5_1`
- `Q8_0`
- `Q2_K`
- `Q3_K`
- `Q4_K`
- `Q5_K`
- `Q6_K`
- `IQ2_XXS`
- other `IQ*` formats supported by GGML

These formats are the primary target of the quantization runtime.

---

### 3.2 Mixed Quantization Schemes

Names such as:

- `Q3_K_S`
- `Q3_K_M`
- `Q3_K_L`
- `Q4_K_S`
- `Q4_K_M`
- `Q4_K_L`
- `Q5_K_S`
- `Q5_K_M`
- `Q5_K_L`
- and similar variants

must **not** be treated as individual tensor quantization formats.

They describe a **model-level mixed-quantization scheme**.

For example, a model labelled `Q4_K_M` may contain tensors using several different underlying formats, potentially including:

- `Q4_K`
- `Q5_K`
- `Q6_K`
- `Q8_0`
- `Q4_0`
- `IQ2_XXS`
- or other formats depending on the model/exporter.

Therefore:

> `Q4_K_M` is not equivalent to "all tensors are Q4_K".

The same principle applies to the other `S`, `M`, `L`, `XL`, etc. variants.

Huldra's quantization registry should therefore operate on the **actual tensor `TensorType`**, while model-level mixed schemes are simply collections of those formats.

---

## 4. GGML Quantization Families

The quantization formats should be considered by family.

### 4.1 Legacy Q Formats

These include formats such as:

- `Q4_0`
- `Q4_1`
- `Q5_0`
- `Q5_1`
- `Q8_0`

These formats are relatively old and structurally simpler than the K-quants.

`Q4_0` is already supported by Huldra and serves as the first quantized reference implementation.

`Q8_0` is particularly important because it is commonly encountered inside mixed-quantization models even when the overall model name indicates another quantization level.

---

### 4.2 K-Quant Formats

The principal K-quant tensor formats are:

- `Q2_K`
- `Q3_K`
- `Q4_K`
- `Q5_K`
- `Q6_K`

These are the actual tensor formats.

Variants such as:

- `Q2_K_S`
- `Q2_K_M`
- `Q2_K_L`
- `Q3_K_S`
- `Q3_K_M`
- `Q3_K_L`
- `Q4_K_S`
- `Q4_K_M`
- `Q4_K_L`
- `Q5_K_S`
- `Q5_K_M`
- `Q5_K_L`

are model-level mixed-quantization schemes rather than separate tensor formats.

The implementation priority should therefore be based on the underlying K-quant tensor types.

---

### 4.3 IQ Formats

GGML also contains importance-aware quantization formats (`IQ*`).

These should be treated as independent tensor formats.

Examples include:

- `IQ2_XXS`
- `IQ2_XS`
- `IQ2_S`
- `IQ3_XXS`
- `IQ3_XS`
- `IQ3_S`
- `IQ4_XS`
- `IQ4_NL`
- and other GGML-supported IQ formats.

The exact set supported by Huldra must be determined from the current GGML implementation and from actual GGUF models available for testing.

IQ formats should not be inferred from the model's advertised mixed-quantization name.

For example, if a model advertised as `Q4_K_M` contains an `IQ2_XXS` tensor, Huldra must support `IQ2_XXS` as an independent tensor format in order to load and execute that model.

---

## 5. Q1 Formats

`Q1_*` formats should not be treated as a primary implementation target at this stage.

They appear to have little practical relevance in current GGUF model distribution compared with the newer K-quants and IQ formats.

However, this is a prioritization decision rather than a claim that the formats can never exist in GGUF files.

If future model coverage or GGML compatibility requirements demonstrate meaningful usage of a Q1 format, it can be added to the audit and implementation roadmap.

---

## 6. Required Audit Method

Quantization support must be audited from two independent perspectives.

### 6.1 GGML Format Inventory

Determine the actual quantization tensor formats implemented by the relevant GGML version.

The inventory should identify:

- Tensor type name.
- Block size.
- Bytes per block.
- Storage layout.
- Scaling representation.
- Additional per-block metadata.
- Dequantization algorithm.
- Whether the format has special alignment requirements.
- Whether the format is currently used by modern GGUF models.

This establishes the theoretical format coverage required by the engine.

---

### 6.2 Real GGUF Model Coverage

Search for real Qwen2.5-0.5B GGUF models and record which tensor formats are actually present.

The audit should inspect the GGUF tensor metadata rather than relying solely on the model filename.

For each tested model, record:

- Model filename.
- Overall advertised quantization.
- Tensor types actually present.
- Number of tensors using each type.
- Whether embedding/output tensors use a different type.
- Whether any unexpected formats occur.

This is particularly important for mixed-quantization schemes.

---

## 7. Coverage Matrix

The final audit should produce a matrix similar to the following:

| Tensor Format | GGML Exists | Found in Qwen2.5-0.5B GGUF | Huldra Supported | Scalar Tested | Priority |
|---|---|---|---|---|---|
| F32 | Yes | Yes | Yes | Yes | Baseline |
| F16 | Yes | Yes | Yes | Yes | Baseline |
| Q4_0 | Yes | Yes | Yes | Yes | Complete |
| Q4_1 | TBD | TBD | TBD | TBD | TBD |
| Q5_0 | TBD | TBD | TBD | TBD | TBD |
| Q5_1 | TBD | TBD | TBD | TBD | TBD |
| Q8_0 | Yes | TBD | TBD | TBD | High |
| Q2_K | Yes | TBD | TBD | TBD | High |
| Q3_K | Yes | TBD | TBD | TBD | High |
| Q4_K | Yes | TBD | TBD | TBD | High |
| Q5_K | Yes | TBD | TBD | TBD | High |
| Q6_K | Yes | TBD | TBD | TBD | High |
| IQ2_XXS | Yes | TBD | TBD | TBD | TBD |
| Other IQ* | TBD | TBD | TBD | TBD | TBD |

The matrix should be updated as the audit progresses.

`TBD` must not be replaced with assumptions.

---

## 8. Implementation Requirements

The quantization architecture should remain format-discoverable.

Adding a new quantization format should ideally require:

1. Defining the tensor storage type.
2. Defining its `TensorFormatDescriptor`.
3. Providing validation.
4. Providing dequantization.
5. Providing tests.
6. Registering the implementation through the existing discovery mechanism.

The engine should avoid large model-family-specific switch statements.

The `TensorType` should identify the actual tensor format, while the quantization registry should resolve the corresponding implementation dynamically.

---

## 9. Scalar Backend Strategy

The Scalar Backend is the **reference implementation** for quantization correctness.

It should eventually support all practically relevant GGML tensor quantization formats required by Huldra's supported GGUF models.

Scalar does not mean single-threaded.

The Scalar Backend should remain a straightforward, readable implementation while still using safe multithreading where appropriate.

The intended distinction is:

- **Scalar Backend:** correctness/reference implementation with safe CPU parallelism.
- **SSE2 Backend:** instruction-set optimized implementation.
- **AVX2 Backend:** instruction-set optimized implementation.
- **CUDA Backend:** GPU implementation.

The Scalar Backend should not contain architecture-specific SIMD intrinsics.

---

## 10. Correctness Requirements

Every newly supported quantization format must have tests covering at least:

1. Format discovery.
2. Tensor metadata validation.
3. Block-size validation.
4. Dequantization correctness.
5. Representative values against an independent/reference implementation.
6. MatMul correctness using the format.
7. Integration with a real GGUF model where practical.

For the Golden Reference Model, the same prompt and generation configuration should be used when comparing different quantization formats.

The purpose is not to require bit-identical output between different quantizations.

Instead, the tests should verify:

- No crashes.
- No invalid tensor access.
- No NaN/Inf propagation under normal inference.
- Reasonable logits.
- Deterministic output under greedy sampling.
- Expected qualitative similarity to the reference model.

---

## 11. Mixed-Quantization Model Testing

A model-level quantization label must never be used as evidence that a single tensor format is sufficient.

For example:

`Q4_K_M`

must be interpreted as:

> "A model using a mixed collection of tensor formats selected by the Q4_K_M quantization scheme."

It must not be interpreted as:

> "A model containing only Q4_K tensors."

Therefore, before declaring support for a mixed scheme, Huldra must inspect the actual tensors and confirm that every tensor format appearing in the GGUF file is supported.

This rule is especially important for the `S`, `M`, `L`, and `XL` variants.

---

## 12. Priority Principles

Quantization implementation priority should be determined by:

1. Formats already present in Qwen2.5-0.5B GGUF models.
2. Formats commonly present in modern mixed-quantization models.
3. Formats required by the GGML/GGUF ecosystem.
4. Formats that unlock significant numbers of additional models.
5. Implementation complexity.

The model filename alone must not determine priority.

---

## 13. Current Known Status

At the beginning of this audit:

- F32 is supported.
- F16 is supported.
- Q4_0 is supported.
- Scalar Backend can execute the Q4_0 reference path.
- Vector Backend has a Q4_0 optimized path.
- Other quantization formats require auditing before implementation decisions are made.

The exact current coverage must be verified against the source tree and current GGUF model files.

---

## 14. Relationship to Backend Development

Quantization support and CPU backend optimization are related but should remain conceptually separate.

The quantization layer defines:

> "How is this tensor stored and decoded?"

The backend defines:

> "How is the operation executed on a particular compute architecture?"

A format should therefore first have a correct reference implementation before architecture-specific optimized kernels are introduced.

The intended development order is:

1. Quantization format definition.
2. Format discovery and validation.
3. Correct Scalar dequantization.
4. Scalar MatMul correctness.
5. Tests and reference validation.
6. SSE2 optimized implementation.
7. AVX2 optimized implementation.
8. CUDA implementation where appropriate.

---

## 15. Audit Completion Criteria

The quantization coverage audit is complete when:

- The relevant GGML quantization formats have been inventoried.
- The currently available Qwen2.5-0.5B GGUF variants have been inspected.
- The actual tensor formats used by those models are recorded.
- Mixed-quantization schemes are explicitly documented as model-level combinations.
- Q1 formats have been evaluated and their implementation priority documented.
- The coverage matrix has no unexplained `TBD` entries for the formats relevant to the immediate roadmap.
- A concrete implementation order has been agreed upon.

Only after this audit should implementation of the next quantization format begin.