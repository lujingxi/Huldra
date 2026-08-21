# Huldra — Development Tasks

> This document tracks the current implementation roadmap for Huldra.
>
> ## Source of truth
>
> The roadmap is based on:
>
> 1. Current `master` source code.
> 2. `ARCHITECTURE.md`.
> 3. `DECISIONS.md`.
> 4. `QUANTIZATION_COVERAGE.md`.
> 5. Previous engineering discussions and decisions.
>
> `TASKS.md` is a planning document, not the sole source of architectural truth.
>
> If the repository and this document disagree, the actual repository state and
> architectural decisions take precedence. Changes to this document should be
> proposed explicitly and accepted before being applied.
>
> ---
>
> ## Current strategic direction
>
> Huldra will use **Qwen2.5-0.5B as the Golden Reference Model** during the
> inference-engine development phase.
>
> Reasons:
>
> - Small enough for fast local testing.
> - Widely used and easy to obtain.
> - Available in many GGUF quantization profiles.
> - Large enough to exercise the real transformer inference pipeline.
> - Suitable for regression testing across multiple tensor formats.
>
> `ScalarBackend` is the correctness/reference backend and will eventually be
> multi-threaded and sufficiently optimized for practical development use.
>
> `VectorBackend` is temporarily frozen. It must not receive new architectural
> features while SSE2 and AVX2 backends are being developed. It should remain
> available as historical/reference code unless a later decision changes this.
>
> The planned CPU backend progression is:
>
> ```text
> Scalar
>   ↓
> SSE2
>   ↓
> AVX2
>   ↓
> CUDA
> ```
>
> CUDA should be capable of running Qwen2.5-0.5B before Huldra moves to broader
> model-architecture support.
>
> ---
>
> # P0 — Quantization coverage and Golden Reference
>
> ## P0.1 — Quantization taxonomy audit
>
> [x] Complete the GGML/GGUF quantization taxonomy used by Huldra.
>
> The taxonomy must distinguish:
>
> ### Actual tensor formats
>
> Classic formats:
>
> - F32
> - F16
> - Q4_0
> - Q4_1
> - Q5_0
> - Q5_1
> - Q8_0
>
> K-quants:
>
> - Q2_K
> - Q3_K
> - Q4_K
> - Q5_K
> - Q6_K
> - Q8_K
>
> I-quants:
>
> - IQ1_S
> - IQ1_M
> - IQ2_XXS
> - IQ2_XS
> - IQ2_S
> - IQ3_XXS
> - IQ3_S
> - IQ4_NL
> - IQ4_XS
>
> Ternary formats:
>
> - TQ1_0
> - TQ2_0
>
> Newer hardware-oriented formats:
>
> - MXFP4
> - NVFP4
>
> Additional GGML types should be tracked if they become relevant to GGUF
> model inference.
>
> Do not treat removed GGML formats as active Huldra targets.
>
> ---
>
> ## P0.2 — Quantization profiles vs tensor formats
>
> [x] Explicitly document the distinction between:
>
> 1. A quantization profile / model file type.
> 2. The actual tensor format stored in each tensor.
> 3. The Huldra runtime/kernel implementation for that tensor format.
>
> Examples of quantization profiles:
>
> - Q4_K_S
> - Q4_K_M
> - Q5_K_S
> - Q5_K_M
> - Q3_K_S
> - Q3_K_M
> - Q3_K_L
>
> These are **mixed quantization profiles**, not individual tensor formats.
>
> A profile may contain multiple actual tensor types, including combinations of:
>
> - Q2_K
> - Q3_K
> - Q4_K
> - Q5_K
> - Q6_K
> - Q8_0
> - F16
> - Q4_0
> - IQ formats
> - other supported tensor formats
>
> Huldra must therefore dispatch based on the **actual GGUF tensor type**, not
> the model's human-readable quantization profile name.
>
> ---
>
> ## P0.3 — Quantization coverage matrix
>
> [ ] Update `docs/QUANTIZATION_COVERAGE.md` to track, for every actual tensor
> format:
>
> - GGML type
> - GGUF representation
> - Block size
> - Bytes per block
> - Tensor storage requirements
> - Parser support
> - TensorFormatRegistry support
> - Dequantization support
> - ScalarBackend support
> - SIMD/backend support
> - Golden Reference availability
> - Tests
>
> The matrix must not treat Q3_K_S/M/L, Q4_K_S/M, Q5_K_S/M etc. as individual
> tensor formats.
>
> ---
>
> ## P0.4 — Golden Reference model matrix
>
> [ ] Establish Qwen2.5-0.5B as the primary regression model.
>
> Prioritize available GGUF profiles that exercise different actual tensor
> formats.
>
> Target initial coverage should include, where a usable Qwen2.5-0.5B GGUF
> artifact is available:
>
> - F16
> - Q4_0
> - Q2_K
> - Q3_K mixed profiles
> - Q4_K mixed profiles
> - Q5_K mixed profiles
> - Q6_K
> - Q8_0
>
> The test matrix must inspect the actual tensor types contained in each GGUF
> file rather than assuming that a profile name corresponds to one tensor type.
>
> Q4_1 and Q5_1 are lower priority because they are uncommon in current model
> distributions. They remain part of the long-term compatibility target unless
> later evidence justifies removing them.
>
> ---
>
> # P0 — Scalar quantization implementation
>
> ## P0.5 — Audit existing quantization infrastructure
>
> [x] Verify that the existing:
>
> - TensorType
> - TensorFormatDescriptor
> - TensorFormatRegistry
> - TensorFormatValidator
> - QuantizationRuntime
> - dynamic format discovery
>
> can represent all planned actual tensor formats without introducing
> model-family-specific switch statements.
>
> ---
> 
> ## P0.6 Scalar development performance            [ ]
>     P0.6.1 Multi-threaded Scalar MatMul
>     P0.6.2 Performance instrumentation         [x]
>     P0.6.3 Benchmark / CLI usability
>     P0.6.4 Basic allocation reduction
> 
> ---
>
> ## P0.7 — Complete classic quantization support
>
> [ ] Implement and test missing classic formats in ScalarBackend/runtime:
>
> - Q4_1
> - Q5_0
> - Q5_1
> - Q8_0
>
> Existing working formats must remain regression-tested.
>
> ---
>
> ## P0.8 — Implement K-quant support
>
> [ ] Implement Scalar support for:
>
> 1. Q2_K
> 2. Q3_K
> 3. Q4_K
> 4. Q5_K
> 5. Q6_K
> 6. Q8_K
>
> Each implementation must include:
>
> - format descriptor
> - storage validation
> - dequantization/reference implementation
> - ScalarBackend compatibility
> - unit tests
> - numerical correctness tests
> - GGUF integration tests where practical
>
> Implement the actual tensor formats, not the mixed profile names.
>
> ---
>
> ## P0.9 — Mixed quantization profile validation
>
> [ ] Validate that Huldra can load a GGUF containing multiple tensor
> quantization formats in the same model.
>
> The test must verify that:
>
> - each tensor retains its actual GGML/GGUF tensor type;
> - TensorFormatRegistry resolves each type correctly;
> - QuantizationRuntime dispatches correctly;
> - ScalarBackend can process the complete model;
> - no model-profile-specific dispatch logic is required.
>
> ---
>
> # P1 — ScalarBackend practical performance
>
> ## P1.1 — Multi-threaded Scalar MatMul
>
> [ ] Improve ScalarBackend MatMul parallelism.
>
> Goals:
>
> - use multiple logical processors where beneficial;
> - avoid unnecessary allocations;
> - avoid repeated Memory<T> → Span<T> conversions in hot loops;
> - preserve safe managed code where practical;
> - avoid `unsafe` unless profiling demonstrates a compelling need;
> - maintain deterministic numerical correctness within accepted tolerance.
>
> The goal is not yet final production performance, but sufficiently useful
> performance for development and regression testing.
>
> ---
>
> ## P1.2 — Scalar performance instrumentation
>
> [ ] Maintain lightweight instrumentation capable of identifying:
>
> - worker count;
> - maximum concurrent workers;
> - thread activity;
> - MatMul execution distribution;
> - elapsed time;
> - tokens/sec.
>
> Instrumentation must be removable or disableable for normal execution.
>
> ---
>
> ## P1.3 — Allocation reduction
>
> [ ] Profile inference for unnecessary:
>
> - Tensor allocations;
> - temporary F32 buffers;
> - ArrayPool usage inefficiencies;
> - repeated metadata lookups;
> - repeated tensor-name construction;
> - repeated shape calculations.
>
> Optimize only after correctness tests are stable.
>
> ---
>
> ## P1.4 — Context/inference memory reuse
>
> [ ] Investigate reusing temporary tensors and buffers between token
> evaluations instead of allocating new arrays for every operation.
>
> KV cache correctness must not be compromised.
>
> ---
>
> # P1 — Inference correctness
>
> ## P1.5 — Golden output regression
>
> [ ] Establish stable regression prompts for Qwen2.5-0.5B.
>
> Compare:
>
> - token IDs;
> - generated token count;
> - EOS behaviour;
> - decoded output;
> - logits where practical.
>
> Exact textual equality should be required where deterministic execution is
> expected; otherwise use defined numerical/token tolerances.
>
> ---
>
> ## P1.6 — Tokenizer validation
>
> [ ] Validate the current tokenizer against all supported Qwen2.5-0.5B GGUF
> variants.
>
> In particular verify:
>
> - special-token handling;
> - EOS token detection;
> - encode/decode round trips;
> - chat template;
> - BPE merge behaviour.
>
> Model-family-specific tokenizer assumptions should be removed where GGUF
> metadata provides the required information.
>
> ---
>
> # P2 — CPU backend architecture
>
> ## P2.1 — Freeze VectorBackend
>
> [ ] Keep VectorBackend unchanged as a reference implementation.
>
> Do not add new quantization features to VectorBackend during this phase.
>
> ---
>
> ## P2.2 — Backend discovery architecture
>
> [ ] Complete:
>
> ```text
> BackendDiscovery
>     ↓
> BackendDescriptor
>     ↓
> BackendRuntime
>     ↓
> IBackend
> ```
>
> Requirements:
>
> - discovery is tolerant;
> - discovery failures are logged;
> - runtime selection is strict;
> - backend instances are cached;
> - backend priority is explicit.
>
> ---
>
> ## P2.3 — SSE2Backend
>
> [ ] Implement SSE2Backend as the first instruction-set-specific CPU backend.
>
> It must support the Golden Reference model before moving on.
>
> Priority operations:
>
> - MatMul
> - RMSNorm
> - RoPE
> - Attention
> - SiLU
> - GELU
> - Add
> - Mul
> - AddBias
>
> Quantization kernels should be added according to the validated
> Quantization Coverage Matrix.
>
> ---
>
> ## P2.4 — AVX2Backend
>
> [ ] Implement AVX2Backend after SSE2 is stable.
>
> AVX2 must preserve the same IBackend contract and correctness behaviour.
>
> ---
>
> ## P2.5 — CPU backend fallback chain
>
> [ ] Establish:
>
> ```text
> AVX2
>   ↓
> SSE2
>   ↓
> Scalar
> ```
>
> based on runtime CPU feature detection.
>
> Scalar remains the universal correctness fallback.
>
> ---
>
> # P3 — CUDA backend
>
> ## P3.1 — CUDA architecture investigation
>
> [ ] Determine the supported Windows CUDA integration strategy for Huldra.
>
> Requirements:
>
> - Windows x86-64;
> - NVIDIA GPUs;
> - minimal unnecessary dependencies;
> - maintainable C# integration;
> - ability to reuse Huldra's tensor/backend abstractions.
>
> ---
>
> ## P3.2 — CUDABackend skeleton
>
> [ ] Implement CUDABackend and runtime discovery.
>
> Initially target Qwen2.5-0.5B only.
>
> ---
>
> ## P3.3 — CUDA Qwen2.5-0.5B
>
> [ ] Run the Golden Reference model successfully on CUDA.
>
> Initial objective:
>
> - correct inference;
> - correct tokenizer;
> - correct KV cache;
> - correct logits;
> - correct generated output.
>
> Performance optimization follows correctness.
>
> ---
>
> # P4 — Production-level performance
>
> ## P4.1 — Profiling infrastructure
>
> [ ] Establish repeatable profiling for:
>
> - CPU;
> - memory;
> - allocations;
> - backend execution;
> - quantization;
> - KV cache;
> - token generation.
>
> ---
>
> ## P4.2 — Scalar production optimization
>
> [ ] Optimize ScalarBackend after quantization coverage is substantially
> complete.
>
> Focus areas:
>
> - cache locality;
> - threading;
> - allocation;
> - memory bandwidth;
> - quantized MatMul;
> - temporary-buffer reuse.
>
> Scalar remains a correctness reference despite optimization.
>
> ---
>
> ## P4.3 — SSE2 production optimization
>
> [ ] Optimize SSE2 kernels based on profiling.
>
> ---
>
> ## P4.4 — AVX2 production optimization
>
> [ ] Optimize AVX2 kernels based on profiling.
>
> ---
>
> ## P4.5 — CUDA production optimization
>
> [ ] Optimize CUDA execution after correctness is stable.
>
> Focus areas:
>
> - GPU memory transfers;
> - kernel launch overhead;
> - quantized GEMM;
> - KV cache;
> - batching;
> - memory reuse.
>
> ---
>
> # P5 — Broader model architecture support
>
> ## P5.1 — Gemma 4
>
> [ ] Add Gemma 4 support after the Qwen2.5-0.5B Golden Reference engine
> pipeline and core quantization/backend work are stable.
>
> Gemma 1–3 are intentionally deferred.
>
> ---
>
> ## P5.2 — Additional model architectures
>
> [ ] Add additional architectures only after the backend and quantization
> abstractions are sufficiently mature.
>
> Candidate architectures should be selected based on:
>
> - practical usage;
> - architecture diversity;
> - tokenizer differences;
> - attention differences;
> - RoPE differences;
> - normalization differences;
> - FFN differences.
>
> ---
>
> # P6 — Advanced quantization coverage
>
> ## P6.1 — I-quants
>
> [ ] Evaluate and implement:
>
> - IQ1_S
> - IQ1_M
> - IQ2_XXS
> - IQ2_XS
> - IQ2_S
> - IQ3_XXS
> - IQ3_S
> - IQ4_NL
> - IQ4_XS
>
> Priority should be based on real-world GGUF availability and model usage.
>
> ---
>
> ## P6.2 — Ternary formats
>
> [ ] Evaluate and implement:
>
> - TQ1_0
> - TQ2_0
>
> ---
>
> ## P6.3 — Newer low-bit formats
>
> [ ] Evaluate:
>
> - MXFP4
> - NVFP4
>
> Do not implement a format merely because it exists in GGML. Confirm that
> Huldra's target hardware and GGUF model ecosystem justify the implementation.
>
> ---
>
> # P7 — Validation and release quality
>
> ## P7.1 — Full regression matrix
>
> [ ] Run the Golden Reference model across all supported:
>
> - quantization formats;
> - CPU backends;
> - CUDA backend;
> - context sizes;
> - prompt lengths.
>
> ---
>
> ## P7.2 — Numerical correctness
>
> [ ] Establish tolerances and reference results for:
>
> - dequantization;
> - MatMul;
> - RMSNorm;
> - RoPE;
> - Attention;
> - activation functions;
> - logits.
>
> ---
>
> ## P7.3 — Memory safety
>
> [ ] Verify:
>
> - no tensor-buffer overrun;
> - no KV-cache overrun;
> - no ArrayPool misuse;
> - no context-position overflow;
> - correct disposal/lifetime behaviour where applicable.
>
> ---
>
> ## P7.4 — Performance regression tests
>
> [ ] Establish repeatable benchmarks and detect significant regressions in:
>
> - prompt processing;
> - token generation;
> - memory usage;
> - allocations;
> - backend throughput.
>
> ---
>
> # Deferred / intentionally excluded
>
> The following are intentionally not current priorities:
>
> - Gemma 1
> - Gemma 2
> - Gemma 3
> - new VectorBackend features
> - macOS Metal
> - non-Windows CPU architectures
> - broad model-family support before backend maturity
>
> These may be reconsidered later.