# Huldra Engine Architecture

## Runtime

.NET 10
C# 14
Windows x64

## Layers

GGUF I/O
    ↓
Tensor / Quantization
    ↓
Model Loading
    ↓
Model / Context
    ↓
Backend
    ↓
Generation

## Supported architectures

- Llama
- Qwen2
- Qwen3
- Gemma
...

## Supported tensor formats

- F32
- F16
- Q4_0
...

## Backends

- Scalar
- Vector

## Current limitations

- CPU only
- ...