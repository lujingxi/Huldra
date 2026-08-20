# Huldra Development Tasks

This document tracks the current development priorities for Huldra Engine.

Priority levels:

- 🟥 P0 — Current development priority
- 🟨 P1 — Important, but not blocking current work
- 🟦 P2 — Future optimization / feature work
- ⚪ Future — Not currently scheduled

---

# 🟥 P0 — CPU Performance Baseline & Optimization

The current CPU inference path is functionally working, but several
backend/model combinations show poor CPU utilization and long test times.

The goal of this phase is **not** production-grade optimization.

The goal is to establish a reliable benchmark and remove obvious,
low-risk performance bottlenecks before continuing with model support.

## P0.1 — CLI Benchmark Harness

- [ ] Accept one user prompt.
- [ ] Run the same prompt against all four combinations:
  - [ ] Scalar + F16
  - [ ] Scalar + Q4_0
  - [ ] Vector + F16
  - [ ] Vector + Q4_0
- [ ] Create a fresh inference context for every benchmark run.
- [ ] Keep generation parameters identical between runs.
- [ ] Report elapsed time.
- [ ] Report generated token count.
- [ ] Report tokens/second.
- [ ] Present the four results in a clear summary.

## P0.2 — Backend Parallelism Audit

Investigate why several CPU workloads do not fully utilize available
logical cores.

- [ ] Review `BackendParallel.For`.
- [ ] Review Scalar backend MatMul partitioning.
- [ ] Review Vector backend MatMul partitioning.
- [ ] Review RMSNorm parallelism.
- [ ] Review Attention parallelism.
- [ ] Identify workloads where parallel scheduling overhead exceeds
      useful computation.
- [ ] Verify that small workloads do not create unnecessary parallel
      overhead.

## P0.3 — Low-Risk CPU Optimization

Apply only optimizations that are clearly justified by the benchmark.

- [ ] Optimize parallel partitioning / work granularity where appropriate.
- [ ] Avoid `unsafe` unless a later measurement demonstrates that it is
      necessary.
- [ ] Avoid changing numerical semantics.
- [ ] Avoid architecture-specific SIMD specialization at this stage.
- [ ] Avoid premature kernel rewrites.

## P0.4 — Performance Regression Validation

- [ ] All existing tests pass.
- [ ] Scalar backend remains the correctness/reference implementation.
- [ ] Vector backend remains numerically consistent with Scalar within
      appropriate tolerance.
- [ ] Benchmark results are recorded before and after optimization.
- [ ] No optimization is accepted without a measurable improvement or
      clear architectural justification.

---

# 🟥 P0 — Gemma 4 Support

Gemma 4 is the first Gemma architecture currently targeted by Huldra.

Gemma 1, Gemma 2 and Gemma 3 are not current targets and should not
drive the architecture of the implementation.

## P0.5 — Gemma 4 Architecture

- [ ] Verify Gemma 4 GGUF metadata requirements.
- [ ] Define the Gemma 4 architecture capability boundary.
- [ ] Update model architecture detection.
- [ ] Add Gemma 4-specific model configuration where required.
- [ ] Remove assumptions that Gemma support means Gemma 1/2/3 support.

## P0.6 — Gemma 4 Tokenizer

- [ ] Verify Gemma 4 tokenizer metadata.
- [ ] Determine whether `LlamaTokenizer` can correctly represent the
      Gemma 4 tokenizer.
- [ ] Introduce a dedicated tokenizer abstraction/implementation if
      required.
- [ ] Validate special tokens and EOS handling.
- [ ] Add tokenizer regression tests.

## P0.7 — Gemma 4 Inference Semantics

- [ ] Verify embedding scaling.
- [ ] Verify normalization semantics.
- [ ] Verify attention semantics.
- [ ] Verify RoPE configuration.
- [ ] Verify MLP activation and tensor layout.
- [ ] Verify output projection.
- [ ] Validate Gemma 4 inference against known-good output.

