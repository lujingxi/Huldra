using System.Text;
using Huldra.Engine.Tensors;

namespace Huldra.Engine.IO;

public sealed class GgufReader
{
    private const uint ExpectedMagic = 0x46554747; // "GGUF" in little-endian
    private const long DefaultAlignment = 32;

    public uint Version { get; private set; }
    public ulong TensorCount { get; private set; }
    public Dictionary<string, object> Metadata { get; } = [];
    public List<GgufTensorInfo> Tensors { get; } = [];

    public void Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A GGUF file path is required.", nameof(filePath));

        Metadata.Clear();
        Tensors.Clear();

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        uint magic = reader.ReadUInt32();
        if (magic != ExpectedMagic)
            throw new InvalidDataException($"Invalid GGUF magic number. Expected 0x{ExpectedMagic:X8}, got 0x{magic:X8}.");

        Version = reader.ReadUInt32();
        if (Version is < 2 or > 3)
            throw new NotSupportedException($"GGUF version {Version} is not supported. Supported versions are 2 and 3.");

        TensorCount = reader.ReadUInt64();
        ulong metadataKvCount = reader.ReadUInt64();

        if (metadataKvCount > int.MaxValue)
            throw new InvalidDataException($"GGUF metadata count {metadataKvCount} is too large.");
        if (TensorCount > int.MaxValue)
            throw new InvalidDataException($"GGUF tensor count {TensorCount} is too large.");

        for (ulong i = 0; i < metadataKvCount; i++)
        {
            string key = ReadString(reader);
            var rawValueType = reader.ReadUInt32();

            if (!Enum.IsDefined(typeof(GgufMetadataValueType), rawValueType))
            {
                throw new InvalidDataException(
                    $"Unsupported GGUF metadata value type: {rawValueType}.");
            }

            var valueType = (GgufMetadataValueType)rawValueType;
            object value = ReadMetadataValue(reader, valueType);
            Metadata[key] = value;
        }

        for (ulong i = 0; i < TensorCount; i++)
        {
            string name = ReadString(reader);
            uint nDims = reader.ReadUInt32();
            if (nDims == 0 || nDims > 16)
                throw new InvalidDataException($"Tensor '{name}' has invalid dimension count {nDims}.");

            int[] shape = new int[nDims];
            long elementCount = 1;
            for (int d = 0; d < nDims; d++)
            {
                ulong rawDim = reader.ReadUInt64();
                if (rawDim == 0 || rawDim > int.MaxValue)
                    throw new InvalidDataException($"Tensor '{name}' has unsupported dimension {rawDim}.");

                shape[d] = (int)rawDim;
                elementCount = checked(elementCount * shape[d]);
            }

            TensorType type = (TensorType)reader.ReadUInt32();
            _ = TensorTypeInfo.For(type); // Fail early for unknown physical layouts.

            ulong relativeOffsetRaw = reader.ReadUInt64();
            if (relativeOffsetRaw > long.MaxValue)
                throw new InvalidDataException($"Tensor '{name}' has an offset too large for this runtime.");

            long sizeInBytes = TensorTypeInfo.GetStorageSize(type, elementCount);
            Tensors.Add(new GgufTensorInfo
            {
                Name = name,
                Shape = shape,
                Type = type,
                RelativeOffset = (long)relativeOffsetRaw,
                DataOffset = 0,
                SizeInBytes = sizeInBytes
            });
        }

        long alignment = GetAlignment();
        long dataStartOffset = AlignUp(stream.Position, alignment);

        foreach (GgufTensorInfo tensor in Tensors)
        {
            tensor.DataOffset = checked(dataStartOffset + tensor.RelativeOffset);
            long end = checked(tensor.DataOffset + tensor.SizeInBytes);
            if (tensor.DataOffset < dataStartOffset || end > stream.Length)
            {
                throw new InvalidDataException(
                    $"Tensor '{tensor.Name}' points outside the GGUF file: offset={tensor.DataOffset}, size={tensor.SizeInBytes}, fileLength={stream.Length}.");
            }
        }
    }

    private long GetAlignment()
    {
        if (!Metadata.TryGetValue("general.alignment", out object? value))
            return DefaultAlignment;

        long alignment;
        try
        {
            alignment = Convert.ToInt64(value);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new InvalidDataException("general.alignment is not a valid integer.", ex);
        }

        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            throw new InvalidDataException($"general.alignment must be a positive power of two; got {alignment}.");

        return alignment;
    }

    private static long AlignUp(long value, long alignment)
    {
        long remainder = value % alignment;
        return remainder == 0 ? value : checked(value + alignment - remainder);
    }

    private static object ReadMetadataValue(BinaryReader reader, GgufMetadataValueType type)
    {
        return type switch
        {
            GgufMetadataValueType.Uint8 => reader.ReadByte(),
            GgufMetadataValueType.Int8 => reader.ReadSByte(),
            GgufMetadataValueType.Uint16 => reader.ReadUInt16(),
            GgufMetadataValueType.Int16 => reader.ReadInt16(),
            GgufMetadataValueType.Uint32 => reader.ReadUInt32(),
            GgufMetadataValueType.Int32 => reader.ReadInt32(),
            GgufMetadataValueType.Float32 => reader.ReadSingle(),
            GgufMetadataValueType.Bool => reader.ReadByte() != 0,
            GgufMetadataValueType.String => ReadString(reader),
            GgufMetadataValueType.Uint64 => reader.ReadUInt64(),
            GgufMetadataValueType.Int64 => reader.ReadInt64(),
            GgufMetadataValueType.Float64 => reader.ReadDouble(),
            GgufMetadataValueType.Array => ReadMetadataArray(reader),
            _ => throw new NotSupportedException($"Unsupported GGUF metadata value type: {type}.")
        };
    }

    private static object[] ReadMetadataArray(BinaryReader reader)
    {
        var arrayType = (GgufMetadataValueType)reader.ReadUInt32();
        ulong arrayLenRaw = reader.ReadUInt64();
        if (arrayLenRaw > int.MaxValue)
            throw new InvalidDataException($"GGUF metadata array length {arrayLenRaw} is too large.");

        var array = new object[(int)arrayLenRaw];
        for (int i = 0; i < array.Length; i++)
        {
            if (arrayType == GgufMetadataValueType.Array)
                throw new NotSupportedException("Nested GGUF metadata arrays are not supported by the current metadata representation.");
            array[i] = ReadMetadataValue(reader, arrayType);
        }
        return array;
    }

    private static string ReadString(BinaryReader reader)
    {
        ulong lenRaw = reader.ReadUInt64();
        if (lenRaw > int.MaxValue)
            throw new InvalidDataException($"GGUF string length {lenRaw} is too large.");

        byte[] bytes = reader.ReadBytes((int)lenRaw);
        if (bytes.Length != (int)lenRaw)
            throw new EndOfStreamException("Unexpected end of file while reading a GGUF string.");

        return Encoding.UTF8.GetString(bytes);
    }
}
