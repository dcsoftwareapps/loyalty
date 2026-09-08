using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace LoyaltyCloud.Cashier.Services;

public interface IQrScannerService
{
    Task<QrScanResult> ScanAsync(CancellationToken ct = default);
}

public sealed record QrScanResult(bool Succeeded, string? Value, string? ErrorMessage)
{
    public static QrScanResult Success(string value) => new(true, value, null);
    public static QrScanResult Cancelled() => new(false, null, null);
    public static QrScanResult Failure(string message) => new(false, null, message);
}

public sealed class MauiQrScannerService : IQrScannerService
{
    public async Task<QrScanResult> ScanAsync(CancellationToken ct = default)
    {
#if WINDOWS
        await Task.CompletedTask;
        return QrScanResult.Failure("El scanner de cámara está disponible en Android y iPhone.");
#else
        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
            return QrScanResult.Failure("Activa el permiso de cámara para escanear códigos QR.");

        var currentPage = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (currentPage is null)
            return QrScanResult.Failure("No fue posible abrir la cámara. Ingresa el ID manualmente.");

        var scannerPage = new QrScannerPage();
        await currentPage.Navigation.PushModalAsync(scannerPage);
        await using var registration = ct.Register(() => scannerPage.Cancel());
        return await scannerPage.Result;
#endif
    }
}

public sealed class QrScannerPage : ContentPage
{
    private readonly TaskCompletionSource<QrScanResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CameraBarcodeReaderView _reader;
    private bool _completed;

    public QrScannerPage()
    {
        Title = "Escanear";
        BackgroundColor = Colors.Black;

        _reader = new CameraBarcodeReaderView
        {
            IsDetecting = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.TwoDimensional,
                AutoRotate = true,
                Multiple = false
            }
        };
        _reader.BarcodesDetected += OnBarcodesDetected;

        var cancel = new Button
        {
            Text = "Cancelar",
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            TextColor = Color.FromArgb("#251F1A"),
            CornerRadius = 8,
            Margin = new Thickness(20, 0, 20, 28),
            HeightRequest = 48
        };
        cancel.Clicked += (_, _) => Cancel();

        Content = new Grid
        {
            Children =
            {
                _reader,
                new Border
                {
                    Stroke = Color.FromArgb("#FFFFFF"),
                    StrokeThickness = 3,
                    BackgroundColor = Colors.Transparent,
                    WidthRequest = 260,
                    HeightRequest = 260,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                },
                new VerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.End,
                    Children = { cancel }
                }
            }
        };
    }

    public Task<QrScanResult> Result => _completion.Task;

    public void Cancel() => Complete(QrScanResult.Cancelled());

    protected override void OnDisappearing()
    {
        _reader.IsDetecting = false;
        _reader.BarcodesDetected -= OnBarcodesDetected;
        base.OnDisappearing();
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return;

        MainThread.BeginInvokeOnMainThread(() => Complete(QrScanResult.Success(value)));
    }

    private async void Complete(QrScanResult result)
    {
        if (_completed)
            return;

        _completed = true;
        _reader.IsDetecting = false;
        _reader.BarcodesDetected -= OnBarcodesDetected;
        _completion.TrySetResult(result);

        if (Navigation.ModalStack.Contains(this))
            await Navigation.PopModalAsync();
    }
}
