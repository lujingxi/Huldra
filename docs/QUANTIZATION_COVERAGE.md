GGML/GGUF Quantization Coverage

Legend:
✓ Implemented
~ Partially implemented
✗ Not implemented
— Not applicable

Format       GGUF    Parser    Storage    Dequantize    Scalar
----------------------------------------------------------------
F32          ✓       ✓        ✓          N/A           ✓
F16          ✓       ✓        ✓          ?             ?
Q4_0         ✓       ✓        ✓          ✓             ✓
Q4_1         ?       ?        ?          ?             ?
Q5_0         ?       ?        ?          ?             ?
Q5_1         ?       ?        ?          ?             ?
Q8_0         ✓       ✓        ✓          ✓             ?
Q2_K         ?       ?        ?          ?             ?