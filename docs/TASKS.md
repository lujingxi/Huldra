# Huldra Development Tasks

This document tracks the current development priorities for Huldra Engine.

Priority levels:

- 🟥 P0 — Current development priority
- 🟨 P1 — Important, but not blocking current work
- 🟦 P2 — Future optimization / feature work
- ⚪ Future — Not currently scheduled

---

# 🟥 P0 — Qwen2.5-0.5B Golden Reference & Quantization

The current development strategy is centered on Qwen2.5-0.5B as the
Golden Reference Model for the CPU inference engine.

Qwen2.5-0.5B is intentionally used because:

- It is small enough to keep development and regression testing practical.
- It is a widely used model architecture.
- Multiple GGUF quantization variants are readily available.
- Its small size allows the same model to be tested repeatedly across
  multiple tensor formats and backends.

The goal of this phase is to establish a correct, well-tested and
reasonably performant CPU inference baseline before expanding to other
model architectures.

## P0.1 — Quantization Coverage Audit

Audit the current Huldra implementation against the GGML/GGUF tensor
types relevant to CPU inference.

The audit must distinguish between:

1. Base GGML tensor/quantization types.
2. Mixed quantization model profiles built from those base types.

For example:

- `Q3_K` is a base GGML quantization type.
- `Q3_K_S`, `Q3_K_M` and `Q3_K_L` are mixed quantization profiles
  that select different base quantization types for different tensors.
- `Q4_K` is a base GGML quantization type.
- `Q4_K_S` and `Q4_K_M` are mixed quantization profiles.
- `Q5_K` is a base GGML quantization type.
- `Q5_K_S` and `Q5_K_M` are mixed quantization profiles.

The coverage audit must therefore not treat every `*_K_S`, `*_K_M` or
`*_K_L` profile as a completely separate low-level quantization
implementation.

### P0.1.1 — Huldra Type Inventory

Audit the current source tree and record:

- `TensorType`
- GGUF tensor type parsing
- tensor storage implementations
- `TensorFormatRegistry`
- `TensorFormatDescriptor`
- `QuantizationRuntime`
- all dequantization implementations
- Scalar backend support
- Vector backend support
- existing quantization tests

Update `docs/QUANTIZATION_COVERAGE.md` with verified information.

Do not infer support from enum values alone.

A format is considered implemented only when the complete required
runtime path is verified.

### P0.1.2 — GGML Base Quantization Inventory

Track the base GGML types that are relevant to Huldra's intended CPU
runtime.

Initial traditional quantization group:

- `Q4_0`
- `Q4_1`
- `Q5_0`
- `Q5_1`
- `Q8_0`

K-quant group:

- `Q2_K`
- `Q3_K`
- `Q4_K`
- `Q5_K`
- `Q6_K`
- `Q8_K`

Other quantization families must also be recorded in the audit,
including:

- IQ family
- TQ family
- newer GGML quantization types introduced by upstream GGML

The audit must distinguish:

- supported
- partially supported
- unsupported
- obsolete/removed GGUF types
- types that are not currently relevant to Huldra's target runtime

Do not commit to implementing every upstream type merely because it
exists.

The upstream GGML type list is evolving and must be re-audited before
the project claims complete coverage.

### P0.1.3 — Floating-Point / Non-Quantized Types

Audit support for:

- `F32`
- `F16`
- `BF16`

Determine whether each type is supported for:

- GGUF loading
- tensor storage
- dequantization/conversion where applicable
- MatMul
- model inference

`F32` and `F16` are part of the Golden Reference baseline.

`BF16` should be tracked even if implementation is deferred.

### P0.1.4 — Mixed Quantization Profiles

Track commonly available mixed quantization profiles separately from
base quantization types.

Examples:

- `Q2_K`
- `Q3_K_S`
- `Q3_K_M`
- `Q3_K_L`
- `Q4_0`
- `Q4_K_S`
- `Q4_K_M`
- `Q5_0`
- `Q5_K_S`
- `Q5_K_M`
- `Q6_K`
- `Q8_0`

For each mixed profile, document which underlying GGML tensor types are
used by the model exporter.