---

# 🟨 P1 — Correctness & Architecture Hardening

## P1.1 — Tensor / Context Validation

- [ ] Validate context size against KV-cache capacity.
- [ ] Validate token IDs before embedding lookup.
- [ ] Validate sequence length against context capacity.
- [ ] Improve error messages for invalid model/context state.

## P1.2 — Backend Numerical Regression

- [ ] Add Scalar ↔ Vector numerical regression tests.
- [ ] Cover F32/F16 paths.
- [ ] Cover Q4_0 paths.
- [ ] Establish appropriate numerical tolerances.

## P1.3 — Model / Backend Separation

- [ ] Remove remaining model-family assumptions from generic backend code.
- [ ] Remove remaining tensor-format hard-coding from model execution
      paths.
- [ ] Keep architecture-specific behavior inside model/context
      implementations where appropriate.

## P1.4 — Code Cleanup

- [ ] Remove `_hiddenStates` dead state if still unused.
- [ ] Remove obsolete compatibility code.
- [ ] Review temporary allocations in inference paths.
- [ ] Review validation duplication.

---

# 🟦 P2 — CPU Performance

These optimizations should be driven by benchmark evidence rather than
implemented speculatively.

## P2.1 — RoPE

- [ ] Precompute reusable RoPE values.
- [ ] Avoid repeated `MathF.Pow`.
- [ ] Avoid repeated `MathF.Sin` / `MathF.Cos` where practical.
- [ ] Benchmark before and after.

## P2.2 — Attention

- [ ] Profile attention separately from MatMul.
- [ ] Reduce temporary allocations.
- [ ] Review KV-cache access patterns.
- [ ] Optimize score calculation.
- [ ] Optimize softmax.
- [ ] Optimize value accumulation.
- [ ] Benchmark each change independently.

## P2.3 — SIMD Specialization

- [ ] Establish SIMD instruction-set detection strategy.
- [ ] Evaluate SSE2 baseline.
- [ ] Evaluate AVX2 backend.
- [ ] Add instruction-set-specific kernels only when justified.
- [ ] Keep a portable fallback.
- [ ] Avoid `unsafe` unless benchmarks demonstrate a meaningful benefit.

---

# 🟦 P2 — Memory & Allocation Optimization

- [ ] Audit per-token temporary allocations.
- [ ] Reduce repeated `new Tensor` allocations where safe.
- [ ] Expand appropriate `ArrayPool<T>` usage.
- [ ] Review KV-cache memory layout.
- [ ] Investigate zero-copy tensor views where useful.
- [ ] Benchmark allocation reduction separately from compute optimization.

---

# 🟦 P2 — GPU Backend

The initial target remains Windows x64 CPU.

GPU acceleration is a later architectural phase.

- [ ] Define GPU backend abstraction requirements.
- [ ] Evaluate DirectX 12 / DirectML / Windows ML options.
- [ ] Determine tensor upload/download strategy.
- [ ] Determine quantized GPU kernel strategy.
- [ ] Implement GPU backend only after CPU architecture is stable.

---

# ⚪ Future — Additional Model Architectures

Not currently scheduled:

- Gemma 1
- Gemma 2
- Gemma 3
- Additional architectures as required

New architectures should be added through the model architecture
capability boundary rather than by expanding generic model code with
architecture-specific conditionals.

---

# Development Rules

- Scalar remains the correctness/reference backend.
- Vector remains the current SIMD backend.
- Correctness takes priority over performance.
- Performance changes should be benchmark-driven.
- Avoid `unsafe` unless there is measured justification.
- Avoid premature architecture-specific optimization.
- Tensor-format discovery continues to use reflection + caching.
- Do not reintroduce the removed `IQuantizer` /
  `IQuantizedDotProduct` abstraction without a demonstrated need.
- GGUF is the model storage format.
- The initial runtime target is Windows x64.