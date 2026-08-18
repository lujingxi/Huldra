using Huldra.Engine.Quantization;
using Huldra.Engine.Tensors;
using System;
using System.Collections.Generic;
using System.Text;

namespace Huldra.Engine.Backends;

public interface IBackend
{
    int Priority { get; }
    string Name { get; }
    bool IsSupported { get; }

    void MatMul(Tensor a, Tensor b, Tensor result);
    void RMSNorm(Tensor input, Tensor weight, Tensor output, float epsilon);
    void RoPE(Tensor q, Tensor k, int headCount, int headCountKv, int headDim, float ropeFreqBase, int startPos);
    void Attention(Tensor q, Tensor k, Tensor v, Tensor kCache, Tensor vCache, Tensor output, int headCount, int headCountKv, int headDim, int seqLen, int startPos);
    void SiLU(Tensor tensor);
    void Gelu(Tensor tensor);
    void Mul(Tensor a, Tensor b, Tensor result);
    void Add(Tensor a, Tensor b, Tensor result);
    void AddBias(Tensor bias, Tensor tensor);
}