The profile itself must not result in unnecessary duplicated tensor
format implementations.

### P0.1.5 — Qwen2.5-0.5B Test Matrix

Use Qwen2.5-0.5B-Instruct GGUF as the primary real-model validation
matrix.

The initial target matrix should include, where publicly available
and practical:

- F16
- Q2_K
- Q3_K_S
- Q3_K_M
- Q3_K_L
- Q4_0
- Q4_K_S
- Q4_K_M
- Q5_0
- Q5_K_S
- Q5_K_M
- Q6_K
- Q8_0

The test matrix must identify the underlying base quantization types
rather than treating mixed profiles as independent low-level formats.

Availability of a model file must not be treated as proof of runtime
support.

### P0.1.6 — Coverage Audit Completion Criteria

The audit is complete when:

- `docs/QUANTIZATION_COVERAGE.md` reflects the actual source code.
- Every currently declared TensorType has a documented status.
- Every supported GGUF tensor type has a verified loading path.
- Every supported quantized type has a verified storage/format
  descriptor.
- Every supported quantized type has a verified dequantization path.
- Scalar MatMul support is explicitly documented.
- Vector direct-kernel support is explicitly documented.
- Unsupported types fail clearly rather than silently falling back to
  incorrect behavior.
- Mixed quantization profiles are documented separately from base
  quantization types.

No new quantization implementation should begin until this audit is
complete.

---

# 🟥 P0.2 — Golden Reference Correctness Infrastructure

Establish Qwen2.5-0.5B F16 as the primary numerical reference.

The purpose is to allow new quantization implementations to be validated
without relying only on final generated text.

## P0.2.1 — Functional Reference

For every supported Qwen2.5-0.5B format:

- load the GGUF model
- create a fresh inference context
- tokenize the same prompt
- evaluate the same token sequence
- obtain logits
- perform deterministic greedy generation

Verify:

- no crash
- no invalid token IDs
- no NaN/Infinity logits
- deterministic output
- correct EOS handling

## P0.2.2 — Numerical Reference

Use F16 as the primary numerical reference.

For supported quantized formats, compare relevant logits against F16.

Record appropriate metrics such as:

- maximum absolute error
- mean absolute error
- relative error where meaningful
- top-K agreement

Do not require quantized models to produce bit-identical logits.

Quantization error is expected.

The purpose of the comparison is to detect implementation errors,
corruption, incorrect tensor layout, incorrect dequantization and other
systematic problems.

## P0.2.3 — Golden Reference Tests

Add automated tests covering:

- F16 baseline
- every newly supported quantization type
- representative tensors from each format
- end-to-end Qwen2.5-0.5B inference
- deterministic generation

Tests should remain small enough to run regularly during development.

---

# 🟥 P0.3 — Scalar Backend Production Baseline

Scalar remains the correctness/reference backend.

However, Scalar must not remain effectively single-threaded.

The objective is to make Scalar a practical CPU baseline while keeping
its implementation simple and portable.

## P0.3.1 — Scalar MatMul Multithreading

Investigate the current Scalar MatMul partitioning.

Goals:

- utilize multiple CPU workers when workload size justifies it
- preserve deterministic numerical behavior where practical
- avoid excessive scheduling overhead
- avoid creating unnecessary threads
- preserve the existing Scalar implementation as the reference path

Compare:

- single-thread baseline
- parallel implementation
- numerical results
- elapsed time
- CPU utilization

The implementation should use safe managed memory unless profiling
demonstrates a compelling reason otherwise.

`unsafe` is not the default solution.

## P0.3.2 — Scalar Quantized MatMul

For each supported base quantization type:

- implement or verify correct Scalar MatMul
- avoid unnecessary full-tensor dequantization when a direct kernel is
  practical
- validate against dequantized reference calculations
- validate against F16 where appropriate
- benchmark the implementation

The first implementation should prioritize correctness and maintainability.

Low-level optimization should follow only after correctness is
established.

## P0.3.3 — Scalar Work Scheduling

Audit parallelism across:

- MatMul
- RMSNorm
- RoPE
- Attention
- elementwise operations
- embedding lookup where applicable

Determine the appropriate parallel granularity for each operation.

