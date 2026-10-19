using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;
using ImageMagitek.Compression;
using ImageMagitek.Plugins;
using TileShop.Shared.Interactions;

namespace TileShop.UI.ViewModels;

public partial class AddCompressedDataViewModel : RequestViewModel<AddCompressedDataViewModel>
{
    private readonly Func<string, MagitekResult> _validateName;
    private readonly DataSource _parent;
    private readonly ICompressorFactory _compressorFactory;

    [ObservableProperty] private string _resourceName = "";
    [ObservableProperty] private string? _nameError;
    [ObservableProperty] private string _offsetText = "0";
    [ObservableProperty] private ObservableCollection<CompressionInfo> _compressions;
    [ObservableProperty] private CompressionInfo? _selectedCompression;
    [ObservableProperty] private string _preview = "";
    [ObservableProperty] private bool _isPreviewValid;

    public string ParentName => _parent.Name;
    public long Offset { get; private set; }

    public AddCompressedDataViewModel(DataSource parent, ICompressorFactory compressorFactory, Func<string, MagitekResult> validateName)
    {
        _parent = parent;
        _compressorFactory = compressorFactory;
        _validateName = validateName;
        Title = "Add Compressed Data";
        AcceptName = "Add";

        _compressions = new(compressorFactory.GetRegisteredCompressors());
        _selectedCompression = _compressions.FirstOrDefault();
        _resourceName = $"{parent.Name} 0";

        UpdateNameError();
        UpdatePreview();
    }

    partial void OnResourceNameChanged(string value) => UpdateNameError();
    partial void OnOffsetTextChanged(string value) => UpdatePreview();
    partial void OnSelectedCompressionChanged(CompressionInfo? value) => UpdatePreview();

    private void UpdateNameError()
    {
        var result = _validateName(ResourceName);
        NameError = result.HasFailed ? result.AsError.Reason : null;
        TryAcceptCommand.NotifyCanExecuteChanged();
    }

    private void UpdatePreview()
    {
        IsPreviewValid = false;

        var text = OffsetText.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];

        if (!long.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset) || offset < 0)
        {
            Preview = "Enter the block's offset in hexadecimal";
        }
        else if (SelectedCompression is null)
        {
            Preview = "Choose a compression format";
        }
        else
        {
            Offset = offset;
            using var source = new CompressedDataSource("preview", _parent, offset, SelectedCompression.Name, _compressorFactory);

            if (source.DecompressionError is { } error)
            {
                Preview = error;
            }
            else
            {
                Preview = $"Decompresses 0x{source.CompressedLength:X} bytes to 0x{source.Length:X} bytes";
                IsPreviewValid = true;
            }
        }

        TryAcceptCommand.NotifyCanExecuteChanged();
    }

    protected override bool CanAccept() => NameError is null && IsPreviewValid;

    public override AddCompressedDataViewModel? ProduceResult() => this;
}
