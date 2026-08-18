using Huldra.Engine.Tensors;

namespace Huldra.Engine.IO;

public sealed class GgufTensorInfo
{
    public required string Name { get; init; }
    public required int[] Shape { get; init; }
    public required TensorType Type { get; init; }

    /// <summary>
    /// The offset relative to the start of the tensor data block.
    /// </summary>
    public required long RelativeOffset { get; init; }

    /// <summary>
    /// The absolute offset in the file where the tensor data starts.
    /// </summary>
    public long DataOffset { get; set; }

    public required long SizeInBytes { get; init; }
}