Do not parallelize every operation indiscriminately.

Small workloads may be faster with sequential execution.

## P0.3.4 — Scalar Performance Baseline

Establish a repeatable benchmark for:

- prompt processing
- token generation
- tokens/second
- total elapsed time
- memory allocation where measurable
- CPU utilization where measurable

The benchmark must remain deterministic under greedy sampling.

---

# 🟥 P0.4 — Quantization Implementation Roadmap

After the audit and Golden Reference infrastructure are complete,
implement missing base quantization types in measured groups.

Recommended order:

## P0.4.1 — Traditional Quantization

Implement and validate:

- Q4_1
- Q5_0
- Q5_1
- Q8_0

`Q4_0` is already implemented and remains the existing reference
quantized format.

## P0.4.2 — K-Quantization

Implement and validate:

- Q2_K
- Q3_K
- Q4_K
- Q5_K
- Q6_K

These are base tensor formats.

Mixed profiles such as:

- Q3_K_S
- Q3_K_M
- Q3_K_L
- Q4_K_S
- Q4_K_M
- Q5_K_S
- Q5_K_M

should become test matrices over the underlying implementations rather
than separate duplicate kernel implementations.

## P0.4.3 — Q8_K

Audit the exact role of Q8_K in GGUF models and implement it when
required by the supported inference paths.

Q8_K may be required as an intermediate/helper representation even when
it is not directly used as the primary model quantization profile.

## P0.4.4 — IQ Quantization

Audit the IQ family separately.

Candidate types include:

- IQ1_S
- IQ1_M
- IQ2_XXS
- IQ2_XS
- IQ2_S
- IQ3_XXS
- IQ3_S
- IQ4_NL
- IQ4_XS

Additional upstream IQ types must be evaluated against the current GGML
master before implementation.

Do not begin IQ implementation until the traditional and K-quant groups
are stable.

## P0.4.5 — Ternary / New Quantization Families

Audit and, where justified, support:

- TQ1_0
- TQ2_0
- newer GGML quantization families

These remain lower priority than the traditional and K-quant families.

---

# 🟥 P0.5 — Vector Backend Freeze

The current Vector backend is not a development target during the
quantization expansion phase.

Current policy:

- do not delete VectorBackend
- do not add new quantization kernels to VectorBackend
- do not perform major VectorBackend refactoring
- keep existing tests passing
- preserve the implementation for historical comparison and regression
  testing

VectorBackend will eventually be superseded by instruction-set-specific
backends.

---

# 🟨 P1 — SSE2 Backend

Create an explicit CPU instruction-set backend based on the x86-64
baseline.

Goals:

- define the backend capability boundary
- determine the appropriate SSE2 implementation strategy
- preserve a portable fallback
- reuse validated quantization semantics
- benchmark against Scalar
- verify numerical equivalence

The SSE2 backend should not duplicate model-specific logic.

Quantization and model semantics remain separate from instruction-set
specialization.

---

# 🟨 P1 — AVX2 Backend

Create an AVX2 backend after the SSE2 architecture is established.

Goals:

- AVX2-specific kernels
- efficient F32 operations
- efficient quantized dot products
- reuse the same model and tensor abstractions
- verify numerical equivalence against Scalar
- benchmark against SSE2 and Scalar

AVX2 must not become a second model implementation.

---

# 🟨 P1 — Backend Selection & Discovery

Extend backend discovery/runtime so that CPU instruction-set support
determines the appropriate backend.

Expected fallback direction:

    AVX2
      ↓
    SSE2
      ↓
    Scalar

The exact discovery mechanism must remain consistent with the existing
BackendDiscovery / BackendDescriptor / BackendRuntime architecture.

Backend selection must be capability-driven rather than based on model
architecture.

---

# 🟨 P1 — CUDA Backend

After the CPU backend architecture is stable, implement CUDA support.

The first CUDA milestone is intentionally narrow:

    CUDA Backend
        ↓
    Qwen2.5-0.5B
        ↓
    F16 / currently supported quantization
        ↓
    Correct inference
        ↓
    Benchmark

The primary purpose is to move the Golden Reference test workload onto
the GPU so that larger quantization matrices and future models can be
tested without the current CPU runtime cost.

