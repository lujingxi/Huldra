# Huldra Quantization Coverage

## Purpose

This document defines Huldra's quantization taxonomy and implementation
coverage.

The document deliberately distinguishes:

1. Quantization profiles / model file types.
2. Actual tensor storage formats.
3. Huldra runtime and backend support.

A model profile such as `Q4_K_M` must never be treated as a single tensor
format.

A GGUF model may contain multiple actual tensor formats.

---

# 1. Quantization model

Huldra uses the following conceptual model:

```text
GGUF quantization profile
        │
        ↓
Mixed tensor assignment
        │
        ├── Tensor A → Q3_K
        ├── Tensor B → Q4_K
        ├── Tensor C → Q5_K
        ├── Tensor D → Q6_K
        ├── Tensor E → Q8_0
        └── Tensor F → F16
                    │
                    ↓
              Actual GGML type
                    │
                    ↓
          TensorFormatRegistry
                    │
                    ↓
          QuantizationRuntime
                    │
                    ↓
              Backend kernel