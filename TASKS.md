| Priority | 工作                                            |
| -------- | --------------------------------------------- |
| 🟥 P0    | **建立 model architecture capability boundary** |
| 🟥 P0    | **Gemma tokenizer 問題**                        |
| 🟥 P0    | **Gemma 2/3 architecture config 不足**          |
| 🟥 P0    | **Context/KV cache capacity validation**      |
| 🟥 P0    | **Token ID validation**                       |
| 🟨 P1    | 移除 `_hiddenStates` dead state                 |
| 🟨 P1    | 移除 ModelContext quantization hard-code        |
| 🟨 P1    | Scalar ↔ Vector numerical regression tests    |
| 🟨 P1    | Gemma-specific attention / norm semantics     |
| 🟦 P2    | RoPE precomputation                           |
| 🟦 P2    | Attention optimization                        |
| 🟦 P2    | SIMD instruction-set specialization           |
| 🟦 P3    | GPU backend                                   |