CUDA implementation should not begin until:

- Scalar correctness is stable
- quantization abstractions are stable
- SSE2/AVX2 backend boundaries are established
- Qwen2.5-0.5B Golden Reference tests are reliable

---

# 🟨 P1 — Production CPU Validation

After Scalar, SSE2 and AVX2 are available:

- benchmark all three CPU paths
- validate numerical consistency
- validate deterministic generation
- measure CPU utilization
- measure memory usage
- identify regression cases
- establish realistic performance expectations

Production-level optimization should be evidence-driven.

---

# 🟦 P2 — CPU Performance Optimization

These optimizations are intentionally deferred until the backend and
quantization architecture is stable.

## P2.1 — MatMul

Investigate:

- cache behavior
- work partitioning
- memory access patterns
- quantized dot-product kernels
- vector width
- batching
- allocation overhead

## P2.2 — RoPE

Investigate:

- precomputed frequencies
- reusable sin/cos values
- memory layout
- parallel granularity

## P2.3 — Attention

Investigate:

- KV-cache layout
- score calculation
- softmax
- value accumulation
- temporary allocations
- cache locality

## P2.4 — Memory

Investigate:

- Tensor allocation frequency
- ArrayPool usage
- temporary buffers
- KV-cache allocation
- zero-copy views
- model loading memory footprint

## P2.5 — Managed vs Unsafe Memory

Prefer safe managed-memory implementations.

Only introduce `unsafe` code if:

1. a specific bottleneck is measured,
2. a safe implementation has been benchmarked,
3. the unsafe implementation provides a meaningful improvement,
4. the complexity and maintenance cost are justified.

---

# 🟦 P2 — Additional Model Architecture

After Qwen2.5-0.5B support and the CPU/GPU backend architecture are
stable, begin additional model architectures.

## P2.1 — Gemma 4

Gemma 4 is the first additional architecture currently planned.

Goals:

- verify Gemma 4 GGUF metadata
- verify tokenizer requirements
- determine whether a dedicated tokenizer is required
- define Gemma 4-specific ModelConfig fields
- implement Gemma 4 model semantics
- validate attention and RoPE behavior
- validate normalization
- validate MLP activation
- validate embedding scaling
- validate output projection
- validate against a known-good implementation

Gemma 1, Gemma 2 and Gemma 3 remain out of scope unless a future
requirement justifies them.

---

# ⚪ Future — Additional Model Architectures

Not currently scheduled:

- Gemma 1
- Gemma 2
- Gemma 3
- additional Qwen architectures
- Mistral
- other architectures as required

New architectures must use the model architecture capability boundary
rather than expanding generic backend code with architecture-specific
conditionals.

---

# Development Rules

- Qwen2.5-0.5B is the current Golden Reference Model.
- F16 is the primary numerical reference for quantized Qwen2.5-0.5B.
- Scalar is the correctness/reference backend.
- Scalar must support practical multi-threaded execution.
- VectorBackend is frozen until superseded by SSE2/AVX2.
- Quantization base types and mixed quantization profiles are separate
  concepts.
- Do not implement separate kernels merely because a mixed profile has a
  different name.
- Correctness takes priority over performance.
- Performance changes must be benchmark-driven.
- Every new quantization type requires automated tests.
- Unsupported GGUF types must fail clearly.
- Do not silently reinterpret an unsupported tensor type.
- Avoid `unsafe` unless measured evidence justifies it.
- Avoid premature architecture-specific optimization.
- Tensor-format discovery continues to use reflection + caching.
- Do not reintroduce the removed `IQuantizer` / `IQuantizedDotProduct`
  abstractions without a demonstrated architectural need.
- GGUF remains the model storage format.
- Windows x64 remains the initial runtime target.
- GitHub `master` is the authoritative current source when it differs
  from previously pasted code.
- When current GitHub code and previous conversation context disagree
  and the reason for the difference is unclear, confirm with the user
  before making architectural assumptions.
- `docs/QUANTIZATION_COVERAGE.md` is the authoritative quantization
  coverage inventory.
- Update documentation when an architectural decision changes.