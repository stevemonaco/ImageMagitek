using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Input;

namespace TileShop.UI.ViewExtenders.DragDrop;

public static class IDataTransferExtensions
{
    private sealed class InProcessDataTransfer<T> : IDataTransfer
        where T : class
    {
        public T Value { get; }

        public InProcessDataTransfer(T value)
        {
            Value = value;
        }

        IReadOnlyList<DataFormat> IDataTransfer.Formats => [];

        IReadOnlyList<IDataTransferItem> IDataTransfer.Items => [];

        public void Dispose() { }
    }

    extension(IDataTransfer)
    {
        public static IDataTransfer CreateInProcessTransfer<T>(T data) where T : class =>
            new InProcessDataTransfer<T>(data);
    }

    public static bool TryGet<T>(this IDataTransfer data, [NotNullWhen(true)] out T? value)
        where T : class
    {
        value = null;

        if (data is InProcessDataTransfer<T> wrapper)
        {
            value = wrapper.Value;
        }

        return value != null;
    }
}